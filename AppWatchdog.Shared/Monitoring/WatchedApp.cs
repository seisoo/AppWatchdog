using AppWatchdog.Shared.Monitoring;

namespace AppWatchdog.Shared;

public sealed class WatchedApp
{
    public string Name { get; set; } = "App";

    public WatchTargetType Type { get; set; } = WatchTargetType.Executable;

    // Executable
    public string ExePath { get; set; } = "";
    public string Arguments { get; set; } = "";

    // Windows Service
    public string? ServiceName { get; set; }

    // HTTP
    public string? Url { get; set; }
    public int ExpectedStatusCode { get; set; } = 200;

    // TCP
    public string? Host { get; set; }
    public int? Port { get; set; }

    public bool Enabled { get; set; } = true;

    public UptimeKumaSettings? UptimeKuma { get; set; }

    public int CheckIntervalSeconds { get; set; } = 60;
}
