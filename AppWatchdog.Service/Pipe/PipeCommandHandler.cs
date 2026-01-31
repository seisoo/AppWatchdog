using AppWatchdog.Service.Backups;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using System.IO.Compression;

namespace AppWatchdog.Service.Pipe;

/// <summary>
/// Handles incoming pipe commands and routes them to service operations.
/// </summary>
public sealed class PipeCommandHandler
{
    private readonly string _configPath;

    private readonly Func<WatchdogConfig> _getConfig;
    private readonly Func<ServiceSnapshot?> _getSnapshot;
    private readonly Action _triggerCheck;
    private readonly Action _rebuildJobs;
    private readonly Action<WatchdogConfig> _onConfigSaved;

    private readonly NotificationDispatcher _dispatcher;
    private readonly JobScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeCommandHandler"/> class.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="getConfig">Delegate that returns the current configuration.</param>
    /// <param name="getSnapshot">Delegate that returns the latest snapshot.</param>
    /// <param name="triggerCheck">Delegate that triggers immediate checks.</param>
    /// <param name="rebuildJobs">Delegate that rebuilds job schedules.</param>
    /// <param name="onConfigSaved">Callback invoked after saving config.</param>
    /// <param name="dispatcher">Notification dispatcher.</param>
    /// <param name="scheduler">Job scheduler.</param>
    public PipeCommandHandler(
        string configPath,
        Func<WatchdogConfig> getConfig,
        Func<ServiceSnapshot?> getSnapshot,
        Action triggerCheck,
        Action rebuildJobs,
        Action<WatchdogConfig> onConfigSaved,
        NotificationDispatcher dispatcher,
        JobScheduler scheduler)
    {
        _configPath = configPath;
        _getConfig = getConfig;
        _getSnapshot = getSnapshot;
        _triggerCheck = triggerCheck;
        _rebuildJobs = rebuildJobs;
        _onConfigSaved = onConfigSaved;
        _dispatcher = dispatcher;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Handles a pipe request synchronously.
    /// </summary>
    /// <param name="req">Incoming request.</param>
    /// <returns>The response to send back.</returns>
    public PipeProtocol.Response Handle(PipeProtocol.Request req)
        => HandleAsync(req).GetAwaiter().GetResult();

    /// <summary>
    /// Handles a pipe request asynchronously.
    /// </summary>
    /// <param name="req">Incoming request.</param>
    /// <returns>The response to send back.</returns>
    private async Task<PipeProtocol.Response> HandleAsync(PipeProtocol.Request req)
    {
        try
        {
            if (req.Version != PipeProtocol.ProtocolVersion)
            {
                return Error(
                    $"Protocol mismatch. Service={PipeProtocol.ProtocolVersion}, Client={req.Version}");
            }

            switch (req.Command)
            {
                case PipeProtocol.CmdPing:
                    return Ok();

                case PipeProtocol.CmdGetConfig:
                    return Ok(_getConfig());

                case PipeProtocol.CmdSaveConfig:
                    return SaveConfig(req);

                case PipeProtocol.CmdGetStatus:
                    return GetStatus();

                case PipeProtocol.CmdTriggerCheck:
                    _triggerCheck();
                    return Ok();

                case PipeProtocol.CmdListLogDays:
                    return Ok(new LogDaysResponse
                    {
                        Days = FileLogStore.ListDays()
                    });

                case PipeProtocol.CmdGetLogDay:
                    return GetLogDay(req);

                case PipeProtocol.CmdTestSmtp:
                    return RunNotificationTest(NotificationChannel.Mail);

                case PipeProtocol.CmdTestNtfy:
                    return RunNotificationTest(NotificationChannel.Ntfy);

                case PipeProtocol.CmdTestDiscord:
                    return RunNotificationTest(NotificationChannel.Discord);

                case PipeProtocol.CmdTestTelegram:
                    return RunNotificationTest(NotificationChannel.Telegram);

                case PipeProtocol.CmdGetLogPath:
                    return Ok(new LogPathResponse
                    {
                        Path = FileLogStore.LogDir
                    });

                case PipeProtocol.CmdGetJobs:
                    return Ok(new JobSnapshotsResponse
                    {
                        Jobs = _scheduler.GetSnapshots()
                    });

                case PipeProtocol.CmdRebuildJobs:
                    _rebuildJobs();
                    FileLogStore.WriteLine("INFO", "Jobs werden neu aufgebaut (per Pipe).");
                    return Ok();

                case PipeProtocol.CmdListBackups:
                    return Ok(new BackupListResponse
                    {
                        Plans = _getConfig().Backups
                    });

                case PipeProtocol.CmdTriggerBackup:
                    return TriggerBackup(req);

                case PipeProtocol.CmdPurgeBackupArtifacts:
                    return await PurgeBackupArtifactsAsync(req);

                case PipeProtocol.CmdListBackupArtifacts:
                    return await ListBackupArtifacts(req);

                case PipeProtocol.CmdGetBackupManifest:
                    return await GetBackupManifest(req);

                case PipeProtocol.CmdTriggerRestore:
                    return await TriggerRestoreAsync(req);

                case PipeProtocol.CmdExportConfig:
                    return ExportConfig();

                case PipeProtocol.CmdImportConfig:
                    return ImportConfig(req);

                default:
                    return Error("Unknown command");
            }
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"Pipe request FAILED: {req.Command} – {ex}");
            return Error(ex.Message);
        }
    }

