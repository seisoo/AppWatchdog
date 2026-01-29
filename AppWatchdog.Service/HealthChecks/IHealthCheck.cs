namespace AppWatchdog.Service.HealthChecks;

/// <summary>
/// Defines a health check for a monitored target.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Executes the health check.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    Task<HealthCheckResult> CheckAsync(CancellationToken ct);
}
