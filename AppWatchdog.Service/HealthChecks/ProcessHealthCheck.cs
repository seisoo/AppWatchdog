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
        if (string.IsNullOrWhiteSpace(_exePath))
            return Task.FromResult(HealthCheckResult.Down("ExePath is empty."));

        bool running = Worker.IsRunning(_exePath);
        return Task.FromResult(running
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Down("Process not running."));
    }
}