    /// <summary>
    /// Saves the configuration provided in the request payload.
    /// </summary>
    /// <param name="req">Request containing the config payload.</param>
    /// <returns>The response indicating success or failure.</returns>
    private PipeProtocol.Response SaveConfig(PipeProtocol.Request req)
    {
        if (string.IsNullOrWhiteSpace(req.PayloadJson))
            return Error("No payload");

        var cfg = PipeProtocol.Deserialize<WatchdogConfig>(req.PayloadJson);
        if (cfg == null)
            return Error("Invalid payload");

        ConfigStore.Save(_configPath, cfg);
        _onConfigSaved(cfg);

        FileLogStore.WriteLine("INFO", "Config gespeichert (per Pipe).");
        return Ok();
    }

    /// <summary>
    /// Returns the latest service status snapshot.
    /// </summary>
    /// <returns>The response containing the snapshot.</returns>
    private PipeProtocol.Response GetStatus()
    {
        var snap = _getSnapshot();
        if (snap == null)
        {
            snap = new ServiceSnapshot
            {
                Timestamp = DateTimeOffset.Now,
                SessionState = UserSessionLauncher.GetSessionState(),
                Apps = new List<AppStatus>(),
                SystemInfo = Worker.SystemInfoCollect(),
                PipeProtocolVersion = PipeProtocol.ProtocolVersion
            };
        }

        return Ok(snap);
    }

    /// <summary>
    /// Retrieves a log day from the request payload.
    /// </summary>
    /// <param name="req">Request containing the log day.</param>
    /// <returns>The response containing log contents.</returns>
    private PipeProtocol.Response GetLogDay(PipeProtocol.Request req)
    {
        if (string.IsNullOrWhiteSpace(req.PayloadJson))
            return Error("No payload");

        var r = PipeProtocol.Deserialize<LogDayRequest>(req.PayloadJson);
        if (r == null)
            return Error("Invalid payload");

        return Ok(new LogDayResponse
        {
            Day = r.Day,
            Content = FileLogStore.ReadDay(r.Day)
        });
    }

