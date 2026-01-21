using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;

namespace AppWatchdog.Service.Jobs;

public sealed class JobScheduler : IDisposable
{
    public sealed class JobEntry
    {
        public required IJob Job { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public required Task Runner { get; init; }

        public DateTimeOffset LastRunUtc;
        public DateTimeOffset NextRunUtc;
    }


    private readonly Dictionary<string, JobEntry> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public void AddOrUpdate(IJob job)
    {
        Remove(job.Id);

        var cts = new CancellationTokenSource();

        JobEntry entry = null!;

        var runner = Task.Run(async () =>
        {
            // entry ist hier gültig, weil Task erst nach Zuweisung läuft
            await RunLoopAsync(entry, cts.Token);
        });

        entry = new JobEntry
        {
            Job = job,
            Cts = cts,
            Runner = runner
        };

        _jobs[job.Id] = entry;
    }

    public void Remove(string id)
    {
        if (_jobs.Remove(id, out var entry))
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
            // job.Dispose() passiert im finally des RunLoops
        }
    }

    public async Task RunAllOnceAsync(CancellationToken ct = default)
    {
        // Snapshot über Values: während Ausführung könnten Jobs neu gebaut werden
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
                // Job selbst loggt i.d.R. — hier bewusst silent, damit Trigger nicht alles abbricht
            }
        }
    }

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

    public IReadOnlyList<JobSnapshot> GetSnapshots()
    {
        return _jobs.Values.Select(e =>
        {
            if (e.Job is HealthMonitorJob hm)
                return hm.CreateSnapshot(e);

            return new JobSnapshot
            {
                JobId = e.Job.Id,
                Kind = e.Job switch
                {
                    HealthMonitorJob => JobKind.HealthMonitor,
                    KumaPingJob => JobKind.KumaPing,
                    SnapshotJob => JobKind.Snapshot,
                    _ => JobKind.Unknown
                },
                JobType = e.Job.GetType().Name,
                Interval = e.Job.Interval,
                LastCheckUtc = e.LastRunUtc,
                NextRunUtc = e.NextRunUtc
            };
        }).ToList();
    }

    public void Dispose()
    {
        foreach (var id in _jobs.Keys.ToList())
            Remove(id);

        _jobs.Clear();
    }
}
