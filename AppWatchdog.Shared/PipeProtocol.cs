using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppWatchdog.Shared;

public static class PipeProtocol
{
    public const int ProtocolVersion = 24;

    public const string PipeName = "AppWatchdogPipe";

    public const string CmdGetConfig = "GetConfig";
    public const string CmdSaveConfig = "SaveConfig";
    public const string CmdGetStatus = "GetStatus";
    public const string CmdTriggerCheck = "TriggerCheck";
    public const string CmdPing = "Ping";
    public const string CmdHealthPing = "HealthPing";

    public const string CmdListLogDays = "ListLogDays";
    public const string CmdGetLogDay = "GetLogDay";

    public const string CmdTestSmtp = "TestSmtp";
    public const string CmdTestNtfy = "TestNtfy";
    public const string CmdTestDiscord = "TestDiscord";
    public const string CmdTestTelegram = "TestTelegram";

    public const string CmdGetLogPath = "GetLogPath";

    public const string CmdGetJobs = "GetJobs";
    public const string CmdRebuildJobs = "RebuildJobs";

    public const string CmdListBackups = "ListBackups";
    public const string CmdTriggerBackup = "TriggerBackup";
    public const string CmdPurgeBackupArtifacts = "PurgeBackupArtifacts";

    public const string CmdListBackupArtifacts = "ListBackupArtifacts";
    public const string CmdGetBackupManifest = "GetBackupManifest";

    public const string CmdTriggerRestore = "TriggerRestore";

    public const string CmdExportConfig = "ExportConfig";
    public const string CmdImportConfig = "ImportConfig";



    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize<T>(T obj)
        => JsonSerializer.Serialize(obj, JsonOptions);

    public static T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions);

    public sealed class Request
    {
        public int Version { get; set; } = ProtocolVersion;
        public string Command { get; set; } = "";
        public string? PayloadJson { get; set; }
    }

    public sealed class Response
    {
        public int Version { get; set; } = ProtocolVersion;
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? PayloadJson { get; set; }
    }
}
