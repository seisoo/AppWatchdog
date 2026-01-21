using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Shared;

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
                IsRunning =
                    a.Enabled &&
                    !string.IsNullOrWhiteSpace(a.ExePath) &&
                    Worker.IsRunning(a.ExePath!)
            }).ToList(),
            SystemInfo = Worker.SystemInfoCollect(),
            PipeProtocolVersion = PipeProtocol.ProtocolVersion
        };

        _publish(snap);
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
