using System.ServiceProcess;

namespace AppWatchdog.Service.HealthChecks;

public sealed class WindowsServiceHealthCheck : IHealthCheck
{
    private readonly string _serviceName;

    public WindowsServiceHealthCheck(string serviceName)
    {
        _serviceName = serviceName;
    }

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
