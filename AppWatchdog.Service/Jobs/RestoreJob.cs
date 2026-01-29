using AppWatchdog.Service.Backups;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Executes restore operations for configured restore plans.
/// </summary>
public sealed class RestoreJob : IJob, IJobStatusProvider
{
    private readonly Func<WatchdogConfig> _getConfig;
    private readonly Action<WatchdogConfig> _onConfigChanged;
    private readonly string _restoreId;

    private readonly object _sync = new();

    private int? _progress;
    private string _status = "Idle";
    private DateTimeOffset _plannedStartUtc = DateTimeOffset.MinValue;
    private bool _running;
    private bool _completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestoreJob"/> class.
    /// </summary>
    /// <param name="getConfig">Delegate to retrieve configuration.</param>
    /// <param name="restoreId">Restore plan identifier.</param>
    /// <param name="onConfigChanged">Callback invoked after config changes.</param>
    public RestoreJob(Func<WatchdogConfig> getConfig, string restoreId, Action<WatchdogConfig> onConfigChanged)
    {
        _getConfig = getConfig;
        _restoreId = restoreId;
        _onConfigChanged = onConfigChanged;
    }

    /// <summary>
    /// Gets the unique job identifier.
    /// </summary>
    public string Id => $"restore:{_restoreId}";

    /// <summary>
    /// Gets the polling interval used to check restore status.
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
    /// Gets the planned start time in UTC.
    /// </summary>
    public DateTimeOffset PlannedStartUtc
    {
        get { lock (_sync) return _plannedStartUtc; }
    }

    /// <summary>
    /// Executes the restore operation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the execution.</returns>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            if (_completed)
                return;
        }

        var cfg = _getConfig();
        var restore = cfg.Restores.FirstOrDefault(x =>
            string.Equals(x.Id, _restoreId, StringComparison.OrdinalIgnoreCase));

        if (restore == null || !restore.Enabled)
        {
            lock (_sync)
            {
                _progress = null;
                _status = "Disabled";
                _plannedStartUtc = DateTimeOffset.UtcNow.AddYears(10);
            }
            return;
        }

        var plan = cfg.Backups.FirstOrDefault(x =>
            string.Equals(x.Id, restore.BackupPlanId, StringComparison.OrdinalIgnoreCase));

        if (plan == null)
        {
            lock (_sync)
            {
                _progress = null;
                _status = "Invalid plan";
            }
            return;
        }

        lock (_sync)
        {
            if (_running)
                return;

            _running = true;
            _progress = 0;
            _status = "Starting";
            _plannedStartUtc = DateTimeOffset.UtcNow;
        }

        try
        {
            await using var storage = CreateStorage(restore.Target);
            var engine = new BackupEngine();

            await engine.RestoreAsync(
                restore,
                plan,
                storage,
                report: (pct, text, _) =>
                {
                    lock (_sync)
                    {
                        _progress = pct;
                        _status = text;
                        _plannedStartUtc = DateTimeOffset.UtcNow;
                    }
                },
                ct);

            lock (_sync)
            {
                _progress = 100;
                _status = "Done";
            }

            // Wenn RunOnce, Restore aus Config entfernen
            if (restore.RunOnce)
            {
                var updatedCfg = _getConfig();
                var idx = updatedCfg.Restores.FindIndex(r => 
                    string.Equals(r.Id, _restoreId, StringComparison.OrdinalIgnoreCase));
                
                if (idx >= 0)
                {
                    updatedCfg.Restores.RemoveAt(idx);
                    _onConfigChanged(updatedCfg);
                    
                    FileLogStore.WriteLine("INFO", 
                        $"RestoreJob '{_restoreId}' completed and removed from config (RunOnce)");
                }

                lock (_sync)
                {
                    _completed = true;
                }
            }
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _progress = null;
                _status = "Failed: " + ex.Message;
            }

            FileLogStore.WriteLine(
                "ERROR",
                $"RestoreJob '{_restoreId}' failed: {ex}");

            // Auch bei Fehler aus Config entfernen wenn RunOnce
            if (restore.RunOnce)
            {
                var updatedCfg = _getConfig();
                var idx = updatedCfg.Restores.FindIndex(r => 
                    string.Equals(r.Id, _restoreId, StringComparison.OrdinalIgnoreCase));
                
                if (idx >= 0)
                {
                    updatedCfg.Restores.RemoveAt(idx);
                    _onConfigChanged(updatedCfg);
                    
                    FileLogStore.WriteLine("INFO", 
                        $"RestoreJob '{_restoreId}' removed from config after failure (RunOnce)");
                }

                lock (_sync)
                {
                    _completed = true;
                }
            }
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
