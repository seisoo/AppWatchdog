using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Notifications;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AppWatchdog.Service;

/// <summary>
/// Dispatches notifications across configured channels.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly WatchdogConfig _cfg;

    private readonly MailNotifier _mail;
    private readonly NtfyNotifier _ntfy;
    private readonly DiscordNotifier _discord;
    private readonly TelegramNotifier _telegram;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDispatcher"/> class.
    /// </summary>
    /// <param name="cfg">Current configuration.</param>
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
    /// <summary>
    /// Queues a notification dispatch task.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    public void Dispatch(NotificationContext ctx)
    {
        _ = Task.Run(() => DispatchInternalAsync(ctx));
    }

    /// <summary>
    /// Queues a backup notification dispatch task.
    /// </summary>
    /// <param name="ctx">Backup notification context.</param>
    public void DispatchBackup(BackupNotificationContext ctx)
    {
        _ = Task.Run(() => DispatchBackupInternalAsync(ctx));
    }

    // =====================================================
    // CORE
    // =====================================================
    /// <summary>
    /// Builds and sends notifications across channels.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <returns>A task representing the dispatch work.</returns>
    private async Task DispatchInternalAsync(NotificationContext ctx)
    {
        try
        {
            var strings = new NotificationStringProvider(_cfg.CultureName);

            var template = ResolveTemplate(ctx.Type);
            var placeholders = BuildPlaceholders(ctx, strings);

            var defaultTitle = $"AppWatchdog – {ctx.App.Name}";
            var defaultSummary = ctx.Type switch
            {
                AppNotificationType.Up => strings.SummaryUp,
                AppNotificationType.Restart => strings.SummaryRestart,
                _ => strings.SummaryDown
            };

            var defaultColor = ctx.Type switch
            {
                AppNotificationType.Up => "#15803d",
                AppNotificationType.Restart => "#2563eb",
                _ => "#b91c1c"
            };

            string title = ApplyTemplate(template.Title, placeholders);
            if (string.IsNullOrWhiteSpace(title))
                title = defaultTitle;

            string summaryText = ApplyTemplate(template.Summary, placeholders);
            if (string.IsNullOrWhiteSpace(summaryText))
                summaryText = defaultSummary;

            string summaryColor = NormalizeColor(template.Color, defaultColor);

            string templateBody = ApplyTemplate(template.Body, placeholders);

            string detailsText = string.IsNullOrWhiteSpace(templateBody)
                ? BuildDetailsText(ctx, strings)
                : templateBody;

            string plainText = string.IsNullOrWhiteSpace(templateBody)
                ? BuildPlainText(ctx, strings)
                : templateBody;

            string html = BuildHtmlMail(
                title,
                summaryText,
                summaryColor,
                detailsText,
                strings);

            DispatchMail(ctx, title, html);
            await DispatchNtfyAsync(ctx, title, plainText);
            await DispatchDiscordAsync(ctx, title, plainText, summaryColor);
            await DispatchTelegramAsync(ctx, plainText);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"NotificationDispatcher failed for '{ctx.App.Name}': {ex}");
        }
    }

    /// <summary>
    /// Builds and sends backup notifications across channels.
    /// </summary>
    /// <param name="ctx">Backup notification context.</param>
    /// <returns>A task representing the dispatch work.</returns>
    private async Task DispatchBackupInternalAsync(BackupNotificationContext ctx)
    {
        try
        {
            var strings = new NotificationStringProvider(_cfg.CultureName);

            var (title, summary, color) = BuildBackupSummary(ctx);
            var detailsText = BuildBackupDetailsText(ctx);
            var plainText = BuildBackupPlainText(ctx);

            var html = BuildHtmlMail(
                title,
                summary,
                color,
                detailsText,
                strings);

            var mappedType = ctx.Type == BackupNotificationType.Failed
                ? AppNotificationType.Down
                : AppNotificationType.Up;

            var dispatchCtx = new NotificationContext
            {
                Type = mappedType,
                App = new WatchedApp { Name = ctx.Plan.Name },
                TestOnlyChannel = ctx.TestOnlyChannel
            };

            DispatchMail(dispatchCtx, title, html);
            await DispatchNtfyAsync(dispatchCtx, title, plainText);
            await DispatchDiscordAsync(dispatchCtx, title, plainText, color);
            await DispatchTelegramAsync(dispatchCtx, plainText);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"Backup notification failed for '{ctx.Plan.Name}': {ex}");
        }
    }

    private NotificationTemplate ResolveTemplate(AppNotificationType type)
    {
        var templates = _cfg.NotificationTemplates ?? NotificationTemplateSet.CreateDefault();

        return type switch
        {
            AppNotificationType.Up => templates.Up ?? NotificationTemplate.CreateDefaultUp(),
            AppNotificationType.Restart => templates.Restart ?? NotificationTemplate.CreateDefaultRestart(),
            _ => templates.Down ?? NotificationTemplate.CreateDefaultDown()
        };
    }

    private static Dictionary<string, string> BuildPlaceholders(
        NotificationContext ctx,
        NotificationStringProvider strings)
    {
        var summary = ctx.Type switch
        {
            AppNotificationType.Up => strings.SummaryUp,
            AppNotificationType.Restart => strings.SummaryRestart,
            _ => strings.SummaryDown
        };

        var status = ctx.Type switch
        {
            AppNotificationType.Up => "UP",
            AppNotificationType.Restart => "RESTART",
            _ => "DOWN"
        };

        var error = ctx.Status?.LastStartError ?? "";
        var ping = ctx.Status?.PingMs.HasValue == true
            ? $"{ctx.Status.PingMs.Value} ms"
            : ctx.Status?.IsRunning == false ? "timeout" : "";

        var target = BuildTargetLabel(ctx.App, strings);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["$jobname"] = ctx.App.Name,
            ["$summary"] = summary,
            ["$status"] = status,
            ["$target"] = target,
            ["$error"] = error,
            ["$ping"] = ping,
            ["$machine"] = Environment.MachineName,
            ["$time"] = DateTimeOffset.Now.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static (string title, string summary, string color) BuildBackupSummary(BackupNotificationContext ctx)
    {
        var title = $"AppWatchdog – Backup {ctx.Plan.Name}";
        return ctx.Type switch
        {
            BackupNotificationType.Started => (title, "Backup started", "#2563eb"),
            BackupNotificationType.Completed => (title, "Backup completed", "#15803d"),
            _ => (title, "Backup failed", "#b91c1c")
        };
    }

    private static string BuildBackupDetailsText(BackupNotificationContext ctx)
    {
        var source = ctx.Plan.Source.Type switch
        {
            BackupSourceType.MsSql => ctx.Plan.Source.SqlDatabase,
            _ => ctx.Plan.Source.Path
        };

        var target = ctx.Plan.Target.Type switch
        {
            BackupTargetType.Sftp => $"{ctx.Plan.Target.SftpHost}:{ctx.Plan.Target.SftpRemoteDirectory}",
            _ => ctx.Plan.Target.LocalDirectory
        };

        var start = ctx.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var finish = ctx.FinishedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        var size = ctx.SizeBytes.HasValue
            ? $"{ctx.SizeBytes.Value / (1024d * 1024d):0.##} MB"
            : "-";

        var sb = new StringBuilder();
        sb.AppendLine($"Plan: {ctx.Plan.Name}");
        sb.AppendLine($"Source: {source}");
        sb.AppendLine($"Target: {target}");
        sb.AppendLine($"Start: {start}");

        if (ctx.Type != BackupNotificationType.Started)
            sb.AppendLine($"Finish: {finish}");

        if (ctx.Type == BackupNotificationType.Completed)
            sb.AppendLine($"Size: {size}");

        if (ctx.Type == BackupNotificationType.Failed)
            sb.AppendLine($"Error: {ctx.Error ?? "Unknown error"}");

        return sb.ToString();
    }

    private static string BuildBackupPlainText(BackupNotificationContext ctx)
        => BuildBackupDetailsText(ctx);

    private static string ApplyTemplate(string template, Dictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template))
            return "";

        var text = template;
        foreach (var kvp in values)
            text = text.Replace(kvp.Key, kvp.Value ?? "", StringComparison.OrdinalIgnoreCase);

        return text;
    }

    private static string NormalizeColor(string? color, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(color) ? fallback : color.Trim();
        if (!candidate.StartsWith('#'))
            candidate = "#" + candidate;

        return IsHexColor(candidate) ? candidate : fallback;
    }

    private static bool IsHexColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.StartsWith('#') ? value[1..] : value;
        if (hex.Length != 6)
            return false;

        return hex.All(c => Uri.IsHexDigit(c));
    }

    private static int? ToDiscordColor(string hex)
    {
        if (!IsHexColor(hex))
            return null;

        var raw = hex.StartsWith('#') ? hex[1..] : hex;
        return int.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? rgb
            : null;
    }

    // =====================================================
    // CHANNELS
    // =====================================================
    /// <summary>
    /// Sends an email notification when configured.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <param name="title">Email subject.</param>
    /// <param name="html">Email HTML body.</param>
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

    /// <summary>
    /// Sends a notification via Ntfy when configured.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <param name="title">Message title.</param>
    /// <param name="text">Message body.</param>
    /// <returns>A task representing the send.</returns>
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

    /// <summary>
    /// Sends a notification via Discord when configured.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <param name="title">Message title.</param>
    /// <param name="text">Message body.</param>
    /// <param name="summaryColor">Message color.</param>
    /// <returns>A task representing the send.</returns>
    private async Task DispatchDiscordAsync(NotificationContext ctx, string title, string text, string summaryColor)
    {
        if (ctx.TestOnlyChannel is { } ch && ch != NotificationChannel.Discord)
            return;

        if (!_discord.IsConfigured(out _))
            return;

        try
        {
            var color = ToDiscordColor(summaryColor)
                ?? (ctx.Type == AppNotificationType.Down ? 0xB91C1C : 0x7C3AED);

            await _discord.SendAsync(
                title,
                text,
                color);
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine("ERROR", $"Discord notification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a notification via Telegram when configured.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <param name="text">Message body.</param>
    /// <returns>A task representing the send.</returns>
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
    /// <summary>
    /// Builds the target label for notification output.
    /// </summary>
    /// <param name="app">Watched app.</param>
    /// <param name="strings">Localized strings.</param>
    /// <returns>The target label.</returns>
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

    /// <summary>
    /// Builds the detailed text body for notifications.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <param name="strings">Localized strings.</param>
    /// <returns>The detailed text.</returns>
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
                        : "Ping: timeout");
            }

        }

        sb.AppendLine();
        sb.AppendLine(strings.Footer(
            Environment.MachineName,
            DateTimeOffset.Now.ToString(CultureInfo.InvariantCulture)));

        return sb.ToString();
    }

    /// <summary>
    /// Builds the plain text body for notifications.
    /// </summary>
    /// <param name="ctx">Notification context.</param>
    /// <param name="strings">Localized strings.</param>
    /// <returns>The plain text body.</returns>
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
    /// <summary>
    /// Builds the HTML email body for notifications.
    /// </summary>
    /// <param name="headerText">Header text.</param>
    /// <param name="summaryText">Summary text.</param>
    /// <param name="summaryColorHex">Summary color hex code.</param>
    /// <param name="detailsText">Details body.</param>
    /// <param name="strings">Localized strings.</param>
    /// <returns>The HTML string.</returns>
    private static string BuildHtmlMail(
        string headerText,
        string summaryText,
        string summaryColorHex,
        string detailsText,
        NotificationStringProvider strings)
    {
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        var os = Environment.OSVersion.ToString();
        var dotnet = Environment.Version.ToString();
        var time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
body {{
    font-family: 'Segoe UI', Arial, sans-serif;
    background-color: #f5f5f7;
    color: #111827;
    margin: 0;
    padding: 24px;
}}
.card {{
    max-width: 760px;
    margin: auto;
    background-color: #ffffff;
    border-radius: 16px;
    border: 1px solid #e5e7eb;
    box-shadow: 0 10px 30px rgba(0,0,0,.08);
    overflow: hidden;
}}
.header {{
    padding: 22px 24px 8px 24px;
    font-size: 20px;
    font-weight: 600;
}}
.accent {{
    height: 4px;
    background: {summaryColorHex};
}}
.summary {{
    margin: 16px 24px 10px 24px;
    padding: 12px 14px;
    border-radius: 12px;
    background-color: #f9fafb;
    border: 1px solid #e5e7eb;
    display: flex;
    align-items: center;
    gap: 10px;
    font-weight: 600;
}}
.dot {{
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background-color: {summaryColorHex};
}}
.section {{
    padding: 0 24px 16px 24px;
}}
.section-title {{
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: .08em;
    color: #6b7280;
    margin: 6px 0 8px 0;
}}
.details {{
    font-size: 13px;
    line-height: 1.6;
    white-space: pre-line;
}}
.meta-grid {{
    display: grid;
    grid-template-columns: 120px 1fr;
    row-gap: 6px;
    column-gap: 12px;
    font-size: 12px;
    color: #374151;
}}
.mono {{
    font-family: Consolas, 'Cascadia Mono', 'Segoe UI Mono', monospace;
    background: #f3f4f6;
    padding: 2px 6px;
    border-radius: 6px;
    border: 1px solid #e5e7eb;
    display: inline-block;
}}
.footer {{
    padding: 10px 24px 20px 24px;
    font-size: 11px;
    color: #6b7280;
}}
</style>
</head>

<body>
<div class='card'>
    <div class='accent'></div>
    <div class='header'>
        {headerText}
    </div>

    <div class='summary'>
        <span class='dot'></span>
        <span>{summaryText}</span>
    </div>

    <div class='section'>
        <div class='section-title'>Details</div>
        <div class='details'>
{detailsText}
        </div>
    </div>

    <div class='section'>
        <div class='section-title'>System</div>
        <div class='meta-grid'>
            <div>Machine</div>
            <div><span class='mono'>{machine}</span></div>
            <div>User</div>
            <div><span class='mono'>{user}</span></div>
            <div>OS</div>
            <div>{os}</div>
            <div>.NET</div>
            <div><span class='mono'>{dotnet}</span></div>
            <div>Time</div>
            <div><span class='mono'>{time}</span></div>
        </div>
    </div>

    <div class='footer'>
        {strings.Footer(machine, DateTimeOffset.Now.ToString())}
    </div>
</div>
</body>
</html>";
    }
}
