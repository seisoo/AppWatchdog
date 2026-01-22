![AppWatchdog Banner](https://repository-images.githubusercontent.com/1137178517/5aff3f7d-291a-4316-a3ab-aa8dcb3bd138)

# 🛡️ AppWatchdog
**Windows Application & Service Watchdog**

AppWatchdog is a modern, lightweight **Windows watchdog system** consisting of a **Windows Service** and a **WPF UI**.  
It continuously monitors applications, detects failures reliably, and performs **automatic recovery** with detailed status tracking.

---

## ✨ Key Features
- 🔍 **Job-based monitoring engine** (snapshot, health, recovery, heartbeat)
- 🔁 Automatic restart with exponential backoff & failure analysis
- 🧠 Multi-stage failure detection (confirm checks, recovery states)
- 🛠 Windows Service + WPF UI (session-independent, admin-controlled)
- 🔔 Notifications: SMTP · ntfy · Discord · Telegram · **Uptime Kuma (per-app heartbeat)**
- 📊 Live service snapshots & job status overview in UI
- 📜 Structured file logging with built-in log viewer
- 🔐 Secure Named Pipes IPC (versioned, validated)
- 🔒 Encrypted secrets (SMTP, tokens, webhooks)
- 🌍 Multi-language UI (auto-detect OS language, manually switchable)

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

**Core idea:**  
Everything in the service runs as a **job**:
- Snapshot jobs (system & app state)
- Health monitor jobs (per application)
- Recovery jobs (restart & backoff)
- Kuma ping jobs (heartbeat per app)

This keeps the service robust, extensible, and easy to reason about.

---

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
**Requirements**
- Windows 10 / 11 (x64 · x86 · ARM64)
- Administrator privileges

**Steps**
1. Download the latest release
2. Extract:
   - `AppWatchdog.Service.exe`
   - `AppWatchdog.UI.WPF.exe`
3. Start the UI
4. Install & start the service
5. Configure applications and notifications

> ✔️ Builds are **self-contained** – no .NET runtime required

---

## ⚙️ Configuration
All configuration is done via the **UI**:

- Application executable & arguments
- Enable / disable monitoring per app
- Check intervals & notification limits
- Notifications:
  - SMTP (mail)
  - ntfy
  - Discord
  - Telegram
  - Uptime Kuma (heartbeat per application)
- UI language selection

🔒 **Sensitive data is encrypted at rest** and transparently decrypted in the UI.

---

## 🧭 Roadmap
- ~~Job-based service architecture~~ ✔️
- ~~Multi-language UI~~ ✔️
- ~~Encrypted configuration~~ ✔️
- ~~Telegram & Discord notifications~~ ✔️
- ~~Website / HTTP health checks~~ ✔️
- Service dependency monitoring
- More automation & recovery strategies

---

## 🤝 Support & Feedback
Found a bug, have an idea, or want to contribute?  
Please open an **issue** in this repository.

Feedback, testing, and pull requests are very welcome ❤️

---

## 📄 License
MIT License  
*(LICENSE file pending)*

---

## 📌 Project Status
Early-access, **actively developed**.  
Windows-only by design for deep OS & session integration.
