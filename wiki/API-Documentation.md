# API Documentation

This document describes the IPC (Inter-Process Communication) protocol used by AppWatchdog.

## Overview

AppWatchdog uses Windows Named Pipes for communication between the UI and Service. The protocol is binary-based with JSON serialization.

## Named Pipe

**Pipe Name**: `AppWatchdog`

**Full Path**: `\\.\pipe\AppWatchdog`

**Access**: Local machine only

## Protocol Version

Current protocol version: Defined in `PipeProtocol.cs`

Both client and server must use compatible protocol versions. A `PipeProtocolMismatchException` is thrown if versions don't match.

## Communication Pattern

**Model**: Request/Response

**Flow**:
1. Client connects to Named Pipe
2. Client sends request message (JSON)
3. Server processes request
4. Server sends response message (JSON)
5. Client disconnects

## Message Format

All messages are JSON-serialized .NET objects.

### Common Structure
```csharp
// Request sent by client
public class SomeRequest
{
    public string Parameter { get; set; }
}

// Response sent by server
public class SomeResponse
{
    public string Result { get; set; }
}
```

## API Operations

### 1. Get Configuration

Retrieve the current watchdog configuration.

**Request**: `GetConfigRequest`
```csharp
public class GetConfigRequest
{
    // Empty - no parameters
}
```

**Response**: `WatchdogConfig`
```csharp
public class WatchdogConfig
{
    public List<WatchedApp> Apps { get; set; }
    public int MailIntervalHours { get; set; }
    public SmtpSettings Smtp { get; set; }
    public NtfySettings Ntfy { get; set; }
    public DiscordSettings Discord { get; set; }
    public TelegramSettings Telegram { get; set; }
    public string CultureName { get; set; }
    public NotificationTemplateSet NotificationTemplates { get; set; }
    public List<BackupPlanConfig> Backups { get; set; }
    public List<RestorePlanConfig> Restores { get; set; }
}
```

### 2. Update Configuration

Save a new configuration to the service.

**Request**: `UpdateConfigRequest`
```csharp
public class UpdateConfigRequest
{
    public WatchdogConfig Config { get; set; }
}
```

**Response**: `UpdateConfigResponse`
```csharp
public class UpdateConfigResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

**Behavior**:
- Validates configuration
- Encrypts sensitive values (passwords, tokens)
- Saves to `config.json`
- Reloads configuration
- Updates job scheduler

### 3. Get Service Snapshot

Get current status of all monitored applications and system info.

**Request**: `GetSnapshotRequest`
```csharp
public class GetSnapshotRequest
{
    // Empty - no parameters
}
```

**Response**: `ServiceSnapshot`
```csharp
public class ServiceSnapshot
{
    public DateTimeOffset Timestamp { get; set; }
    public UserSessionState SessionState { get; set; }
    public List<AppStatus> Apps { get; set; }
    public SystemInfo SystemInfo { get; set; }
    public int PipeProtocolVersion { get; set; }
}
```

**AppStatus**:
```csharp
public class AppStatus
{
    public string Name { get; set; }
    public string ExePath { get; set; }
    public bool Enabled { get; set; }
    public bool IsRunning { get; set; }
    public string? LastStartError { get; set; }
    public long? PingMs { get; set; }
}
```

**SystemInfo**:
```csharp
public class SystemInfo
{
    public string MachineName { get; set; }
    public string UserName { get; set; }
    public string OsVersion { get; set; }
    public string DotNetVersion { get; set; }
    public TimeSpan Uptime { get; set; }
    public int ProcessorCount { get; set; }
    public long TotalMemoryMb { get; set; }
    public long AvailableMemoryMb { get; set; }
    public int PipeProtocol { get; set; }
}
```

### 4. Get Job Snapshots

Get status of all active jobs.

**Request**: `GetJobSnapshotsRequest`
```csharp
public class GetJobSnapshotsRequest
{
    // Empty - no parameters
}
```

**Response**: `JobSnapshotsResponse`
```csharp
public class JobSnapshotsResponse
{
    public List<JobSnapshot> Jobs { get; set; }
}
```

**JobSnapshot**:
```csharp
public class JobSnapshot
{
    public JobKind Kind { get; init; }
    public string JobId { get; set; }
    public string JobType { get; set; }
    
