using AppWatchdog.Shared;
using AppWatchdog.Shared.Jobs;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace AppWatchdog.UI.WPF.ViewModels;

public sealed partial class JobRow : ObservableObject
{
    private JobSnapshot _snapshot;
    private string _signature;

    public JobRow(JobSnapshot snapshot)
    {
        _snapshot = snapshot;
        _signature = BuildSignature(snapshot);
    }

    public JobSnapshot Snapshot => _snapshot;

    public string JobId => _snapshot.JobId;

    [ObservableProperty]
    private bool _isExpanded;

    public bool IsSameSnapshot(JobSnapshot snapshot)
        => _signature == BuildSignature(snapshot);

    public void UpdateSnapshot(JobSnapshot snapshot)
    {
        _snapshot = snapshot;
        _signature = BuildSignature(snapshot);
        OnPropertyChanged(string.Empty);
    }

    private static string BuildSignature(JobSnapshot snapshot)
    {
        var last = snapshot.Events.LastOrDefault();
        return string.Join("|",
            snapshot.JobId,
            snapshot.EffectiveState,
            snapshot.LastCheckUtc?.ToUnixTimeSeconds(),
            snapshot.LastStartAttemptUtc?.ToUnixTimeSeconds(),
            snapshot.NextRunUtc?.ToUnixTimeSeconds(),
            snapshot.PlannedStartUtc?.ToUnixTimeSeconds(),
            snapshot.ProgressPercent,
            snapshot.StatusText,
            last?.Type,
            last?.Progress,
            last?.Status,
            snapshot.Events.Count);
    }

    public string AppName =>
        _snapshot.Kind switch
        {
            JobKind.Backup => $"Backup: {_snapshot.BackupPlanName ?? _snapshot.JobId.Replace("backup:", "")}",
            JobKind.Restore => $"Restore: {_snapshot.RestorePlanName ?? _snapshot.JobId.Replace("restore:", "")}",
            _ => !string.IsNullOrWhiteSpace(_snapshot.AppName)
                ? _snapshot.AppName
                : $"Job: {_snapshot.JobId}"
        };

    private JobEvent? LastEvent => _snapshot.Events.LastOrDefault();

    public bool HasProgress =>
    LastEvent?.Progress.HasValue == true;

    public double Progress =>
    LastEvent?.Progress ?? 0;

    public string EventStatus =>
        LastEvent?.Status
        ?? _snapshot.StatusText
        ?? "—";

    public Brush EventBrush => LastEvent?.Type switch
    {
        JobEventType.Started => Brushes.SteelBlue,
        JobEventType.Progress => Brushes.SteelBlue,
        JobEventType.Completed => Brushes.ForestGreen,
        JobEventType.Failed => Brushes.DarkRed,
        _ => Brushes.Gray
    };

    public string State =>
        _snapshot.EffectiveState switch
        {
            "" => "NORMAL",
            _ => _snapshot.EffectiveState
        };

    public Brush StateBrush => _snapshot.EffectiveState switch
    {
        "UP" => Brushes.ForestGreen,
        "DOWN" => Brushes.DarkRed,
        "RECOVERY_FAILED" => Brushes.DarkOrange,
        _ => Brushes.Gray
    };

    public bool IsBackupOrRestore =>
        _snapshot.Kind is JobKind.Backup or JobKind.Restore;

    public string LastLabel =>
        IsBackupOrRestore ? "Last run" : "Last check";

    public string NextLabel =>
        IsBackupOrRestore ? "Next planned" : "Next check";

    public string LastRun =>
        IsBackupOrRestore
            ? _snapshot.LastStartAttemptUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "—"
            : _snapshot.LastCheckUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "—";

    public string NextPlanned =>
        IsBackupOrRestore
            ? FormatPlanned(_snapshot.PlannedStartUtc)
            : _snapshot.NextRunUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "—";

    private static string FormatPlanned(DateTimeOffset? planned)
    {
        if (!planned.HasValue)
            return "—";

        var local = planned.Value.ToLocalTime();
        return local.ToString("ddd dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    public string Stats =>
        _snapshot.Kind switch
        {
            JobKind.Backup => "Backup job",
            JobKind.Restore => "Restore job",
            _ => $"Down: {_snapshot.ConsecutiveDown} | Fails: {_snapshot.ConsecutiveStartFailures}"
        };

    public string JobType => _snapshot.JobType;

    // Job-spezifische Details
    public string? BackupSourcePath => _snapshot.BackupSourcePath;
    public string? BackupTargetPath => _snapshot.BackupTargetPath;
    public bool ShowBackupDetails => _snapshot.Kind == JobKind.Backup && !string.IsNullOrEmpty(_snapshot.BackupSourcePath);

    public string? HealthCheckTarget => _snapshot.HealthCheckTarget;
    public string? HealthCheckType => _snapshot.HealthCheckType;
    public bool ShowHealthDetails => _snapshot.Kind == JobKind.HealthMonitor && !string.IsNullOrEmpty(_snapshot.HealthCheckTarget);
    public string HealthCheckLabel => _snapshot.HealthCheckType switch
    {
        "HttpEndpoint" => "Endpoint",
        "TcpPort" => "Port",
        "Executable" => "Executable",
        "WindowsService" => "Service",
        _ => "Target"
    };

    public bool ShowRestoreDetails => _snapshot.Kind == JobKind.Restore && !string.IsNullOrEmpty(_snapshot.RestorePlanName);
}
