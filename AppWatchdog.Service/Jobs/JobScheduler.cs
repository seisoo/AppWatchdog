using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using System.Collections;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Schedules and executes background jobs on a fixed interval.
/// </summary>
public sealed class JobScheduler : IDisposable
{
    /// <summary>
    /// Tracks a scheduled job and its execution metadata.
    /// </summary>
    public sealed class JobEntry
    {
        /// <summary>
        /// Gets or sets the job instance.
        /// </summary>
        public required IJob Job { get; init; }

        /// <summary>
        /// Gets or sets the cancellation token source for the job loop.
        /// </summary>
        public required CancellationTokenSource Cts { get; init; }

        /// <summary>
        /// Gets or sets the task running the job loop.
        /// </summary>
        public required Task Runner { get; set; }

        /// <summary>
        /// Last run time in UTC.
        /// </summary>
        public DateTimeOffset LastRunUtc;

        /// <summary>
        /// Next scheduled run time in UTC.
        /// </summary>
        public DateTimeOffset NextRunUtc;

        /// <summary>
        /// Gets the recent events for the job.
        /// </summary>
        public List<JobEvent> Events { get; } = new();

        /// <summary>
        /// Synchronization object for accessing events.
        /// </summary>
        public object EventsSync { get; } = new();

    }

    private readonly Dictionary<string, JobEntry> _jobs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds or updates a job entry in the scheduler.
    /// </summary>
    /// <param name="job">Job to add or update.</param>
    public void AddOrUpdate(IJob job)
    {
        Remove(job.Id);

        var cts = new CancellationTokenSource();

        var entry = new JobEntry
        {
            Job = job,
            Cts = cts,
            Runner = Task.CompletedTask
        };

        entry.Runner = Task.Run(async () =>
        {
            await RunLoopAsync(entry, cts.Token);
        });

        if (job is IJobEvents ev)
        {
            ev.EventRaised += e =>
            {
                lock (entry.EventsSync)
                {
                    entry.Events.Add(e);
                    if (entry.Events.Count > 100)
                        entry.Events.RemoveAt(0);
                }

            };
        }

        _jobs[job.Id] = entry;
    }

