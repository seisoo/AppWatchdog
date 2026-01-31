# Configuration Guide

This guide covers all configuration options available in AppWatchdog.

## Table of Contents

- [Application Monitoring](#application-monitoring)
- [Notification Configuration](#notification-configuration)
- [Backup Configuration](#backup-configuration)
- [Restore Configuration](#restore-configuration)
- [Advanced Settings](#advanced-settings)

---

## Application Monitoring

### Adding a Monitored Application

1. Open the AppWatchdog UI
2. Navigate to the **Applications** tab
3. Click **Add New Application**
4. Configure based on the application type

### Monitor Types

AppWatchdog supports four types of health checks:

#### 1. Executable (Process) Monitoring

Monitor a running process/application.

**Configuration**:
- **Name**: Friendly name for the application
- **Type**: Select "Executable"
- **Executable Path**: Full path to the `.exe` file
  - Example: `C:\Program Files\MyApp\MyApp.exe`
- **Arguments**: Command-line arguments (optional)
  - Example: `--port 8080 --config config.xml`
- **Check Interval**: Seconds between checks (default: 60)
- **Enable Monitoring**: Enable/disable the check
- **Enable Restart**: Allow automatic restart on failure

**How it works**:
- Checks if the process is running
- Monitors by process name and path
- Restarts the process if it crashes or stops unexpectedly

**Example Use Cases**:
- Desktop applications
- Background services
- Custom server applications

#### 2. Windows Service Monitoring

Monitor a Windows Service.

**Configuration**:
- **Name**: Friendly name
- **Type**: Select "Windows Service"
- **Service Name**: The Windows service name (not display name)
  - Example: `W3SVC` (for IIS), `MSSQLSERVER` (for SQL Server)
  - Find it in `services.msc` under "Service name"
- **Check Interval**: Seconds between checks
- **Enable Monitoring**: Enable/disable
- **Enable Restart**: Allow automatic restart

**How it works**:
- Checks if the service is running
- Restarts the service using Windows Service Control Manager
- Requires administrator privileges

**Example Use Cases**:
- IIS Web Server
- SQL Server
- Custom Windows Services
- Third-party services

#### 3. HTTP Endpoint Monitoring

Monitor a web service or HTTP endpoint.

**Configuration**:
- **Name**: Friendly name
- **Type**: Select "HTTP"
- **URL**: Full URL to check
  - Example: `https://example.com/health`
  - Example: `http://localhost:5000/api/status`
- **Expected Status Code**: HTTP status code indicating success (default: 200)
  - Use `200` for standard OK
  - Use `204` for No Content
  - Use `301`/`302` for redirects (if expected)
- **Check Interval**: Seconds between checks
- **Enable Monitoring**: Enable/disable
- **Enable Restart**: Usually disabled (can't restart external services)

**How it works**:
- Sends HTTP GET request to the URL
- Checks response status code matches expected value
- Measures response time (shown as "Ping")
- Follows redirects by default

**Features**:
- Supports HTTP and HTTPS
- Displays response time in milliseconds
- Can check localhost or remote endpoints

**Example Use Cases**:
- Web applications
- REST APIs
- Health check endpoints
- Public websites

#### 4. TCP Port Monitoring

Monitor if a TCP port is open and accepting connections.

**Configuration**:
- **Name**: Friendly name
- **Type**: Select "TCP"
- **Host**: Hostname or IP address
  - Example: `localhost`, `192.168.1.100`, `example.com`
- **Port**: TCP port number
  - Example: `3306` (MySQL), `5432` (PostgreSQL), `8080` (custom)
- **Check Interval**: Seconds between checks
- **Enable Monitoring**: Enable/disable
- **Enable Restart**: Usually disabled (for external services)

**How it works**:
- Attempts to establish TCP connection
- Checks if port accepts connections
- Measures connection time
- Closes connection immediately

**Example Use Cases**:
- Database servers (MySQL, PostgreSQL, MongoDB)
- Cache servers (Redis, Memcached)
- Message queues (RabbitMQ, Kafka)
- Custom TCP services

### Check Intervals

The check interval determines how often AppWatchdog checks the application's status.

**Recommendations**:
- **Critical services**: 30-60 seconds
- **Standard applications**: 60-300 seconds (1-5 minutes)
- **External services**: 300-600 seconds (5-10 minutes)
- **Low-priority checks**: 600+ seconds (10+ minutes)

**Considerations**:
- Shorter intervals = faster detection, higher resource usage
- HTTP checks consume bandwidth
- Balance monitoring frequency with performance impact

### Restart Behavior

When an application fails and restart is enabled:

1. **Detection**: Failure detected during health check
2. **Notification**: Alert sent via configured channels
3. **Restart Attempt**: AppWatchdog tries to restart the application
4. **Retry Logic**: Uses exponential backoff for repeated failures
5. **Recovery Notification**: Alert sent when application is back up

**Restart Settings**:
- **Enable Restart**: Toggle automatic restart
- **Backoff**: Automatic delay between restart attempts increases with consecutive failures

---

## Notification Configuration

Configure multiple notification channels to receive alerts.

### Email (SMTP)

Send email notifications via SMTP.

**Configuration**:
1. Navigate to **Notifications** tab
2. Expand **SMTP Settings**
3. Configure:
   - **Server**: SMTP server address (e.g., `smtp.gmail.com`)
   - **Port**: SMTP port (typically 587 for TLS, 465 for SSL, 25 for non-encrypted)
   - **Enable SSL**: Enable secure connection (recommended)
   - **Username**: SMTP authentication username
   - **Password**: SMTP authentication password
   - **From**: Sender email address
   - **To**: Recipient email address
4. **Mail Interval Hours**: Minimum hours between repeat notifications (default: 12)

**Example - Gmail**:
```
Server: smtp.gmail.com
Port: 587
Enable SSL: Yes
Username: your-email@gmail.com
Password: your-app-password (not regular password)
From: your-email@gmail.com
To: recipient@example.com
```

**Note**: Gmail requires an [App Password](https://support.google.com/accounts/answer/185833) for security.

### ntfy

Send push notifications via ntfy.sh or self-hosted ntfy server.

**Configuration**:
1. Navigate to **Notifications** tab
2. Expand **ntfy Settings**
3. Configure:
   - **Enabled**: Check to enable
   - **Base URL**: ntfy server URL (default: `https://ntfy.sh`)
   - **Topic**: Your unique topic name (e.g., `my-appwatchdog-alerts`)
   - **Token**: Access token (optional, for protected topics)
   - **Priority**: Notification priority (1-5, default: 3)

**Setup**:
1. Choose a unique topic name
2. Install ntfy app on your phone
3. Subscribe to your topic
4. Receive instant push notifications

**Example**:
```
Base URL: https://ntfy.sh
Topic: mycompany-watchdog-prod
Priority: 4 (high)
```

### Discord

Send notifications to Discord via webhooks.

**Configuration**:
1. Create a Discord webhook:
   - Open Discord server
   - Server Settings → Integrations → Webhooks
   - Click "New Webhook"
   - Copy webhook URL
2. In AppWatchdog **Notifications** tab:
   - **Enabled**: Check to enable
   - **Webhook URL**: Paste Discord webhook URL
   - **Username**: Bot display name (default: "AppWatchdog")
   - **Avatar URL**: Bot avatar image URL (optional)

**Example**:
```
Webhook URL: https://discord.com/api/webhooks/123.../abc...
Username: AppWatchdog Monitor
```

**Features**:
- Rich embed messages with color coding
- Green for UP, Red for DOWN, Blue for RESTART
- Shows target, status, error details, and timestamp

### Telegram

Send notifications via Telegram bot.

**Configuration**:
1. Create a Telegram bot:
   - Message @BotFather on Telegram
   - Send `/newbot` and follow instructions
   - Copy the bot token
2. Get your Chat ID:
   - Message your bot
   - Visit: `https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates`
   - Find your `chat.id` in the response
3. In AppWatchdog **Notifications** tab:
   - **Enabled**: Check to enable
   - **Bot Token**: Your bot token from BotFather
   - **Chat ID**: Your chat ID (can be user or group)
   - **Use Markdown**: Enable Markdown formatting (recommended)

**Example**:
```
Bot Token: 123456789:ABCdefGHIjklMNOpqrsTUVwxyz
Chat ID: 987654321
Use Markdown: Yes
```

### Uptime Kuma

Send heartbeat signals to Uptime Kuma monitoring.

**Configuration**:
1. Create a Push monitor in Uptime Kuma
2. Copy the push URL and token
3. In AppWatchdog, configure per-application:
   - **Enable Uptime Kuma**: Check to enable (in app settings)
   - **Base URL**: Uptime Kuma server URL
   - **Push Token**: The push token from monitor settings
   - **Interval Seconds**: Heartbeat interval (default: 60)
   - **Monitor Name**: Optional custom name

**How it works**:
- AppWatchdog sends periodic heartbeat pings
- Uptime Kuma marks monitor as DOWN if heartbeats stop
- Useful for external monitoring of AppWatchdog itself

### Notification Templates

Customize notification messages:

1. Navigate to **Notifications** tab
2. Expand template sections
3. Customize templates for:
   - **UP**: Application recovered
   - **DOWN**: Application failed
   - **RESTART**: Application being restarted

**Available Variables**:
- `$jobname` - Application name
- `$target` - Target path/URL/service
- `$status` - Current status
- `$error` - Error message (if any)
- `$ping` - Response time in ms
- `$machine` - Computer name
- `$time` - Timestamp
- `$summary` - Auto-generated summary

**Template Fields**:
- **Title**: Notification title/subject
- **Summary**: Brief summary line
- **Body**: Detailed message body
- **Color**: Color code for visual notifications (Discord, etc.)

---

## Backup Configuration

Configure automated backups of files, folders, or databases.

### Creating a Backup Plan

1. Navigate to **Backups** tab
2. Click **Add Backup Plan**
3. Configure the backup plan

### Backup Plan Settings

#### General
- **Name**: Friendly name for the backup plan
- **Enabled**: Enable/disable the backup plan

#### Schedule
- **Time (Local)**: Daily backup time (e.g., "02:00" for 2 AM)
- **Days**: Days of the week to run backups
  - Select one or more days
  - Default: All days

#### Source
- **Type**: What to backup
  - **Folder**: Backup a directory and its contents
  - **File**: Backup a single file
  - **MS SQL**: Backup a SQL Server database

**For Folder/File**:
- **Path**: Full path to folder or file
  - Example: `C:\Important\Data`
  - Example: `C:\App\config.json`

**For MS SQL**:
- **SQL Connection String**: Database connection string
  - Example: `Server=localhost;Database=MyDB;Integrated Security=true;`
- **SQL Database**: Database name

#### Target (Destination)
- **Type**: Where to store backups
  - **Local**: Store on local disk or network share
  - **SFTP**: Store on remote SFTP server

**For Local**:
- **Local Directory**: Destination folder
  - Example: `D:\Backups\MyApp`
  - Example: `\\NAS\Backups\MyApp`

**For SFTP**:
- **Host**: SFTP server address
- **Port**: SFTP port (default: 22)
- **User**: SFTP username
- **Password**: SFTP password (encrypted in config)
- **Remote Directory**: Destination folder on SFTP server
- **Host Key Fingerprint**: SFTP server fingerprint (optional, for verification)

#### Compression
- **Compress**: Enable compression (recommended)
- **Level**: Compression level (0-9)
  - 0 = No compression
  - 5 = Balanced (default)
  - 9 = Maximum compression (slower)

#### Encryption
- **Encrypt**: Enable encryption (recommended)
- **Password**: Encryption password (stored encrypted)
- **Iterations**: Key derivation iterations (default: 200,000)
  - Higher = more secure but slower

#### Retention
- **Keep Last**: Number of backups to retain
  - Default: 14 (two weeks daily)
  - Older backups are automatically deleted

#### Verification
- **Verify After Create**: Verify backup integrity after creation (recommended)

### Manual Backup Trigger

Trigger a backup manually:
1. Go to **Backups** tab
2. Find the backup plan
3. Click **Trigger Backup Now**
4. Monitor progress in **Jobs** tab

### Backup Artifacts

View created backups:
1. Select a backup plan
2. Click **View Artifacts**
3. See list of all backup files with timestamps
4. Use these for restore operations

---

## Restore Configuration

Configure restore plans to recover from backups.

### Creating a Restore Plan

1. Navigate to **Restore** tab (if available) or **Backups** tab
2. Click **Add Restore Plan** or use manual restore
3. Configure the restore

### Restore Settings

- **Backup Plan**: Select source backup plan
- **Artifact**: Select specific backup file to restore
- **Restore To Directory**: Destination folder
  - Can be original location or different location
- **Overwrite Existing**: Allow overwriting existing files
- **Include Paths**: Optional filter for specific files/folders
  - Leave empty to restore everything
  - Example: `["logs/", "data/*.db"]` to restore specific paths
- **Run Once**: Execute restore only once (vs. scheduled)

### Manual Restore

Restore a backup manually:
1. Go to **Backups** tab
2. Select a backup plan
3. Click **View Artifacts**
4. Select a specific backup
5. Click **Restore**
6. Configure restore options:
   - Destination directory
   - Overwrite setting
   - File filters (if needed)
7. Click **Start Restore**
8. Monitor progress in **Jobs** tab

---

## Advanced Settings

### Culture/Language

Change UI language:
1. Navigate to **Notifications** or **Settings** tab
2. Find **Culture Name** dropdown
3. Select your preferred language:
   - `en-US` - English (United States)
   - `de-DE` - German (Germany)
   - Add more as available
4. Restart UI to apply changes

### Configuration Export/Import

**Export Configuration**:
1. Use UI export feature (if available)
2. Or manually copy `config.json` from service directory
3. Store in a safe location

**Import Configuration**:
1. Use UI import feature (if available)
2. Or manually replace `config.json` in service directory
3. Restart service to apply

### Encrypted Configuration Values

Sensitive values (passwords, tokens) are automatically encrypted in the configuration file using Windows DPAPI.

**Features**:
- Automatic encryption on save
- Machine-specific encryption
- No manual intervention required
- Config file can be safely backed up (encrypted values are machine-bound)

### Configuration File Location

Configuration is stored as `config.json` in the service directory.

**Default locations** (depending on installation):
- `C:\Program Files\AppWatchdog\config.json`
- Or wherever `AppWatchdog.Service.exe` is located

---

## Configuration Best Practices

1. **Start Simple**: Begin with one application, then add more
2. **Test Notifications**: Send test notifications to verify setup
3. **Secure Credentials**: Use encrypted config, don't share config files
4. **Regular Backups**: Backup critical data regularly
5. **Monitor Logs**: Periodically review logs for issues
6. **Document Settings**: Keep notes on custom configurations
7. **Version Control**: Track configuration changes for production systems
8. **Least Privilege**: Run service with minimum required permissions
9. **Network Security**: Be cautious with remote notifications and SFTP
10. **Test Restores**: Periodically test backup restores to ensure they work

---

[← Back to Home](Home.md) | [Troubleshooting →](Troubleshooting.md)
