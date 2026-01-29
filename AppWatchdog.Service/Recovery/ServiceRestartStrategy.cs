using AppWatchdog.Shared;
using System.ServiceProcess;

namespace AppWatchdog.Service.Recovery;

/// <summary>
/// Recovery strategy that restarts a Windows service.
/// </summary>
public sealed class ServiceRestartStrategy : IRecoveryStrategy
{
    /// <summary>
    /// Attempts to restart the target Windows service.
    /// </summary>
    /// <param name="app">App to recover.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recovery result.</returns>
    public Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(app.ServiceName))
        {
            return Task.FromResult(new RecoveryResult
            {
                Attempted = false,
                Succeeded = false,
                Error = "ServiceName is empty."
            });
        }

        try
        {
            using var sc = new ServiceController(app.ServiceName);

            if (sc.Status == ServiceControllerStatus.Running)
            {
                return Task.FromResult(new RecoveryResult
                {
                    Attempted = false,
                    Succeeded = true
                });
            }

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));

            return Task.FromResult(sc.Status == ServiceControllerStatus.Running
                ? new RecoveryResult { Attempted = true, Succeeded = true }
                : new RecoveryResult { Attempted = true, Succeeded = false, Error = $"Service did not start. State={sc.Status}" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RecoveryResult
            {
                Attempted = true,
                Succeeded = false,
                Error = ex.Message
            });
        }
    }
}
