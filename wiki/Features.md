# Features

This page provides a comprehensive overview of all AppWatchdog features.

## Monitoring & Health Checks

### Process Monitoring
- **Executable path detection**: Monitors processes by path and name
- **Command-line arguments**: Support for process arguments
- **Automatic process discovery**: Detects if process is running
- **Restart capability**: Can start/restart crashed processes
- **Response time**: N/A for processes

**Use Cases**:
- Desktop applications
- Console applications
- Background processes
- Custom server applications

### Windows Service Monitoring
- **Service name-based monitoring**: Uses Windows service name
- **Service Control Manager integration**: Native Windows service control
- **Start/stop/restart**: Full service lifecycle management
- **Service state detection**: Running, stopped, starting, stopping states
- **Administrator privileges**: Required for service control

**Use Cases**:
- IIS Web Server
- SQL Server
- Custom Windows Services
- Third-party services

### HTTP Endpoint Monitoring
- **HTTP/HTTPS support**: Supports both protocols
- **Status code validation**: Configurable expected status code
- **Response time measurement**: Displays ping time in milliseconds
- **Redirect following**: Automatically follows HTTP redirects
- **Timeout handling**: Configurable timeouts
- **Header support**: (Future enhancement)

**Use Cases**:
- Web applications
- REST APIs
- Health check endpoints
- Microservices
- Public websites

### TCP Port Monitoring
- **Connection testing**: Verifies TCP port is accepting connections
- **Hostname/IP support**: Works with domain names and IP addresses
- **Connection timing**: Measures connection establishment time
- **Immediate closure**: Doesn't hold connections open
- **Local and remote**: Monitor localhost or remote servers

**Use Cases**:
- Database servers (MySQL, PostgreSQL, MongoDB, etc.)
- Cache servers (Redis, Memcached)
- Message queues (RabbitMQ, Kafka)
- Custom TCP services
- Game servers

## Recovery & Restart

### Automatic Restart
- **Failure detection**: Immediately detects when application fails
- **Restart execution**: Launches or restarts failed applications
- **Process monitoring**: Tracks process lifecycle
- **Service control**: Uses Windows Service Manager for services
- **Retry mechanism**: Continues trying if restart fails

### Backoff Strategy
- **Exponential backoff**: Increases delay between restart attempts
- **Consecutive failure tracking**: Counts failures since last success
- **Prevents restart loops**: Avoids rapid restart cycles
- **Configurable limits**: Adjustable retry parameters
- **Auto-reset**: Resets counters on successful recovery

### Recovery Notifications
- **Failure alerts**: Immediate notification when application fails
- **Restart notifications**: Alert when restart is attempted
- **Recovery confirmation**: Notification when application is back up
- **Error details**: Includes error messages and failure reasons
- **Timing information**: Shows when events occurred

## Notifications

### Multi-Channel Support
AppWatchdog supports five notification channels:

#### Email (SMTP)
- **Standard SMTP**: Compatible with any SMTP server
- **SSL/TLS support**: Secure email transmission
- **Authentication**: Username/password authentication
- **Configurable ports**: Support for standard and custom ports
- **HTML support**: (Future enhancement)
- **Rate limiting**: Minimum interval between email notifications

**Supported Providers**:
- Gmail (with App Password)
- Outlook/Office 365
- SendGrid
- AWS SES
- Custom SMTP servers

#### ntfy
- **Push notifications**: Real-time push to mobile devices
- **Self-hosted option**: Use ntfy.sh or host your own server
- **Priority levels**: 5 priority levels (1-5)
- **Topic-based**: Simple topic subscription model
- **Token authentication**: Optional security with access tokens
- **Multi-device**: Receive on all subscribed devices

#### Discord
- **Webhook integration**: Simple webhook setup
- **Rich embeds**: Color-coded embed messages
- **Custom branding**: Configurable username and avatar
- **Instant delivery**: Real-time notifications
- **Thread support**: (Future enhancement)
- **Formatting**: Markdown and emoji support

