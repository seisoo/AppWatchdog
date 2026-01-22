namespace AppWatchdog.Shared;

public sealed class NotificationContext
{
    public required AppNotificationType Type { get; init; }
    public required WatchedApp App { get; init; }
    public AppStatus? Status { get; init; }
    public bool StartAttempted { get; init; }
    public NotificationChannel? TestOnlyChannel { get; init; }
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
