using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Notifications;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;
using System.Globalization;
using System.Text;

namespace AppWatchdog.Service;

public sealed class NotificationDispatcher
{
    private readonly WatchdogConfig _cfg;

    private readonly MailNotifier _mail;
    private readonly NtfyNotifier _ntfy;
    private readonly DiscordNotifier _discord;
    private readonly TelegramNotifier _telegram;

    public NotificationDispatcher(WatchdogConfig cfg)
    {
        _cfg = cfg;

        _mail = new MailNotifier(cfg.Smtp);
        _ntfy = new NtfyNotifier(cfg.Ntfy);
        _discord = new DiscordNotifier(cfg.Discord);
        _telegram = new TelegramNotifier(cfg.Telegram);
    }

    // =====================================================
    // ENTRY POINT
    // =====================================================
    public void Dispatch(NotificationContext ctx)
    {
        _ = Task.Run(() => DispatchInternalAsync(ctx));
    }

    // =====================================================
    // CORE
    // =====================================================
    private async Task DispatchInternalAsync(NotificationContext ctx)
    {
        try
        {
            var strings = new NotificationStringProvider(_cfg.CultureName);

            string title = $"AppWatchdog – {ctx.App.Name}";

            string summaryText = ctx.Type switch
            {
                AppNotificationType.Up => strings.SummaryUp,
                AppNotificationType.Restart => strings.SummaryRestart,
                _ => strings.SummaryDown
            };

            string summaryColor = ctx.Type switch
            {
                AppNotificationType.Up => "#15803d",
                AppNotificationType.Restart => "#2563eb",
                _ => "#b91c1c"
            };

            string detailsText = BuildDetailsText(ctx, strings);
            string plainText = BuildPlainText(ctx, strings);
            string html = BuildHtmlMail(
                ctx.App.Name,
                summaryText,
                summaryColor,
                detailsText,
                strings);

            DispatchMail(ctx, title, html);
            await DispatchNtfyAsync(ctx, title, plainText);
            await DispatchDiscordAsync(ctx, title, plainText);
            await DispatchTelegramAsync(ctx, plainText);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"NotificationDispatcher failed for '{ctx.App.Name}': {ex}");
        }
    }

    // =====================================================
    // CHANNELS
    // =====================================================
    private void DispatchMail(NotificationContext ctx, string title, string html)
    {
        if (ctx.TestOnlyChannel is { } ch && ch != NotificationChannel.Mail)
            return;

        if (!_mail.IsConfigured(out _))
            return;

        try
        {
            MailNotifier.SendHtml(_cfg.Smtp, title, html);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Mail notification failed: {ex.Message}");
        }
    }

    private async Task DispatchNtfyAsync(NotificationContext ctx, string title, string text)
    {
        if (ctx.TestOnlyChannel is { } ch && ch != NotificationChannel.Ntfy)
            return;

        if (!_ntfy.IsConfigured(out _))
            return;

        try
        {
            await NtfyNotifier.SendAsync(
                _cfg.Ntfy,
                title,
                text,
                ctx.Type == AppNotificationType.Down ? "warning,server" : "ok,server",
                ctx.Type == AppNotificationType.Down ? 4 : 2);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Ntfy notification failed: {ex.Message}");
        }
    }

    private async Task DispatchDiscordAsync(NotificationContext ctx, string title, string text)
    {
        if (ctx.TestOnlyChannel is { } ch && ch != NotificationChannel.Discord)
            return;

        if (!_discord.IsConfigured(out _))
            return;

        try
        {
            await _discord.SendAsync(
                title,
                text,
                ctx.Type == AppNotificationType.Down ? 0xB91C1C : 0x7C3AED);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Discord notification failed: {ex.Message}");
        }
    }

    private async Task DispatchTelegramAsync(NotificationContext ctx, string text)
    {
        if (ctx.TestOnlyChannel is { } ch && ch != NotificationChannel.Telegram)
            return;

        if (!_telegram.IsConfigured(out _))
            return;

        try
        {
            await TelegramNotifier.SendAsync(_cfg.Telegram, text);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Telegram notification failed: {ex.Message}");
        }
    }

    // =====================================================
    // MESSAGE BUILDERS
    // =====================================================
    private static string BuildTargetLabel(
        WatchedApp app,
        NotificationStringProvider strings)
    {
        return app.Type switch
        {
            WatchTargetType.Executable =>
                strings.TargetExecutable(app.ExePath),

            WatchTargetType.WindowsService =>
                strings.TargetService(app.ServiceName),

            WatchTargetType.HttpEndpoint =>
                strings.TargetHttp(app.Url),

            WatchTargetType.TcpPort =>
                strings.TargetTcp(app.Host, app.Port ?? 0),

            _ => app.Name
        };
    }

    private static string BuildDetailsText(
        NotificationContext ctx,
        NotificationStringProvider strings)
    {
        var sb = new StringBuilder();

        sb.AppendLine(BuildTargetLabel(ctx.App, strings));
        sb.AppendLine();

        if (ctx.Status != null)
        {
            sb.AppendLine(
                ctx.Status.IsRunning
                    ? strings.StatusReachable
                    : strings.StatusUnreachable);

            if (ctx.StartAttempted &&
                (ctx.App.Type == WatchTargetType.Executable ||
                 ctx.App.Type == WatchTargetType.WindowsService))
            {
                sb.AppendLine(strings.RestartAttempted);
            }

            if (!string.IsNullOrWhiteSpace(ctx.Status.LastStartError))
            {
                sb.AppendLine();
                sb.AppendLine(strings.ErrorLabel);
                sb.AppendLine(ctx.Status.LastStartError);
            }

            if (ctx.Status.PingMs.HasValue)
            {
                sb.AppendLine(
                    ctx.Status.IsRunning
                        ? $"Ping: {ctx.Status.PingMs.Value} ms"
                        : $"Ping: timeout");
            }

        }

        sb.AppendLine();
        sb.AppendLine(strings.Footer(
            Environment.MachineName,
            DateTimeOffset.Now.ToString(CultureInfo.InvariantCulture)));

        return sb.ToString();
    }

    private static string BuildPlainText(
        NotificationContext ctx,
        NotificationStringProvider strings)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"AppWatchdog – {ctx.App.Name}");
        sb.AppendLine(BuildTargetLabel(ctx.App, strings));
        sb.AppendLine();

        if (ctx.Status != null)
        {
            sb.AppendLine(
                ctx.Status.IsRunning
                    ? strings.SummaryUp
                    : strings.SummaryDown);

            if (!string.IsNullOrWhiteSpace(ctx.Status.LastStartError))
            {
                sb.AppendLine();
                sb.AppendLine(ctx.Status.LastStartError);
            }
        }

        return sb.ToString();
    }

    // =====================================================
    // HTML MAIL (PURPLE / WPF-UI STYLE)
    // =====================================================
    private static string BuildHtmlMail(
        string appName,
        string summaryText,
        string summaryColorHex,
        string detailsText,
        NotificationStringProvider strings)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
