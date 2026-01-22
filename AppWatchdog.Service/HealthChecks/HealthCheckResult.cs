public sealed class HealthCheckResult
{
    public bool IsHealthy { get; }
    public string? Error { get; }
    public long? DurationMs { get; }

    private HealthCheckResult(bool healthy, string? error, long? durationMs)
    {
        IsHealthy = healthy;
        Error = error;
        DurationMs = durationMs;
    }

    public static HealthCheckResult Healthy(long? durationMs = null)
        => new(true, null, durationMs);

    public static HealthCheckResult Down(string error, long? durationMs = null)
        => new(false, error, durationMs);
}
