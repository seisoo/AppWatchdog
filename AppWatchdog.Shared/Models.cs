using AppWatchdog.Shared.Jobs;
using System.Text.Json.Serialization;

namespace AppWatchdog.Shared;

public sealed class WatchdogConfig
{
    public List<WatchedApp> Apps { get; set; } = new();
    public int MailIntervalHours { get; set; } = 12;
    public SmtpSettings Smtp { get; set; } = new();
    public NtfySettings Ntfy { get; set; } = new();
    public DiscordSettings Discord { get; set; } = new();
    public TelegramSettings Telegram { get; set; } = new();
    public string CultureName { get; set; } = "en-US";

    public NotificationTemplateSet NotificationTemplates { get; set; } = NotificationTemplateSet.CreateDefault();

    public List<BackupPlanConfig> Backups { get; set; } = new();
    public List<RestorePlanConfig> Restores { get; set; } = new();
}

public sealed class NotificationTemplateSet
{
    public NotificationTemplate Up { get; set; } = NotificationTemplate.CreateDefaultUp();
    public NotificationTemplate Down { get; set; } = NotificationTemplate.CreateDefaultDown();
    public NotificationTemplate Restart { get; set; } = NotificationTemplate.CreateDefaultRestart();

    public static NotificationTemplateSet CreateDefault()
        => new();
}

public sealed class NotificationTemplate
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string Color { get; set; } = "#2563eb";

    public static NotificationTemplate CreateDefaultUp()
        => new()
        {
            Title = "AppWatchdog – $jobname",
            Summary = "$summary",
            Body = "$target\n\nStatus: $status\nPing: $ping\n\nHost: $machine\nTime: $time",
            Color = "#15803d"
        };

    public static NotificationTemplate CreateDefaultDown()
        => new()
        {
            Title = "AppWatchdog – $jobname",
            Summary = "$summary",
            Body = "$target\n\nStatus: $status\nError: $error\nPing: $ping\n\nHost: $machine\nTime: $time",
            Color = "#b91c1c"
        };

    public static NotificationTemplate CreateDefaultRestart()
        => new()
        {
            Title = "AppWatchdog – $jobname",
            Summary = "$summary",
            Body = "$target\n\nStatus: $status\nError: $error\nPing: $ping\n\nHost: $machine\nTime: $time",
            Color = "#2563eb"
        };
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

    public bool IsRunning { get; set; }
    public int ConsecutiveDown { get; set; }
    public int ConsecutiveStartFailures { get; set; }

    public DateTimeOffset? LastCheckUtc { get; set; }
    public DateTimeOffset? LastStartAttemptUtc { get; set; }
    public DateTimeOffset? NextStartAttemptUtc { get; set; }

    public TimeSpan Interval { get; set; }
    public DateTimeOffset? NextRunUtc { get; set; }

    public long? PingMs { get; init; }

    public string EffectiveState { get; set; } = "";

    public int? ProgressPercent { get; set; }
    public string? StatusText { get; set; }
    public DateTimeOffset? PlannedStartUtc { get; set; }

    public IReadOnlyList<JobEvent> Events { get; init; } = Array.Empty<JobEvent>();

    // Job-spezifische Details
    public string? BackupPlanName { get; set; }
    public string? BackupSourcePath { get; set; }
    public string? BackupTargetPath { get; set; }
    public string? HealthCheckType { get; set; } // z.B. "HttpEndpoint", "TcpPort", "Executable", "WindowsService"
    public string? HealthCheckTarget { get; set; } // z.B. URL, Port, Path
    public string? RestorePlanName { get; set; }
}


public sealed class JobSnapshotsResponse
{
    public List<JobSnapshot> Jobs { get; set; } = new();
}
public enum BackupSourceType
{
    Folder = 0,
    File = 1,
    MsSql = 2
}

public enum BackupTargetType
{
    Local = 0,
    Sftp = 1
}

public enum BackupMode
{
    Full = 0
}

public sealed class BackupScheduleConfig
{
    public string TimeLocal { get; set; } = "02:00";
    public List<DayOfWeek> Days { get; set; } = new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
}

public sealed class BackupSourceConfig
{
    public BackupSourceType Type { get; set; } = BackupSourceType.Folder;

    public string Path { get; set; } = "";

    public string SqlConnectionString { get; set; } = "";
    public string SqlDatabase { get; set; } = "";
}

public sealed class BackupTargetConfig
{
    public BackupTargetType Type { get; set; } = BackupTargetType.Local;

    public string LocalDirectory { get; set; } = "";

    public string SftpHost { get; set; } = "";
    public int SftpPort { get; set; } = 22;
    public string SftpUser { get; set; } = "";
    public string SftpPassword { get; set; } = "";
    public string SftpRemoteDirectory { get; set; } = "/";

    public string? SftpHostKeyFingerprint { get; set; }
}

public sealed class BackupCryptoConfig
{
    public bool Encrypt { get; set; } = true;
    public string Password { get; set; } = "";
    public int Iterations { get; set; } = 200_000;
}

public sealed class BackupCompressionConfig
{
    public bool Compress { get; set; } = true;
    public int Level { get; set; } = 5;
}

public sealed class BackupRetentionConfig
{
    public int KeepLast { get; set; } = 14;
}

public sealed class BackupPlanConfig
{
    public bool Enabled { get; set; } = false;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public BackupScheduleConfig Schedule { get; set; } = new();
    public BackupSourceConfig Source { get; set; } = new();
    public BackupTargetConfig Target { get; set; } = new();

    public BackupCompressionConfig Compression { get; set; } = new();
    public BackupCryptoConfig Crypto { get; set; } = new();
    public BackupRetentionConfig Retention { get; set; } = new();

    public bool VerifyAfterCreate { get; set; } = true;
}

public sealed class RestorePlanConfig
{
    public bool Enabled { get; set; } = false;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public string BackupPlanId { get; set; } = "";
    public string BackupArtifactName { get; set; } = "";

    public string RestoreToDirectory { get; set; } = "";
    public bool OverwriteExisting { get; set; } = false;

    public List<string> IncludePaths { get; set; } = new();

    public bool RunOnce { get; set; } = true;

    public BackupTargetConfig Target { get; set; } = new();
    public BackupCryptoConfig Crypto { get; set; } = new();
}

public sealed class BackupListResponse
{
    public List<BackupPlanConfig> Plans { get; set; } = new();
}

public sealed class BackupTriggerRequest
{
    public string BackupPlanId { get; set; } = "";
}

public sealed class BackupArtifactListRequest
{
    public string BackupPlanId { get; set; } = "";
}

public sealed class BackupArtifactListResponse
{
    public List<string> Artifacts { get; set; } = new();
}

public sealed class BackupManifestRequest
{
    public string BackupPlanId { get; set; } = "";
    public string ArtifactName { get; set; } = "";
}

public sealed class RestoreTriggerRequest
{
    public string BackupPlanId { get; set; } = "";
    public string ArtifactName { get; set; } = "";
    public string RestoreToDirectory { get; set; } = "";
    public bool OverwriteExisting { get; set; }
    public List<string> IncludePaths { get; set; } = new();
}