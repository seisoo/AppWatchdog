# Architecture

This document describes the technical architecture of AppWatchdog.

## System Overview

AppWatchdog consists of two main components that communicate via Named Pipes:

```
┌────────────────────────────────────────────────────────────┐
│                    AppWatchdog System                       │
├────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────┐                               │
│  │  AppWatchdog.UI.WPF     │  ← WPF Desktop Application    │
│  │  (User Interface)        │                               │
│  │                          │                               │
│  │  - Configuration UI      │                               │
│  │  - Live Status View      │                               │
│  │  - Log Viewer            │                               │
│  │  - Service Control       │                               │
│  │  - Backup Management     │                               │
│  └──────────┬──────────────┘                               │
│             │                                                │
│             │ Named Pipes (IPC, versioned)                  │
│             │ Request/Response Protocol                     │
│             │                                                │
│  ┌──────────▼──────────────┐                               │
│  │  AppWatchdog.Service    │  ← Windows Service            │
│  │  (Background Service)    │                               │
│  │                          │                               │
│  │  ┌──────────────────┐   │                               │
│  │  │  Job Scheduler    │   │  ← Manages all jobs          │
│  │  └────────┬─────────┘   │                               │
│  │           │               │                               │
│  │  ┌────────▼─────────────────────────┐                   │
│  │  │  Jobs (IJob implementations)     │                   │
│  │  │                                   │                   │
│  │  │  - HealthMonitorJob               │                   │
│  │  │  - BackupJob                      │                   │
│  │  │  - RestoreJob                     │                   │
│  │  │  - SnapshotJob                    │                   │
│  │  │  - KumaPingJob                    │                   │
│  │  └───────────────────────────────────┘                   │
│  │                          │                               │
│  │  ┌──────────────────┐   │                               │
│  │  │  Health Checks    │   │  ← Check implementations     │
│  │  │                   │   │                               │
│  │  │  - ProcessHealthCheck             │                   │
│  │  │  - WindowsServiceHealthCheck      │                   │
│  │  │  - HttpHealthCheck                │                   │
│  │  │  - TcpPortHealthCheck             │                   │
│  │  └──────────────────┘   │                               │
│  │                          │                               │
│  │  ┌──────────────────┐   │                               │
│  │  │  Recovery Engine  │   │  ← Restart failed targets    │
│  │  └──────────────────┘   │                               │
│  │                          │                               │
│  │  ┌──────────────────┐   │                               │
│  │  │  Notifications    │   │  ← Send alerts               │
│  │  │                   │   │                               │
│  │  │  - SMTP Notifier                  │                   │
│  │  │  - ntfy Notifier                  │                   │
│  │  │  - Discord Notifier               │                   │
│  │  │  - Telegram Notifier              │                   │
│  │  └──────────────────┘   │                               │
│  │                          │                               │
│  │  ┌──────────────────┐   │                               │
│  │  │  Backup System    │   │  ← Backup/Restore operations │
│  │  │                   │   │                               │
│  │  │  - Compression                    │                   │
│  │  │  - Encryption                     │                   │
│  │  │  - SFTP Support                   │                   │
│  │  │  - SQL Server Backup              │                   │
│  │  └──────────────────┘   │                               │
│  │                          │                               │
│  └──────────────────────────┘                               │
│                                                              │
└────────────────────────────────────────────────────────────┘
```

## Project Structure

### AppWatchdog.Shared
Common code shared between Service and UI:
- **Models**: Data models (WatchedApp, Config, etc.)
- **IPC Protocol**: Named Pipe communication protocol
- **Configuration**: Config storage and encryption
- **Job Events**: Job event definitions

### AppWatchdog.Service
Windows Service (background process):
- **Worker**: Main service worker
- **JobScheduler**: Schedules and executes jobs
- **HealthChecks**: Health check implementations
- **Recovery**: Restart and recovery logic
- **Notifications**: Notification dispatchers and notifiers
- **Backups**: Backup/restore system
- **Pipe Server**: Named Pipe server for IPC

### AppWatchdog.UI.WPF
WPF Desktop Application:
- **Views**: XAML UI views
- **ViewModels**: MVVM view models
- **Services**: UI services (PipeClient, etc.)
- **Converters**: Value converters
- **Localization**: Multi-language resources

## Core Components

### 1. Job Scheduler

**Purpose**: Central coordinator for all timed operations.

**Responsibilities**:
- Schedule jobs with intervals
- Execute jobs at specified times
- Manage job lifecycle
- Track job status and events
- Handle job failures

