using AppWatchdog.Service.HealthChecks;
using System.Diagnostics;

public sealed class HttpHealthCheck : IHealthCheck
{
    private readonly string _urlRaw;
    private readonly int _expectedStatus;

    public HttpHealthCheck(string url, int expectedStatus = 200)
    {
        _urlRaw = url;
        _expectedStatus = expectedStatus;
    }

    public async Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        if (!Uri.TryCreate(_urlRaw, UriKind.Absolute, out var uri))
            return HealthCheckResult.Down("Invalid URL format.");

        try
        {
            using var _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var sw = Stopwatch.StartNew();
            using var resp = await _client.GetAsync(_urlRaw, ct);
            sw.Stop();

            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(sw.ElapsedMilliseconds)
                : HealthCheckResult.Down(
                    $"HTTP {(int)resp.StatusCode}",
                    sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Down("HTTP request timed out.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Down($"HTTP failed: {ex.Message}");
        }
    }
}
