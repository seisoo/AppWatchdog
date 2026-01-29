using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AppWatchdog.Service;

/// <summary>
/// Sends notifications via the Telegram Bot API.
/// </summary>
public sealed class TelegramNotifier : NotifierBase<TelegramSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramNotifier"/> class.
    /// </summary>
    /// <param name="settings">Telegram settings.</param>
    public TelegramNotifier(TelegramSettings settings)
        : base(settings)
    {
    }

    /// <summary>
    /// Gets the notifier name.
    /// </summary>
    public override string Name => "telegram";

    /// <summary>
    /// Validates that Telegram settings are configured.
    /// </summary>
    /// <param name="error">Error message if invalid.</param>
    /// <returns><c>true</c> when configured.</returns>
    public override bool IsConfigured(out string? error)
    {
        if (!Settings.Enabled)
        {
            error = "Telegram ist deaktiviert.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.BotToken))
        {
            error = "Telegram BotToken fehlt.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.ChatId))
        {
            error = "Telegram ChatId fehlt.";
            return false;
        }

        error = null;
        return true;
    }

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    /// <summary>
    /// Sends a message using the Telegram Bot API.
    /// </summary>
    /// <param name="telegram">Telegram settings.</param>
    /// <param name="message">Message content.</param>
    /// <returns>A task representing the send operation.</returns>
    public static async Task SendAsync(
        TelegramSettings telegram,
        string message)
    {
        if (telegram == null || !telegram.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(telegram.BotToken) ||
            string.IsNullOrWhiteSpace(telegram.ChatId))
            return;

        var url =
            $"https://api.telegram.org/bot{telegram.BotToken}/sendMessage";

        var payload = new
        {
            chat_id = telegram.ChatId,
            text = message,
            parse_mode = telegram.UseMarkdown ? "Markdown" : null,
            disable_web_page_preview = true
        };

        var json = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}