    public string AppName { get; set; }
    public string ExePath { get; set; }
    
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
    
    public string EffectiveState { get; set; }
    
    public int? ProgressPercent { get; set; }
    public string? StatusText { get; set; }
    public DateTimeOffset? PlannedStartUtc { get; set; }
    
    public IReadOnlyList<JobEvent> Events { get; init; }
    
    // Job-specific details
    public string? BackupPlanName { get; set; }
    public string? BackupSourcePath { get; set; }
    public string? BackupTargetPath { get; set; }
    public string? HealthCheckType { get; set; }
    public string? HealthCheckTarget { get; set; }
    public string? RestorePlanName { get; set; }
}
```

### 5. Get Log Days

Get list of available log file dates.

**Request**: `GetLogDaysRequest`
```csharp
public class GetLogDaysRequest
{
    // Empty - no parameters
}
```

**Response**: `LogDaysResponse`
```csharp
public class LogDaysResponse
{
    public List<string> Days { get; set; } // Format: "YYYY-MM-DD"
}
```

### 6. Get Log Day

Get log content for a specific day.

**Request**: `LogDayRequest`
```csharp
public class LogDayRequest
{
    public string Day { get; set; } // Format: "YYYY-MM-DD"
}
```

**Response**: `LogDayResponse`
```csharp
public class LogDayResponse
{
    public string Day { get; set; }
    public string Content { get; set; } // Full log file content
}
```

### 7. Get Log Path

Get the directory path where logs are stored.

**Request**: `GetLogPathRequest`
```csharp
public class GetLogPathRequest
{
    // Empty - no parameters
}
```

**Response**: `LogPathResponse`
```csharp
public class LogPathResponse
{
    public string Path { get; set; } // Full directory path
}
```

### 8. Trigger Backup

Manually trigger a backup job.

**Request**: `BackupTriggerRequest`
```csharp
public class BackupTriggerRequest
{
    public string BackupPlanId { get; set; }
}
```

**Response**: No specific response (standard acknowledgment)

**Behavior**:
- Validates backup plan exists
- Queues backup job for immediate execution
- Job runs asynchronously

### 9. Get Backup List

Get all configured backup plans.

**Request**: `GetBackupListRequest`
```csharp
public class GetBackupListRequest
{
    // Empty - no parameters
}
```

**Response**: `BackupListResponse`
```csharp
public class BackupListResponse
{
    public List<BackupPlanConfig> Plans { get; set; }
}
```

### 10. Get Backup Artifacts

Get list of backup files for a specific plan.

**Request**: `BackupArtifactListRequest`
```csharp
public class BackupArtifactListRequest
{
    public string BackupPlanId { get; set; }
}
```

**Response**: `BackupArtifactListResponse`
```csharp
public class BackupArtifactListResponse
{
    public List<string> Artifacts { get; set; } // Backup file names
}
```

### 11. Trigger Restore

Manually trigger a restore operation.

**Request**: `RestoreTriggerRequest`
```csharp
public class RestoreTriggerRequest
{
    public string BackupPlanId { get; set; }
    public string ArtifactName { get; set; }
    public string RestoreToDirectory { get; set; }
    public bool OverwriteExisting { get; set; }
    public List<string> IncludePaths { get; set; }
}
```

**Response**: No specific response (standard acknowledgment)

**Behavior**:
- Validates backup artifact exists
- Creates and queues restore job
- Job runs asynchronously

### 12. Purge Backup Artifacts

Delete all backup artifacts for a specific plan.

**Request**: `PurgeBackupArtifactsRequest`
```csharp
public class PurgeBackupArtifactsRequest
{
    public string BackupPlanId { get; set; }
}
```

**Response**: No specific response (standard acknowledgment)

### 13. Export Configuration

Export configuration as JSON string.

**Request**: `ConfigExportRequest`
```csharp
public class ConfigExportRequest
{
    // Empty - no parameters
}
```

**Response**: `ConfigExportResponse`
```csharp
public class ConfigExportResponse
{
    public string ConfigJson { get; set; } // JSON-serialized config
}
```

### 14. Import Configuration

Import configuration from JSON string.

**Request**: `ConfigImportRequest`
```csharp
public class ConfigImportRequest
{
    public string ConfigJson { get; set; } // JSON-serialized config
}
```

**Response**: No specific response (standard acknowledgment)

**Behavior**:
- Deserializes JSON to WatchdogConfig
- Validates configuration
- Saves to file
- Reloads service configuration

## Data Models

### WatchedApp

Represents a monitored application.

```csharp
public class WatchedApp
{
    public string Name { get; set; }
    
