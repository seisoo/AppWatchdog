using AppWatchdog.Shared;

namespace AppWatchdog.Service.Recovery;

/// <summary>
/// Recovery strategy that performs no recovery action.
/// </summary>
public sealed class NoRecoveryStrategy : IRecoveryStrategy
{
    /// <summary>
    /// Returns a recovery result indicating no attempt was made.
    /// </summary>
    /// <param name="app">App to recover.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recovery result.</returns>
    public Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct)
        => Task.FromResult(new RecoveryResult
        {
            Attempted = false,
            Succeeded = false,
            Error = null
        });
}
