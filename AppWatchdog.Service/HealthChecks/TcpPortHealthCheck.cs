using System.Diagnostics;
using System.Net.Sockets;

namespace AppWatchdog.Service.HealthChecks;

/// <summary>
/// Health check that verifies a TCP port is reachable.
/// </summary>
public sealed class TcpPortHealthCheck : IHealthCheck
{
    private readonly string _host;
    private readonly int _port;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpPortHealthCheck"/> class.
    /// </summary>
    /// <param name="host">Target host name.</param>
    /// <param name="port">Target port.</param>
    public TcpPortHealthCheck(string host, int port)
    {
        _host = host;
        _port = port;
    }

    /// <summary>
    /// Executes the TCP port health check.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public async Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_host))
            return HealthCheckResult.Down("Host is empty.");

        if (_port <= 0 || _port > 65535)
            return HealthCheckResult.Down("Port is invalid.");

        var sw = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, ct);
            sw.Stop();

            return HealthCheckResult.Healthy(sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return HealthCheckResult.Down(
                $"TCP connect failed: {ex.Message}",
                sw.ElapsedMilliseconds);
        }
    }
}
