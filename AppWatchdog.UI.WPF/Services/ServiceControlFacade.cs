using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AppWatchdog.UI.WPF.Services;

public sealed class ServiceControlFacade
{
    private readonly string _serviceName;
    private const string InstallDirectory = @"C:\AppWatchdog\Service";
    private const string ServiceExeName = "AppWatchdog.Service.exe";

    public ServiceControlFacade(string serviceName)
    {
        _serviceName = serviceName;
    }

    public string GetServiceExecutablePath()
        => Path.Combine(InstallDirectory, ServiceExeName);

    public string GetInstallDirectory()
        => InstallDirectory;

    public string GetServiceVersion()
    {
        try
        {
            var exe = GetServiceExecutablePath();
            if (!File.Exists(exe))
                return "-";

            var info = FileVersionInfo.GetVersionInfo(exe);
            return string.IsNullOrWhiteSpace(info.FileVersion) ? "-" : info.FileVersion;
        }
        catch
        {
            return "-";
        }
    }

    public string GetServiceStatusText()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {_serviceName}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();

            if (output.Contains("RUNNING")) return "Running";
            if (output.Contains("STOPPED")) return "Stopped";
            return "Unbekannt";
        }
        catch
        {
            return "Nicht installiert";
        }
    }


    public void StartService()
        => RunElevated($"start {_serviceName}");

    public void StopService()
        => RunElevated($"stop {_serviceName}");

    public void InstallServiceFromLocalExe()
    {
        string targetDir = @"C:\AppWatchdog\Service";
        string targetExe = Path.Combine(targetDir, "AppWatchdog.Service.exe");
        string sourceExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppWatchdog.Service.exe");

        if (!File.Exists(sourceExe))
            throw new FileNotFoundException("Service-EXE nicht gefunden.", sourceExe);

        Directory.CreateDirectory(targetDir);

        File.Copy(sourceExe, targetExe, true);

        RunElevated($"delete {_serviceName}");
        Thread.Sleep(1000);
        RunElevated($"create {_serviceName} binPath= \"{targetExe}\" start= auto");
    }

    public void UninstallService()
    {
        RunElevated($"stop {_serviceName}");
        Thread.Sleep(1000);
        RunElevated($"delete {_serviceName}");
    }

    public void ReinstallService()
    {
        // Uninstall if exists (ignore errors if not installed)
        try { UninstallService(); }
        catch { }

        // Install fresh
        InstallServiceFromLocalExe();
    }

    private static void RunElevated(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = args,
            Verb = "runas",
            UseShellExecute = true
        };

        using var p = Process.Start(psi);
        if (p == null)
            throw new InvalidOperationException("UAC-Abfrage wurde abgebrochen.");
    }

    public bool IsServiceRunning()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {_serviceName}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();

            return output.Contains("RUNNING");
        }
        catch
        {
            return false;
        }
    }

    public bool IsServiceInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {_serviceName}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            return !output.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void RestartService()
    {
        RunElevated($"stop {_serviceName}");
        Thread.Sleep(1000);
        RunElevated($"start {_serviceName}");
    }

}