#### Telegram
- **Bot API**: Uses Telegram Bot API
- **Chat support**: Send to users or groups
- **Markdown formatting**: Optional Markdown for rich text
- **Inline buttons**: (Future enhancement)
- **File attachments**: (Future enhancement)
- **Silent notifications**: (Future enhancement)

#### Uptime Kuma
- **Heartbeat monitoring**: Periodic heartbeat signals
- **External monitoring**: Uptime Kuma monitors AppWatchdog
- **Push monitor**: Uses Uptime Kuma's push API
- **Configurable intervals**: Adjustable heartbeat frequency
- **Per-application**: Different settings per monitored app
- **Monitor name**: Optional custom naming

### Notification Templates

**Customizable Templates**:
- **UP template**: When application recovers
- **DOWN template**: When application fails
- **RESTART template**: When restart is triggered

**Template Variables**:
- `$jobname` - Application name
- `$target` - Target (path, URL, service name)
- `$status` - Current status
- `$error` - Error message
- `$ping` - Response time in milliseconds
- `$machine` - Computer/host name
- `$time` - Timestamp
- `$summary` - Auto-generated summary

**Template Components**:
- **Title**: Notification subject/title
- **Summary**: One-line summary
- **Body**: Detailed message body
- **Color**: Visual color coding (hex color)

## Backup & Restore

### Backup Sources
- **Folder backups**: Entire directories and subdirectories
- **File backups**: Individual files
- **MS SQL backups**: SQL Server database backups
- **Recursive**: Automatic recursive directory backup
- **Exclusions**: (Future enhancement)

### Backup Targets
- **Local storage**: Save to local disk or network share
- **SFTP**: Remote backup via SFTP
- **Network paths**: UNC paths supported
- **Path verification**: Validates target accessibility

### SFTP Support
- **Secure transfer**: SSH File Transfer Protocol
- **Authentication**: Username/password authentication
- **Custom ports**: Support for non-standard ports
- **Host key verification**: Optional fingerprint verification
- **Remote directories**: Specify remote path
- **Connection pooling**: (Future enhancement)

### Backup Scheduling
- **Daily schedules**: Run backups at specific times
- **Day selection**: Choose which days to run
- **Time-based**: Specify exact time (local timezone)
- **Multiple plans**: Different schedules for different backups
- **Timezone aware**: Uses local system time

### Compression
- **Built-in compression**: Reduces backup size
- **Adjustable levels**: 0-9 compression levels
  - 0: No compression (fastest)
  - 5: Balanced (default)
  - 9: Maximum compression (slowest)
- **Archive format**: Efficient archiving
- **Selective compression**: Can disable if needed

### Encryption
- **AES encryption**: Strong encryption for backups
- **Password protection**: Encrypted with user password
- **Key derivation**: PBKDF2 with configurable iterations
- **Secure storage**: Password stored encrypted in config
- **Machine-bound**: Uses Windows DPAPI for config encryption

### Retention
- **Automatic cleanup**: Removes old backups
- **Keep last N**: Configurable retention count
- **Date-based**: Keeps most recent backups
- **Space management**: Prevents unlimited disk usage
- **Manual deletion**: Override with manual cleanup

### Backup Verification
- **Post-creation verification**: Verifies backup integrity
- **Checksum validation**: Ensures backup is not corrupted
- **Automatic testing**: Optional after each backup
- **Error detection**: Identifies backup failures early

### Manual Triggers
- **On-demand backups**: Trigger backups manually
- **Ad-hoc execution**: Outside of schedule
- **Testing**: Test backup configuration
- **Immediate execution**: Runs in job queue

### Restore Operations
- **Selective restore**: Choose what to restore
- **Path filtering**: Include only specific files/folders
- **Destination selection**: Restore to original or new location
- **Overwrite control**: Choose to overwrite existing files
- **Manual restore**: UI-driven restore process

