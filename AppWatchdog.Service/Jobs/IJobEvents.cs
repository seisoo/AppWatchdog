using AppWatchdog.Shared.Jobs;

namespace AppWatchdog.Service.Jobs;

/// <summary>
/// Exposes job events for reporting.
/// </summary>
public interface IJobEvents
{
    /// <summary>
    /// Raised when a job event occurs.
    /// </summary>
    event Action<JobEvent> EventRaised;
}
