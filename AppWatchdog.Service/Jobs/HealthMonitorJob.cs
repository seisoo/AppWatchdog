using AppWatchdog.Service.HealthChecks;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Recovery;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.Jobs;

public sealed class HealthMonitorJob : IJob
{
    private const bool ISDEBUG = false;

    private static readonly TimeSpan RecoveryRetryMinInterval = TimeSpan.FromSeconds(10);

    private readonly WatchedApp _app;
    private readonly IHealthCheck _healthCheck;
    private readonly IRecoveryStrategy _recovery;
    private readonly NotificationDispatcher _dispatcher;
    private readonly TimeSpan _interval;
    private readonly int _mailIntervalHours;

    private AppStatus? _lastStatus;
    private int _consecutiveDown;
    private int _consecutiveStartFailures;
    private bool _recoveryAttemptedInThisDown;
    private bool _upAlreadyNotifiedByRestart;
    private DateTimeOffset _lastDownNotificationUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _nextRecoveryTryUtc = DateTimeOffset.MinValue;

    public HealthMonitorJob(
        WatchedApp app,
        IHealthCheck healthCheck,
        IRecoveryStrategy recovery,
        NotificationDispatcher dispatcher,
        TimeSpan interval,
        int mailIntervalHours)
    {
        _app = app;
        _healthCheck = healthCheck;
        _recovery = recovery;
        _dispatcher = dispatcher;
        _interval = interval;
        _mailIntervalHours = mailIntervalHours;
    }

    public string Id => $"health:{_app.Type}:{_app.Name}";
    public TimeSpan Interval => _interval;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        Debug("ExecuteAsync tick");

        if (!_app.Enabled)
        {
            Debug("App disabled");
            return;
        }

