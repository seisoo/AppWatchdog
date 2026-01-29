using AppWatchdog.Service.HealthChecks;
using System.Diagnostics;

namespace AppWatchdog.Service.HealthChecks
{
    /// <summary>
    /// Health check that validates an HTTP endpoint response.
    /// </summary>
    public sealed class HttpHealthCheck : IHealthCheck
    {
        private readonly string _urlRaw;
        private readonly int _expectedStatus;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpHealthCheck"/> class.
        /// </summary>
        /// <param name="url">Endpoint URL.</param>
        /// <param name="expectedStatus">Expected HTTP status code.</param>
        public HttpHealthCheck(string url, int expectedStatus = 200)
        {
            _urlRaw = url;
            _expectedStatus = expectedStatus;
        }

        /// <summary>
        /// Executes the HTTP health check.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The health check result.</returns>
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

                var statusCode = (int)resp.StatusCode;
                return statusCode == _expectedStatus
                    ? HealthCheckResult.Healthy(sw.ElapsedMilliseconds)
                    : HealthCheckResult.Down(
                        $"HTTP {statusCode}",
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
}
