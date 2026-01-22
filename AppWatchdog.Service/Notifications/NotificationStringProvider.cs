using System.Globalization;

namespace AppWatchdog.Service.Notifications;

internal sealed class NotificationStringProvider
{
    private readonly CultureInfo _culture;

    public NotificationStringProvider(string? cultureName)
    {
        try
        {
            _culture = string.IsNullOrWhiteSpace(cultureName)
                ? CultureInfo.InvariantCulture
                : new CultureInfo(cultureName);
        }
        catch
        {
            _culture = CultureInfo.InvariantCulture;
        }
    }

    private string Get(string key)
        => NotificationStrings.ResourceManager.GetString(key, _culture)
           ?? $"!!{key}!!";

    public string SummaryUp => Get(nameof(NotificationStrings.Summary_Up));
    public string SummaryDown => Get(nameof(NotificationStrings.Summary_Down));
    public string SummaryRestart => Get(nameof(NotificationStrings.Summary_Restart));

    public string StatusReachable => Get(nameof(NotificationStrings.Status_Reachable));
    public string StatusUnreachable => Get(nameof(NotificationStrings.Status_Unreachable));
    public string RestartAttempted => Get(nameof(NotificationStrings.Restart_Attempted));
    public string ErrorLabel => Get(nameof(NotificationStrings.Error_Label));

    public string TargetExecutable(string path)
        => string.Format(Get(nameof(NotificationStrings.Target_Executable)), path);

    public string TargetService(string name)
        => string.Format(Get(nameof(NotificationStrings.Target_Service)), name);

    public string TargetHttp(string url)
        => string.Format(Get(nameof(NotificationStrings.Target_Http)), url);

    public string TargetTcp(string host, int port)
        => string.Format(Get(nameof(NotificationStrings.Target_Tcp)), host, port);

    public string Footer(string host, string time)
        => string.Format(Get(nameof(NotificationStrings.Footer)), host, time);
}
