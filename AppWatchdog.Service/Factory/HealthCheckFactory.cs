using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Service.HealthChecks;

/// <summary>
/// Creates health check implementations for watched apps.
/// </summary>
public static class HealthCheckFactory
{
    /// <summary>
    /// Creates a health check instance for the given app configuration.
    /// </summary>
    /// <param name="app">Watched app configuration.</param>
    /// <returns>The health check implementation.</returns>
    /// <exception cref="NotSupportedException">Thrown for unsupported app types.</exception>
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
