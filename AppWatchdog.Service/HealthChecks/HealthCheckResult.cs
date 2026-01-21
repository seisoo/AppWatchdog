namespace AppWatchdog.Service.HealthChecks;

public sealed class HealthCheckResult
{
    public bool IsHealthy { get; init; }
    public string? Error { get; init; }
}
