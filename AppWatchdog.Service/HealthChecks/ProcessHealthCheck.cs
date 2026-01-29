namespace AppWatchdog.Service.HealthChecks;

/// <summary>
/// Health check that verifies a process is running.
/// </summary>
public sealed class ProcessHealthCheck : IHealthCheck
{
    private readonly string _exePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessHealthCheck"/> class.
    /// </summary>
    /// <param name="exePath">Executable path to check.</param>
    public ProcessHealthCheck(string exePath)
    {
        _exePath = exePath;
    }

    /// <summary>
    /// Checks whether the process is running.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The health check result.</returns>
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