    /// <summary>
    /// Removes a job entry by ID.
    /// </summary>
    /// <param name="id">Job identifier.</param>
    public void Remove(string id)
    {
        if (_jobs.Remove(id, out var entry))
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
        }
    }

    /// <summary>
    /// Executes all jobs once immediately.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the execution.</returns>
    public async Task RunAllOnceAsync(CancellationToken ct = default)
    {
        var entries = _jobs.Values.ToList();
        foreach (var e in entries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                e.LastRunUtc = DateTimeOffset.UtcNow;
                e.NextRunUtc = e.LastRunUtc + e.Job.Interval;

                await e.Job.ExecuteAsync(ct);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Runs a job on its interval until canceled.
    /// </summary>
    /// <param name="entry">Job entry to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the loop.</returns>
    private async Task RunLoopAsync(JobEntry entry, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(entry.Job.Interval);

            while (await timer.WaitForNextTickAsync(ct))
            {
                entry.LastRunUtc = DateTimeOffset.UtcNow;
                entry.NextRunUtc = entry.LastRunUtc + entry.Job.Interval;

                await entry.Job.ExecuteAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            entry.Job.Dispose();
        }
    }

    /// <summary>
    /// Forces execution of a specific job when supported.
    /// </summary>
    /// <param name="jobId">Job identifier.</param>
    public void ForceRun(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var entry))
        {
            if (entry.Job is BackupJob bj)
                bj.ForceRun();
            // RestoreJob doesn't need ForceRun - it's triggered via config
        }
    }

    private Func<WatchdogConfig>? _getConfig;

    /// <summary>
    /// Configures the scheduler with a configuration provider.
    /// </summary>
    /// <param name="getConfig">Delegate that returns configuration.</param>
    public void Configure(Func<WatchdogConfig> getConfig)
    {
        _getConfig = getConfig;
    }

    /// <summary>
    /// Creates job snapshots for UI consumption.
    /// </summary>
    /// <returns>List of job snapshots.</returns>
    public List<JobSnapshot> GetSnapshots()
    {
        var cfg = _getConfig?.Invoke();
        
        return _jobs.Values.Select(e =>
        {
            if (e.Job is HealthMonitorJob hm)
                return hm.CreateSnapshot(e);

            IReadOnlyList<JobEvent> events;

            lock (e.EventsSync)
            {
                events = e.Events.Count == 0
                    ? Array.Empty<JobEvent>()
                    : e.Events.ToList();
            }

            int? progress = null;
            string? status = null;
            DateTimeOffset planned = default;

            if (e.Job is IJobStatusProvider sp)
            {
                progress = sp.ProgressPercent;
                status = sp.StatusText;
                planned = sp.PlannedStartUtc;
            }

            var snapshot = new JobSnapshot
            {
                JobId = e.Job.Id,
                Kind = e.Job switch
                {
                    HealthMonitorJob => JobKind.HealthMonitor,
                    KumaPingJob => JobKind.KumaPing,
                    SnapshotJob => JobKind.Snapshot,
                    BackupJob => JobKind.Backup,
                    RestoreJob => JobKind.Restore,
                    _ => JobKind.Unknown
                },
                JobType = e.Job.GetType().Name,
                Interval = e.Job.Interval,
                LastCheckUtc = e.LastRunUtc,
                NextRunUtc = e.NextRunUtc,
                ProgressPercent = progress,
                StatusText = status,
                PlannedStartUtc = planned,
                Events = events
            };

            if (e.Job is KumaPingJob kuma)
            {
                snapshot.AppName = kuma.AppName;
                snapshot.ExePath = kuma.ExePath;
                snapshot.HealthCheckType = "KumaPing";
                snapshot.HealthCheckTarget = kuma.ExePath;
            }

            // Backup Job Details
            if (e.Job is BackupJob && cfg != null)
            {
                var planId = e.Job.Id.Replace("backup:", "");
                var plan = cfg.Backups.FirstOrDefault(x => string.Equals(x.Id, planId, StringComparison.OrdinalIgnoreCase));
                if (plan != null)
                {
                    snapshot.BackupPlanName = plan.Name;
                    snapshot.BackupSourcePath = plan.Source.Type switch
                    {
                        BackupSourceType.Folder => plan.Source.Path,
                        BackupSourceType.File => plan.Source.Path,
                        BackupSourceType.MsSql => plan.Source.SqlDatabase,
                        _ => plan.Source.Path
                    };
                    snapshot.BackupTargetPath = plan.Target.Type switch
                    {
                        BackupTargetType.Local => plan.Target.LocalDirectory,
                        BackupTargetType.Sftp => $"{plan.Target.SftpHost}:{plan.Target.SftpRemoteDirectory}",
                        _ => plan.Target.LocalDirectory
                    };
                }
            }

            // Restore Job Details
            if (e.Job is RestoreJob && cfg != null)
            {
                var restoreId = e.Job.Id.Replace("restore:", "");
                var restore = cfg.Restores.FirstOrDefault(x => string.Equals(x.Id, restoreId, StringComparison.OrdinalIgnoreCase));
                if (restore != null)
                {
                    snapshot.RestorePlanName = restore.Name;
                    var backupPlan = cfg.Backups.FirstOrDefault(x => string.Equals(x.Id, restore.BackupPlanId, StringComparison.OrdinalIgnoreCase));
                    snapshot.BackupSourcePath = backupPlan?.Name;
                }
            }

            return snapshot;
        }).ToList();
    }



    /// <summary>
    /// Disposes all jobs and clears the scheduler.
    /// </summary>
    public void Dispose()
    {
        foreach (var id in _jobs.Keys.ToList())
            Remove(id);

        _jobs.Clear();
    }
}
