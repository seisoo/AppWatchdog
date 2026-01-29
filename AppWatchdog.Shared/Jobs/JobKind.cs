namespace AppWatchdog.Shared.Jobs;

public enum JobKind
{
    HealthMonitor,
    KumaPing,
    Snapshot,
    Unknown,
    Backup = 100,
    Restore = 101
}
