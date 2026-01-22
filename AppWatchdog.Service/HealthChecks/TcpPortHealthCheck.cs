using System.Diagnostics;
using System.Net.Sockets;

namespace AppWatchdog.Service.HealthChecks;

public sealed class TcpPortHealthCheck : IHealthCheck
{
    private readonly string _host;
    private readonly int _port;

    public TcpPortHealthCheck(string host, int port)
    {
        _host = host;
        _port = port;
    }

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
