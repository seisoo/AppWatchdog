using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.HealthChecks;

public static class HealthCheckFactory
{
    public static IHealthCheck Create(WatchedApp app)
    {
        return app.Type switch
        {
            WatchTargetType.Executable =>
                new ProcessHealthCheck(app.ExePath),

            WatchTargetType.WindowsService =>
                new WindowsServiceHealthCheck(app.ServiceName ?? ""),

            WatchTargetType.HttpEndpoint =>
                new HttpHealthCheck(app.Url ?? "", app.ExpectedStatusCode),

            WatchTargetType.TcpPort =>
                new TcpPortHealthCheck(app.Host ?? "", app.Port ?? 0),

            _ => throw new NotSupportedException($"Unsupported type: {app.Type}")
        };
    }
}
