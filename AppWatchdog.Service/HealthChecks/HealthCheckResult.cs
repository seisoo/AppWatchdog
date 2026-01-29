/// <summary>
/// Represents the result of a health check.
/// </summary>
public sealed class HealthCheckResult
{
    /// <summary>
    /// Gets a value indicating whether the check was healthy.
    /// </summary>
    public bool IsHealthy { get; }

    /// <summary>
    /// Gets an error message when the check failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the duration of the health check in milliseconds.
    /// </summary>
    public long? DurationMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckResult"/> class.
    /// </summary>
    /// <param name="healthy">Whether the check was healthy.</param>
    /// <param name="error">Optional error message.</param>
    /// <param name="durationMs">Optional duration in milliseconds.</param>
    private HealthCheckResult(bool healthy, string? error, long? durationMs)
    {
        IsHealthy = healthy;
        Error = error;
        DurationMs = durationMs;
    }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    /// <param name="durationMs">Optional duration in milliseconds.</param>
    /// <returns>A healthy result.</returns>
    public static HealthCheckResult Healthy(long? durationMs = null)
        => new(true, null, durationMs);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">Error message.</param>
    /// <param name="durationMs">Optional duration in milliseconds.</param>
    /// <returns>A failed result.</returns>
    public static HealthCheckResult Down(string error, long? durationMs = null)
        => new(false, error, durationMs);
}
