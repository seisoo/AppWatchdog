# Frequently Asked Questions (FAQ)

Quick answers to common questions about AppWatchdog.

## General Questions

### What is AppWatchdog?

AppWatchdog is a Windows application monitoring tool that consists of a Windows Service and a WPF UI. It monitors applications, services, and endpoints, automatically restarting them when failures are detected.

### Is AppWatchdog free?

Yes, AppWatchdog is open-source and released under the MIT License, which means it's free to use, modify, and distribute.

### What operating systems are supported?

AppWatchdog is **Windows-only** and supports:
- Windows 10 (version 1809 or later)
- Windows 11
- Windows Server 2019 or later

### Do I need to install .NET Runtime?

No, AppWatchdog releases are **self-contained**, meaning the .NET runtime is included. You don't need to install anything else.

### Can I run AppWatchdog on Linux or macOS?

No, AppWatchdog uses Windows-specific features (Windows Services, Named Pipes, WPF UI) and cannot run on other operating systems.

---

## Installation & Setup

### Do I need Administrator rights?

**For installation**: Yes, installing the Windows Service requires Administrator privileges.

**For running**: 
- The UI should be run as Administrator to communicate with the service
- The service runs with the configured service account (default: Local System)

### Where should I install AppWatchdog?

You can install anywhere, but common locations are:
- `C:\Program Files\AppWatchdog\`
- `C:\Tools\AppWatchdog\`
- Any permanent directory (don't use temporary folders)

**Important**: Don't move files after installing the service.

### Can I move the files after installation?

Not easily. The service registers its installation path. If you need to move:
1. Uninstall the service
2. Move the files
3. Reinstall the service

### Do I need to keep the UI open?

No! The Windows Service runs in the background. You can close the UI anytime - monitoring continues.

---

## Monitoring

### How many applications can I monitor?

There's no hard limit, but practical limits depend on:
- Check intervals
- System resources
- Network capacity (for HTTP/TCP checks)

Dozens of applications should work fine on typical hardware.

### What happens if my computer restarts?

If the Windows Service is set to start automatically (default):
- Service starts with Windows
- Monitoring resumes automatically
- No intervention needed

### Can I monitor applications on other computers?

**Yes and No**:
- ✅ **HTTP/TCP**: Can monitor remote endpoints (URLs, IP addresses)
- ❌ **Process/Service**: Can only monitor local applications

Future versions may add remote monitoring capabilities.

### How quickly does AppWatchdog detect failures?

Detection time = Check Interval
- If check interval is 60 seconds, detection within ~60 seconds
- Shorter intervals = faster detection but more resource usage

### Why is my HTTP check marked as down?

Common reasons:
- URL is incorrect or inaccessible
- Expected status code doesn't match actual
- Timeout (slow response)
- SSL certificate issues
- Firewall blocking

**Debug**: Try accessing the URL from a browser on the same machine.

### Can I monitor a website that requires authentication?

Currently, basic HTTP checks don't support authentication headers. Workarounds:
- Use a health check endpoint that doesn't require auth
- Future enhancement: Custom headers support

---

## Restart & Recovery

### Why isn't my application restarting?

Check:
1. **"Enable Restart" is checked**: Must be enabled
2. **"Enable Monitoring" is checked**: Must be enabled
3. **Permissions**: Service needs permission to start the application
4. **Path/Arguments**: Verify executable path and arguments are correct
5. **Application itself**: Does it start successfully when launched manually?

### How often does AppWatchdog try to restart?

Restart attempts use exponential backoff:
- 1st attempt: Immediate
- 2nd attempt: ~10 seconds
- 3rd attempt: ~30 seconds
- 4th attempt: ~1 minute
- 5th+ attempts: ~5 minutes

This prevents rapid restart loops.

### Can I restart an application manually?

Not directly from the UI (currently). You can:
- Disable and re-enable monitoring to trigger a fresh check
- Use Windows Services console for Windows Services
- Start the application manually

### What if an application keeps failing?

AppWatchdog will keep trying to restart it (with backoff delays). Check:
- Application logs for the root cause
- Why the application keeps failing
- Dependencies that may be unavailable

---

## Notifications

### How do I test if notifications are working?

1. Configure notification settings
2. Temporarily disable a monitored application
3. Wait for check interval to pass
4. You should receive a "DOWN" notification
5. Re-enable the application
6. You should receive an "UP" notification

### Why am I not receiving email notifications?

Common issues:
- SMTP settings incorrect (server, port, SSL)
- Authentication failure (wrong username/password)
- Gmail: Must use App Password, not regular password
- Firewall blocking outbound SMTP
- Rate limiting (check `MailIntervalHours`)

### Can I send notifications to multiple recipients?

**Email**: Use comma-separated email addresses in the "To" field (check if your SMTP server supports this, some may not)

**Other channels**: 
- Discord: Use multiple webhooks (not currently supported natively)
- Telegram: Send to group chat (all members receive)
- ntfy: Multiple devices can subscribe to same topic

### How often are notifications sent?

- **Email**: Respects `MailIntervalHours` (default: 12 hours minimum between emails)
- **Discord, Telegram, ntfy**: Immediate (every state change)
- **Uptime Kuma**: Periodic heartbeats per configured interval

### Can I customize notification messages?

Yes! Edit notification templates in the **Notifications** tab:
- UP template (application recovered)
- DOWN template (application failed)
- RESTART template (application being restarted)

Use variables like `$jobname`, `$target`, `$status`, etc.

---

## Backups

### Where are backups stored?

Depends on configuration:
- **Local**: Specified directory (can be local disk or network share)
- **SFTP**: Remote server in specified directory

### Are backups encrypted?

Yes, if you enable encryption in the backup plan:
- Uses AES encryption
- Password-protected
- PBKDF2 key derivation

**Important**: Remember your encryption password - it's needed for restore!

### Are backups compressed?

Yes, if you enable compression in the backup plan (enabled by default):
- Adjustable compression level (0-9)
- Reduces storage space
- Slightly increases backup time

### How many backups are kept?

Defined by retention policy:
- Default: Keep last 14 backups
- Configurable per backup plan
- Older backups automatically deleted

### Can I backup a running SQL Server database?

Yes! AppWatchdog supports MS SQL Server backups:
- Configure connection string
- Specify database name
- Backup runs using SQL Server backup functionality

### What if I forget my encryption password?

**Unfortunately**: Encrypted backups cannot be recovered without the password. The encryption is designed to be secure, which means no backdoor.

**Prevention**: Store passwords securely (password manager, secure documentation).

---

## Performance

### How much RAM does AppWatchdog use?

Typical usage: **50-100 MB**

Depends on:
- Number of monitored applications
- Number of backup plans
- Frequency of operations

### Does AppWatchdog slow down my computer?

No, it's designed to be lightweight:
- Low CPU usage (mostly idle, brief spikes during checks)
- Small memory footprint
- Efficient I/O operations
- Minimal impact on system performance

### Can I reduce resource usage?

Yes:
- **Increase check intervals**: Check less frequently
- **Reduce monitored applications**: Only monitor what's necessary
- **Disable unused features**: Don't run backup jobs if not needed

---

## Configuration

### Where is configuration stored?

Configuration is stored in `config.json` in the service installation directory.

### Can I edit the configuration file manually?

Yes, but:
- **Stop the service first**: Changes are loaded on startup
- **Valid JSON required**: Syntax errors prevent service from starting
- **Encrypted values**: Passwords/tokens are encrypted, you'll see encrypted strings
- **Backup first**: Keep a backup of working config

**Recommendation**: Use the UI to edit configuration.

### Can I backup/restore my configuration?

Yes:
- **Export**: Use UI export feature (if available) or copy `config.json`
- **Import**: Use UI import feature or replace `config.json` and restart service

**Note**: Encrypted values are machine-specific (won't work on different machine).

### How do I reset configuration?

1. Stop the service
2. Delete or rename `config.json`
3. Start the service (creates new default config)
4. Reconfigure via UI

---

## Troubleshooting

### Where can I find logs?

Logs are stored in the `Logs` subdirectory of the installation folder:
- One log file per day
- Format: `YYYY-MM-DD.log`
- View in UI (Logs tab) or any text editor

### The service starts but nothing happens

Check:
1. Service is actually running (`services.msc`)
2. No errors in logs or Event Viewer
3. Applications are enabled in configuration
4. Check intervals are reasonable (not extremely long)

### UI says "Service not connected"

Solutions:
1. Verify service is running
2. Run UI as Administrator
3. Restart both service and UI
4. Check for antivirus blocking Named Pipes

### I found a bug, what should I do?

1. Check [Troubleshooting Guide](Troubleshooting.md)
2. Check if already reported in [GitHub Issues](https://github.com/seisoo/AppWatchdog/issues)
3. If not reported, [create a new issue](https://github.com/seisoo/AppWatchdog/issues/new) with:
   - Description
   - Steps to reproduce
   - Your environment
   - Relevant logs

---

## Advanced

### Can I run multiple instances of AppWatchdog?

Not easily - the service name and Named Pipe name would conflict. You'd need to:
- Modify source code
- Build with different names
- Not officially supported

### Can I extend AppWatchdog with plugins?

Not currently. AppWatchdog is not designed as a plugin architecture. To extend:
- Fork the repository
- Add your features
- Build custom version
- Consider contributing back!

### Is there an API?

Yes, internal IPC API via Named Pipes. See [API Documentation](API-Documentation.md).

**No REST API** currently, but could be added in future.

### Can I monitor AppWatchdog itself?

Yes! Use Uptime Kuma heartbeat:
- Configure per-application Uptime Kuma settings
- Uptime Kuma monitors the heartbeat
- If AppWatchdog fails, heartbeat stops, Uptime Kuma alerts

### Does AppWatchdog work with Docker/containers?

No, AppWatchdog is designed for Windows native applications. For containers:
- Use container orchestration health checks (Kubernetes, Docker Swarm)
- Or monitor container HTTP/TCP endpoints with AppWatchdog

---

## Feature Requests

### Will feature X be added?

Check:
1. [GitHub Issues](https://github.com/seisoo/AppWatchdog/issues) - may already be planned
2. README Roadmap section - lists planned features

If not listed, [create a feature request](https://github.com/seisoo/AppWatchdog/issues/new)!

### How can I request a feature?

1. Go to [GitHub Issues](https://github.com/seisoo/AppWatchdog/issues)
2. Click "New Issue"
3. Describe:
   - What you want
   - Why it's useful
   - How it should work
   - Use cases

### Can I contribute code?

Absolutely! See [Contributing Guide](Contributing.md) for how to get started.

---

## Support

### Where can I get help?

- **Documentation**: This wiki
- **Troubleshooting**: [Troubleshooting Guide](Troubleshooting.md)
- **Issues**: [GitHub Issues](https://github.com/seisoo/AppWatchdog/issues)
- **Discussions**: GitHub Discussions (if available)

### Is there a community?

The project is open-source with an active GitHub repository:
- Report issues
- Request features
- Contribute code
- Help others

### Is commercial support available?

Not officially. AppWatchdog is a community-driven open-source project.

---

## Still Have Questions?

If your question isn't answered here:
1. Check other wiki pages (especially [Troubleshooting](Troubleshooting.md))
2. Search [GitHub Issues](https://github.com/seisoo/AppWatchdog/issues)
3. [Open a new issue](https://github.com/seisoo/AppWatchdog/issues/new) with your question

---

[← Back to Home](Home.md)
