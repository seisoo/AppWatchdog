using System.Globalization;

namespace AppWatchdog.Service.Notifications;

/// <summary>
/// Provides localized notification strings for the service.
/// </summary>
internal sealed class NotificationStringProvider
{
    private readonly CultureInfo _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationStringProvider"/> class.
    /// </summary>
    /// <param name="cultureName">Culture name to use for localization.</param>
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

    /// <summary>
    /// Gets a localized string by resource key.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <returns>The localized string.</returns>
    private string Get(string key)
        => NotificationStrings.ResourceManager.GetString(key, _culture)
           ?? $"!!{key}!!";

    /// <summary>
    /// Gets the "up" summary text.
    /// </summary>
    public string SummaryUp => Get(nameof(NotificationStrings.Summary_Up));

    /// <summary>
    /// Gets the "down" summary text.
    /// </summary>
    public string SummaryDown => Get(nameof(NotificationStrings.Summary_Down));

    /// <summary>
    /// Gets the "restart" summary text.
    /// </summary>
    public string SummaryRestart => Get(nameof(NotificationStrings.Summary_Restart));

    /// <summary>
    /// Gets the reachable status text.
    /// </summary>
    public string StatusReachable => Get(nameof(NotificationStrings.Status_Reachable));

    /// <summary>
    /// Gets the unreachable status text.
    /// </summary>
    public string StatusUnreachable => Get(nameof(NotificationStrings.Status_Unreachable));

    /// <summary>
    /// Gets the restart attempted text.
    /// </summary>
    public string RestartAttempted => Get(nameof(NotificationStrings.Restart_Attempted));

    /// <summary>
    /// Gets the error label text.
    /// </summary>
    public string ErrorLabel => Get(nameof(NotificationStrings.Error_Label));

    /// <summary>
    /// Formats the executable target label.
    /// </summary>
    /// <param name="path">Executable path.</param>
    /// <returns>The formatted label.</returns>
    public string TargetExecutable(string path)
        => string.Format(Get(nameof(NotificationStrings.Target_Executable)), path);

    /// <summary>
    /// Formats the service target label.
    /// </summary>
    /// <param name="name">Service name.</param>
    /// <returns>The formatted label.</returns>
    public string TargetService(string name)
        => string.Format(Get(nameof(NotificationStrings.Target_Service)), name);

    /// <summary>
    /// Formats the HTTP target label.
    /// </summary>
    /// <param name="url">Endpoint URL.</param>
    /// <returns>The formatted label.</returns>
    public string TargetHttp(string url)
        => string.Format(Get(nameof(NotificationStrings.Target_Http)), url);

    /// <summary>
    /// Formats the TCP target label.
    /// </summary>
    /// <param name="host">Host name.</param>
    /// <param name="port">Port number.</param>
    /// <returns>The formatted label.</returns>
    public string TargetTcp(string host, int port)
        => string.Format(Get(nameof(NotificationStrings.Target_Tcp)), host, port);

    /// <summary>
    /// Formats the notification footer.
    /// </summary>
    /// <param name="host">Host name.</param>
    /// <param name="time">Timestamp.</param>
    /// <returns>The formatted footer.</returns>
    public string Footer(string host, string time)
        => string.Format(Get(nameof(NotificationStrings.Footer)), host, time);
}
