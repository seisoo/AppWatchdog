using System.ServiceProcess;

namespace AppWatchdog.Service.HealthChecks;

/// <summary>
/// Health check that verifies a Windows service is running.
/// </summary>
public sealed class WindowsServiceHealthCheck : IHealthCheck
{
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsServiceHealthCheck"/> class.
    /// </summary>
    /// <param name="serviceName">Windows service name.</param>
    public WindowsServiceHealthCheck(string serviceName)
    {
        _serviceName = serviceName;
    }

    /// <summary>
    /// Checks whether the Windows service is running.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_serviceName))
            return Task.FromResult(HealthCheckResult.Down("ServiceName is empty."));

        try
        {
            using var sc = new ServiceController(_serviceName);
            return Task.FromResult(
                sc.Status == ServiceControllerStatus.Running
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Down($"Service state: {sc.Status}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Down($"Service query failed: {ex.Message}"));
        }
    }
}
