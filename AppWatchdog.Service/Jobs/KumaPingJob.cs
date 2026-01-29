using AppWatchdog.Service.Jobs;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Sends periodic Uptime Kuma pings for executable targets.
/// </summary>
public sealed class KumaPingJob : IJob
{
    private readonly WatchedApp _app;

    /// <summary>
    /// Initializes a new instance of the <see cref="KumaPingJob"/> class.
    /// </summary>
    /// <param name="app">Watched application configuration.</param>
    public KumaPingJob(WatchedApp app)
    {
        _app = app;
    }

    /// <summary>
    /// Gets the unique job identifier.
    /// </summary>
    public string Id => $"kuma:{_app.ExePath}";

    /// <summary>
    /// Gets the job execution interval.
    /// </summary>
    public TimeSpan Interval =>
        TimeSpan.FromSeconds(Math.Max(10, _app.UptimeKuma!.IntervalSeconds));

    /// <summary>
    /// Executes the Kuma ping check.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var kuma = _app.UptimeKuma;
        if (kuma?.Enabled != true)
            return;

        if (_app.Type != WatchTargetType.Executable)
            return;

        if (string.IsNullOrWhiteSpace(_app.ExePath))
            return;

        bool running = Worker.IsRunning(_app.ExePath);

        await KumaNotifier.SendAsync(
            kuma.BaseUrl,
            kuma.PushToken,
            running,
            running ? "UP" : "DOWN");
    }

    /// <summary>
    /// Disposes the job.
    /// </summary>
    public void Dispose() { }
}