body {{
    font-family: 'Segoe UI', Arial, sans-serif;
    background-color: #0f172a;
    color: #e5e7eb;
    padding: 24px;
}}
.card {{
    max-width: 720px;
    margin: auto;
    background-color: #020617;
    border-radius: 14px;
    box-shadow: 0 10px 30px rgba(0,0,0,.5);
    overflow: hidden;
}}
.header {{
    background: linear-gradient(90deg, #7c3aed, #6366f1);
    padding: 20px;
    font-size: 22px;
    font-weight: 600;
}}
.badge {{
    display: inline-block;
    padding: 6px 14px;
    border-radius: 999px;
    font-weight: 600;
    background-color: {summaryColorHex};
    margin-bottom: 12px;
}}
.content {{
    padding: 24px;
}}
.mono {{
    font-family: Consolas, monospace;
    font-size: 13px;
    opacity: .9;
    white-space: pre-line;
}}
.footer {{
    padding: 16px;
    font-size: 11px;
    opacity: .6;
    text-align: center;
}}
</style>
</head>

<body>
<div class='card'>
    <div class='header'>
        AppWatchdog – {appName}
    </div>

    <div class='content'>
        <div class='badge'>{summaryText}</div>

        <div class='mono'>
{detailsText}
        </div>
    </div>

    <div class='footer'>
        {strings.Footer(Environment.MachineName, DateTimeOffset.Now.ToString())}
    </div>
</div>
</body>
</html>";
    }
}
