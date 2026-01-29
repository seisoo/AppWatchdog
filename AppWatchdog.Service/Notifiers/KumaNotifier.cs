using AppWatchdog.Service.Helpers;
using System.Net.Http;

namespace AppWatchdog.Service.Notifiers;

/// <summary>
/// Sends heartbeat pings to an Uptime Kuma server.
/// </summary>
internal static class KumaNotifier
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Sends a Kuma heartbeat ping.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Kuma server.</param>
    /// <param name="token">Push token.</param>
    /// <param name="isUp">Whether the app is up.</param>
    /// <param name="message">Optional message.</param>
    /// <returns>A task representing the send operation.</returns>
    public static async Task SendAsync(string baseUrl, string token, bool isUp, string? message)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            return;

        baseUrl = baseUrl.Trim();
        token = token.Trim();

        var status = isUp ? "up" : "down";
        var msg = Uri.EscapeDataString(message ?? "");

        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var path = baseUri.AbsolutePath.TrimEnd('/');
        var pushPath = path.EndsWith("/api/push", StringComparison.OrdinalIgnoreCase)
            ? $"{path}/{token}"
            : $"{path}/api/push/{token}";

        var ub = new UriBuilder(baseUri)
        {
            Path = pushPath,
            Query = $"status={status}&msg={msg}&ping="
        };

        var url = ub.Uri.AbsoluteUri;

        try
        {
            using var resp = await _http.GetAsync(ub.Uri).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                FileLogStore.WriteLine("WARN", $"Kuma HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} URL={url} BODY={body}");
            else
                FileLogStore.WriteLine("INFO", $"Kuma OK {(int)resp.StatusCode} URL={url} BODY={body}");
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Kuma EXCEPTION URL={url}", ex);
        }
    }
}
