using AppWatchdog.Service.Helpers;
using AppWatchdog.Service.Notifiers;
using AppWatchdog.Shared;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AppWatchdog.Service;

/// <summary>
/// Central dispatcher for all application notifications.
/// - Owns notifier lifecycle
/// - Validates active notification channels once
/// - Sends notifications
/// - Triggers passive Uptime Kuma pushes per app
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly WatchdogConfig _cfg;
    private readonly ILogger _log;

    // Active notifiers (global)
    private readonly MailNotifier _mail;
    private readonly NtfyNotifier _ntfy;
    private readonly DiscordNotifier _discord;
    private readonly TelegramNotifier _telegram;

    private bool _validated;

    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public NotificationDispatcher(WatchdogConfig cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;

        // Dispatcher owns all notifier instances
        _mail = new MailNotifier(cfg.Smtp);
        _ntfy = new NtfyNotifier(cfg.Ntfy);
        _discord = new DiscordNotifier(cfg.Discord);
        _telegram = new TelegramNotifier(cfg.Telegram);
    }

    // ============================================================
    // PUBLIC ENTRY
    // ============================================================

    public void Dispatch(NotificationContext ctx)
    {
        Task.Run(async () =>
        {
            try
            {
                await DispatchInternalAsync(ctx).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Notification dispatch failed: {Type} {App}",
                    ctx.Type,
                    ctx.App.Name);
            }
        });
    }

    // ============================================================
    // CORE
    // ============================================================

    private async Task DispatchInternalAsync(NotificationContext ctx)
    {
        ValidateOnce();

        var sys = SystemInfoCollector.Collect();
        var sysHtml = SystemInfoCollector.FormatForHtml(sys);

        var summaryHtml = BuildStatusSummaryHtml(
            ctx.SummaryStatus,
            ctx.SummaryColorHex,
            ctx.App.Name);

        var detailsHtml = BuildDetailsHtml(ctx);

        var textMessage = BuildPlainTextMessage(ctx, sys);

        // ---------------- MAIL ----------------
        if (_mail.IsConfigured(out _) &&
            (ctx.TestOnlyChannel == null || ctx.TestOnlyChannel == NotificationChannel.Mail))
        {
            var mailHtml = MailNotifier.WrapHtmlTemplate(
                ctx.Title,
                summaryHtml,
                detailsHtml,
                sysHtml);

            MailNotifier.SendHtml(_cfg.Smtp, ctx.Title, mailHtml);
        }

        // ---------------- NTFY ----------------
        if (_ntfy.IsConfigured(out _) &&
            (ctx.TestOnlyChannel == null || ctx.TestOnlyChannel == NotificationChannel.Ntfy))
        {
            await NtfyNotifier.SendAsync(
                _cfg.Ntfy,
                ctx.Title,
                textMessage,
                ctx.NtfyTags,
                ctx.NtfyPriority)
                .ConfigureAwait(false);
        }

        // ---------------- DISCORD ----------------
        if (_discord.IsConfigured(out _) &&
            (ctx.TestOnlyChannel == null || ctx.TestOnlyChannel == NotificationChannel.Discord))
        {
            await _discord.SendAsync(
                ctx.Title,
                $"{ctx.DiscordEmoji} **{ctx.Type.ToString().ToUpper()}**\n\n{textMessage}",
                ctx.DiscordColor)
                .ConfigureAwait(false);
        }

        // ---------------- TELEGRAM ----------------
        if (_telegram.IsConfigured(out _) &&
            (ctx.TestOnlyChannel == null || ctx.TestOnlyChannel == NotificationChannel.Telegram))
        {
            await TelegramNotifier.SendAsync(
                _cfg.Telegram,
                $"{ctx.DiscordEmoji} *{ctx.Type.ToString().ToUpper()}*\n\n{textMessage}")
                .ConfigureAwait(false);
        }

        _log.LogInformation(
            "Notification {Type} dispatched: {App} [{channel}]",
            ctx.Type,
            ctx.App.Name,
            ctx.TestOnlyChannel);
    }

    private void ValidateOnce()
    {
        if (_validated)
            return;

        _validated = true;

        ValidateNotifier(_mail);
        ValidateNotifier(_ntfy);
        ValidateNotifier(_discord);
        ValidateNotifier(_telegram);
    }

    private void ValidateNotifier<T>(NotifierBase<T> notifier)
    {
        if (!notifier.IsConfigured(out var error))
        {
            _log.LogWarning(
                "Notifier '{Name}' disabled: {Error}",
                notifier.Name,
                error);
        }
        else
        {
            _log.LogInformation(
                "Notifier '{Name}' configured.",
                notifier.Name);
        }
    }

    private static string BuildDetailsHtml(NotificationContext ctx)
    {
        var sb = new StringBuilder();

        sb.Append("<ul style=\"margin:0; padding-left:18px;\">");
        sb.Append($"<li><b>Name:</b> {Html(ctx.App.Name)}</li>");
        sb.Append($"<li><b>Exe:</b> {Html(ctx.App.ExePath)}</li>");

        if (ctx.Type == AppNotificationType.Down && ctx.Status != null)
        {
            sb.Append($"<li><b>Startversuch:</b> {(ctx.StartAttempted ? "ja" : "nein")}</li>");

            if (!string.IsNullOrWhiteSpace(ctx.Status.LastStartError))
            {
                sb.Append(
                    $"<li><b>Fehler:</b> <span style=\"color:#b91c1c;\">{Html(ctx.Status.LastStartError)}</span></li>");
            }
        }

        sb.Append("</ul>");
        return sb.ToString();
    }

    private static string BuildPlainTextMessage(
        NotificationContext ctx,
        SystemInfoSnapshot sys)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Status: {ctx.Type.ToString().ToUpper()}");
        sb.AppendLine($"App: {ctx.App.Name}");

        if (ctx.Type == AppNotificationType.Down && ctx.Status != null)
        {
            sb.AppendLine($"Exe: {ctx.Status.ExePath}");
            sb.AppendLine($"Startversuch: {(ctx.StartAttempted ? "ja" : "nein")}");

            if (!string.IsNullOrWhiteSpace(ctx.Status.LastStartError))
                sb.AppendLine($"Fehler: {ctx.Status.LastStartError}");
        }

        sb.AppendLine($"Host: {sys.MachineName}");
        sb.AppendLine($"Uptime: {sys.Uptime}");

        return sb.ToString();
    }

    private static string BuildStatusSummaryHtml(
        string status,
        string color,
        string appName)
    {
        return
            $"<div>Die Anwendung <b>{Html(appName)}</b> ist " +
            $"<span style=\"color:{color};\"><b>{status}</b></span>.</div>" +
            $"<div style=\"margin-top:6px; color:#6b7280;\">" +
            $"Zeit: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}</div>";
    }

    private static string Html(string s)
        => (s ?? "")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}


public sealed class NotificationContext
{
    public required AppNotificationType Type;
    public required WatchedApp App;
    public AppStatus? Status;
    public bool StartAttempted;

    public required string Title;
    public required string SummaryStatus;
    public required string SummaryColorHex;

    public required string NtfyTags;
    public required int NtfyPriority;

    public required string DiscordEmoji;
    public required int DiscordColor;

    public NotificationChannel? TestOnlyChannel;
}

public enum AppNotificationType
{
    Down,
    Restart,
    Up
}

public enum NotificationChannel
{
    Mail,
    Ntfy,
    Discord,
    Telegram
}
