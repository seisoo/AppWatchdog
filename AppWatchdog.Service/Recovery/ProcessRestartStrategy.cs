using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using System.Diagnostics;

namespace AppWatchdog.Service.Recovery;

public sealed class ProcessRestartStrategy : IRecoveryStrategy
{
    private static readonly TimeSpan StartBackoffMin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartBackoffMax = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan StartGuardWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan EventLogWindow = TimeSpan.FromSeconds(10);

    private int _consecutiveFailures;
    private DateTimeOffset _nextStartAttemptUtc = DateTimeOffset.MinValue;

    public Task<RecoveryResult> TryRecoverAsync(WatchedApp app, CancellationToken ct)
    {
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

        if (_nextStartAttemptUtc > DateTimeOffset.UtcNow)
        {
            return Task.FromResult(new RecoveryResult
            {
                Attempted = false,
                Succeeded = false,
                Error = $"Restart-Backoff aktiv bis {_nextStartAttemptUtc:HH:mm:ss}"
            });
        }

        try
        {
            FileLogStore.WriteLine(
                "INFO",
                $"Versuche Neustart von '{app.Name}' ({app.ExePath})");

            UserSessionLauncher.StartInActiveUserSession(
                app.ExePath,
                app.Arguments);

            bool running = WaitForRunning(app.ExePath, StartGuardWindow);

            if (running)
            {
                _consecutiveFailures = 0;
                _nextStartAttemptUtc = DateTimeOffset.MinValue;

                FileLogStore.WriteLine(
                    "INFO",
                    $"Neustart erfolgreich für '{app.Name}'");

                return Task.FromResult(new RecoveryResult
                {
                    Attempted = true,
                    Succeeded = true
                });
            }

            var ev = FindAppErrorInEventLog(
                app.ExePath,
                DateTimeOffset.UtcNow,
                EventLogWindow);

            _consecutiveFailures++;
            _nextStartAttemptUtc =
                DateTimeOffset.UtcNow + ComputeBackoffDelay(_consecutiveFailures);

            string err = ev != null
                ? $"EventLog: {ev}"
                : "Anwendung beendet sich direkt nach dem Start (Early-Exit).";

            FileLogStore.WriteLine(
                "WARN",
                $"Neustart fehlgeschlagen für '{app.Name}': {err}");

            return Task.FromResult(new RecoveryResult
            {
                Attempted = true,
                Succeeded = false,
                Error = err
            });
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _nextStartAttemptUtc =
                DateTimeOffset.UtcNow + ComputeBackoffDelay(_consecutiveFailures);

            FileLogStore.WriteLine(
                "ERROR",
                $"Start fehlgeschlagen für '{app.Name}': {ex.Message}");

            return Task.FromResult(new RecoveryResult
            {
                Attempted = true,
                Succeeded = false,
                Error = ex.Message
            });
        }
    }

    private static TimeSpan ComputeBackoffDelay(int failures)
    {
        double seconds =
            StartBackoffMin.TotalSeconds *
            Math.Pow(2, Math.Max(0, failures - 1));

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

    private static string? FindAppErrorInEventLog(
        string exePath,
        DateTimeOffset startUtc,
        TimeSpan window)
    {
        try
        {
            var fromUtc = startUtc.UtcDateTime;
            var toUtc = fromUtc.Add(window);

            var exeName = Path.GetFileName(exePath) ?? exePath;

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

                if ((e.Message ?? "")
                    .Contains(exeName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{src} (EventId {e.InstanceId})";
                }
            }
        }
        catch
        {
            // ignore EventLog access issues
        }

        return null;
    }
}
