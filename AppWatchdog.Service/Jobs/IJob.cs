namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Defines a scheduled job executed by the service scheduler.
/// </summary>
public interface IJob : IDisposable
{
    /// <summary>
    /// Gets the unique job identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the execution interval for the job.
    /// </summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Executes the job logic.
    /// </summary>
    /// <param name="ct">Token used to cancel execution.</param>
    /// <returns>A task representing the execution.</returns>
    Task ExecuteAsync(CancellationToken ct);
}
