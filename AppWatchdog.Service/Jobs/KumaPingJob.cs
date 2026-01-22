using AppWatchdog.Service.Jobs;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.Jobs;

public sealed class KumaPingJob : IJob
{
    private readonly WatchedApp _app;

    public KumaPingJob(WatchedApp app)
    {
        _app = app;
    }

    public string Id => $"kuma:{_app.ExePath}";
    public TimeSpan Interval =>
        TimeSpan.FromSeconds(Math.Max(10, _app.UptimeKuma!.IntervalSeconds));

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

    public void Dispose() { }
}