## System Monitoring

### System Information
- **Machine name**: Computer name
- **User name**: Current user context
- **OS version**: Windows version
- **.NET version**: Runtime version
- **System uptime**: How long system has been running

### Resource Monitoring
- **Processor count**: Number of CPU cores
- **Total memory**: Total RAM in MB
- **Available memory**: Free RAM in MB
- **Memory percentage**: (Future enhancement)
- **CPU usage**: (Future enhancement)
- **Disk space**: (Future enhancement)

### Session State
- **User session detection**: Interactive user presence
- **Session state tracking**: Active, locked, disconnected states
- **Session changes**: (Future enhancement)

## Job System

### Job Scheduler
- **Unified job system**: All operations run as jobs
- **Job queue**: Managed job execution
- **Concurrent jobs**: Multiple jobs can run simultaneously
- **Job priorities**: (Future enhancement)
- **Job dependencies**: (Future enhancement)

### Job Types
1. **Health Monitor Jobs**: Monitor applications
2. **Backup Jobs**: Execute backups
3. **Restore Jobs**: Execute restores
4. **Snapshot Jobs**: Capture system snapshots
5. **Kuma Ping Jobs**: Send Uptime Kuma heartbeats

### Job Status
- **Live status**: Real-time job state
- **Progress tracking**: Percentage complete (for backups/restores)
- **Event history**: Recent job events
- **Timing information**: Last run, next run
- **Failure tracking**: Consecutive failures
- **Status messages**: Detailed status text

### Job Control
- **Enable/disable**: Toggle jobs on/off
- **Manual triggers**: Run jobs on demand
- **Job intervals**: Configurable check intervals
- **Job cancellation**: (Future enhancement)

## User Interface

### WPF Application
- **Native Windows UI**: Windows Presentation Foundation
- **Modern design**: Clean, intuitive interface
- **Real-time updates**: Live status information
- **Tab-based navigation**: Organized by function
- **Responsive**: Updates immediately

### Main Features
- **Service control**: Install, start, stop, restart, uninstall service
- **Application management**: Add, edit, delete monitored applications
- **Live status view**: See all applications at a glance
- **Job list**: View all active jobs
- **Log viewer**: Built-in log browser
- **Configuration**: All settings accessible via UI
- **Backup management**: Create and manage backup plans
- **Restore operations**: Execute restores from UI

### Tabs/Sections
1. **Service**: Service installation and control
2. **Applications**: Monitored application list and status
3. **Backups**: Backup plan management
4. **Restore**: Restore operations
5. **Jobs**: Active job list and status
6. **Notifications**: Notification configuration
7. **Logs**: Log viewer

### Multi-Language Support
- **Internationalization**: UI text localization
- **Culture selection**: Choose UI language
- **Resource-based**: Extensible translation system
- **Supported languages**: English, German, (more can be added)

## Windows Service

### Service Features
- **Background execution**: Runs independently of user session
- **Auto-start**: Can start with Windows
- **Service account**: Runs as Local System or custom account
- **Low resource usage**: Minimal CPU and memory footprint
- **Logging**: File-based logs

### Service Control
- **Standard Windows Service**: Managed via Services console
- **Command-line control**: Install, start, stop, uninstall
- **UI control**: Service tab in UI
- **Status monitoring**: Service state visible in UI
- **Automatic recovery**: Windows service recovery options

### Process Isolation
- **Separate process**: UI and service are separate processes
- **Service always runs**: UI can be closed, service continues
- **IPC communication**: Named Pipes for inter-process communication
- **Session independence**: Service runs in Session 0

## IPC (Inter-Process Communication)

### Named Pipes
- **Windows Named Pipes**: Reliable IPC mechanism
- **Bidirectional**: Two-way communication
- **Protocol versioning**: Forward compatibility
- **Binary protocol**: Efficient data transfer
- **Local only**: Service and UI on same machine

