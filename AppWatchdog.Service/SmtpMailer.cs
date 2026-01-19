using System.Net;
using System.Net.Mail;
using System.Text;
using AppWatchdog.Shared;

namespace AppWatchdog.Service;

public static class SmtpMailer
{
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

    public static string WrapHtmlTemplate(string title, string summaryHtml, string detailsHtml, string systemInfoHtml)
    {
        return $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
</head>
<body style=""margin:0; padding:0; background:#f3f4f6;"">
  <div style=""padding:24px 12px;"">
    <div style=""max-width:820px; margin:0 auto; background:#ffffff; border:1px solid #e5e7eb; border-radius:10px; overflow:hidden;"">
      <div style=""background:#111827; color:#ffffff; padding:16px 20px;"">
        <div style=""font-size:18px; font-weight:700;"">AppWatchdog</div>
        <div style=""font-size:13px; opacity:0.9;"">{Html(title)}</div>
      </div>

      <div style=""padding:18px 20px;"">
        <div style=""font-size:14px; margin-bottom:12px;"">{summaryHtml}</div>

        <div style=""margin:16px 0; padding:14px 14px; border:1px solid #e5e7eb; border-radius:8px; background:#f9fafb;"">
          <div style=""font-size:13px; font-weight:700; margin-bottom:8px;"">Details</div>
          {detailsHtml}
        </div>

        <div style=""margin:16px 0; padding:14px 14px; border:1px solid #e5e7eb; border-radius:8px; background:#ffffff;"">
          <div style=""font-size:13px; font-weight:700; margin-bottom:8px;"">Systeminformationen</div>
          {systemInfoHtml}
        </div>

        <div style=""font-size:12px; color:#6b7280; margin-top:18px;"">
          Hinweis: Diese Nachricht wurde automatisch generiert.
        </div>
      </div>
    </div>

    <div style=""max-width:820px; margin:10px auto 0 auto; text-align:center; font-size:12px; color:#6b7280;"">
      AppWatchdog &nbsp;•&nbsp; {Html(Environment.MachineName)} &nbsp;•&nbsp; {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}
    </div>
  </div>
</body>
</html>";
    }

    private static string Html(string s)
        => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
