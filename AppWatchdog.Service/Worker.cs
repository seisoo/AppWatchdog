using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace AppWatchdog.Service;

public sealed class Worker : BackgroundService
{
    private const int DownConfirmChecks = 2;

    private static readonly TimeSpan StartBackoffMin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartBackoffMax = TimeSpan.FromMinutes(10);

    private readonly ILogger<Worker> _log;

    private readonly string _configPath = ConfigStore.GetDefaultConfigPath();
    private WatchdogConfig _cfg;

    private readonly StatusTracker _status = new();

    private NotificationDispatcher _dispatcher = null!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private Timer? _monitorTimer;

    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(5);
    private Timer? _snapshotTimer;

    private static readonly TimeSpan KumaBaseInterval = TimeSpan.FromSeconds(5);
    private Timer? _kumaTimer;

    private const bool EnableOptionalPingDetection = false;

    private static readonly TimeSpan StartGuardWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan EventLogWindow = TimeSpan.FromSeconds(10);

    private sealed class AppMailState
    {
        public bool WasRunning;
        public DateTimeOffset LastDownMail = DateTimeOffset.MinValue;
        public DateTimeOffset LastStartAttemptUtc = DateTimeOffset.MinValue;

        public int ConsecutiveDown;
        public int ConsecutiveStartFailures;
        public DateTimeOffset NextStartAttemptUtc = DateTimeOffset.MinValue;

        public bool RestartNotified;
        public DateTimeOffset LastKumaPing = DateTimeOffset.MinValue;
    }

    private readonly Dictionary<string, AppMailState> _appMailStates =
        new(StringComparer.OrdinalIgnoreCase);

