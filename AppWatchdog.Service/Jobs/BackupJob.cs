using AppWatchdog.Service.Backups;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Executes scheduled backups based on configured plans.
/// </summary>
public sealed class BackupJob : IJob, IJobStatusProvider
{
    private readonly Func<WatchdogConfig> _getConfig;
    private readonly NotificationDispatcher _dispatcher;
    private readonly string _planId;
    private volatile bool _forceRun;

    private readonly object _sync = new();

    /// <summary>
    /// Raised when the job reports a status event.
    /// </summary>
    public event Action<JobEvent>? EventRaised;

    private int? _progress;
    private string _status = "Idle";
    private DateTimeOffset _plannedStartUtc = DateTimeOffset.MinValue;
    private bool _running;
    private bool _scheduleInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupJob"/> class.
    /// </summary>
    /// <param name="getConfig">Delegate to retrieve configuration.</param>
    /// <param name="dispatcher">Notification dispatcher.</param>
    /// <param name="planId">Backup plan identifier.</param>
    public BackupJob(Func<WatchdogConfig> getConfig, NotificationDispatcher dispatcher, string planId)
    {
        _getConfig = getConfig;
        _dispatcher = dispatcher;
        _planId = planId;
    }

    /// <summary>
    /// Gets the unique job identifier.
    /// </summary>
    public string Id => $"backup:{_planId}";

    /// <summary>
    /// Gets the polling interval used to check if the job is due.
    /// </summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the current progress percentage.
    /// </summary>
    public int? ProgressPercent
    {
        get { lock (_sync) return _progress; }
    }

    /// <summary>
    /// Gets the current status text.
    /// </summary>
    public string StatusText
    {
        get { lock (_sync) return _status; }
    }

    /// <summary>
    /// Gets the next planned start time in UTC.
    /// </summary>
    public DateTimeOffset PlannedStartUtc
    {
        get { lock (_sync) return _plannedStartUtc; }
    }

    /// <summary>
    /// Forces the next execution regardless of schedule.
    /// </summary>
    public void ForceRun()
    {
        _forceRun = true;
    }

    /// <summary>
    /// Raises a job event and logs it.
    /// </summary>
    /// <param name="type">Event type.</param>
    /// <param name="progress">Optional progress percentage.</param>
    /// <param name="status">Optional status text.</param>
    private void Raise(JobEventType type, int? progress = null, string? status = null)
    {
        EventRaised?.Invoke(new JobEvent
        {
            JobId = Id,
            Type = type,
            Progress = progress,
            Status = status,
            Timestamp = DateTimeOffset.UtcNow
        });

        if (type == JobEventType.Planned)
            return;

        FileLogStore.WriteLine(
                "INFO",
                $"BackupJob '{Id}' type: {type} progress: {progress}");
    }

    /// <summary>
    /// Executes the backup when due or forced.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when execution finishes.</returns>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cfg = _getConfig();
        var plan = cfg.Backups.FirstOrDefault(x =>
            string.Equals(x.Id, _planId, StringComparison.OrdinalIgnoreCase));

        if (plan == null || !plan.Enabled)
        {
            lock (_sync)
            {
                _progress = null;
                _status = "Disabled";
                _plannedStartUtc = DateTimeOffset.UtcNow.AddYears(10);
            }

            Raise(JobEventType.Planned, status: "Disabled");
            return;
        }

        var nowLocal = DateTimeOffset.Now;

        lock (_sync)
        {
            if (!_scheduleInitialized)
            {
                _plannedStartUtc = BackupSchedule.ComputeNextPlannedUtc(plan.Schedule, nowLocal);
                _scheduleInitialized = true;
            }

            if (_running)
                return;
        }


        var due = BackupSchedule.IsDue(nowLocal, _plannedStartUtc);

        if (!due && !_forceRun)
        {
            lock (_sync)
            {
                _progress = null;
                _status = "Waiting";
            }

            Raise(JobEventType.Planned, status: "Waiting");
            return;
        }

        _forceRun = false;

        var startedUtc = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            _running = true;
            _progress = 0;
            _status = "Starting";
        }

        Raise(JobEventType.Started, 0, "Starting");

        _dispatcher.DispatchBackup(new BackupNotificationContext
        {
            Type = BackupNotificationType.Started,
            Plan = plan,
            StartedUtc = startedUtc
        });

        try
        {
            await using var storage = CreateStorage(plan.Target);
            var engine = new BackupEngine();

            var result = await engine.CreateBackupAsync(
                plan,
                storage,
                report: (pct, text, _) =>
                {
                    lock (_sync)
                    {
                        _progress = pct;
                        _status = text;
                        _plannedStartUtc = BackupSchedule.ComputeNextPlannedUtc(
                            plan.Schedule,
                            DateTimeOffset.Now);
                    }

                    Raise(JobEventType.Progress, pct, text);
                },
                ct);

            lock (_sync)
            {
                _progress = 100;
                _status = "Done";
                _plannedStartUtc = BackupSchedule.ComputeNextPlannedUtc(
                                                                       plan.Schedule,
                                                                       DateTimeOffset.Now);
            }

            Raise(JobEventType.Completed, 100, "Done");

            _dispatcher.DispatchBackup(new BackupNotificationContext
            {
                Type = BackupNotificationType.Completed,
                Plan = plan,
                StartedUtc = startedUtc,
                FinishedUtc = DateTimeOffset.UtcNow,
                SizeBytes = result.SizeBytes
            });
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _progress = null;
                _status = "Failed: " + ex.Message;
            }

            Raise(JobEventType.Failed, null, ex.Message);

            _dispatcher.DispatchBackup(new BackupNotificationContext
            {
                Type = BackupNotificationType.Failed,
                Plan = plan,
                StartedUtc = startedUtc,
                FinishedUtc = DateTimeOffset.UtcNow,
                Error = ex.Message
            });

            FileLogStore.WriteLine(
                "ERROR",
                $"BackupJob '{_planId}' failed: {ex}");
        }
        finally
        {
            lock (_sync)
            {
                _running = false;
            }
        }
    }

    /// <summary>
    /// Creates a backup storage implementation from target settings.
    /// </summary>
    /// <param name="target">Backup target configuration.</param>
    /// <returns>The storage implementation.</returns>
    private static IBackupStorage CreateStorage(BackupTargetConfig target)
    {
        return target.Type switch
        {
            BackupTargetType.Sftp => new SftpStorage(
                target.SftpHost,
                target.SftpPort <= 0 ? 22 : target.SftpPort,
                target.SftpUser,
                target.SftpPassword,
                target.SftpRemoteDirectory,
                target.SftpHostKeyFingerprint),

            _ => new LocalStorage(target.LocalDirectory)
        };
    }

    /// <summary>
    /// Disposes the job.
    /// </summary>
    public void Dispose() { }
}
