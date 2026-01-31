# Installation Guide

This guide provides detailed instructions for installing and setting up AppWatchdog on Windows.

## System Requirements

### Operating System
- Windows 10 (1809 or later)
- Windows 11
- Windows Server 2019 or later

### Hardware
- CPU: x64 architecture
- RAM: 100 MB minimum (more depending on monitored applications)
- Disk: 50 MB for application files, additional space for logs and backups

### Permissions
- **Administrator privileges** are required for:
  - Installing the Windows Service
  - Monitoring and restarting other services
  - Some monitoring operations

### Software
- No .NET runtime installation required (self-contained builds)

## Installation Methods

### Method 1: Standard Installation (Recommended)

#### 1. Download

1. Visit the [Releases page](https://github.com/seisoo/AppWatchdog/releases)
2. Download the latest release archive
3. Verify the download (optional but recommended)

#### 2. Extract Files

1. Create a permanent installation directory:
   ```
   C:\Program Files\AppWatchdog\
   ```
   Or any directory of your choice (e.g., `C:\Tools\AppWatchdog\`)

2. Extract the archive to this directory
3. You should see:
   - `AppWatchdog.Service.exe`
   - `AppWatchdog.UI.WPF.exe`

**Important**: Do not move these files after installation, as the service registers its path.

#### 3. Install the Service

**Option A: Using the UI (Easiest)**
1. Right-click `AppWatchdog.UI.WPF.exe`
2. Select **Run as Administrator**
3. Navigate to the **Service** tab
4. Click **Install Service**
5. Wait for confirmation
6. Click **Start Service**

**Option B: Using Command Line**
```cmd
# Open Command Prompt as Administrator
cd C:\Program Files\AppWatchdog

# Install the service
AppWatchdog.Service.exe install

# Start the service
AppWatchdog.Service.exe start
```

#### 4. Verify Installation

1. Open Services (Win + R, type `services.msc`)
2. Look for "AppWatchdog Service"
3. Verify it's running
4. Check startup type is "Automatic" (if you want it to start with Windows)

### Method 2: Portable Installation

If you prefer a portable setup:

1. Extract files to a USB drive or portable location
2. Always run `AppWatchdog.UI.WPF.exe` as Administrator
3. Use the UI to install/start the service when needed
4. Uninstall the service before moving the files

## Service Configuration

### Startup Type

Set the service to start automatically:

1. Open `services.msc`
2. Right-click "AppWatchdog Service"
3. Select **Properties**
4. Set **Startup type** to **Automatic**
5. Click **OK**

### Service Account

By default, the service runs as Local System. To change:

1. In `services.msc`, right-click "AppWatchdog Service"
2. Select **Properties** → **Log On** tab
3. Choose an account with appropriate permissions
4. Click **OK**
5. Restart the service

## Post-Installation Setup

### 1. Configure Applications

See [Configuration Guide](Configuration.md) for detailed steps:
- Add applications to monitor
- Set check intervals
- Enable restart options

### 2. Set Up Notifications (Optional)

Configure notification channels:
- Email (SMTP)
- ntfy
- Discord webhooks
- Telegram bots
- Uptime Kuma

### 3. Configure Backups (Optional)

Set up automated backups:
- Define backup plans
- Configure retention policies
- Set encryption passwords

### 4. Adjust UI Language (Optional)

Change the interface language:
1. Open the UI
2. Go to **Settings** or **Notifications** tab
3. Select your preferred language from the culture dropdown

## Upgrading

To upgrade AppWatchdog to a newer version:

### Upgrade Steps

1. **Stop the service**:
   - Via UI: Click **Stop Service** in the Service tab
   - Via Command Line: `AppWatchdog.Service.exe stop`

2. **Backup configuration** (optional but recommended):
   - Configuration is stored in the service directory
   - Copy `config.json` or use the UI's export feature

3. **Replace files**:
   - Extract the new version over the old files
   - Or delete old files and extract new ones

4. **Restart the service**:
   - Via UI: Click **Start Service**
   - Via Command Line: `AppWatchdog.Service.exe start`

5. **Verify**:
   - Check that all applications are still monitored
   - Test notifications if configured

### Configuration Migration

- Configuration files are forward-compatible
- New features may require additional setup
- Check release notes for breaking changes

## Uninstallation

To remove AppWatchdog:

### Complete Removal

1. **Stop and uninstall the service**:
   - Via UI: Click **Stop Service**, then **Uninstall Service**
   - Via Command Line:
     ```cmd
     AppWatchdog.Service.exe stop
     AppWatchdog.Service.exe uninstall
     ```

2. **Delete application files**:
   - Remove the installation directory
   - Example: Delete `C:\Program Files\AppWatchdog\`

3. **Remove data (optional)**:
   - Configuration and logs are stored in the installation directory
   - If you want a clean slate, delete all files
   - To preserve configuration, backup `config.json` first

### Keep Configuration for Reinstall

If you plan to reinstall later:
1. Uninstall the service (step 1 above)
2. Keep the installation directory
3. Reinstall when ready - configuration will be preserved

## Installation Troubleshooting

### Service Won't Install

**Problem**: "Access denied" or "Insufficient permissions"
- **Solution**: Run as Administrator

**Problem**: "Service already exists"
- **Solution**: Uninstall the old service first

### Service Won't Start

**Problem**: Service starts then stops immediately
- **Solution**: 
  - Check Windows Event Viewer for errors
  - Verify `AppWatchdog.Service.exe` is in the correct location
  - Ensure configuration file is valid JSON

### UI Can't Connect to Service

**Problem**: UI shows "Service not connected"
- **Solution**:
  - Verify service is running (`services.msc`)
  - Check that Named Pipes are not blocked by security software
  - Restart both service and UI

### Port Conflicts

AppWatchdog uses Named Pipes (not TCP ports), so port conflicts are rare. If you experience IPC issues:
- Check Windows Firewall settings
- Verify antivirus is not blocking Named Pipes
- Ensure no other software is using the same Named Pipe name

## Command Line Reference

The service executable supports these commands:

```cmd
# Install the service
AppWatchdog.Service.exe install

# Start the service
AppWatchdog.Service.exe start

# Stop the service
AppWatchdog.Service.exe stop

# Restart the service
AppWatchdog.Service.exe restart

# Uninstall the service
AppWatchdog.Service.exe uninstall

# Run interactively (for debugging)
AppWatchdog.Service.exe
```

## Next Steps

- [Configuration Guide](Configuration.md) - Configure monitoring and notifications
- [Features Overview](Features.md) - Learn about all features
- [Getting Started](Getting-Started.md) - Quick start guide

---

[← Back to Home](Home.md)
