namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Provides status details for long-running jobs.
/// </summary>
public interface IJobStatusProvider
{
    /// <summary>
    /// Gets the current progress percent, if available.
    /// </summary>
    int? ProgressPercent { get; }

    /// <summary>
    /// Gets the current status text.
    /// </summary>
    string StatusText { get; }

    /// <summary>
    /// Gets the planned start time in UTC.
    /// </summary>
    DateTimeOffset PlannedStartUtc { get; }
}
