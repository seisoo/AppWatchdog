using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Shared;
using Microsoft.Extensions.Logging;

namespace AppWatchdog.Service.Pipe;

public sealed class PipeCommandHandler
{
    private readonly ILogger _log;
    private readonly string _configPath;

    private readonly Func<WatchdogConfig> _getConfig;
    private readonly Func<ServiceSnapshot?> _getSnapshot;
    private readonly Action _triggerCheck;
    private readonly Action<WatchdogConfig> _onConfigSaved;

    private readonly NotificationDispatcher _dispatcher;
    private readonly JobScheduler _scheduler;

    public PipeCommandHandler(
     ILogger log,
     string configPath,
     Func<WatchdogConfig> getConfig,
     Func<ServiceSnapshot?> getSnapshot,
     Action triggerCheck,
     Action<WatchdogConfig> onConfigSaved,
     NotificationDispatcher dispatcher,
     JobScheduler scheduler)
    {
        _log = log;
        _configPath = configPath;
        _getConfig = getConfig;
        _getSnapshot = getSnapshot;
        _triggerCheck = triggerCheck;
        _onConfigSaved = onConfigSaved;
        _dispatcher = dispatcher;
        _scheduler = scheduler;
    }


    // =========================================================
    // ENTRY
    // =========================================================
    public PipeProtocol.Response Handle(PipeProtocol.Request req)
    {
        try
        {
            if (req.Version != PipeProtocol.ProtocolVersion)
            {
                return new PipeProtocol.Response
                {
                    Ok = false,
                    Error =
                        $"Protocol mismatch. Service={PipeProtocol.ProtocolVersion}, Client={req.Version}"
                };
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
                    return RunNotificationTest(
                        AppNotificationType.Up,
                        "AppWatchdog – SMTP Test",
                        "SMTP TEST",
                        "#2563eb",
                        "test,smtp",
                        3,
                        "📧",
                        0x2563EB,
                        NotificationChannel.Mail);

                case PipeProtocol.CmdTestNtfy:
                    return RunNotificationTest(
                        AppNotificationType.Up,
                        "AppWatchdog – NTFY Test",
                        "NTFY TEST",
                        "#22c55e",
                        "test,ntfy",
                        3,
                        "📢",
                        0x22C55E,
                        NotificationChannel.Ntfy);

                case PipeProtocol.CmdTestDiscord:
                    return RunNotificationTest(
                        AppNotificationType.Up,
                        "AppWatchdog – Discord Test",
                        "DISCORD TEST",
                        "#5865F2",
                        "test,discord",
                        3,
                        "💬",
                        0x5865F2,
                        NotificationChannel.Discord);

                case PipeProtocol.CmdTestTelegram:
                    return RunNotificationTest(
                        AppNotificationType.Up,
                        "AppWatchdog – Telegram Test",
                        "TELEGRAM TEST",
                        "#0ea5e9",
                        "test,telegram",
                        3,
                        "📨",
                        0x0EA5E9,
                        NotificationChannel.Telegram);

                case PipeProtocol.CmdGetLogPath:
                    return Ok(new LogPathResponse
                    {
                        Path = FileLogStore.LogDir
                    });

                case PipeProtocol.CmdGetJobs:
                    {
                        var jobs = _scheduler.GetSnapshots();

                        return new PipeProtocol.Response
                        {
                            Ok = true,
                            PayloadJson = PipeProtocol.Serialize(jobs)
                        };
                    }


                default:
                    return new PipeProtocol.Response
                    {
                        Ok = false,
                        Error = "Unknown command"
                    };
            }
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", string.Format("Pipe request FAILED: {0}", req.Command));
            return new PipeProtocol.Response
            {
                Ok = false,
                Error = ex.ToString()
            };
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private PipeProtocol.Response SaveConfig(PipeProtocol.Request req)
    {
        if (string.IsNullOrWhiteSpace(req.PayloadJson))
            return Error("No payload");

        var cfg = PipeProtocol.Deserialize<WatchdogConfig>(req.PayloadJson);
        if (cfg == null)
            return Error("Invalid payload");

        ConfigStore.Save(_configPath, cfg);
        _onConfigSaved(cfg);   
        FileLogStore.WriteLine("INFO","Config gespeichert (per Pipe).");
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

        var content = FileLogStore.ReadDay(r.Day);

        return Ok(new LogDayResponse
        {
            Day = r.Day,
            Content = content
        });
    }

    private PipeProtocol.Response RunNotificationTest(
        AppNotificationType type,
        string title,
        string summaryStatus,
        string summaryColor,
        string ntfyTags,
        int ntfyPriority,
        string emoji,
        int discordColor,
        NotificationChannel channel)
    {
        try
        {
            FileLogStore.WriteLine("INFO",string.Format("{0} Test gestartet (per Pipe) [{1}]", type, channel));

            var dummyApp = new WatchedApp
            {
                Name = "AppWatchdog Test",
                ExePath = "manual-test"
            };

            var ctx = new NotificationContext
            {
                Type = type,
                App = dummyApp,

                Title = title,
                SummaryStatus = summaryStatus,
                SummaryColorHex = summaryColor,

                NtfyTags = ntfyTags,
                NtfyPriority = ntfyPriority,

                DiscordEmoji = emoji,
                DiscordColor = discordColor,

                TestOnlyChannel = channel
            };

            _dispatcher.Dispatch(ctx);

            return Ok();
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", string.Format("{0} Test fehlgeschlagen: {1}", type, ex));
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
