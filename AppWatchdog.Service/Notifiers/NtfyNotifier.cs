using System.Net.Http.Headers;
using System.Text;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Notifiers;

/// <summary>
/// Sends notifications using the Ntfy service.
/// </summary>
public sealed class NtfyNotifier : NotifierBase<NtfySettings>
{
    /// <summary>
    /// Gets the notifier name.
    /// </summary>
    public override string Name => "ntfy";

    /// <summary>
    /// Initializes a new instance of the <see cref="NtfyNotifier"/> class.
    /// </summary>
    /// <param name="settings">Ntfy settings.</param>
    public NtfyNotifier(NtfySettings settings)
        : base(settings)
    {
    }

    /// <summary>
    /// Validates that Ntfy settings are configured.
    /// </summary>
    /// <param name="error">Error message if invalid.</param>
    /// <returns><c>true</c> when configured.</returns>
    public override bool IsConfigured(out string? error)
    {
        if (!Settings.Enabled)
        {
            error = "NTFY ist deaktiviert.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.BaseUrl))
        {
            error = "NTFY BaseUrl fehlt.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.Topic))
        {
            error = "NTFY Topic fehlt.";
            return false;
        }

        error = null;
        return true;
    }

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    /// <summary>
    /// Sends a notification through Ntfy.
    /// </summary>
    /// <param name="ntfy">Ntfy settings.</param>
    /// <param name="title">Message title.</param>
    /// <param name="message">Message body.</param>
    /// <param name="tagsCsv">Optional tags.</param>
    /// <param name="priority">Optional priority.</param>
    /// <returns>A task representing the send operation.</returns>
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
