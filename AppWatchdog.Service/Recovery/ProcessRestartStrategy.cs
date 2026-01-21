using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AppWatchdog.Service.Recovery;

public sealed class ProcessRestartStrategy : IRecoveryStrategy
{
    private static readonly TimeSpan StartBackoffMin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartBackoffMax = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan StartGuardWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan EventLogWindow = TimeSpan.FromSeconds(10);

    private readonly ILogger _log;
    private readonly Func<MonitorState> _state;
    private readonly Func<int> _getMailIntervalHours;

    public ProcessRestartStrategy(
        ILogger log,
        Func<MonitorState> stateAccessor,
        Func<int> getMailIntervalHours)
    {
        _log = log;
        _state = stateAccessor;
        _getMailIntervalHours = getMailIntervalHours;
    }

    public Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct)
    {
        var st = _state();

        var sessionState = UserSessionLauncher.GetSessionState();
        bool interactive = sessionState == UserSessionState.InteractiveUserPresent;

        if (!interactive)
        {
            return Task.FromResult(new RecoveryResult
            {
                Attempted = false,
                Succeeded = false,
                Error = "Kein interaktiver Benutzer angemeldet (Start nicht möglich)."
            });
        }

        if (st.NextStartAttemptUtc > DateTimeOffset.UtcNow)
        {
            return Task.FromResult(new RecoveryResult
            {
                Attempted = false,
                Succeeded = false,
                Error = $"Restart-Backoff aktiv bis {st.NextStartAttemptUtc:HH:mm:ss}"
            });
        }

        try
        {
            st.LastStartAttemptUtc = DateTimeOffset.UtcNow;

            UserSessionLauncher.StartInActiveUserSession(app.ExePath, app.Arguments);

            bool ok = WaitForRunning(app.ExePath, StartGuardWindow);

            if (ok)
            {
                st.ConsecutiveStartFailures = 0;
                st.NextStartAttemptUtc = DateTimeOffset.MinValue;

                return Task.FromResult(new RecoveryResult
                {
                    Attempted = true,
                    Succeeded = true
                });
            }

            // early exit -> EventLog check
            var ev = FindAppErrorInEventLog(app.ExePath, st.LastStartAttemptUtc, EventLogWindow);

            st.ConsecutiveStartFailures++;
            st.NextStartAttemptUtc = DateTimeOffset.UtcNow + ComputeBackoffDelay(st.ConsecutiveStartFailures);

            return Task.FromResult(new RecoveryResult
            {
                Attempted = true,
                Succeeded = false,
                Error = ev != null
                    ? $"EventLog: {ev}"
                    : "Anwendung beendet sich direkt nach dem Start (Early-Exit)."
            });
        }
        catch (Exception ex)
        {
            st.ConsecutiveStartFailures++;
            st.NextStartAttemptUtc = DateTimeOffset.UtcNow + ComputeBackoffDelay(st.ConsecutiveStartFailures);

            FileLogStore.WriteLine("ERROR",
                    $"Start fehlgeschlagen für '{app.Name}': {ex.Message}");


            return Task.FromResult(new RecoveryResult
            {
                Attempted = true,
                Succeeded = false,
                Error = ex.Message
            });
        }
    }

    private static TimeSpan ComputeBackoffDelay(int consecutiveStartFailures)
    {
        double seconds = StartBackoffMin.TotalSeconds * Math.Pow(2, Math.Max(0, consecutiveStartFailures - 1));
        seconds = Math.Min(StartBackoffMax.TotalSeconds, seconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool WaitForRunning(string exePath, TimeSpan maxWait)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < maxWait)
        {
            if (Worker.IsRunning(exePath))
                return true;

            Thread.Sleep(200);
        }

        return Worker.IsRunning(exePath);
    }

    private static string? FindAppErrorInEventLog(string exePath, DateTimeOffset startAttemptUtc, TimeSpan window)
    {
        try
        {
            var fromUtc = startAttemptUtc.UtcDateTime;
            var toUtc = startAttemptUtc.UtcDateTime.Add(window);

            var exeName = Path.GetFileName(exePath) ?? "";
            if (string.IsNullOrWhiteSpace(exeName))
                exeName = exePath;

            using var log = new EventLog("Application");

            for (int i = log.Entries.Count - 1; i >= 0; i--)
            {
                var e = log.Entries[i];
                if (e.EntryType != EventLogEntryType.Error)
                    continue;

                var tUtc = e.TimeGenerated.ToUniversalTime();
                if (tUtc > toUtc)
                    continue;

                if (tUtc < fromUtc)
                    break;

                var src = e.Source ?? "";
                if (src.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("Application Error", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("Windows Error Reporting", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{src} (EventId {e.InstanceId})";
                }

                var msg = e.Message ?? "";
                if (msg.Contains(exeName, StringComparison.OrdinalIgnoreCase))
                    return $"{src} (EventId {e.InstanceId})";
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
