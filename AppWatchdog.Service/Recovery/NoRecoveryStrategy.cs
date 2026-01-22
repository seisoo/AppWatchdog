using AppWatchdog.Shared;

namespace AppWatchdog.Service.Recovery;

public sealed class NoRecoveryStrategy : IRecoveryStrategy
{
    public Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct)
        => Task.FromResult(new RecoveryResult
        {
            Attempted = false,
            Succeeded = false,
            Error = null
        });
}
