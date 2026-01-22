using AppWatchdog.Service.HealthChecks;
using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Service.Pipe;
using AppWatchdog.Service.Recovery;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;
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
        try { _scheduler?.Dispose(); } catch { }

        FileLogStore.WriteLine("INFO", "AppWatchdog Service gestoppt");
        return base.StopAsync(cancellationToken);
    }

    private void LoadConfig()
    {
        _cfg = ConfigStore.LoadOrCreateDefault(_configPath);
        _dispatcher = new NotificationDispatcher(_cfg);
    }

    private void SaveConfig(WatchdogConfig cfg)
    {
        ConfigStore.Save(_configPath, cfg);
        _cfg = cfg;

        FileLogStore.WriteLine("INFO", "Config gespeichert – Jobs werden neu aufgebaut");

        _dispatcher = new NotificationDispatcher(_cfg);

        BuildJobs();
    }

    private void BuildJobs()
    {
        var desiredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Snapshot
        {
            var snapJob = new SnapshotJob(
                getConfig: () => _cfg,
                publish: snap => _lastSnapshot = snap);

            _scheduler.AddOrUpdate(snapJob);
            desiredIds.Add(snapJob.Id);
        }

        foreach (var app in _cfg.Apps)
        {
            if (!app.Enabled)
                continue;

            if (!IsValidTarget(app, out var reason))
            {
                FileLogStore.WriteLine("WARNING", $"App '{app.Name}' skipped: {reason}");
                continue;
            }

            IHealthCheck health;
            try
            {
                health = HealthCheckFactory.Create(app);
            }
            catch (Exception ex)
            {
                FileLogStore.WriteLine(
                    "ERROR",
                    $"HealthCheck creation failed for '{app.Name}': {ex.Message}");

                continue; 
            }

            var healthJob = CreateHealthJob(app, health);
            _scheduler.AddOrUpdate(healthJob);
            desiredIds.Add(healthJob.Id);

            // Kuma (macht nur Sinn bei Executable, weil es deinen IsRunning-Check nutzt)
            if (app.Type == WatchTargetType.Executable && app.UptimeKuma?.Enabled == true)
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

    private HealthMonitorJob CreateHealthJob(
    WatchedApp app,
    IHealthCheck health)
    {
        IRecoveryStrategy recovery = app.Type switch
        {
            WatchTargetType.Executable =>
                new ProcessRestartStrategy(),

            WatchTargetType.WindowsService =>
                new ServiceRestartStrategy(),

            _ =>
                new NoRecoveryStrategy() // HTTP / TCP: nur melden
        };

        return new HealthMonitorJob(
            app: app,
            healthCheck: health,
            recovery: recovery,
            dispatcher: _dispatcher,
            interval: TimeSpan.FromSeconds(app.CheckIntervalSeconds),
            mailIntervalHours: _cfg.MailIntervalHours
        );
    }


    private static bool IsValidTarget(WatchedApp app, out string reason)
    {
        reason = "";

        switch (app.Type)
        {
            case WatchTargetType.Executable:
                if (string.IsNullOrWhiteSpace(app.ExePath))
                {
                    reason = "ExePath is empty.";
                    return false;
                }
                return true;

            case WatchTargetType.WindowsService:
                if (string.IsNullOrWhiteSpace(app.ServiceName))
                {
                    reason = "ServiceName is empty.";
                    return false;
                }
                return true;

            case WatchTargetType.HttpEndpoint:
                if (string.IsNullOrWhiteSpace(app.Url))
                {
                    reason = "Url is empty.";
                    return false;
                }
                if (app.CheckIntervalSeconds < 3)
                {
                    reason = "CheckIntervalSeconds must be >= 3 seconds.";
                    return false;
                }
                if (app.Type is WatchTargetType.HttpEndpoint or WatchTargetType.TcpPort &&
                    app.CheckIntervalSeconds < 15)
                {
                    reason = "HTTP/TCP interval too low (min 15s).";
                    return false;
                }
                return true;

            case WatchTargetType.TcpPort:
                if (string.IsNullOrWhiteSpace(app.Host))
                {
                    reason = "Host is empty.";
                    return false;
                }
                if (!app.Port.HasValue || app.Port.Value <= 0 || app.Port.Value > 65535)
                {
                    reason = "Port is missing/invalid.";
                    return false;
                }
                if(app.CheckIntervalSeconds < 3) 
                { 
                    reason = "CheckIntervalSeconds must be >= 3 seconds.";
                    return false;
                }
                if (app.Type is WatchTargetType.HttpEndpoint or WatchTargetType.TcpPort &&
                    app.CheckIntervalSeconds < 15)
                {
                    reason = "HTTP/TCP interval too low (min 15s).";
                    return false;
                }
                return true;

            default:
                reason = $"Unknown type {app.Type}";
                return false;
        }
    }

    private void InitPipe()
    {
        _pipeHandler = new PipeCommandHandler(
            _configPath,
            getConfig: () => _cfg,
            getSnapshot: () => _lastSnapshot,
            triggerCheck: () => _ = TriggerCheckAsync(),
            onConfigSaved: cfg => SaveConfig(cfg),
            dispatcher: _dispatcher,
            scheduler: _scheduler
        );

        _pipe = new PipeServer(_pipeHandler.Handle);
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
