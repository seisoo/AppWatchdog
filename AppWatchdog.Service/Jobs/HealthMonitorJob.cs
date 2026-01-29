using AppWatchdog.Service.HealthChecks;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Recovery;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Monitors application health and triggers recovery and notifications.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthMonitorJob"/> class.
    /// </summary>
    /// <param name="app">Watched application configuration.</param>
    /// <param name="healthCheck">Health check implementation.</param>
    /// <param name="recovery">Recovery strategy for failures.</param>
    /// <param name="dispatcher">Notification dispatcher.</param>
    /// <param name="interval">Execution interval.</param>
    /// <param name="mailIntervalHours">Notification throttle interval in hours.</param>
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

    /// <summary>
    /// Gets the unique job identifier.
    /// </summary>
    public string Id => $"health:{_app.Type}:{_app.Name}";

    /// <summary>
    /// Gets the execution interval.
    /// </summary>
    public TimeSpan Interval => _interval;

    /// <summary>
    /// Executes the health check and recovery logic.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the execution.</returns>
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

    /// <summary>
    /// Attempts to recover the application when down.
    /// </summary>
    /// <param name="current">Current status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the recovery attempt.</returns>
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

    /// <summary>
    /// Dispatches a notification for the specified state.
    /// </summary>
    /// <param name="type">Notification type.</param>
    /// <param name="status">Current app status.</param>
    /// <param name="startAttempted">Whether a recovery attempt occurred.</param>
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

    /// <summary>
    /// Resets counters for down and recovery state.
    /// </summary>
    private void ResetCounters()
    {
        _consecutiveDown = 0;
        _consecutiveStartFailures = 0;
        _recoveryAttemptedInThisDown = false;
        _nextRecoveryTryUtc = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Resets cached status and counters.
    /// </summary>
    private void ResetState()
    {
        _lastStatus = null;
        _upAlreadyNotifiedByRestart = false;
        ResetCounters();
    }

    /// <summary>
    /// Schedules the next recovery attempt.
    /// </summary>
    private void ScheduleRetry()
    {
        _recoveryAttemptedInThisDown = false;
        _nextRecoveryTryUtc = DateTimeOffset.UtcNow + RecoveryRetryMinInterval;
        Debug($"Next recovery try scheduled at {FormatTime(_nextRecoveryTryUtc)}");
    }

    /// <summary>
    /// Formats a timestamp for logging.
    /// </summary>
    /// <param name="t">Timestamp to format.</param>
    /// <returns>Formatted time string.</returns>
    private static string FormatTime(DateTimeOffset t)
        => t == DateTimeOffset.MinValue
            ? "-"
            : t.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>
    /// Writes debug output when enabled.
    /// </summary>
    /// <param name="message">Message to write.</param>
    private void Debug(string message)
    {
        if (!ISDEBUG)
            return;

        FileLogStore.WriteLine(
            "DEBUG",
            $"[{_app.Name}] {message}");
    }

    /// <summary>
    /// Creates a job snapshot for UI display.
    /// </summary>
    /// <param name="entry">Scheduler entry.</param>
    /// <returns>The snapshot.</returns>
    public JobSnapshot CreateSnapshot(JobScheduler.JobEntry entry)
    {
        var healthCheckType = _app.Type.ToString();
        var healthCheckTarget = _app.Type switch
        {
            WatchTargetType.HttpEndpoint => _app.Url ?? "—",
            WatchTargetType.TcpPort => $"{_app.Host}:{_app.Port}",
            WatchTargetType.Executable => _app.ExePath ?? "—",
            WatchTargetType.WindowsService => _app.ServiceName ?? "—",
            _ => "—"
        };

        return new JobSnapshot
        {
            JobId = Id,
            Kind = JobKind.HealthMonitor,
            JobType = _app.Type.ToString(),
            Interval = _interval,
            LastCheckUtc = entry.LastRunUtc,
            NextRunUtc = entry.NextRunUtc,
            AppName = _app.Name,
            ExePath = _app.ExePath ?? "",
            EffectiveState = _lastStatus == null
                ? "UNKNOWN"
                : _lastStatus.IsRunning ? "UP" : "DOWN",
            ConsecutiveDown = _consecutiveDown,
            ConsecutiveStartFailures = _consecutiveStartFailures,
            PingMs = _lastStatus?.PingMs,
            HealthCheckType = healthCheckType,
            HealthCheckTarget = healthCheckTarget
        };
    }

    /// <summary>
    /// Disposes the job.
    /// </summary>
    public void Dispose() { }
}
