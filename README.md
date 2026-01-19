# 🛡️ AppWatchdog  
**Windows Application & Service Watchdog**

![overview](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_service.png?raw=true)

**AppWatchdog** is a Windows watchdog solution consisting of a **Windows Service** and a **WPF-based user interface**.

It monitors configured applications, detects failures reliably, and performs automatic recovery actions such as restarts.  
Additional features include notifications, logging, and basic self-healing capabilities.

---

## ✨ Features

- 🔍 **Process Monitoring**
  - Multi-step down detection to avoid false positives
  - Time-based confirmation before recovery actions
- 🔁 **Automatic Recovery**
  - Application restarts
  - Retry handling with backoff strategy
- 🛠 **Windows Service + WPF UI**
  - Service runs independently of user sessions
  - UI for configuration, control, and diagnostics
- 🔔 **Notifications**
  - SMTP (email)
  - ntfy
  - Optional: Uptime Kuma push monitoring
- 📜 **Logging**
  - Structured log files
  - Integrated log viewer in the UI
- 🔐 **IPC via Named Pipes**
  - Versioned protocol
  - Timeout and compatibility validation
- 🧠 **Self-Healing**
  - Detects missing or incompatible service versions
  - Repair or reinstall directly from the UI

---

## 🧩 Architecture

```
┌──────────────────────┐
│ AppWatchdog.UI.WPF │ ← Configuration & Monitoring (WPF)
└──────────▲───────────┘
│ Named Pipes (IPC)
┌──────────┴───────────┐
│ AppWatchdog.Service │ ← Windows Service (Watchdog Engine)
└──────────────────────┘
```

### Components

- **AppWatchdog.Service**
  - Windows Service
  - Executes monitoring logic
  - Restarts applications and records status
- **AppWatchdog.UI.WPF**
  - MVVM-based WPF application
  - Configuration of monitored applications
  - Displays status, logs, and notification settings
- **IPC (Named Pipes)**
  - Versioned and fault-tolerant
  - Protection against protocol mismatches and timeouts

---

## 🖥️ Screenshots

### Service Management
![Service Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_service.png?raw=true)

### Application Monitoring
![Apps Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_apps.png?raw=true)

### Notifications
![Notifications Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_notifications.png?raw=true)

### Logs
![Logs Page](https://github.com/seisoo/AppWatchdog/blob/master/AppWatchdog.UI.WPF/README.md.Images/md_logs.png?raw=true)

---

## 🚀 Installation

### Requirements

- Windows 10 / 11 (x64)
- **Administrator privileges** (required for service installation)

> ℹ️ All provided builds are **self-contained**  
> → No separate .NET runtime installation required

---

### Steps

1. Download the appropriate release
2. Extract both executables into the same directory:
   - `AppWatchdog.Service.exe`
   - `AppWatchdog.UI.WPF.exe`
3. Start **AppWatchdog.UI.WPF.exe**
4. Install and start the service via the UI
5. Configure the applications to monitor
6. Configure notifications (optional)

---

## ⚙️ Configuration

Configuration is handled via the UI:

- Executable path
- Command-line arguments
- Enable/disable monitoring
- Notification settings:
  - SMTP (host, port, credentials, TLS)
  - ntfy server and topic

Logs are stored **locally on disk**.

> 🔒 **Note**  
> Credentials are stored locally.  
> For production systems, access to configuration files should be restricted appropriately.

---

## 🧪 Build & Releases

- Builds are created using **GitHub Actions**
- Target platform:
  - **Windows x64**
- Service and UI are distributed as **separate, self-contained single executables**
- No runtime dependencies

---

## 🔐 Security

- No external network communication unless explicitly configured
- IPC communication is validated and versioned
- Service runs with only the required privileges

Please report security-related issues **privately** and not via public issue trackers.

---

## 📄 License

This project is licensed under the **MIT License**.  
See [LICENSE](LICENSE) for details.

---

## 📌 Project Status

**AppWatchdog is early-access and not production-ready**, but under active development.

The project is intentionally focused on **Windows systems** to allow deep integration with:

- Windows Service Control Manager
- Event Logs
- Desktop and server environments
