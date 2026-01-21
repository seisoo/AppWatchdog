namespace AppWatchdog.Service.HealthChecks;

public sealed class ProcessHealthCheck : IHealthCheck
{
    private readonly string _exePath;

    public ProcessHealthCheck(string exePath)
    {
        _exePath = exePath;
    }

    public Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        bool running = AppWatchdog.Service.Worker.IsRunning(_exePath);

        return Task.FromResult(new HealthCheckResult
        {
            IsHealthy = running,
            Error = running ? null : "Prozess läuft nicht"
        });
    }
}
