using AppWatchdog.Service.HealthChecks;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Service.Pipe;
using AppWatchdog.Service.Recovery;
using AppWatchdog.Shared;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AppWatchdog.Service;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;

    private readonly string _configPath = ConfigStore.GetDefaultConfigPath();
    private WatchdogConfig _cfg = null!;

    private NotificationDispatcher _dispatcher = null!;
    private JobScheduler _scheduler = null!;

    private PipeServer _pipe = null!;
    private PipeCommandHandler _pipeHandler = null!;

    private volatile ServiceSnapshot? _lastSnapshot;

    // Tracke Job-IDs, die im Scheduler aktuell registriert sind,
    // damit wir beim Rebuild sauber "obsolete" Jobs entfernen können,
    // ohne den Scheduler ersetzen zu müssen.
    private HashSet<string> _knownJobIds = new(StringComparer.OrdinalIgnoreCase);

    public Worker(ILogger<Worker> log)
    {
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        FileLogStore.WriteLine("INFO", "AppWatchdog Service gestartet");
        FileLogStore.WriteLine("INFO", string.Format("Protocol-Version: {0}", PipeProtocol.ProtocolVersion));

        LoadConfig();

        _scheduler = new JobScheduler();

        BuildJobs();

        InitPipe();

        _ = _pipe.RunAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _scheduler?.Dispose();
        }
        catch { }

        FileLogStore.WriteLine("INFO", "AppWatchdog Service gestoppt");
        return base.StopAsync(cancellationToken);
    }

    private void LoadConfig()
    {
        _cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        _dispatcher = new NotificationDispatcher(_cfg, _log);
    }

    private void SaveConfig(WatchdogConfig cfg)
    {
        ConfigStore.Save(_configPath, cfg);
        _cfg = cfg;

        FileLogStore.WriteLine("INFO", "Config gespeichert – Jobs werden neu aufgebaut");

        // Dispatcher hängt an Config (SMTP/Ntfy/Discord/Telegram) -> neu erstellen
        _dispatcher = new NotificationDispatcher(_cfg, _log);

        BuildJobs();
    }

    private void BuildJobs()
    {
        var desiredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        {
            var snapJob = new SnapshotJob(
                getConfig: () => _cfg,
                publish: snap => _lastSnapshot = snap);

            _scheduler.AddOrUpdate(snapJob);
            desiredIds.Add(snapJob.Id);
        }

        foreach (var app in _cfg.Apps)
        {
            if (!app.Enabled || string.IsNullOrWhiteSpace(app.ExePath))
                continue;

            // Health + Recovery
            var health = new ProcessHealthCheck(app.ExePath);

            var healthJob = CreateHealthJob(app, health);
            _scheduler.AddOrUpdate(healthJob);
            desiredIds.Add(healthJob.Id);

            // Kuma pro App
            if (app.UptimeKuma?.Enabled == true)
            {
                var kumaJob = new KumaPingJob(app);
                _scheduler.AddOrUpdate(kumaJob);
                desiredIds.Add(kumaJob.Id);
            }
        }

        foreach (var oldId in _knownJobIds)
        {
            if (!desiredIds.Contains(oldId))
                _scheduler.Remove(oldId);
        }

        _knownJobIds = desiredIds;
    }

    private HealthMonitorJob CreateHealthJob(WatchedApp app, IHealthCheck health)
    {
        var state = new MonitorState();

        var recovery = new ProcessRestartStrategy(
            _log,
            stateAccessor: () => state,
            getMailIntervalHours: () => _cfg.MailIntervalHours);

        return new HealthMonitorJob(
            log: _log,
            getConfig: () => _cfg,
            app: app,
            check: health,
            recovery: recovery,
            state: state,
            dispatcher: _dispatcher
        );
    }

    private void InitPipe()
    {
        _pipeHandler = new PipeCommandHandler(
            _log,
            _configPath,
            getConfig: () => _cfg,
            getSnapshot: () => _lastSnapshot,
            triggerCheck: () => _ = TriggerCheckAsync(),
            onConfigSaved: cfg => SaveConfig(cfg),
            dispatcher: _dispatcher,
            scheduler: _scheduler
        );

        _pipe = new PipeServer(_log, _pipeHandler.Handle);
    }

    private async Task TriggerCheckAsync()
    {
        FileLogStore.WriteLine("INFO", "TriggerCheck: Jobs werden sofort ausgeführt");
        await _scheduler.RunAllOnceAsync();
    }

    public static bool IsRunning(string exePath)
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
            catch { }
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
        catch { }

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
