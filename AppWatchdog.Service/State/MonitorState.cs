public sealed class MonitorState
{
    public bool WasRunning;

    public int ConsecutiveDown;
    public int ConsecutiveStartFailures;

    public DateTimeOffset LastDownNotify = DateTimeOffset.MinValue;
    public DateTimeOffset LastStartAttemptUtc = DateTimeOffset.MinValue;
    public DateTimeOffset NextStartAttemptUtc = DateTimeOffset.MinValue;

    public bool RestartNotified;
    public bool RecoveryFailedNotified;

    public DateTimeOffset LastCheckUtc;
}