    public Worker(ILogger<Worker> log)
    {
        _log = log;
        _cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        _dispatcher = new NotificationDispatcher(_cfg, _log);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogInfo("AppWatchdog Service gestartet");
        LogInfo($"Protocol-Version: {PipeProtocol.ProtocolVersion}");

        _snapshotTimer = new Timer(_ => SnapshotTickSafe(), null, TimeSpan.Zero, SnapshotInterval);
        _monitorTimer = new Timer(_ => TickSafe(), null, TimeSpan.Zero, CheckInterval);
        _kumaTimer = new Timer(_ => KumaTickSafe(), null, TimeSpan.Zero, KumaBaseInterval);

        _ = Task.Run(() => PipeAcceptLoop(stoppingToken), stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _snapshotTimer?.Dispose();
        _monitorTimer?.Dispose();
        _kumaTimer?.Dispose();
        LogInfo("AppWatchdog Service gestoppt.");
        await base.StopAsync(cancellationToken);
    }

    private void SnapshotTickSafe()
    {
        try { SnapshotTick(); }
        catch (Exception ex)
        {
            _log.LogError(ex, "SnapshotTick Fehler");
            LogError("SnapshotTick Fehler", ex);
        }
    }

    private void SnapshotTick()
    {
        var snapshot = new ServiceSnapshot
        {
            Timestamp = DateTimeOffset.Now,
            SessionState = UserSessionLauncher.GetSessionState(),
            Apps = BuildAppStatusSnapshot(),
            SystemInfo = SystemInfoCollect(),
            PipeProtocolVersion = PipeProtocol.ProtocolVersion
        };

        _status.LastSnapshot = snapshot;
    }

    private List<AppStatus> BuildAppStatusSnapshot()
    {
        _cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        var result = new List<AppStatus>();

        foreach (var app in _cfg.Apps)
        {
            var exePath = app.ExePath ?? "";
            result.Add(new AppStatus
            {
                Name = app.Name,
                ExePath = exePath,
                Enabled = app.Enabled,
                IsRunning = app.Enabled && !string.IsNullOrWhiteSpace(exePath) && IsRunning(exePath),
                LastStartError = null
            });
        }

        return result;
    }

    private void TickSafe()
    {
        try { Tick(); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tick Fehler");
            LogError("Tick Fehler", ex);
        }
    }

    private void Tick()
    {
        _cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        _dispatcher = new NotificationDispatcher(_cfg, _log);

        var validExePaths = new HashSet<string>(
            _cfg.Apps
                .Where(a => !string.IsNullOrWhiteSpace(a.ExePath))
                .Select(a => a.ExePath!),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in _appMailStates.Keys.ToList())
            if (!validExePaths.Contains(key))
                _appMailStates.Remove(key);

        var sessionState = UserSessionLauncher.GetSessionState();
        bool interactive = sessionState == UserSessionState.InteractiveUserPresent;

        var snapshot = new ServiceSnapshot
        {
            Timestamp = DateTimeOffset.Now,
            SessionState = sessionState,
            Apps = new List<AppStatus>(),
            SystemInfo = SystemInfoCollect(),
            PipeProtocolVersion = PipeProtocol.ProtocolVersion
        };

        var now = DateTimeOffset.Now;
        var downMailInterval = TimeSpan.FromHours(Math.Max(1, _cfg.MailIntervalHours));

        foreach (var app in _cfg.Apps)
        {
            var exePath = app.ExePath ?? "";
            var st = new AppStatus
            {
                Name = app.Name,
                ExePath = exePath,
                Enabled = app.Enabled
            };

            if (!app.Enabled || string.IsNullOrWhiteSpace(exePath))
            {
                st.IsRunning = false;
                snapshot.Apps.Add(st);
                continue;
            }

            if (!_appMailStates.TryGetValue(exePath, out var ms))
            {
                ms = new AppMailState();
                _appMailStates[exePath] = ms;
            }

            st.IsRunning = IsRunning(exePath);

            if (st.IsRunning)
            {
                ms.ConsecutiveDown = 0;
                ms.ConsecutiveStartFailures = 0;
                ms.NextStartAttemptUtc = DateTimeOffset.MinValue;
            }
            else
            {
                ms.ConsecutiveDown++;
            }

            if (!st.IsRunning)
            {
                if (ms.ConsecutiveDown < DownConfirmChecks)
                {
                    st.LastStartError =
                        $"Kurzzeitiger Ausfall ({ms.ConsecutiveDown}/{DownConfirmChecks})";

                    ms.WasRunning = false;
                    snapshot.Apps.Add(st);
                    continue;
                }

                bool justWentDown = ms.WasRunning;
                bool maySendDownMail =
                    justWentDown ||
                    (now - ms.LastDownMail) >= downMailInterval;

                bool startAttempted = false;
                bool startSucceeded = false;

                if (interactive)
                {
                    if (ms.NextStartAttemptUtc <= DateTimeOffset.UtcNow)
                    {
                        startAttempted = true;
                        ms.LastStartAttemptUtc = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        st.LastStartError =
                            $"Restart-Backoff aktiv bis {ms.NextStartAttemptUtc:HH:mm:ss}";
                    }

                    try
                    {
                        if (startAttempted)
                        {
                            UserSessionLauncher.StartInActiveUserSession(
                                exePath,
                                app.Arguments);

                            if (WaitForRunning(exePath, StartGuardWindow))
                            {
                                startSucceeded = true;
                                st.IsRunning = true;
                            }
                            else
                            {
                                var ev = FindAppErrorInEventLog(
                                    exePath,
                                    ms.LastStartAttemptUtc,
                                    EventLogWindow);

                                st.LastStartError = ev != null
                                    ? $"EventLog: {ev}"
                                    : "Anwendung beendet sich direkt nach dem Start (Early-Exit).";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        st.LastStartError = ex.Message;
                        LogError($"Start fehlgeschlagen: {app.Name}", ex);
                    }
                }
                else
                {
                    st.LastStartError =
                        "Kein interaktiver Benutzer angemeldet (Start nicht möglich).";
                }

                if (!st.IsRunning)
                {
                                        ms.RestartNotified = false;

                    if (maySendDownMail)
                    {
                        SendNotification_AppDownAsync(app, st, startAttempted);
                        ms.LastDownMail = now;
                    }

                    if (startAttempted)
                    {
                        ms.ConsecutiveStartFailures++;

                        var delay = ComputeBackoffDelay(ms.ConsecutiveStartFailures);
                        ms.NextStartAttemptUtc = DateTimeOffset.UtcNow + delay;
                    }

                    ms.WasRunning = false;
                }
                else
                {
                    ms.ConsecutiveStartFailures = 0;
                    ms.NextStartAttemptUtc = DateTimeOffset.MinValue;

                    if (justWentDown && !ms.RestartNotified)
                    {
                        SendNotification_AppRestartedAsync(app, st);
                        ms.RestartNotified = true;
                    }

                    if (!ms.WasRunning)
                        SendNotification_AppUpAsync(app);

                    ms.WasRunning = true;
                }
            }
            else
            {
                if (!ms.WasRunning)
                    SendNotification_AppUpAsync(app);

                ms.WasRunning = true;
            }

            snapshot.Apps.Add(st);
        }

        _status.LastSnapshot = snapshot;
    }

    private void KumaTickSafe()
    {
        try { KumaTick(); }
        catch { }
    }

    private void KumaTick()
    {
        _cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        var now = DateTimeOffset.Now;

        foreach (var app in _cfg.Apps)
        {
            var kuma = app.UptimeKuma;
            if (kuma?.Enabled != true)
                continue;

            var exePath = app.ExePath;
            if (string.IsNullOrWhiteSpace(exePath))
                continue;

            if (!_appMailStates.TryGetValue(exePath, out var ms))
                continue;

            var interval = TimeSpan.FromSeconds(Math.Max(10, kuma.IntervalSeconds));
            if ((now - ms.LastKumaPing) < interval)
                continue;

            bool isRunning = ms.WasRunning;

            _ = KumaNotifier.SendAsync(
                kuma.BaseUrl,
                kuma.PushToken,
                isRunning,
                isRunning ? "UP" : "DOWN");

            ms.LastKumaPing = now;
        }
    }

    private static TimeSpan ComputeBackoffDelay(int consecutiveStartFailures)
    {
        double seconds = StartBackoffMin.TotalSeconds * Math.Pow(2, Math.Max(0, consecutiveStartFailures - 1));
        seconds = Math.Min(StartBackoffMax.TotalSeconds, seconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool WaitForRunning(string exePath, TimeSpan maxWait)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < maxWait)
        {
            if (IsRunning(exePath))
                return true;

            Thread.Sleep(200);
        }
        return IsRunning(exePath);
    }

    private static string? FindAppErrorInEventLog(string exePath, DateTimeOffset startAttemptUtc, TimeSpan window)
    {
        try
        {
            var fromUtc = startAttemptUtc.UtcDateTime;
            var toUtc = startAttemptUtc.UtcDateTime.Add(window);

            var exeName = Path.GetFileName(exePath) ?? "";
            if (string.IsNullOrWhiteSpace(exeName))
                exeName = exePath;

            using var log = new EventLog("Application");

            for (int i = log.Entries.Count - 1; i >= 0; i--)
            {
                var e = log.Entries[i];
                if (e.EntryType != EventLogEntryType.Error)
                    continue;

                var tUtc = e.TimeGenerated.ToUniversalTime();
                if (tUtc > toUtc)
                    continue;

                if (tUtc < fromUtc)
                    break; 
                var src = e.Source ?? "";
                if (src.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("Application Error", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("Windows Error Reporting", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{src} (EventId {e.InstanceId})";
                }

                var msg = e.Message ?? "";
                if (msg.Contains(exeName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{src} (EventId {e.InstanceId})";
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool PingApplicationOptional(WatchedApp app)
    {
        // TODO: health check ping
        return true;
    }


    private void SendNotification_AppDownAsync(WatchedApp app, AppStatus st, bool startAttempted)
    {
        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Down,
            App = app,
            Status = st,
            StartAttempted = startAttempted,

            Title = $"AppWatchdog – {app.Name} DOWN",
            SummaryStatus = "NICHT AKTIV",
            SummaryColorHex = "#b91c1c",

            NtfyTags = "warning,server",
            NtfyPriority = 4,

            DiscordEmoji = "🛑",
            DiscordColor = 0xB91C1C
        });
    }

    private void SendNotification_AppRestartedAsync(WatchedApp app, AppStatus st)
    {
        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Restart,
            App = app,

            Title = $"AppWatchdog – {app.Name} RESTART",
            SummaryStatus = "NEU GESTARTET",
            SummaryColorHex = "#2563eb",

            NtfyTags = "restart,server",
            NtfyPriority = 3,

            DiscordEmoji = "🔄",
            DiscordColor = 0x2563EB
        });
    }

    private void SendNotification_AppUpAsync(WatchedApp app)
    {
        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Up,
            App = app,

            Title = $"AppWatchdog – {app.Name} UP",
            SummaryStatus = "WIEDER AKTIV",
            SummaryColorHex = "#15803d",

            NtfyTags = "white_check_mark,server",
            NtfyPriority = 2,

            DiscordEmoji = "✅",
            DiscordColor = 0x15803D
        });
    }

    private async Task PipeAcceptLoop(CancellationToken token)
    {
        LogInfo("PipeServer AcceptLoop gestartet");

        while (!token.IsCancellationRequested)
        {
            var pipe = CreatePipe();

            try
            {
                await pipe.WaitForConnectionAsync(token);
                LogInfo("Pipe client connected");

                _ = Task.Run(() => HandlePipeClientAsync(pipe), token);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (Exception ex)
            {
                pipe.Dispose();
                _log.LogError(ex, "Pipe Accept Fehler");
                LogError("Pipe Accept Fehler", ex);
                await Task.Delay(500, token);
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        var ps = new PipeSecurity();

        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous,
            0, 0,
            ps);
    }

    private async Task HandlePipeClientAsync(NamedPipeServerStream pipe)
    {
        try
        {
            using var br = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
            using var bw = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);

            int reqLen = br.ReadInt32();
            if (reqLen <= 0 || reqLen > 1024 * 1024 * 4)
                throw new InvalidOperationException("Ungültige Request-Länge.");

            var reqBytes = br.ReadBytes(reqLen);
            if (reqBytes.Length != reqLen)
                throw new InvalidOperationException("Unvollständiger Request.");

            var reqJson = Encoding.UTF8.GetString(reqBytes);
            var req = PipeProtocol.Deserialize<PipeProtocol.Request>(reqJson)
                      ?? throw new InvalidOperationException("Ungültiger Request.");

            var resp = HandlePipeRequest(req);

            var respJson = PipeProtocol.Serialize(resp);
            var respBytes = Encoding.UTF8.GetBytes(respJson);

            bw.Write(respBytes.Length);
            bw.Write(respBytes);
            bw.Flush();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "PIPE: client handling failed");
            LogError("PIPE: client handling failed", ex);
        }
        finally
        {
            pipe.Dispose();
        }

        await Task.CompletedTask;
    }

    // pipe command handler
    private PipeProtocol.Response HandlePipeRequest(PipeProtocol.Request req)
    {
        try
        {
            if (req.Version != PipeProtocol.ProtocolVersion)
            {
                return new PipeProtocol.Response
                {
                    Ok = false,
                    Error = $"Protocol mismatch. Service={PipeProtocol.ProtocolVersion}, Client={req.Version}"
                };
            }

            switch (req.Command)
            {
                case PipeProtocol.CmdPing:
                    return new PipeProtocol.Response { Ok = true };

                case PipeProtocol.CmdGetConfig:
                    return new PipeProtocol.Response
                    {
                        Ok = true,
                        PayloadJson = PipeProtocol.Serialize(_cfg)
                    };

                case PipeProtocol.CmdSaveConfig:
                    {
                        if (string.IsNullOrWhiteSpace(req.PayloadJson))
                            return new PipeProtocol.Response { Ok = false, Error = "No payload" };

                        var cfg = PipeProtocol.Deserialize<WatchdogConfig>(req.PayloadJson);
                        if (cfg == null)
                            return new PipeProtocol.Response { Ok = false, Error = "Invalid payload" };

                        ConfigStore.Save(_configPath, cfg);
                        _cfg = cfg;

                        LogInfo("Config gespeichert (per Pipe).");
                        return new PipeProtocol.Response { Ok = true };
                    }

                case PipeProtocol.CmdGetStatus:
                    {
                        var snap = _status.LastSnapshot;
                        if (snap == null)
                        {
                            snap = new ServiceSnapshot
                            {
                                Timestamp = DateTimeOffset.Now,
                                SessionState = UserSessionLauncher.GetSessionState(),
                                Apps = new List<AppStatus>(),
                                SystemInfo = SystemInfoCollect(),
                                PipeProtocolVersion = PipeProtocol.ProtocolVersion
                            };
                        }

                        return new PipeProtocol.Response
                        {
                            Ok = true,
                            PayloadJson = PipeProtocol.Serialize(snap)
                        };
                    }

                case PipeProtocol.CmdTriggerCheck:
                    TickSafe();
                    return new PipeProtocol.Response { Ok = true };

                case PipeProtocol.CmdListLogDays:
                    {
                        var days = FileLogStore.ListDays();
                        return new PipeProtocol.Response
                        {
                            Ok = true,
                            PayloadJson = PipeProtocol.Serialize(new LogDaysResponse { Days = days })
                        };
                    }

                case PipeProtocol.CmdGetLogDay:
                    {
                        if (string.IsNullOrWhiteSpace(req.PayloadJson))
                            return new PipeProtocol.Response { Ok = false, Error = "No payload" };

                        var r = PipeProtocol.Deserialize<LogDayRequest>(req.PayloadJson);
                        if (r == null)
                            return new PipeProtocol.Response { Ok = false, Error = "Invalid payload" };

                        var content = FileLogStore.ReadDay(r.Day);

                        return new PipeProtocol.Response
                        {
                            Ok = true,
                            PayloadJson = PipeProtocol.Serialize(new LogDayResponse { Day = r.Day, Content = content })
                        };
                    }

                case PipeProtocol.CmdTestSmtp:
                    {
                        return RunNotificationTest(
                            type: AppNotificationType.Up,
                            title: "AppWatchdog – SMTP Test",
                            summaryStatus: "SMTP TEST",
                            summaryColor: "#2563eb",
                            ntfyTags: "test,smtp",
                            ntfyPriority: 3,
                            emoji: "📧",
                            discordColor: 0x2563EB,
                            NotificationChannel.Mail
                        );
                    }

                case PipeProtocol.CmdTestNtfy:
                    {
                        return RunNotificationTest(
                            type: AppNotificationType.Up,
                            title: "AppWatchdog – NTFY Test",
                            summaryStatus: "NTFY TEST",
                            summaryColor: "#22c55e",
                            ntfyTags: "test,ntfy",
                            ntfyPriority: 3,
                            emoji: "📢",
                            discordColor: 0x22C55E,
                            NotificationChannel.Ntfy
                        );
                    }

                case PipeProtocol.CmdTestDiscord:
                    {
                        return RunNotificationTest(
                            type: AppNotificationType.Up,
                            title: "AppWatchdog – Discord Test",
                            summaryStatus: "DISCORD TEST",
                            summaryColor: "#5865F2",
                            ntfyTags: "test,discord",
                            ntfyPriority: 3,
                            emoji: "💬",
                            discordColor: 0x5865F2,
                            NotificationChannel.Discord
                        );
                    }

                case PipeProtocol.CmdTestTelegram:
                    {
                        return RunNotificationTest(
                            type: AppNotificationType.Up,
                            title: "AppWatchdog – Telegram Test",
                            summaryStatus: "TELEGRAM TEST",
                            summaryColor: "#0ea5e9",
                            ntfyTags: "test,telegram",
                            ntfyPriority: 3,
                            emoji: "📨",
                            discordColor: 0x0EA5E9,
                            NotificationChannel.Telegram
                        );
                    }


                case PipeProtocol.CmdGetLogPath:
                    {
                        try
                        {
                            var logPath = FileLogStore.LogDir;
                            LogInfo($"Log-Path requested '{logPath}'");
                            return new PipeProtocol.Response
                            {
                                Ok = true,
                                PayloadJson = PipeProtocol.Serialize(new LogPathResponse { Path = logPath })
                            };
                        }
                        catch (Exception ex)
                        {
                            LogError("Log path not resolved.", ex);
                            return new PipeProtocol.Response
                            {
                                Ok = false,
                                Error = ex.Message
                            };
                        }
                    }

                default:
                    return new PipeProtocol.Response { Ok = false, Error = "Unknown command" };
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Pipe request FAILED: {Cmd}", req.Command);
            LogError($"Pipe request FAILED: {req.Command}", ex);
            return new PipeProtocol.Response
            {
                Ok = false,
                Error = ex.ToString()
            };
        }
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
            LogInfo($"{type} Test gestartet (per Pipe) [{channel}]");

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

            return new PipeProtocol.Response { Ok = true };
        }
        catch (Exception ex)
        {
            LogError($"{type} Test fehlgeschlagen", ex);
            return new PipeProtocol.Response
            {
                Ok = false,
                Error = ex.Message
            };
        }
    }


    private static string Html(string s)
        => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private void LogInfo(string msg)
    {
        _log.LogInformation(msg);
        FileLogStore.WriteLine("INFO", msg);
    }

    private void LogWarn(string msg)
    {
        _log.LogWarning(msg);
        FileLogStore.WriteLine("WARN", msg);
    }

    private void LogError(string msg, Exception ex)
    {
        FileLogStore.WriteLine("ERROR", msg, ex);
    }

    private static bool IsRunning(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        if (string.IsNullOrWhiteSpace(name)) return false;

        string exeFull;
        try { exeFull = Path.GetFullPath(exePath); }
        catch { exeFull = exePath; }

        foreach (var p in Process.GetProcessesByName(name))
        {
            try
            {
                var path = TryGetProcessPath(p);
                if (path == null)
                    continue;

                string pFull;
                try { pFull = Path.GetFullPath(path); }
                catch { pFull = path; }

                if (string.Equals(pFull, exeFull, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static string? TryGetProcessPath(Process p)
    {
        try
        {
            const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
            if (h == IntPtr.Zero)
                return null;

            try
            {
                var sb = new StringBuilder(4096);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(h, 0, sb, ref size))
                    return sb.ToString();
            }
            finally
            {
                CloseHandle(h);
            }
        }
        catch
        {
        }
        try { return p.MainModule?.FileName; } catch { return null; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    public static SystemInfo SystemInfoCollect()
    {
        var proc = Process.GetCurrentProcess();
        var (totalMb, availableMb) = GetMemoryInfo();

        return new SystemInfo
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OsVersion = Environment.OSVersion.ToString(),
            DotNetVersion = Environment.Version.ToString(),
            Uptime = DateTime.Now - proc.StartTime,
            ProcessorCount = Environment.ProcessorCount,
            TotalMemoryMb = totalMb,
            AvailableMemoryMb = availableMb,
            PipeProtocol = PipeProtocol.ProtocolVersion
        };
    }

    private static (long totalMb, long availableMb) GetMemoryInfo()
    {
        var mem = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };

        if (!GlobalMemoryStatusEx(ref mem))
            throw new InvalidOperationException("GlobalMemoryStatusEx fehlgeschlagen.");

        long totalMb = (long)(mem.ullTotalPhys / (1024 * 1024));
        long availableMb = (long)(mem.ullAvailPhys / (1024 * 1024));

        return (totalMb, availableMb);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
