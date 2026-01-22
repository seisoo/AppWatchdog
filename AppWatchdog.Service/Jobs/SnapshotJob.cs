using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;
using System.ServiceProcess;

namespace AppWatchdog.Service.Jobs;

public sealed class SnapshotJob : IJob
{
    private readonly Func<WatchdogConfig> _getConfig;
    private readonly Action<ServiceSnapshot> _publish;

    public SnapshotJob(
        Func<WatchdogConfig> getConfig,
        Action<ServiceSnapshot> publish)
    {
        _getConfig = getConfig;
        _publish = publish;
    }

    public string Id => "snapshot";
    public TimeSpan Interval => TimeSpan.FromSeconds(2);

    public Task ExecuteAsync(CancellationToken ct)
    {
        var cfg = _getConfig();

        var snap = new ServiceSnapshot
        {
            Timestamp = DateTimeOffset.Now,
            SessionState = UserSessionLauncher.GetSessionState(),
            Apps = cfg.Apps.Select(a => new AppStatus
            {
                Name = a.Name,
                ExePath = a.ExePath ?? "",
                Enabled = a.Enabled,
                IsRunning = a.Enabled && IsRunningLightweight(a),
            }).ToList(),
            SystemInfo = Worker.SystemInfoCollect(),
            PipeProtocolVersion = PipeProtocol.ProtocolVersion
        };

        _publish(snap);
        return Task.CompletedTask;
    }

    private static bool IsRunningLightweight(WatchedApp a)
    {
        try
        {
            return a.Type switch
            {
                WatchTargetType.Executable =>
                    !string.IsNullOrWhiteSpace(a.ExePath) && Worker.IsRunning(a.ExePath),

                WatchTargetType.WindowsService =>
                    !string.IsNullOrWhiteSpace(a.ServiceName) && IsServiceRunning(a.ServiceName),

                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool IsServiceRunning(string serviceName)
    {
        using var sc = new ServiceController(serviceName);
        return sc.Status == ServiceControllerStatus.Running;
    }

    public void Dispose() { }
}