### Protocol Features
- **Request/response**: Synchronous communication model
- **JSON serialization**: Human-readable message format
- **Type safety**: Strongly-typed messages
- **Version checking**: Protocol version negotiation
- **Error handling**: Graceful error responses

### API Operations
- **Get configuration**: Retrieve current config
- **Update configuration**: Save configuration changes
- **Get snapshot**: Current system and application status
- **Get logs**: Retrieve log files
- **Trigger backups**: Manually start backups
- **Trigger restores**: Manually start restores
- **Service control**: Start/stop service
- **Job list**: Get all jobs

## Logging

### File-Based Logs
- **Daily log files**: One file per day
- **Structured logging**: Consistent log format
- **Log rotation**: Automatic daily rotation
- **Log levels**: Info, Warning, Error
- **Timestamped**: Every entry has timestamp

### Log Viewer
- **Built-in viewer**: View logs in UI
- **Date selection**: Browse logs by date
- **Text display**: Full log content
- **Search**: (Future enhancement)
- **Filtering**: (Future enhancement)
- **Export**: Copy log content

### Log Location
- **Service directory**: Logs stored with service
- **Organized by date**: Easy to find specific day
- **Retention**: Manual cleanup (automatic cleanup is future enhancement)

## Security

### Configuration Encryption
- **Windows DPAPI**: Data Protection API
- **Machine-specific**: Encrypted values tied to machine
- **Automatic encryption**: No user intervention required
- **Secure storage**: Passwords, tokens encrypted in config
- **Decryption**: Automatic on service start

### Backup Encryption
- **AES encryption**: Industry-standard encryption
- **Password-based**: User-provided password
- **Key derivation**: PBKDF2 with iterations
- **Configurable security**: Adjustable iteration count
- **Encrypted backups**: Secure storage of sensitive data

### Privilege Management
- **Administrator required**: For service installation
- **Elevated operations**: Service restart, Windows service control
- **Least privilege**: Run with minimum required permissions
- **Service account**: Configurable service account

## Performance

### Resource Efficiency
- **Low CPU**: Minimal CPU usage
- **Small memory footprint**: ~50-100 MB typical
- **Efficient polling**: Optimized health checks
- **Async operations**: Non-blocking I/O
- **Lightweight**: Suitable for resource-constrained systems

### Scalability
- **Multiple applications**: Monitor dozens of applications
- **Concurrent checks**: Parallel health checks
- **Background jobs**: Non-interfering backup operations
- **Efficient networking**: Reused connections where possible

## Deployment

### Self-Contained Build
- **.NET runtime included**: No installation required
- **Single binary**: Simple deployment
- **Portable**: Copy to any Windows machine
- **Version independent**: No dependency conflicts
- **Quick deployment**: Extract and run

### Platform Support
- **Windows-only**: Windows 10, 11, Server 2019+
- **x64 architecture**: 64-bit builds
- **No dependencies**: Self-contained runtime
- **Framework-dependent**: (Can also build framework-dependent)

## Future Enhancements

Features planned for future releases:

- **Enhanced HTTP checks**: Custom headers, authentication, POST requests
- **Database health checks**: Native database connection checks
- **Custom scripts**: Run PowerShell/batch scripts for checks
- **Webhooks**: Generic webhook notifications
- **Web dashboard**: Browser-based monitoring interface
- **Remote monitoring**: Monitor multiple machines from one UI
- **Alert grouping**: Combine multiple alerts
- **Maintenance windows**: Suppress alerts during maintenance
- **Report generation**: Generate monitoring reports
- **Graph/charts**: Visual status history
- **Mobile app**: iOS/Android monitoring app
- **Cloud storage**: S3, Azure Blob, Google Cloud backups
- **Database backups**: More database types
- **Incremental backups**: Only backup changes
- **Configuration versioning**: Track config changes
- **Audit log**: Track all configuration changes
- **Role-based access**: Multi-user with permissions
- **API**: REST API for programmatic access

---

[← Back to Home](Home.md)