**Job Types**:
- `HealthMonitorJob` - Monitor applications
- `BackupJob` - Execute scheduled backups
- `RestoreJob` - Execute restore operations
- `SnapshotJob` - Capture system snapshots
- `KumaPingJob` - Send Uptime Kuma heartbeats

**Interface**: `IJob`
```csharp
public interface IJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
    // Job metadata and state
}
```

### 2. Health Check System

**Purpose**: Determine if a monitored target is healthy.

**Architecture**:
```
HealthMonitorJob
    └─> HealthCheckFactory
            └─> IHealthCheck implementations
                    ├─> ProcessHealthCheck
                    ├─> WindowsServiceHealthCheck
                    ├─> HttpHealthCheck
                    └─> TcpPortHealthCheck
```

**Interface**: `IHealthCheck`
```csharp
public interface IHealthCheck
{
    Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken);
}
```

**HealthCheckResult**:
- `IsHealthy` - Success/failure
- `ResponseTimeMs` - Check duration
- `ErrorMessage` - Failure details

**Factory Pattern**:
- `HealthCheckFactory` creates appropriate `IHealthCheck` based on `WatchTargetType`
- Decouples job from specific check implementations

### 3. Recovery Engine

**Purpose**: Restart failed applications.

**Process**:
1. Health check fails
2. Determine restart strategy
3. Execute restart based on target type:
   - **Process**: Start new process
   - **Windows Service**: Use Service Control Manager
   - **HTTP/TCP**: No restart (external services)
4. Apply exponential backoff
5. Track consecutive failures

**Backoff Strategy**:
```
Attempt 1: Immediate
Attempt 2: 10 seconds
Attempt 3: 30 seconds
Attempt 4: 1 minute
Attempt 5+: 5 minutes
```

### 4. Notification Dispatcher

**Purpose**: Send notifications to configured channels.

**Architecture**:
```
NotificationDispatcher
    └─> Parallel notification to:
            ├─> SmtpNotifier (Email)
            ├─> NtfyNotifier (ntfy)
            ├─> DiscordNotifier (Discord webhook)
            └─> TelegramNotifier (Telegram bot)
```

**Notification Context**:
- Event type (Up, Down, Restart)
- Application details
- Error information
- Timestamp
- Template variables

**Template Processing**:
1. Load template (Up/Down/Restart)
2. Replace variables ($jobname, $target, etc.)
3. Format for each channel
4. Send in parallel

**Rate Limiting**:
- Email notifications respect `MailIntervalHours`
- Other channels send immediately

### 5. Backup System

**Components**:
- **BackupJob**: Scheduled backup execution
- **Backup Sources**: File, Folder, MS SQL
- **Backup Targets**: Local, SFTP
- **Compression**: Built-in compression
- **Encryption**: AES encryption with PBKDF2
- **Retention**: Automatic cleanup

**Backup Flow**:
```
1. Read source data
2. Compress (if enabled)
3. Encrypt (if enabled)
4. Transfer to target (local or SFTP)
5. Verify (if enabled)
6. Apply retention policy
```

**Backup Artifact Naming**:
```
{PlanName}_{Timestamp}.backup
Example: MyApp_20260131_020000.backup
```

### 6. IPC Protocol (Named Pipes)

**Purpose**: Communication between UI and Service.

**Protocol**:
- Binary protocol over Named Pipes
- JSON serialization for messages
- Request/response pattern
- Protocol versioning

**Messages**:
- `GetConfigRequest` / `GetConfigResponse`
- `UpdateConfigRequest` / `UpdateConfigResponse`
- `GetSnapshotRequest` / `ServiceSnapshot`
- `GetLogDaysRequest` / `LogDaysResponse`
- `GetLogDayRequest` / `LogDayResponse`
- `TriggerBackupRequest`
- `TriggerRestoreRequest`

**Protocol Versioning**:
- Both sides declare protocol version
- Mismatch throws `PipeProtocolMismatchException`
- Allows forward compatibility

**Named Pipe Name**: `AppWatchdog`

### 7. Configuration System

**Storage**:
- JSON file: `config.json`
- Located in service directory

**Structure**:
```json
{
  "Apps": [ /* WatchedApp objects */ ],
  "Smtp": { /* SMTP settings */ },
  "Ntfy": { /* ntfy settings */ },
  "Discord": { /* Discord settings */ },
  "Telegram": { /* Telegram settings */ },
  "NotificationTemplates": { /* Templates */ },
  "Backups": [ /* Backup plans */ ],
  "Restores": [ /* Restore plans */ ],
  "CultureName": "en-US"
}
```

**Encryption**:
- Sensitive values (passwords, tokens) encrypted with Windows DPAPI
- `ConfigCrypto` class handles encryption/decryption
- Machine-specific encryption
- Automatic on save/load

