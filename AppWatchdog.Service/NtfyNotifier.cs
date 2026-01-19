using System.Net.Http.Headers;
using System.Text;
using AppWatchdog.Shared;

namespace AppWatchdog.Service;

public static class NtfyNotifier
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    public static async Task SendAsync(NtfySettings ntfy, string title, string message, string? tagsCsv = null, int? priority = null)
    {
        if (ntfy == null || !ntfy.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(ntfy.BaseUrl) || string.IsNullOrWhiteSpace(ntfy.Topic))
            return;

        var baseUrl = ntfy.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{ntfy.Topic.Trim()}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(message ?? "", Encoding.UTF8, "text/plain")
        };

        req.Headers.TryAddWithoutValidation("Title", title ?? "AppWatchdog");
        req.Headers.TryAddWithoutValidation("Priority", (priority ?? ntfy.Priority).ToString());

        if (!string.IsNullOrWhiteSpace(tagsCsv))
            req.Headers.TryAddWithoutValidation("Tags", tagsCsv);

        if (!string.IsNullOrWhiteSpace(ntfy.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ntfy.Token);

        using var resp = await _http.SendAsync(req).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}
