namespace AppWatchdog.Shared.Jobs;

public sealed class JobEvent
{
    public required string JobId { get; init; }
    public required JobEventType Type { get; init; }
    public int? Progress { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public enum JobEventType
{
    Planned,
    Started,
    Progress,
    StatusChanged,
    Completed,
    Failed
}
