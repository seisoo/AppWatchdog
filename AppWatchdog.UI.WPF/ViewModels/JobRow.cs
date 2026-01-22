using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using System.Windows.Media;

namespace AppWatchdog.UI.WPF.ViewModels;

public sealed class JobRow
{
    public JobSnapshot Snapshot { get; }

    public JobRow(JobSnapshot s)
        => Snapshot = s;

    public string AppName =>
    !string.IsNullOrWhiteSpace(Snapshot.AppName)
        ? Snapshot.AppName
        : Snapshot.Kind switch
        {
            JobKind.HealthMonitor => "Service",
            JobKind.KumaPing => "Uptime Kuma",
            JobKind.Snapshot => "System",
            _ => "Global"
        };

    public bool HasPing => Snapshot.PingMs.HasValue;

    public string PingText =>
        Snapshot.PingMs.HasValue
            ? $"{Snapshot.PingMs.Value} ms"
            : "—";

    public Brush PingBrush => Snapshot.PingMs switch
    {
        null => Brushes.Gray,
        < 200 => Brushes.ForestGreen,
        < 500 => Brushes.DarkOrange,
        _ => Brushes.DarkRed
    };


    public string JobType => Snapshot.JobType;

    public string State => Snapshot.EffectiveState switch
    {
        "" => "NORMAL",
        _ => Snapshot.EffectiveState
    };

    public Brush StateBrush => Snapshot.EffectiveState switch
    {
        "UP" => Brushes.ForestGreen,
        "DOWN" => Brushes.DarkRed,
        "RECOVERY_FAILED" => Brushes.DarkOrange,
        _ => Brushes.Gray
    };

    public string LastCheck =>
        Snapshot.LastCheckUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—";

    public string NextRun =>
        Snapshot.NextRunUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—";

    public string Stats =>
        $"Down: {Snapshot.ConsecutiveDown} | Fails: {Snapshot.ConsecutiveStartFailures}";

    public string Interval =>
        $"{Snapshot.Interval.TotalSeconds:0}s";
}