        HealthCheckResult hc;
        try
        {
            hc = await _healthCheck.CheckAsync(ct);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR",
                $"[{_app.Name}] HealthCheck threw: {ex.Message}");
            return;
        }

        var currentStatus = new AppStatus
        {
            Name = _app.Name,
            ExePath = _app.ExePath,
            Enabled = _app.Enabled,
            IsRunning = hc.IsHealthy,
            LastStartError = hc.Error,
            PingMs = hc.DurationMs
        };

        Debug($"HealthCheck result: IsRunning={currentStatus.IsRunning}, ExePath={currentStatus.ExePath}");

        if (_lastStatus != null &&
            !string.Equals(_lastStatus.ExePath, currentStatus.ExePath, StringComparison.OrdinalIgnoreCase))
        {
            FileLogStore.WriteLine("INFO",
                $"[{_app.Name}] ExePath changed, resetting state");
            ResetState();
        }

        if (_lastStatus == null)
        {
            _lastStatus = currentStatus;

            if (!currentStatus.IsRunning)
            {
                FileLogStore.WriteLine("INFO",
                    $"[{_app.Name}] Initial DOWN detected");

                _consecutiveDown = 1;
                _recoveryAttemptedInThisDown = false;

                await TryRecoveryAsync(currentStatus, ct);
            }
            else
            {
                FileLogStore.WriteLine("INFO",
                    $"[{_app.Name}] Initial state is UP");
            }

            return;
        }

        if (_lastStatus.IsRunning && !currentStatus.IsRunning)
        {
            FileLogStore.WriteLine("INFO",
                $"[{_app.Name}] State transition UP -> DOWN");

            _consecutiveDown = 1;
            _recoveryAttemptedInThisDown = false;
            _upAlreadyNotifiedByRestart = false;

            await TryRecoveryAsync(currentStatus, ct);

            _lastStatus = currentStatus;
            return;
        }

        if (!_lastStatus.IsRunning && !currentStatus.IsRunning)
        {
            _consecutiveDown++;

            var now = DateTimeOffset.UtcNow;

            if (!_recoveryAttemptedInThisDown && now >= _nextRecoveryTryUtc)
            {
                FileLogStore.WriteLine("INFO",
                    $"[{_app.Name}] DOWN retry (count={_consecutiveDown})");

                await TryRecoveryAsync(currentStatus, ct);
            }
            else
            {
                Debug($"Still DOWN (count={_consecutiveDown}), nextTry={FormatTime(_nextRecoveryTryUtc)}");
            }

            _lastStatus = currentStatus;
            return;
        }

        if (!_lastStatus.IsRunning && currentStatus.IsRunning)
        {
            FileLogStore.WriteLine("INFO",
                $"[{_app.Name}] State transition DOWN -> UP");

            ResetCounters();

            if (!_upAlreadyNotifiedByRestart)
            {
                Dispatch(AppNotificationType.Up, currentStatus, false);
            }
            else
            {
                Debug("UP notification skipped (already sent via Restart)");
            }

            _upAlreadyNotifiedByRestart = false;
            _lastStatus = currentStatus;
            return;
        }

        Debug("State unchanged (UP)");
        _lastStatus = currentStatus;
    }

    private async Task TryRecoveryAsync(AppStatus current, CancellationToken ct)
    {
        _recoveryAttemptedInThisDown = true;

        Debug("Entering recovery");

        bool startAttempted = false;

        if (_recovery is NoRecoveryStrategy)
        {
            FileLogStore.WriteLine("INFO",
                $"[{_app.Name}] Recovery disabled");
        }
        else
        {
            try
            {
                var rec = await _recovery.TryRecoverAsync(_app, ct);
                startAttempted = rec.Attempted;

                if (!rec.Attempted)
                {
                    FileLogStore.WriteLine("INFO",
                        $"[{_app.Name}] Recovery skipped: {rec.Error}");
                    ScheduleRetry();
                }
                else if (!rec.Succeeded)
                {
                    _consecutiveStartFailures++;

                    FileLogStore.WriteLine("WARN",
                        $"[{_app.Name}] Recovery failed ({_consecutiveStartFailures}): {rec.Error}");
                    ScheduleRetry();
                }

                if (rec.Attempted)
                {
                    var hc = await _healthCheck.CheckAsync(ct);
                    Debug($"Post-recovery HealthCheck: IsHealthy={hc.IsHealthy}");

                    if (hc.IsHealthy)
                    {
                        var up = new AppStatus
                        {
                            Name = current.Name,
                            ExePath = current.ExePath,
                            Enabled = current.Enabled,
                            IsRunning = true,
                            PingMs = hc.DurationMs
                        };

                        ResetCounters();
                        _lastStatus = up;
                        _upAlreadyNotifiedByRestart = true;

                        FileLogStore.WriteLine("INFO",
                            $"[{_app.Name}] App confirmed RUNNING after recovery");

                        Dispatch(AppNotificationType.Restart, up, true);
                        return;
                    }

                    FileLogStore.WriteLine("WARN",
                        $"[{_app.Name}] App still DOWN after recovery attempt");
                }
            }
            catch (Exception ex)
            {
                FileLogStore.WriteLine("ERROR",
                    $"[{_app.Name}] Recovery threw exception: {ex.Message}");
                ScheduleRetry();
            }
        }

        Dispatch(AppNotificationType.Down, current, startAttempted);
    }

    private void Dispatch(
        AppNotificationType type,
        AppStatus status,
        bool startAttempted)
    {
        if (type == AppNotificationType.Down && _mailIntervalHours > 0)
        {
            var now = DateTimeOffset.UtcNow;

            if (_lastDownNotificationUtc != DateTimeOffset.MinValue)
            {
                var delta = now - _lastDownNotificationUtc;
                var min = TimeSpan.FromHours(_mailIntervalHours);

                if (delta < min)
                {
                    FileLogStore.WriteLine("INFO",
                        $"[{_app.Name}] DOWN notification skipped (throttled: {delta:mm\\:ss} < {min.TotalHours}h)");
                    return;
                }
            }

            _lastDownNotificationUtc = now;
        }

        FileLogStore.WriteLine("INFO",
            $"[{_app.Name}] Dispatching notification: {type}, startAttempted={startAttempted}");

        _dispatcher.Dispatch(new NotificationContext
        {
            Type = type,
            App = _app,
            Status = status,
            StartAttempted = startAttempted
        });
    }

    private void ResetCounters()
    {
        _consecutiveDown = 0;
        _consecutiveStartFailures = 0;
        _recoveryAttemptedInThisDown = false;
        _nextRecoveryTryUtc = DateTimeOffset.MinValue;
    }

    private void ResetState()
    {
        _lastStatus = null;
        _upAlreadyNotifiedByRestart = false;
        ResetCounters();
    }

    private void ScheduleRetry()
    {
        _recoveryAttemptedInThisDown = false;
        _nextRecoveryTryUtc = DateTimeOffset.UtcNow + RecoveryRetryMinInterval;
        Debug($"Next recovery try scheduled at {FormatTime(_nextRecoveryTryUtc)}");
    }

    private static string FormatTime(DateTimeOffset t)
        => t == DateTimeOffset.MinValue
            ? "-"
            : t.ToLocalTime().ToString("HH:mm:ss");

    private void Debug(string message)
    {
        if (!ISDEBUG)
            return;

        FileLogStore.WriteLine(
            "DEBUG",
            $"[{_app.Name}] {message}");
    }

    public JobSnapshot CreateSnapshot(JobScheduler.JobEntry entry)
    {
        return new JobSnapshot
        {
            JobId = Id,
            Kind = JobKind.HealthMonitor,
            JobType = _app.Type.ToString(),
            Interval = _interval,
            LastCheckUtc = entry.LastRunUtc,
            NextRunUtc = entry.NextRunUtc,
            AppName = _app.Name,
            EffectiveState = _lastStatus == null
                ? "UNKNOWN"
                : _lastStatus.IsRunning ? "UP" : "DOWN",
            ConsecutiveDown = _consecutiveDown,
            ConsecutiveStartFailures = _consecutiveStartFailures,
            PingMs = _lastStatus?.PingMs
        };
    }

    public void Dispose() { }
}
