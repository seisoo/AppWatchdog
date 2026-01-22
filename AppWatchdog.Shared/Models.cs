using AppWatchdog.Shared.Jobs;
using System.Text.Json.Serialization;

namespace AppWatchdog.Shared;

public sealed class WatchdogConfig
{
    public List<WatchedApp> Apps { get; set; } = new();
    public int CheckIntervalSeconds { get; set; } = 5;
    public int MailIntervalHours { get; set; } = 12;
    public SmtpSettings Smtp { get; set; } = new();
    public NtfySettings Ntfy { get; set; } = new();
    public DiscordSettings Discord { get; set; } = new();
    public TelegramSettings Telegram { get; set; } = new();
    public string CultureName { get; set; } = "en-US";
}

public sealed class SmtpSettings
{
    public string Server { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;

    public string User { get; set; } = "";
    public string Password { get; set; } = "";

    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class NtfySettings
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "https://ntfy.sh";
    public string Topic { get; set; } = "";
    public string Token { get; set; } = "";
    public int Priority { get; set; } = 3;
}

public sealed class TelegramSettings
{
    public bool Enabled { get; set; } = false;
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public bool UseMarkdown { get; set; } = true;
}

public sealed class DiscordSettings
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = "";
    public string Username { get; set; } = "AppWatchdog";
    public string AvatarUrl { get; set; } = "";
}



public enum UserSessionState
{
    NoInteractiveUser = 0,
    InteractiveUserPresent = 1
}

public sealed class AppStatus
{
    public string Name { get; set; } = "";
    public string ExePath { get; set; } = "";
    public bool Enabled { get; set; }
    public bool IsRunning { get; set; }
    public string? LastStartError { get; set; }
    public long? PingMs { get; set; }
}

public sealed class ServiceSnapshot
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public UserSessionState SessionState { get; set; }
    public List<AppStatus> Apps { get; set; } = new();
    public SystemInfo SystemInfo { get; set; } = new();

    public int PipeProtocolVersion { get; set; } = 0;
}

public sealed class LogDaysResponse
{
    public List<string> Days { get; set; } = new();
}

public sealed class LogDayResponse
{
    public string Day { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class LogPathResponse
{
    public string Path { get; set; } = "";
}

public sealed class LogDayRequest
{
    public string Day { get; set; } = ""; 
}

public sealed class SystemInfo
{
    public string MachineName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string DotNetVersion { get; set; } = "";
    public TimeSpan Uptime { get; set; }

    public int ProcessorCount { get; set; }
    public long TotalMemoryMb { get; set; }
    public long AvailableMemoryMb { get; set; }
    public int PipeProtocol { get; set; } = 0;
}

public sealed class UptimeKumaSettings
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "";

    public string PushToken { get; set; } = "";

    public int IntervalSeconds { get; set; } = 60;

    public string? MonitorName { get; set; }
}

public sealed class JobSnapshot
{
    public JobKind Kind { get; init; }
    public string JobId { get; set; } = "";
    public string JobType { get; set; } = "";

    public string AppName { get; set; } = "";
    public string ExePath { get; set; } = "";

    public bool Enabled { get; set; }

    // Health
    public bool IsRunning { get; set; }
    public int ConsecutiveDown { get; set; }
    public int ConsecutiveStartFailures { get; set; }

    // Timing
    public DateTimeOffset? LastCheckUtc { get; set; }
    public DateTimeOffset? LastStartAttemptUtc { get; set; }
    public DateTimeOffset? NextStartAttemptUtc { get; set; }

    // Notifications
    public bool DownNotified { get; set; }
    public bool RestartNotified { get; set; }
    public bool RecoveryFailedNotified { get; set; }

    // Scheduler
    public TimeSpan Interval { get; set; }
    public DateTimeOffset? NextRunUtc { get; set; }

    public long? PingMs { get; init; }

    // Derived state (für UI!)
    public string EffectiveState { get; set; } = ""; // UP / DOWN / RECOVERY_FAILED
}
