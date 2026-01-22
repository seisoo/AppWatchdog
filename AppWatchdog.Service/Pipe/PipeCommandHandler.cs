using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Pipe;

public sealed class PipeCommandHandler
{
    private readonly string _configPath;

    private readonly Func<WatchdogConfig> _getConfig;
    private readonly Func<ServiceSnapshot?> _getSnapshot;
    private readonly Action _triggerCheck;
    private readonly Action<WatchdogConfig> _onConfigSaved;

    private readonly NotificationDispatcher _dispatcher;
    private readonly JobScheduler _scheduler;

    public PipeCommandHandler(
        string configPath,
        Func<WatchdogConfig> getConfig,
        Func<ServiceSnapshot?> getSnapshot,
        Action triggerCheck,
        Action<WatchdogConfig> onConfigSaved,
        NotificationDispatcher dispatcher,
        JobScheduler scheduler)
    {
        _configPath = configPath;
        _getConfig = getConfig;
        _getSnapshot = getSnapshot;
        _triggerCheck = triggerCheck;
        _onConfigSaved = onConfigSaved;
        _dispatcher = dispatcher;
        _scheduler = scheduler;
    }

    public PipeProtocol.Response Handle(PipeProtocol.Request req)
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
                    return Ok(_scheduler.GetSnapshots());

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

    private PipeProtocol.Response RunNotificationTest(NotificationChannel channel)
    {
        try
        {
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

    private static PipeProtocol.Response Ok()
        => new() { Ok = true };

    private static PipeProtocol.Response Ok<T>(T payload)
        => new()
        {
            Ok = true,
            PayloadJson = PipeProtocol.Serialize(payload)
        };

    private static PipeProtocol.Response Error(string msg)
        => new()
        {
            Ok = false,
            Error = msg
        };
}
