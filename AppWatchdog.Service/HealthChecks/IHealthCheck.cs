namespace AppWatchdog.Service.HealthChecks;

public interface IHealthCheck
{
    Task<HealthCheckResult> CheckAsync(CancellationToken ct);
}
