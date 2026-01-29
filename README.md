![AppWatchdog Banner](https://repository-images.githubusercontent.com/1137178517/5aff3f7d-291a-4316-a3ab-aa8dcb3bd138)

# AppWatchdog
Windows application and service watchdog.

AppWatchdog is a Windows watchdog made of a Windows Service and a WPF UI. It monitors apps and services, checks endpoints, and can restart targets when needed.

---

## Features
- Process, Windows service, TCP, and HTTP checks
- Automatic restart with retry and backoff
- Windows Service + WPF UI
- Notifications: SMTP, ntfy, Discord, Telegram, Uptime Kuma
- Job-based service (health checks, snapshots, heartbeats)
- Live status and job list in the UI
- File-based logs with a log viewer
- Named Pipes IPC (versioned)
- Encrypted configuration values
- Multi-language UI
- Backup and restore plans (local or SFTP targets)
- Backup scheduling, retention, compression, and encryption
- Manual backup/restore triggers from the UI
- Snapshot-based system info (CPU, memory, uptime)
- Service control from the UI (install/start/stop/reinstall)
- Single-file, self-contained builds

---

## Architecture
```
┌────────────────────────┐
│ AppWatchdog.UI.WPF     │ ← Configuration, Live Status, Logs
└───────────▲────────────┘
│ Named Pipes (IPC, versioned)
┌───────────┴────────────┐
│ AppWatchdog.Service    │
│ - Job Scheduler        │
│ - Health Monitoring    │
│ - Recovery Engine      │
│ - Notifications        │
└────────────────────────┘
```

## Screenshots

<table width="100%" border="0">
  <tr>
    <td align="center" width="50%">
      <b>Service</b><br>---
      <img src="https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/main/AppWatchdog.UI.WPF/README.md.Images/md_service.png" width="100%">
    </td>
    <td align="center" width="50%">
      <b>Applications</b><br>---
      <img src="https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_apps.png" width="100%">
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <b>Jobs</b><br>---
      <img src="https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_jobs.png" width="100%">
    </td>
    <td align="center" width="50%">
      <b>Notifications</b><br>---
      <img src="https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_notifications.png" width="100%">
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <b>Logs</b><br>---
      <img src="https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_logs.png" width="100%">
    </td>
    <td align="right" valign="bottom">
      <img
        src="https://external-preview.redd.it/OwJoR0j5OOJSRTGMbLnfC0_p2Qp8pfxIfw72vTJh0YQ.png?auto=webp&s=5a6773c58d5217824b968baefea338d51a24d6b8"
        width="150"
      />
    </td>
  </tr>
</table>

---

## Installation
- Windows 10 / 11
- Administrator rights required

**Steps**
1. Download a release
2. Extract:
   - `AppWatchdog.Service.exe`
   - `AppWatchdog.UI.WPF.exe`
3. Start the UI
4. Install and start the service
5. Configure applications

> Builds are self-contained (no .NET runtime needed)

---

## Configuration
Configured via the UI:

- Application path and arguments
- Enable/disable monitoring
- Check interval
- Notifications (SMTP, ntfy, Discord, Telegram)
- Uptime Kuma heartbeat
- UI language

Logs are stored locally.

---

## Roadmap
- Multi-language UI
- Encryption
- Telegram and Discord notifications
- HTTP/TCP checks
- More recovery options

---

## Support
If you find a bug or have a suggestion, please open an issue.

---

## License
MIT License  
*(LICENSE file pending)*

---

## Status
Early access, under development.  
Windows-only.

Thanks for using AppWatchdog. Feedback and contributions are welcome.<br>
<img src="https://i.imgur.com/WXDHQi0.gif" />
