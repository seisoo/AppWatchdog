namespace AppWatchdog.Shared;

public sealed class NotificationContext
{
    public required AppNotificationType Type { get; init; }
    public required WatchedApp App { get; init; }
    public AppStatus? Status { get; init; }
    public bool StartAttempted { get; init; }
    public NotificationChannel? TestOnlyChannel { get; init; }
}

public sealed class BackupNotificationContext
{
    public required BackupNotificationType Type { get; init; }
    public required BackupPlanConfig Plan { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset? FinishedUtc { get; init; }
    public long? SizeBytes { get; init; }
    public string? Error { get; init; }
    public NotificationChannel? TestOnlyChannel { get; init; }
}

public enum AppNotificationType
{
    Down,
    Restart,
    Up
}

public enum BackupNotificationType
{
    Started,
    Completed,
    Failed
}

public enum NotificationChannel
{
    Mail,
    Ntfy,
    Discord,
    Telegram
}
