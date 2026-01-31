# Getting Started with AppWatchdog

This guide will help you get AppWatchdog up and running in just a few minutes.

## Prerequisites

Before you begin, ensure you have:
- Windows 10 or Windows 11
- Administrator privileges on your machine
- A downloaded release of AppWatchdog (both `.exe` files)

## Quick Start (5 Minutes)

### Step 1: Download and Extract

1. Download the latest release from the [Releases page](https://github.com/seisoo/AppWatchdog/releases)
2. Extract the archive to a folder of your choice (e.g., `C:\AppWatchdog\`)
3. You should have two files:
   - `AppWatchdog.Service.exe` - The Windows Service
   - `AppWatchdog.UI.WPF.exe` - The User Interface

### Step 2: Launch the UI

1. Right-click `AppWatchdog.UI.WPF.exe` and select **Run as Administrator**
2. The main window will open

### Step 3: Install the Service

1. In the UI, navigate to the **Service** tab
2. Click the **Install Service** button
3. Wait for the installation to complete
4. Click the **Start Service** button
5. The service status should show as "Running"

### Step 4: Add Your First Application

1. Navigate to the **Applications** tab
2. Click **Add New Application**
3. Fill in the details:
   - **Name**: A friendly name (e.g., "My Web App")
   - **Type**: Select the monitoring type:
     - **Executable**: Monitor a process
     - **Windows Service**: Monitor a Windows service
     - **HTTP**: Monitor an HTTP endpoint
     - **TCP**: Monitor a TCP port
   - Configure type-specific settings (path, URL, service name, etc.)
   - **Check Interval**: How often to check (in seconds, default: 60)
   - **Enable Monitoring**: Check this box
   - **Enable Restart**: Allow automatic restart on failure
4. Click **Save**

### Step 5: Monitor Status

1. The application will now be monitored
2. View real-time status in the **Applications** tab
3. Check the **Jobs** tab to see active monitoring jobs
4. Review logs in the **Logs** tab

## What's Next?

Now that you have AppWatchdog running, explore these topics:

- **[Configuration Guide](Configuration.md)** - Set up notifications, backups, and advanced features
- **[Features Overview](Features.md)** - Learn about all available features
- **[Troubleshooting](Troubleshooting.md)** - Solutions to common problems

## Example: Monitoring a Web Server

Here's a quick example of monitoring an HTTP endpoint:

1. **Add Application**:
   - Name: "Company Website"
   - Type: HTTP
   - URL: `https://example.com`
   - Expected Status Code: 200
   - Check Interval: 300 (5 minutes)
   - Enable Monitoring: ✓
   - Enable Restart: ✗ (can't restart external services)

2. **Set Up Notifications** (Optional):
   - Go to the **Notifications** tab
   - Enable and configure your preferred notification method (e.g., Email, Discord)
   - You'll be notified when the site goes down or comes back up

3. **Monitor**:
   - Check the **Applications** tab for real-time status
   - The "Ping" column shows response time in milliseconds
   - Any failures will be logged and trigger notifications

## Common First-Time Questions

**Q: Do I need to keep the UI open?**  
A: No, the Windows Service runs in the background. Close the UI anytime - monitoring continues.

**Q: What happens if my computer restarts?**  
A: The Windows Service starts automatically with Windows (if configured to do so).

**Q: Can I monitor multiple applications?**  
A: Yes! Add as many applications as you need.

**Q: Where are logs stored?**  
A: Logs are stored in the service's data directory. Use the **Logs** tab in the UI to view them.

## Need Help?

- Check the [FAQ](FAQ.md) for answers to common questions
- Review [Troubleshooting](Troubleshooting.md) if something isn't working
- [Open an issue](https://github.com/seisoo/AppWatchdog/issues) for bugs or feature requests

---

[← Back to Home](Home.md) | [Next: Installation Guide →](Installation.md)
