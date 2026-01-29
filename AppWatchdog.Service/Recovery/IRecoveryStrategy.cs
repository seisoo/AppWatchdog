using AppWatchdog.Shared;

namespace AppWatchdog.Service.Recovery;

/// <summary>
/// Defines a recovery strategy for a monitored app.
/// </summary>
public interface IRecoveryStrategy
{
    /// <summary>
    /// Attempts to recover a failing app.
    /// </summary>
    /// <param name="app">App to recover.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recovery result.</returns>
    Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct);
}

/// <summary>
/// Represents the outcome of a recovery attempt.
/// </summary>
public sealed class RecoveryResult
{
    /// <summary>
    /// Gets whether a recovery attempt was made.
    /// </summary>
    public bool Attempted { get; init; }

    /// <summary>
    /// Gets whether the recovery succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the error message when recovery fails.
    /// </summary>
    public string? Error { get; init; }
}
