using System.Net.Http;

namespace AppWatchdog.Service;

internal static class UptimeKumaClient
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    static string Vis(string s)
    {
        if (s == null) return "<null>";
        return s
            .Replace("\r", "<CR>")
            .Replace("\n", "<LF>")
            .Replace("\t", "<TAB>")
            .Replace("\u00A0", "<NBSP>")
            .Replace("\u200B", "<ZWSP>");
    }

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
