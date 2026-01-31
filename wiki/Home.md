# AppWatchdog Wiki

![AppWatchdog Banner](https://repository-images.githubusercontent.com/1137178517/5aff3f7d-291a-4316-a3ab-aa8dcb3bd138)

Welcome to the AppWatchdog wiki! This comprehensive guide will help you understand, install, configure, and use AppWatchdog effectively.

## What is AppWatchdog?

AppWatchdog is a Windows watchdog application consisting of a Windows Service and a WPF UI. It monitors applications, services, and endpoints, automatically restarting them when issues are detected. It also provides backup/restore capabilities, system monitoring, and multi-channel notifications.

## Quick Links

- **[Getting Started](Getting-Started.md)** - Quick start guide for new users
- **[Installation](Installation.md)** - Detailed installation instructions
- **[Configuration](Configuration.md)** - How to configure monitoring and notifications
- **[Features](Features.md)** - Complete feature overview
- **[Architecture](Architecture.md)** - System architecture and design
- **[Troubleshooting](Troubleshooting.md)** - Common issues and solutions
- **[API Documentation](API-Documentation.md)** - IPC protocol and API reference
- **[Contributing](Contributing.md)** - How to contribute to the project
- **[FAQ](FAQ.md)** - Frequently asked questions

## Key Features

- ✅ **Multi-Type Monitoring**: Process, Windows Service, HTTP, and TCP checks
- 🔄 **Automatic Recovery**: Restart failed applications with retry and backoff
- 🖥️ **Windows Service + WPF UI**: Background monitoring with user-friendly interface
- 📢 **Multiple Notification Channels**: SMTP, ntfy, Discord, Telegram, Uptime Kuma
- 📦 **Backup & Restore**: Scheduled backups with encryption, compression, and SFTP support
- 📊 **System Monitoring**: Real-time system info, logs, and job status
- 🔒 **Security**: Encrypted configuration values, secure IPC
- 🌍 **Multi-Language**: Internationalization support
- 🚀 **Self-Contained**: No .NET runtime required

## System Requirements

- **Operating System**: Windows 10 or Windows 11
- **Privileges**: Administrator rights required for service installation
- **Architecture**: x64 (self-contained builds included)

## Project Structure

```
AppWatchdog/
├── AppWatchdog.Service/      # Background Windows Service
├── AppWatchdog.UI.WPF/       # WPF User Interface
├── AppWatchdog.Shared/       # Shared models and utilities
└── wiki/                     # Documentation (you are here!)
```

## Getting Help

- 📖 Browse the wiki pages for detailed information
- 🐛 [Report bugs or request features](https://github.com/seisoo/AppWatchdog/issues)
- 💬 Check the [FAQ](FAQ.md) for common questions

## License

AppWatchdog is released under the MIT License.

---

*Last updated: 2026-01-31*
