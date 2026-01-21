using AppWatchdog.Service.HealthChecks;
using AppWatchdog.Service.Recovery;
using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using static AppWatchdog.Service.Jobs.JobScheduler;

namespace AppWatchdog.Service.Jobs;

public sealed class HealthMonitorJob : IJob
{
    private const int DownConfirmChecks = 2;

    private readonly ILogger _log;
    private readonly Func<WatchdogConfig> _getConfig;

    private readonly WatchedApp _app;
    private readonly IHealthCheck _check;
    private readonly IRecoveryStrategy _recovery;

    private readonly MonitorState _state = new();
    private readonly NotificationDispatcher _dispatcher;

    public HealthMonitorJob(
        ILogger log,
        Func<WatchdogConfig> getConfig,
        WatchedApp app,
        IHealthCheck check,
        IRecoveryStrategy recovery,
        MonitorState state,
        NotificationDispatcher dispatcher)
    {
        _log = log;
        _getConfig = getConfig;
        _app = app;
        _check = check;
        _recovery = recovery;
        _dispatcher = dispatcher;
        _state = state;
    }

    public string Id => $"app:{_app.ExePath}";
    public TimeSpan Interval =>
    TimeSpan.FromSeconds(_getConfig().CheckIntervalSeconds);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _state.LastCheckUtc = DateTimeOffset.UtcNow;

        if (!_app.Enabled || string.IsNullOrWhiteSpace(_app.ExePath))
            return;

        var cfg = _getConfig();

        // aktueller Zustand
        var hc = await _check.CheckAsync(ct);
        bool isRunning = hc.IsHealthy;

        if (isRunning)
        {
            _state.ConsecutiveDown = 0;
            _state.ConsecutiveStartFailures = 0;
            _state.NextStartAttemptUtc = DateTimeOffset.MinValue;

            if (!_state.WasRunning)
            {
                SendUp();
                _state.RestartNotified = false;
            }

            _state.WasRunning = true;
            _state.RecoveryFailedNotified = false;
            return;
        }

        // DOWN
        _state.ConsecutiveDown++;

        if (_state.ConsecutiveDown < DownConfirmChecks)
            return;

        bool justWentDown = !_state.WasRunning && _state.ConsecutiveDown == DownConfirmChecks;
        var recovery = await _recovery.TryRecoverAsync(_app, ct);
        bool nowRunning = Worker.IsRunning(_app.ExePath);

        if (!nowRunning)
        {
            _state.RestartNotified = false;

            // 🔔 NEU: Recovery-Fehler explizit melden (1× oder rate-limited)
            if (recovery.Attempted && !recovery.Succeeded && !_state.RecoveryFailedNotified)
            {
                SendRecoveryFailed(recovery.Error);
                _state.RecoveryFailedNotified = true;
            }

            var now = DateTimeOffset.Now;
            var downMailInterval = TimeSpan.FromHours(Math.Max(1, cfg.MailIntervalHours));

            bool firstConfirmedDown = _state.LastDownNotify == DateTimeOffset.MinValue;
            bool mayNotifyDown =
                firstConfirmedDown ||
                justWentDown ||
                (now - _state.LastDownNotify) >= downMailInterval;

            if (mayNotifyDown)
            {
                SendDown(
                    startAttempted: recovery.Attempted,
                    lastError: recovery.Error);

                _state.LastDownNotify = now;
            }

            _state.WasRunning = false;
            return;
        }


        // wieder UP nach erfolgreicher Recovery
        if (_state.WasRunning && !_state.RestartNotified)
        {
            SendRestart();
            _state.RestartNotified = true;
        }

        if (!_state.WasRunning)
            SendUp();

        _state.WasRunning = true;
    }

    private void SendDown(bool startAttempted, string? lastError)
    {
        var st = new AppStatus
        {
            Name = _app.Name,
            ExePath = _app.ExePath,
            Enabled = _app.Enabled,
            IsRunning = false,
            LastStartError = lastError
        };

        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Down,
            App = _app,
            Status = st,
            StartAttempted = startAttempted,

            Title = $"AppWatchdog – {_app.Name} DOWN",
            SummaryStatus = "NICHT AKTIV",
            SummaryColorHex = "#b91c1c",

            NtfyTags = "warning,server",
            NtfyPriority = 4,

            DiscordEmoji = "🛑",
            DiscordColor = 0xB91C1C
        });
    }

    private void SendRecoveryFailed(string? error)
    {

        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Down, // oder eigener Typ
            App = _app,

            Title = $"AppWatchdog – {_app.Name} START FEHLGESCHLAGEN",
            SummaryStatus = "START FEHLGESCHLAGEN",
            SummaryColorHex = "#7c2d12",

            NtfyTags = "error,server",
            NtfyPriority = 5,

            DiscordEmoji = "❌",
            DiscordColor = 0x7C2D12
        });
    }


    private void SendRestart()
    {
        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Restart,
            App = _app,

            Title = $"AppWatchdog – {_app.Name} RESTART",
            SummaryStatus = "NEU GESTARTET",
            SummaryColorHex = "#2563eb",

            NtfyTags = "restart,server",
            NtfyPriority = 3,

            DiscordEmoji = "🔄",
            DiscordColor = 0x2563EB
        });
    }

    private void SendUp()
    {
        _dispatcher.Dispatch(new NotificationContext
        {
            Type = AppNotificationType.Up,
            App = _app,

            Title = $"AppWatchdog – {_app.Name} UP",
            SummaryStatus = "WIEDER AKTIV",
            SummaryColorHex = "#15803d",

            NtfyTags = "white_check_mark,server",
            NtfyPriority = 2,

            DiscordEmoji = "✅",
            DiscordColor = 0x15803D
        });
    }

    public JobSnapshot CreateSnapshot(JobEntry entry)
    {
        var st = _state;

        string effectiveState =
            st.RecoveryFailedNotified ? "RECOVERY_FAILED" :
            st.WasRunning ? "UP" :
            st.ConsecutiveDown >= DownConfirmChecks ? "DOWN" :
            "UNKNOWN";

        return new JobSnapshot
        {
            JobId = Id,
            Kind = JobKind.HealthMonitor,
            JobType = nameof(HealthMonitorJob),

            AppName = _app.Name,
            ExePath = _app.ExePath ?? "",
            Enabled = _app.Enabled,

            IsRunning = st.WasRunning,
            ConsecutiveDown = st.ConsecutiveDown,
            ConsecutiveStartFailures = st.ConsecutiveStartFailures,

            LastCheckUtc = st.LastCheckUtc,
            LastStartAttemptUtc = st.LastStartAttemptUtc,
            NextStartAttemptUtc = st.NextStartAttemptUtc,

            DownNotified = st.LastDownNotify != DateTimeOffset.MinValue,
            RestartNotified = st.RestartNotified,
            RecoveryFailedNotified = st.RecoveryFailedNotified,

            Interval = entry.Job.Interval,
            NextRunUtc = entry.NextRunUtc,

            EffectiveState = effectiveState
        };
    }


    public void Dispose() { }
}
