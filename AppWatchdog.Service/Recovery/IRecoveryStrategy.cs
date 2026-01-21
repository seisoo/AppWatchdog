using AppWatchdog.Shared;

namespace AppWatchdog.Service.Recovery;

public interface IRecoveryStrategy
{
    Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct);
}

public sealed class RecoveryResult
{
    public bool Attempted { get; init; }
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
}
