![AppWatchdog Banner](https://repository-images.githubusercontent.com/1137178517/5aff3f7d-291a-4316-a3ab-aa8dcb3bd138)

# 🛡️ AppWatchdog
**Windows Application & Service Watchdog**

AppWatchdog is a lightweight **Windows watchdog** consisting of a **Windows Service** and a **WPF UI**.  
It monitors applications, detects failures reliably, and performs automatic recovery.

--- 

## ✨ Features
- 🔍 Process monitoring with multi-step failure detection
- 🔁 Automatic application restart with retry logic
- 🛠 Windows Service + WPF UI (session-independent)
- 🔔 Notifications: SMTP · ntfy · Discord · Telegram · Uptime Kuma (heartbeat)
- 📜 Structured logging with UI log viewer
- 🔐 Named Pipes IPC (versioned & validated)
- 🧠 Self-healing service detection and repair

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

## 🖥️ Screenshots
**Service**
![Service](https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/main/AppWatchdog.UI.WPF/README.md.Images/md_service.png)

**Applications**
![Apps](https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_apps.png)

**Jobs**
![Jobs](https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_jobs.png)

**Notifications**
![Notifications](https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_notifications.png)

**Logs**
![Logs](https://raw.githubusercontent.com/seisoo/AppWatchdog/refs/heads/master/AppWatchdog.UI.WPF/README.md.Images/md_logs.png)

---

## 🚀 Installation
- Windows 10 / 11 (x64 · x86 · ARM64)
- Administrator privileges required

**Steps**
1. Download release
2. Extract:
   - `AppWatchdog.Service.exe`
   - `AppWatchdog.UI.WPF.exe`
3. Start UI → install & start service → configure apps

> Builds are **self-contained** (no .NET runtime required)

---

## ⚙️ Configuration
Configured entirely via the UI:

- Executable & arguments
- Enable/disable monitoring
- Notifications (SMTP, ntfy, Discord webhooks, Telegram Bot)
- Uptime Kuma heartbeat

Logs are stored locally.

---

## 🧭 Roadmap
- ~~Multi-language UI~~ ✔️
- ~~Encryption~~ ✔️
- ~~Telegram & Discord notifications~~ ✔️
- Service & website checks
- more? idk

---

## Support & Contact

If you find a bug, have a question, or would like to suggest an improvement, please create an **issue** in this repository.  
Alternatively, you can contact me directly. Feedback and contributions are always welcome!


---

## 📄 License
MIT License  
*(LICENSE file pending)*

---

## 📌 Status
Early-access, under active development.  
Windows-only by design for deep OS integration.