## Data Flow

### Monitoring Flow
```
1. JobScheduler triggers HealthMonitorJob
2. HealthMonitorJob creates HealthCheck via Factory
3. HealthCheck executes (HTTP request, process check, etc.)
4. HealthCheck returns HealthCheckResult
5. If failure:
   a. RecoveryEngine attempts restart
   b. NotificationDispatcher sends alerts
6. Update MonitorState
7. UI polls for snapshot, displays status
```

### Configuration Update Flow
```
1. User edits config in UI
2. UI sends UpdateConfigRequest via Named Pipe
3. Service receives request
4. Service validates config
5. Service encrypts sensitive values
6. Service saves to config.json
7. Service reloads configuration
8. Service sends UpdateConfigResponse
9. UI receives confirmation
10. JobScheduler applies changes (add/remove/update jobs)
```

### Backup Flow
```
1. JobScheduler triggers BackupJob at scheduled time
2. BackupJob reads source data
3. Compress data (optional)
4. Encrypt data (optional)
5. Transfer to target (Local or SFTP)
6. Verify backup (optional)
7. Apply retention policy (delete old backups)
8. Update job status
9. Send notification (if configured)
```

## Design Patterns

### Factory Pattern
- **HealthCheckFactory**: Creates appropriate `IHealthCheck` based on target type
- Decouples job from check implementations

### Strategy Pattern
- **IHealthCheck**: Different strategies for health checking
- **INotifier**: Different strategies for notifications

### Observer Pattern
- **JobScheduler**: Observes job events, updates state
- **UI**: Polls service snapshot, updates UI

### Dependency Injection
- Service uses .NET dependency injection
- Jobs registered in DI container
- Services injected into jobs

### Repository Pattern
- **ConfigStore**: Abstracts config persistence
- Can swap implementations (file, database, etc.)

## Threading Model

### Service Threading
- **Main Thread**: Service host
- **Worker Thread**: Long-running background worker
- **Job Threads**: Each job executes asynchronously
- **Timer Threads**: Job scheduler timers

### Concurrency
- Jobs can run concurrently
- Thread-safe configuration access
- CancellationToken for graceful shutdown

### Async/Await
- All I/O operations are async
- HTTP requests, TCP connections, file I/O
- Prevents thread blocking

## Error Handling

### Strategy
1. **Catch at boundaries**: Service, jobs, health checks
2. **Log all errors**: Comprehensive error logging
3. **Graceful degradation**: One failure doesn't affect others
4. **User notification**: Critical errors trigger alerts
5. **Retry logic**: Transient failures are retried

### Error Propagation
- Health checks return error in `HealthCheckResult`
- Jobs catch exceptions, log, and continue
- Service catches job exceptions
- UI displays error messages

## Logging

### Structure
```
Logs/
├── 2026-01-30.log
├── 2026-01-31.log
└── 2026-02-01.log
```

### Log Levels
- **Information**: Normal operations
- **Warning**: Potential issues
- **Error**: Failures and exceptions

### Log Sinks
- File sink (daily rotation)
- Console sink (when running interactively)

## Security Considerations

### Configuration Encryption
- Passwords and tokens encrypted with DPAPI
- Machine-bound encryption
- Can't copy config to another machine

### Service Privileges
- Runs as Local System by default
- Can be configured to run as specific user
- Needs admin privileges for service control

### IPC Security
- Named Pipes are local-only
- No network exposure
- Windows security applies to pipe access

### Backup Encryption
- User-provided password
- AES encryption
- PBKDF2 key derivation

## Performance Optimization

### Resource Management
- Connection pooling (HTTP client)
- Efficient polling intervals
- Async I/O (no thread blocking)
- Minimal memory footprint

### Scalability
- Concurrent health checks
- Parallel notifications
- Background job execution
- Optimized serialization

## Extension Points

To extend AppWatchdog:

1. **New Health Check Type**:
   - Implement `IHealthCheck`
   - Add to `HealthCheckFactory`
   - Add `WatchTargetType` enum value
   - Update UI to configure

2. **New Notification Channel**:
   - Implement notifier class
   - Add settings to `WatchdogConfig`
   - Register in `NotificationDispatcher`
   - Update UI to configure

3. **New Job Type**:
   - Implement `IJob`
   - Register in DI container
   - Add to `JobScheduler`
   - Update UI to display

4. **New Backup Source/Target**:
   - Add enum values
   - Implement reader/writer
   - Update `BackupJob`
   - Update UI to configure

---

[← Back to Home](Home.md)
