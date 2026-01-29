using System.Net.Http;
using System.Text;
using System.Text.Json;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Notifiers;

/// <summary>
/// Sends notifications to a Discord webhook.
/// </summary>
public sealed class DiscordNotifier : NotifierBase<DiscordSettings>
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordNotifier"/> class.
    /// </summary>
    /// <param name="settings">Discord settings.</param>
    public DiscordNotifier(DiscordSettings settings)
        : base(settings)
    {
    }

    /// <summary>
    /// Gets the notifier name.
    /// </summary>
    public override string Name => "discord";

    /// <summary>
    /// Validates that Discord settings are configured.
    /// </summary>
    /// <param name="error">Error message if invalid.</param>
    /// <returns><c>true</c> when configured.</returns>
    public override bool IsConfigured(out string? error)
    {
        if (!Settings.Enabled)
        {
            error = "Discord ist deaktiviert.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.WebhookUrl))
        {
            error = "Discord WebhookUrl fehlt.";
            return false;
        }

        if (!Uri.TryCreate(Settings.WebhookUrl.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Discord WebhookUrl ist keine gültige absolute URL.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Discord WebhookUrl muss https sein.";
            return false;
        }

        if (!uri.AbsolutePath.Contains("/api/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            error = "Discord WebhookUrl sieht nicht wie ein Discord-Webhook aus.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Sends a message to the configured Discord webhook.
    /// </summary>
    /// <param name="title">Embed title.</param>
    /// <param name="message">Embed message body.</param>
    /// <param name="color">Optional embed color.</param>
    /// <returns>A task representing the send operation.</returns>
    public async Task SendAsync(
        string title,
        string message,
        int? color = null)
    {
        if (!IsConfigured(out _))
            return;

        var payload = new
        {
            username = string.IsNullOrWhiteSpace(Settings.Username)
                ? "AppWatchdog"
                : Settings.Username,

            avatar_url = string.IsNullOrWhiteSpace(Settings.AvatarUrl)
                ? null
                : Settings.AvatarUrl,

            embeds = new[]
            {
                new
                {
                    title = title,
                    description = message,
                    color = color ?? 0x5865F2 // Discord Blau
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, Settings.WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}
