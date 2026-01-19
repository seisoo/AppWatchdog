using AppWatchdog.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Pipes;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;


namespace AppWatchdog.Service;

public static class SystemInfoCollector
{
    public static SystemInfoSnapshot Collect()
    {
        var proc = Process.GetCurrentProcess();

        var (totalMb, availableMb) = CpuInfoHelper.GetMemoryInfo();

        var si = new SystemInfoSnapshot
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OsVersion = Environment.OSVersion.ToString(),
            Is64BitOS = Environment.Is64BitProcess,
            Uptime = DateTime.Now - proc.StartTime,
            ProcessorCount = Environment.ProcessorCount,
            TotalPhysicalMemoryBytes = totalMb,
            AvailablePhysicalMemoryBytes = availableMb
        };


        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed) continue;
                if (!d.IsReady) continue;

                si.Disks.Add(new DiskInfo
                {
                    Name = d.Name,
                    TotalBytes = (ulong)d.TotalSize,
                    FreeBytes = (ulong)d.TotalFreeSpace
                });
            }
        }
        catch
        {
        }

        return si;
    }

    
    public static string FormatForHtml(SystemInfoSnapshot s)
    {
        static string FmtBytes(ulong b)
        {
            double v = b;
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        var sb = new StringBuilder();

        sb.Append("<div style=\"font-size:13px; line-height:1.45;\">");
        sb.Append("<div><b>System</b></div>");
        sb.Append("<table style=\"border-collapse:collapse; width:100%;\">");

        void Row(string k, string v)
        {
            sb.Append("<tr>");
            sb.Append("<td style=\"padding:4px 8px; border:1px solid #e5e7eb; width:220px; background:#f9fafb;\"><b>")
              .Append(Html(k))
              .Append("</b></td>");
            sb.Append("<td style=\"padding:4px 8px; border:1px solid #e5e7eb;\">")
              .Append(Html(v))
              .Append("</td>");
            sb.Append("</tr>");
        }

        Row("Computername", s.MachineName);
        Row("Benutzer", s.UserName);
        Row("OS", s.OsVersion);
        Row("64-bit OS", s.Is64BitOS ? "Ja" : "Nein");
        Row("CPU Kerne", s.ProcessorCount.ToString());
        Row("Windows Uptime", FormatUptime(s.Uptime));

        if (s.TotalPhysicalMemoryBytes > 0)
        {
            Row("RAM gesamt", s.TotalPhysicalMemoryBytes.ToString());
            Row("RAM frei", s.AvailablePhysicalMemoryBytes.ToString());
        }

        if (s.Disks.Count > 0)
        {
            sb.Append("<tr><td colspan=\"2\" style=\"padding:6px 8px; border:1px solid #e5e7eb; background:#f3f4f6;\"><b>Datenträger</b></td></tr>");
            foreach (var d in s.Disks)
            {
                var used = (d.TotalBytes > d.FreeBytes) ? (d.TotalBytes - d.FreeBytes) : 0;
                Row(d.Name.TrimEnd('\\'), $"Frei: {FmtBytes(d.FreeBytes)} / Gesamt: {FmtBytes(d.TotalBytes)} (Belegt: {FmtBytes(used)})");
            }
        }

        sb.Append("</table>");
        sb.Append("</div>");

        return sb.ToString();
    }

    private static string FormatUptime(TimeSpan t)
    {
        var days = (int)t.TotalDays;
        return days > 0
            ? $"{days} Tage, {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    private static string Html(string s)
        => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    
}

public sealed class SystemInfoSnapshot
{
    public string MachineName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public bool Is64BitOS { get; set; }
    public int ProcessorCount { get; set; }
    public TimeSpan Uptime { get; set; }

    public long TotalPhysicalMemoryBytes { get; set; }
    public long AvailablePhysicalMemoryBytes { get; set; }

    public List<DiskInfo> Disks { get; set; } = new();
}

public sealed class DiskInfo
{
    public string Name { get; set; } = "";
    public ulong TotalBytes { get; set; }
    public ulong FreeBytes { get; set; }
}


public static class CpuInfoHelper
{
    public static (long totalMb, long availableMb) GetMemoryInfo()
    {
        var mem = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };

        if (!GlobalMemoryStatusEx(ref mem))
            throw new InvalidOperationException("GlobalMemoryStatusEx fehlgeschlagen.");

        long totalMb = (long)(mem.ullTotalPhys / (1024 * 1024));
        long availableMb = (long)(mem.ullAvailPhys / (1024 * 1024));

        return (totalMb, availableMb);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}