using AppWatchdog.Service.HealthChecks;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Recovery;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.Jobs;

public sealed class HealthMonitorJob : IJob
{
    private readonly WatchedApp _app;
    private readonly IHealthCheck _healthCheck;
    private readonly IRecoveryStrategy _recovery;
    private readonly NotificationDispatcher _dispatcher;

    private readonly TimeSpan _interval;
    private readonly int _mailIntervalHours;

    private AppStatus? _lastStatus;
    private DateTimeOffset _lastDownNotificationUtc = DateTimeOffset.MinValue;

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
        if (!_app.Enabled)
            return;

        HealthCheckResult hc;
        try
        {
            hc = await _healthCheck.CheckAsync(ct);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"HealthCheck threw for '{_app.Name}': {ex.Message}");
            return;
        }

        var currentStatus = new AppStatus
        {
            Name = _app.Name,
            Enabled = _app.Enabled,
            IsRunning = hc.IsHealthy,
            LastStartError = hc.Error,
            PingMs = hc.DurationMs
        };

        // First run → baseline only
        if (_lastStatus == null)
        {
            _lastStatus = currentStatus;
            return;
        }

        // UP -> DOWN
        if (_lastStatus.IsRunning && !currentStatus.IsRunning)
        {
            await HandleDownAsync(currentStatus, ct);
        }
        // DOWN -> UP
        else if (!_lastStatus.IsRunning && currentStatus.IsRunning)
        {
            Dispatch(AppNotificationType.Up, currentStatus, false);
        }

        _lastStatus = currentStatus;
    }

    private async Task HandleDownAsync(AppStatus current, CancellationToken ct)
    {
        bool startAttempted = false;

        if (_recovery is not NoRecoveryStrategy)
        {
            try
            {
                var rec = await _recovery.TryRecoverAsync(_app, ct);
                startAttempted = rec.Attempted;

                if (startAttempted)
                {
                    var hc = await _healthCheck.CheckAsync(ct);
                    if (hc.IsHealthy)
                    {
                        Dispatch(
                            AppNotificationType.Restart,
                            new AppStatus
                            {
                                Name = _app.Name,
                                ExePath = _app.ExePath,
                                Enabled = _app.Enabled,
                                IsRunning = true
                            },
                            true);

                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogStore.WriteLine(
                    "ERROR",
                    $"Recovery failed for '{_app.Name}': {ex.Message}");
            }
        }

        Dispatch(AppNotificationType.Down, current, startAttempted);
    }

    public JobSnapshot CreateSnapshot(JobScheduler.JobEntry entry)
    {
        var status = _lastStatus;

        return new JobSnapshot
        {
            JobId = Id,
            Kind = JobKind.HealthMonitor,
            JobType = _app.Type.ToString(),
            Interval = _interval,

            LastCheckUtc = entry.LastRunUtc,
            NextRunUtc = entry.NextRunUtc,

            AppName = _app.Name,

            EffectiveState = status == null
                ? "UNKNOWN"
                : status.IsRunning ? "UP" : "DOWN",

            ConsecutiveDown = status != null && !status.IsRunning ? 1 : 0,
            ConsecutiveStartFailures = 0, 

            PingMs = status?.PingMs
        };
    }


    private void Dispatch(
        AppNotificationType type,
        AppStatus status,
        bool startAttempted)
    {
        if (type == AppNotificationType.Down && _mailIntervalHours > 0)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastDownNotificationUtc < TimeSpan.FromHours(_mailIntervalHours))
                return;

            _lastDownNotificationUtc = now;
        }

        _dispatcher.Dispatch(new NotificationContext
        {
            Type = type,
            App = _app,
            Status = status,
            StartAttempted = startAttempted
        });
    }

    public void Dispose() { }
}