    /// <summary>
    /// Runs a notification test for the specified channel.
    /// </summary>
    /// <param name="channel">Channel to test.</param>
    /// <returns>The response indicating success or failure.</returns>
    private PipeProtocol.Response RunNotificationTest(NotificationChannel channel)
    {
        try
        {
            var cfg = _getConfig();
            string? error = channel switch
            {
                NotificationChannel.Mail => new MailNotifier(cfg.Smtp).IsConfigured(out var err) ? null : err,
                NotificationChannel.Ntfy => new NtfyNotifier(cfg.Ntfy).IsConfigured(out var err) ? null : err,
                NotificationChannel.Discord => new DiscordNotifier(cfg.Discord).IsConfigured(out var err) ? null : err,
                NotificationChannel.Telegram => new TelegramNotifier(cfg.Telegram).IsConfigured(out var err) ? null : err,
                _ => "Unknown channel"
            };

            if (!string.IsNullOrWhiteSpace(error))
                return Error(error);

            FileLogStore.WriteLine(
                "INFO",
                $"Notification Test gestartet (per Pipe) [{channel}]");

            var dummyApp = new WatchedApp
            {
                Name = "AppWatchdog Test",
                ExePath = "manual-test",
                Enabled = true
            };

            var ctx = new NotificationContext
            {
                Type = AppNotificationType.Up,
                App = dummyApp,
                Status = new AppStatus
                {
                    Name = dummyApp.Name,
                    Enabled = true,
                    IsRunning = true
                },
                StartAttempted = false,
                TestOnlyChannel = channel
            };

            _dispatcher.Dispatch(ctx);

            return Ok();
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"Notification Test fehlgeschlagen [{channel}]: {ex}");
            return Error(ex.Message);
        }
    }

    /// <summary>
    /// Triggers a backup job by plan ID.
    /// </summary>
    /// <param name="req">Request containing the plan ID.</param>
    /// <returns>The response indicating success or failure.</returns>
    private PipeProtocol.Response TriggerBackup(PipeProtocol.Request req)
    {
        var r = PipeProtocol.Deserialize<BackupTriggerRequest>(req.PayloadJson ?? "");
        if (r == null || string.IsNullOrWhiteSpace(r.BackupPlanId))
            return Error("Invalid payload");

        var jobId = $"backup:{r.BackupPlanId}";
        
        try
        {
            // Force-run the specific backup job instead of running all jobs
            _scheduler.ForceRun(jobId);
            FileLogStore.WriteLine("INFO", $"Backup triggered for plan: {r.BackupPlanId}");
            return Ok();
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Failed to trigger backup: {ex.Message}");
            return Error(ex.Message);
        }
    }

    /// <summary>
    /// Lists backup artifacts for a given plan.
    /// </summary>
    /// <param name="req">Request containing the plan ID.</param>
    /// <returns>The response containing artifact names.</returns>
    private async Task<PipeProtocol.Response> ListBackupArtifacts(PipeProtocol.Request req)
    {
        var r = PipeProtocol.Deserialize<BackupArtifactListRequest>(req.PayloadJson ?? "");
        if (r == null || string.IsNullOrWhiteSpace(r.BackupPlanId))
            return Error("Invalid payload");

        var plan = _getConfig().Backups.FirstOrDefault(x =>
            string.Equals(x.Id, r.BackupPlanId, StringComparison.OrdinalIgnoreCase));

        if (plan == null)
            return Error("Backup plan not found");

        var prefix = Sanitize(plan.Id) + "_";

        await using var storage = CreateStorage(plan.Target);
        var list = await storage.ListAsync(CancellationToken.None);

        return Ok(new BackupArtifactListResponse
        {
            Artifacts = list
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList()
        });
    }

    /// <summary>
    /// Retrieves a backup manifest for a specific artifact.
    /// </summary>
    /// <param name="req">Request containing plan and artifact identifiers.</param>
    /// <returns>The response containing the manifest JSON.</returns>
    private async Task<PipeProtocol.Response> GetBackupManifest(PipeProtocol.Request req)
    {
        var r = PipeProtocol.Deserialize<BackupManifestRequest>(req.PayloadJson ?? "");
        if (r == null ||
            string.IsNullOrWhiteSpace(r.BackupPlanId) ||
            string.IsNullOrWhiteSpace(r.ArtifactName))
            return Error("Invalid payload");

        var plan = _getConfig().Backups.FirstOrDefault(x =>
            string.Equals(x.Id, r.BackupPlanId, StringComparison.OrdinalIgnoreCase));

        if (plan == null)
            return Error("Backup plan not found");

        var tmpEnc = Path.Combine(
            BackupPaths.Staging,
            "pipe_man_" + Guid.NewGuid().ToString("N") + ".awdb");

        var tmpZip = Path.Combine(
            BackupPaths.Staging,
            "pipe_man_" + Guid.NewGuid().ToString("N") + ".zip");

        try
        {
            await using var storage = CreateStorage(plan.Target);
            await storage.DownloadAsync(r.ArtifactName, tmpEnc, null, CancellationToken.None);

            if (plan.Crypto.Encrypt)
                await AesCrypto.DecryptToFileAsync(tmpEnc, tmpZip, plan.Crypto.Password, CancellationToken.None);
            else
                File.Copy(tmpEnc, tmpZip, true);

            using var fs = File.OpenRead(tmpZip);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, false);
            var entry = zip.GetEntry("manifest.json");
            if (entry == null)
                return Error("Manifest missing");

            using var sr = new StreamReader(entry.Open());
            var json = await sr.ReadToEndAsync();

            return new PipeProtocol.Response
            {
                Ok = true,
                PayloadJson = json
            };
        }
        catch (FileNotFoundException)
        {
            return Error("Backup artifact not found");
        }
        finally
        {
            FileHelper.TryDelete(tmpEnc);
            FileHelper.TryDelete(tmpZip);
        }
    }


    /// <summary>
    /// Triggers a restore operation by creating a temporary restore plan.
    /// </summary>
    /// <param name="req">Request containing restore parameters.</param>
    /// <returns>The response indicating success or failure.</returns>
    private async Task<PipeProtocol.Response> TriggerRestoreAsync(PipeProtocol.Request req)
    {
        var r = PipeProtocol.Deserialize<RestoreTriggerRequest>(req.PayloadJson ?? "");
        if (r == null ||
            string.IsNullOrWhiteSpace(r.BackupPlanId) ||
            string.IsNullOrWhiteSpace(r.ArtifactName))
            return Error("Invalid payload");

        var cfg = _getConfig();
        var plan = cfg.Backups.FirstOrDefault(x =>
            string.Equals(x.Id, r.BackupPlanId, StringComparison.OrdinalIgnoreCase));

        if (plan == null)
            return Error("Backup plan not found");

// --- [CRITICAL CHANGE] ---
        var restore = new RestorePlanConfig
        {
            Enabled = true,
            Id = "pipe_" + Guid.NewGuid().ToString("N"),
            BackupPlanId = r.BackupPlanId,
            BackupArtifactName = r.ArtifactName,
            RestoreToDirectory = r.RestoreToDirectory,
            OverwriteExisting = r.OverwriteExisting,
            IncludePaths = r.IncludePaths,
            RunOnce = true,
            Target = plan.Target,
            Crypto = plan.Crypto
        };
// -----------------------

        cfg.Restores.Add(restore);
        ConfigStore.Save(_configPath, cfg);
        _onConfigSaved(cfg);

        return Ok();
    }

    /// <summary>
    /// Creates a backup storage implementation based on target settings.
    /// </summary>
    /// <param name="target">Backup target configuration.</param>
    /// <returns>The storage instance.</returns>
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
    /// Creates a success response without payload.
    /// </summary>
    /// <returns>The success response.</returns>
    private static PipeProtocol.Response Ok()
        => new() { Ok = true };

    /// <summary>
    /// Creates a success response with a payload.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="payload">Payload to serialize.</param>
    /// <returns>The success response.</returns>
    private static PipeProtocol.Response Ok<T>(T payload)
        => new()
        {
            Ok = true,
            PayloadJson = PipeProtocol.Serialize(payload)
        };

    /// <summary>
    /// Creates an error response with a message.
    /// </summary>
    /// <param name="msg">Error message.</param>
    /// <returns>The error response.</returns>
    private static PipeProtocol.Response Error(string msg)
        => new()
        {
            Ok = false,
            Error = msg
        };

    private async Task<PipeProtocol.Response> PurgeBackupArtifactsAsync(PipeProtocol.Request req)
    {
        var r = PipeProtocol.Deserialize<PurgeBackupArtifactsRequest>(req.PayloadJson ?? "");
        if (r == null || string.IsNullOrWhiteSpace(r.BackupPlanId))
            return Error("Invalid payload");

        var cfg = _getConfig();
        var plan = cfg.Backups.FirstOrDefault(x =>
            string.Equals(x.Id, r.BackupPlanId, StringComparison.OrdinalIgnoreCase));

        if (plan == null)
            return Error("Backup plan not found");

        var prefix = Sanitize(plan.Id) + "_";

        await using (var storage = CreateStorage(plan.Target))
        {
            var list = await storage.ListAsync(CancellationToken.None);
            foreach (var name in list.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                await storage.DeleteAsync(name, CancellationToken.None);
            }
        }

        var removed = cfg.Restores.RemoveAll(x =>
            string.Equals(x.BackupPlanId, plan.Id, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            ConfigStore.Save(_configPath, cfg);
            _onConfigSaved(cfg);
        }

        return Ok();
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "backup";

        var bad = Path.GetInvalidFileNameChars();
        var parts = s.Split(bad, StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join("_", parts);
        return string.IsNullOrWhiteSpace(joined) ? "backup" : joined;
    }

    private PipeProtocol.Response ExportConfig()
    {
        if (!File.Exists(_configPath))
            return Error("Config not found");

        var json = File.ReadAllText(_configPath);
        return Ok(new ConfigExportResponse { ConfigJson = json });
    }

    private PipeProtocol.Response ImportConfig(PipeProtocol.Request req)
    {
        var r = PipeProtocol.Deserialize<ConfigImportRequest>(req.PayloadJson ?? "");
        if (r == null || string.IsNullOrWhiteSpace(r.ConfigJson))
            return Error("Invalid payload");

        File.WriteAllText(_configPath, r.ConfigJson);

        var cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        _onConfigSaved(cfg);
        return Ok();
    }
}
