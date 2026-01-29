using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Jobs;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;
using System.ServiceProcess;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Periodically captures a service status snapshot.
/// </summary>
public sealed class SnapshotJob : IJob
{
    private readonly Func<WatchdogConfig> _getConfig;
    private readonly Action<ServiceSnapshot> _publish;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotJob"/> class.
    /// </summary>
    /// <param name="getConfig">Delegate to retrieve configuration.</param>
    /// <param name="publish">Callback to publish snapshots.</param>
    public SnapshotJob(
        Func<WatchdogConfig> getConfig,
        Action<ServiceSnapshot> publish)
    {
        _getConfig = getConfig;
        _publish = publish;
    }

    /// <summary>
    /// Gets the fixed snapshot job identifier.
    /// </summary>
    public string Id => "snapshot";

    /// <summary>
    /// Gets the snapshot interval.
    /// </summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(2);

    /// <summary>
    /// Executes snapshot collection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task.</returns>
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

    /// <summary>
    /// Performs a lightweight running check for the app.
    /// </summary>
    /// <param name="a">App to check.</param>
    /// <returns><c>true</c> when the app is running.</returns>
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

    /// <summary>
    /// Checks whether a Windows service is running.
    /// </summary>
    /// <param name="serviceName">Windows service name.</param>
    /// <returns><c>true</c> if the service is running.</returns>
    private static bool IsServiceRunning(string serviceName)
    {
        using var sc = new ServiceController(serviceName);
        return sc.Status == ServiceControllerStatus.Running;
    }

    /// <summary>
    /// Disposes the job.
    /// </summary>
    public void Dispose() { }
}
