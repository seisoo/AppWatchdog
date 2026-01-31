# Troubleshooting

This guide helps you resolve common issues with AppWatchdog.

## Table of Contents
- [Service Issues](#service-issues)
- [Monitoring Issues](#monitoring-issues)
- [Notification Issues](#notification-issues)
- [Backup Issues](#backup-issues)
- [UI Issues](#ui-issues)
- [Performance Issues](#performance-issues)
- [Getting More Help](#getting-more-help)

---

## Service Issues

### Service Won't Install

**Symptom**: Error when clicking "Install Service" or running `install` command.

**Common Causes & Solutions**:

1. **Not running as Administrator**
   - **Solution**: Right-click UI or Command Prompt and select "Run as Administrator"

2. **Service already exists**
   - **Check**: Open `services.msc` and look for "AppWatchdog Service"
   - **Solution**: Uninstall the old service first, then reinstall

3. **Path contains spaces and not quoted**
   - **Solution**: Use quotes around paths with spaces
   - Usually handled automatically by the UI

4. **Insufficient permissions**
   - **Solution**: Ensure your user account has admin privileges

5. **Antivirus blocking**
   - **Solution**: Add AppWatchdog to antivirus exclusions

### Service Won't Start

**Symptom**: Service fails to start or starts then immediately stops.

**Diagnosis Steps**:

1. **Check Windows Event Viewer**:
   - Press Win + R, type `eventvwr.msc`
   - Navigate to Windows Logs → Application
   - Look for errors from "AppWatchdog.Service"

2. **Check AppWatchdog logs**:
   - Navigate to installation directory
   - Open latest log file in `Logs/` folder
   - Look for error messages

**Common Causes & Solutions**:

1. **Corrupted configuration file**
   - **Check**: Open `config.json` - is it valid JSON?
   - **Solution**: Fix JSON syntax or delete config.json (will reset config)

2. **Missing dependencies**
   - **Solution**: Re-extract all files from release archive
   - Ensure `AppWatchdog.Service.exe` is complete

3. **Port/resource conflict**
   - **Check**: Named Pipe might be in use (rare)
   - **Solution**: Restart computer to release all resources

4. **.NET runtime issue**
   - **Solution**: Ensure you're using the self-contained build
   - If using framework-dependent build, install .NET 8 runtime

5. **File permissions**
   - **Solution**: Ensure service has read/write access to installation directory

### Service Disconnected / UI Can't Connect

**Symptom**: UI shows "Service not connected" or similar message.

**Solutions**:

1. **Verify service is running**:
   - Open `services.msc`
   - Find "AppWatchdog Service"
   - Verify status is "Running"
   - If not, start the service

2. **Restart both service and UI**:
   - Stop service
   - Close UI
   - Start service
   - Open UI as Administrator

3. **Check for Named Pipe blocking**:
   - Some security software blocks Named Pipes
   - Add exception for AppWatchdog

4. **Protocol version mismatch**:
   - UI and Service versions must match
   - Re-download and extract both files together

### Service Crashes or Stops Unexpectedly

**Diagnosis**:
1. Check Windows Event Viewer (Application log)
2. Check AppWatchdog logs
3. Look for patterns (specific application, specific time, etc.)

**Solutions**:
1. Update to latest version
2. Check for resource exhaustion (memory, disk space)
3. Temporarily disable problematic applications
4. Report bug with logs

---

## Monitoring Issues

### Application Shows as Down but It's Running

**Possible Causes**:

1. **Process Monitoring**:
   - **Issue**: Process name mismatch
   - **Solution**: Verify exact executable path matches
   - **Check**: Some apps spawn child processes - monitor the right one

2. **HTTP Monitoring**:
   - **Issue**: Wrong URL or expected status code
   - **Solution**: Test URL in browser, verify status code
   - **Check**: Ensure URL is accessible from server machine

3. **TCP Monitoring**:
   - **Issue**: Firewall blocking, wrong host/port
   - **Solution**: Test with `telnet <host> <port>` or `Test-NetConnection`
   - **Check**: Verify port is open and listening

4. **Windows Service Monitoring**:
   - **Issue**: Wrong service name (display name vs. service name)
   - **Solution**: Use actual service name from `services.msc`, not display name

### Application Won't Restart

**Symptom**: Restart is enabled but application doesn't restart on failure.

**Diagnosis**:

1. **Check if restart is actually enabled**:
   - Verify "Enable Restart" is checked
   - Verify "Enable Monitoring" is checked

2. **Check logs**:
   - Look for restart attempts in logs
   - Check for error messages during restart

**Common Causes**:

1. **Insufficient Permissions**:
   - **Process**: Need permission to launch executable
   - **Windows Service**: Need admin privileges
   - **Solution**: Run service as administrator or appropriate account

2. **Wrong Path/Arguments**:
   - **Solution**: Verify executable path exists and is correct
   - **Solution**: Check arguments are valid

3. **Application Fails to Start**:
   - **Issue**: Application starts but immediately crashes
   - **Check**: Try starting manually to verify it works
   - **Solution**: Fix underlying application issue

4. **Backoff Delay**:
   - **Issue**: Restart is delayed due to exponential backoff
   - **Check**: Look at "Next Start Attempt" time in Jobs tab
   - **Info**: This is normal after multiple failures

5. **External Dependencies**:
   - **Issue**: Application depends on database, network, etc.
   - **Solution**: Ensure dependencies are available

### False Positives (App marked down incorrectly)

**HTTP Monitoring**:
- Increase timeout if application is slow to respond
- Check for intermittent network issues
- Verify SSL certificate is valid (for HTTPS)

**TCP Monitoring**:
- Increase timeout
- Check for network latency
- Verify firewall rules

**Process Monitoring**:
- Ensure process name is unique
- Check for process path changes

---

## Notification Issues

### Email Notifications Not Sending

**Diagnosis**:
1. Check logs for SMTP errors
2. Verify email settings

**Common Issues**:

1. **Wrong SMTP Settings**:
   - **Verify**: Server address, port, SSL setting
   - **Test**: Use email client to verify settings work

2. **Authentication Failure**:
   - **Gmail**: Use App Password, not regular password
   - **Office 365**: Verify credentials, check MFA settings
   - **Solution**: Double-check username and password

3. **Blocked by Firewall**:
   - **Check**: Firewall rules for outbound SMTP
   - **Solution**: Allow traffic on port 587/465/25

4. **Rate Limiting**:
   - **Info**: Email has minimum interval (default 12 hours)
   - **Check**: `MailIntervalHours` setting
   - **Solution**: Reduce interval if needed, but avoid spam

5. **Invalid Email Addresses**:
   - **Verify**: From and To addresses are valid
   - **Check**: Some SMTP servers require From to match authentication

### ntfy Notifications Not Appearing

**Issues**:
1. **Wrong Topic Name**:
   - **Verify**: Topic name in config matches subscription
   - **Case-sensitive**: Topic names are case-sensitive

2. **Token Issues**:
   - **Solution**: If topic is protected, provide valid token
   - **Solution**: If topic is public, leave token empty

3. **Base URL Wrong**:
   - **Default**: `https://ntfy.sh`
   - **Self-hosted**: Use your server URL

4. **Network Issues**:
   - **Check**: Firewall allows outbound HTTPS
   - **Test**: Visit ntfy URL in browser

### Discord Notifications Not Working

**Issues**:
1. **Invalid Webhook URL**:
   - **Verify**: Webhook URL is complete and correct
   - **Format**: `https://discord.com/api/webhooks/...`
   - **Test**: Send test message via webhook

2. **Webhook Deleted**:
   - **Check**: Webhook still exists in Discord
   - **Solution**: Recreate webhook if deleted

3. **Rate Limiting**:
   - **Issue**: Discord rate limits webhooks
   - **Solution**: Reduce notification frequency

### Telegram Notifications Not Working

**Issues**:
1. **Invalid Bot Token**:
   - **Verify**: Token from @BotFather is correct
   - **Format**: `123456789:ABC...`

2. **Wrong Chat ID**:
   - **Verify**: Chat ID is correct
   - **Negative for groups**: Group IDs are negative numbers
   - **Positive for users**: User IDs are positive numbers

3. **Bot Not in Group**:
   - **Solution**: Add bot to group before sending messages

4. **Bot Blocked**:
   - **Check**: User hasn't blocked the bot
   - **Solution**: Unblock bot in Telegram

### Uptime Kuma Not Receiving Heartbeats

**Issues**:
1. **Wrong Push URL**:
   - **Verify**: Base URL and Push Token are correct

2. **Monitor Not Configured as Push**:
   - **Check**: Monitor type in Uptime Kuma is "Push"
   - **Solution**: Create a Push monitor, not other types

3. **Heartbeat Interval Too Long**:
   - **Check**: Interval in AppWatchdog matches Uptime Kuma settings
   - **Solution**: Adjust interval to match

---

## Backup Issues

### Backup Fails to Start

**Common Causes**:

1. **Source Path Doesn't Exist**:
   - **Solution**: Verify path exists and is accessible

2. **Insufficient Permissions**:
   - **Solution**: Service account needs read access to source

3. **Invalid Schedule**:
   - **Check**: Time format is correct (HH:mm)
   - **Check**: At least one day is selected

### Backup Fails During Execution

**Diagnosis**: Check logs and job status

**Common Issues**:

1. **Disk Space**:
   - **Issue**: Not enough space for backup
   - **Solution**: Free up disk space or change target

2. **File Locked**:
   - **Issue**: Source file is locked by another application
   - **Solution**: Close applications, backup during off-hours

3. **Network Issues** (SFTP):
   - **Issue**: Can't connect to SFTP server
   - **Check**: Server address, port, credentials
   - **Solution**: Test SFTP connection with client like FileZilla

4. **SQL Server Backup**:
   - **Issue**: Invalid connection string or permissions
   - **Solution**: Verify connection string, ensure account has backup permissions

5. **Encryption Password**:
   - **Issue**: Invalid password characters
   - **Solution**: Use alphanumeric passwords, avoid special characters

### SFTP Connection Issues

**Diagnosis Steps**:
1. Test connection with SFTP client
2. Check logs for detailed error

**Common Issues**:

1. **Wrong Credentials**:
   - **Verify**: Username, password, host, port

2. **Host Key Mismatch**:
   - **Solution**: Update host key fingerprint
   - **Or**: Leave fingerprint empty to skip verification (less secure)

3. **Firewall**:
   - **Check**: Outbound port 22 (or custom port) is allowed

4. **Server Key Algorithm**:
   - **Issue**: Server uses unsupported key algorithm
   - **Solution**: Update server SSH configuration

### Restore Fails

**Common Issues**:

1. **Backup File Not Found**:
   - **Verify**: Artifact name is correct
   - **Check**: Backup file exists in target location

2. **Wrong Decryption Password**:
   - **Solution**: Use same password as backup encryption

3. **Destination Path Issues**:
   - **Check**: Path exists or can be created
   - **Check**: Permissions to write to destination

4. **Overwrite Protection**:
   - **Issue**: Files exist and overwrite is disabled
   - **Solution**: Enable overwrite or use different destination

---

## UI Issues

### UI Won't Start

**Solutions**:
1. **Run as Administrator**: Required for service communication
2. **Check dependencies**: Ensure all files from release are present
3. **Check logs**: Look for startup errors

### UI is Slow or Unresponsive

**Possible Causes**:
1. **Large Log Files**: Viewing very large logs can be slow
2. **Many Applications**: Monitoring 100+ applications
3. **Network Issues**: If checking many remote endpoints

**Solutions**:
1. Reduce log file size (archive old logs)
2. Disable unused applications
3. Increase check intervals

### Configuration Changes Not Saving

**Check**:
1. **Service Running**: Service must be running to save config
2. **Permissions**: Service needs write access to config file
3. **Validation**: Check for error messages in UI

**Solution**:
- Review configuration for errors
- Check service logs for save failures

### Language Not Changing

**Solution**:
1. Select culture in UI
2. Save configuration
3. **Restart UI** (must restart for language to apply)

---

## Performance Issues

### High CPU Usage

**Diagnosis**:
1. Check which applications are being monitored
2. Review check intervals
3. Check for network issues causing timeouts

**Solutions**:
1. **Increase check intervals**: Reduce check frequency
2. **Reduce monitored apps**: Disable or remove unused checks
3. **Fix timeout issues**: Resolve network or application issues

### High Memory Usage

**Typical Usage**: 50-100 MB

**If Excessive**:
1. Check for memory leaks (report issue)
2. Reduce number of monitored applications
3. Restart service periodically

### Disk Space Issues

**Causes**:
1. **Log Files**: Logs accumulate over time
2. **Backup Files**: Backups consume disk space

**Solutions**:
1. **Delete Old Logs**: Manually delete old log files
2. **Adjust Retention**: Reduce backup retention count
3. **Move Backups**: Use external storage or SFTP

---

## Diagnostic Tools

### Windows Event Viewer
```
Win + R → eventvwr.msc
Navigate to: Windows Logs → Application
Filter by: AppWatchdog
```

### AppWatchdog Logs
```
Location: {InstallDir}\Logs\
Files: YYYY-MM-DD.log
```

### Command Line Service Control
```cmd
# As Administrator
AppWatchdog.Service.exe stop
AppWatchdog.Service.exe start
AppWatchdog.Service.exe restart
```

### Test HTTP Endpoint
```powershell
# PowerShell
Invoke-WebRequest -Uri "http://example.com" -Method GET
```

### Test TCP Port
```powershell
# PowerShell
Test-NetConnection -ComputerName localhost -Port 8080
```

### Check Process
```powershell
# PowerShell
Get-Process -Name "MyApp"
```

### Check Windows Service
```powershell
# PowerShell
Get-Service -Name "MyService"
```

---

## Getting More Help

### Before Reporting an Issue

1. **Check this troubleshooting guide**
2. **Review logs** (AppWatchdog logs and Windows Event Viewer)
3. **Check FAQ** ([FAQ.md](FAQ.md))
4. **Search existing issues** on GitHub

### When Reporting an Issue

Include:
1. **AppWatchdog version**: Check About or release version
2. **Windows version**: Windows 10/11, build number
3. **Problem description**: What happened vs. what you expected
4. **Steps to reproduce**: How to recreate the issue
5. **Logs**: Relevant log excerpts (remove sensitive data)
6. **Configuration**: Relevant config (remove passwords/tokens)
7. **Screenshots**: If UI-related

### Where to Get Help

- **GitHub Issues**: [Report bugs or request features](https://github.com/seisoo/AppWatchdog/issues)
- **FAQ**: [Frequently Asked Questions](FAQ.md)
- **Documentation**: Review wiki pages

---

[← Back to Home](Home.md)
