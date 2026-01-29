using System.Net;
using System.Net.Mail;
using System.Text;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Notifiers;

/// <summary>
/// Sends notifications via SMTP.
/// </summary>
public sealed class MailNotifier : NotifierBase<SmtpSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailNotifier"/> class.
    /// </summary>
    /// <param name="settings">SMTP settings.</param>
    public MailNotifier(SmtpSettings settings)
        : base(settings)
    {
    }

    /// <summary>
    /// Gets the notifier name.
    /// </summary>
    public override string Name => "smtp";

    /// <summary>
    /// Validates that SMTP settings are configured.
    /// </summary>
    /// <param name="error">Error message if invalid.</param>
    /// <returns><c>true</c> when configured.</returns>
    public override bool IsConfigured(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Settings.Server))
        {
            error = "SMTP Server fehlt.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.From))
        {
            error = "SMTP From-Adresse fehlt.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Settings.To))
        {
            error = "SMTP To-Adresse fehlt.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Sends an HTML email using the given SMTP settings.
    /// </summary>
    /// <param name="smtp">SMTP settings.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="htmlBody">HTML body.</param>
    public static void SendHtml(SmtpSettings smtp, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(smtp.Server) ||
            string.IsNullOrWhiteSpace(smtp.From) ||
            string.IsNullOrWhiteSpace(smtp.To))
        {
            return;
        }

        using var client = new SmtpClient(smtp.Server, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(smtp.User))
            client.Credentials = new NetworkCredential(smtp.User, smtp.Password);

        using var msg = new MailMessage
        {
            From = new MailAddress(smtp.From),
            Subject = subject,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = htmlBody
        };

        msg.To.Add(smtp.To);

        client.Send(msg);
    }
}