    public WatchTargetType Type { get; set; }
    
    // Executable
    public string ExePath { get; set; }
    public string Arguments { get; set; }
    
    // Windows Service
    public string? ServiceName { get; set; }
    
    // HTTP
    public string? Url { get; set; }
    public int ExpectedStatusCode { get; set; } = 200;
    
    // TCP
    public string? Host { get; set; }
    public int? Port { get; set; }
    
    public bool Enabled { get; set; } = true;
    public bool RestartEnabled { get; set; } = true;
    
    public UptimeKumaSettings? UptimeKuma { get; set; }
    
    public int CheckIntervalSeconds { get; set; } = 60;
}
```

### WatchTargetType

```csharp
public enum WatchTargetType
{
    Executable = 0,
    WindowsService = 1,
    HttpEndpoint = 2,
    TcpPort = 3
}
```

### BackupPlanConfig

```csharp
public class BackupPlanConfig
{
    public bool Enabled { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    
    public BackupScheduleConfig Schedule { get; set; }
    public BackupSourceConfig Source { get; set; }
    public BackupTargetConfig Target { get; set; }
    
    public BackupCompressionConfig Compression { get; set; }
    public BackupCryptoConfig Crypto { get; set; }
    public BackupRetentionConfig Retention { get; set; }
    
    public bool VerifyAfterCreate { get; set; }
}
```

## Client Implementation

### Example: PipeClient Usage

```csharp
using var client = new PipeClient("AppWatchdog");

// Get configuration
var config = await client.GetConfigAsync();

// Update configuration
config.MailIntervalHours = 24;
await client.UpdateConfigAsync(config);

// Get snapshot
var snapshot = await client.GetSnapshotAsync();
foreach (var app in snapshot.Apps)
{
    Console.WriteLine($"{app.Name}: {(app.IsRunning ? "UP" : "DOWN")}");
}

// Get jobs
var jobs = await client.GetJobSnapshotsAsync();

// Trigger backup
await client.TriggerBackupAsync("backup-plan-id");
```

## Error Handling

### Protocol Mismatch

If client and server protocol versions don't match:
```csharp
throw new PipeProtocolMismatchException(
    $"Protocol version mismatch: client={clientVersion}, server={serverVersion}"
);
```

**Solution**: Update both UI and Service to matching versions.

### Connection Errors

If service is not running:
- Named Pipe connection will fail
- Client should check if service is running
- UI should display appropriate error message

### Request Errors

If request is invalid:
- Server returns error response
- Response may include error message
- Client should handle and display error

## Security Considerations

### Local Only
- Named Pipes are local machine only
- No network exposure
- Can't be accessed remotely

### Windows Security
- Pipe access controlled by Windows security
- Service runs as Local System or configured account
- UI must have permission to access pipe

### Data Encryption
- Sensitive values (passwords) are encrypted in config file
- Uses Windows DPAPI (machine-bound encryption)
- Transmitted in plain text over pipe (local only)

## Best Practices

### Client Implementation
1. **Connection Management**: Open connection, send request, close connection
2. **Error Handling**: Handle connection failures gracefully
3. **Timeouts**: Set reasonable timeout values
4. **Retries**: Retry on transient failures
5. **Version Checking**: Verify protocol compatibility

### Performance
1. **Minimize Requests**: Batch operations when possible
2. **Cache Data**: Cache snapshot data, refresh periodically
3. **Async Operations**: Use async/await for all calls
4. **Connection Pooling**: Not needed - pipes are lightweight

---

[← Back to Home](Home.md)
