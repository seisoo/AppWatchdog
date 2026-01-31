using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Dialogs;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Microsoft.Win32;
using System.IO;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class ServiceViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly ServiceControlFacade _svc;
    private readonly DispatcherTimer _snapshotTimer;
    private readonly BackendStateService _backend;
    private readonly ISnackbarService _snackbar;
    private readonly AppDialogService _dialog;

    private bool _isRefreshing;
    private bool _activated;

    public LanguageSelectorViewModel LanguageSelector { get; }

    private enum ServiceStateKind
    {
        Unknown,
        Running,
        Stopped,
        NotInstalled,
        UpdateRequired
    }

    // -------------------------------------------------
    // STATUS / SNAPSHOT SUMMARY
    // -------------------------------------------------
    [ObservableProperty] private string _serviceStateText = "";
    [ObservableProperty] private string _snapshotTimestamp = "";
    [ObservableProperty] private string _sessionStateText = "";
    [ObservableProperty] private string _appsSummaryText = "";

    [ObservableProperty] private string _serviceStateChipText = "";
    [ObservableProperty] private SymbolRegular _serviceStateSymbol = SymbolRegular.Info24;
    [ObservableProperty] private Brush _serviceStateBrush = Brushes.Gray;

    [ObservableProperty] private string _serviceVersionText = "-";
    [ObservableProperty] private string _serviceAccountText = "-";
    [ObservableProperty] private string _lastActionText = "-";

    [ObservableProperty] private string _primaryActionText = "";
    [ObservableProperty] private SymbolRegular _primaryActionSymbol = SymbolRegular.Play24;
    [ObservableProperty] private ControlAppearance _primaryActionAppearance = ControlAppearance.Primary;

    [ObservableProperty] private bool _isActionRunning;
    [ObservableProperty] private string _actionStatusText = "";

    // -------------------------------------------------
    // STATUS LINE
    // -------------------------------------------------
    [ObservableProperty] private string _statusLine = "";

    // -------------------------------------------------
    // SYSTEM INFO
    // -------------------------------------------------
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private int _corecount = -1;
    [ObservableProperty] private string _machineName = "";
    [ObservableProperty] private string _osVersion = "";
    [ObservableProperty] private string _dotNetVersion = "";
    [ObservableProperty] private string _uptime = "";
    [ObservableProperty] private string _memoryInfo = "";
    [ObservableProperty] private string _clientProtocolVersion = "";
    [ObservableProperty] private string _serviceProtocolVersion = "";
    [ObservableProperty] private string _isAdminText = "";

    // -------------------------------------------------
    // DIAGNOSTICS
    // -------------------------------------------------
    [ObservableProperty] private string _backendStatusText = "";
    [ObservableProperty] private string _protocolMismatchText = "";
    [ObservableProperty] private bool _hasProtocolMismatch;

    // -------------------------------------------------
    // APPS
    // -------------------------------------------------
    public ObservableCollection<AppStatus> EnabledApps { get; } = new();
    [ObservableProperty] private bool _hasEnabledApps;
    public bool HasNoEnabledApps => !HasEnabledApps;

    private ServiceStateKind _currentState = ServiceStateKind.Unknown;

    // -------------------------------------------------
    // CTOR
    // -------------------------------------------------
    public ServiceViewModel(
        PipeFacade pipe,
        ServiceControlFacade svc,
        BackendStateService backend,
        LanguageSelectorViewModel languageSelector,
        ISnackbarService snackbar,
        AppDialogService dialog)
    {
        _pipe = pipe;
        _svc = svc;
        _backend = backend;
        _snackbar = snackbar;
        _dialog = dialog;
        LanguageSelector = languageSelector;

        IsAdminText = IsAdmin() ? AppStrings.yes : AppStrings.no;
        ServiceVersionText = _svc.GetServiceVersion();

        _backend.PropertyChanged += (_, __) =>
        {
            BackendStatusText = _backend.StatusMessage;
        };

        _snapshotTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _snapshotTimer.Tick += (_, _) => RefreshSnapshot();
    }

    partial void OnIsActionRunningChanged(bool value)
    {
        PrimaryActionCommand.NotifyCanExecuteChanged();
        RestartServiceCommand.NotifyCanExecuteChanged();
        ReinstallServiceCommand.NotifyCanExecuteChanged();
        OpenLogsFolderCommand.NotifyCanExecuteChanged();
        OpenServiceFolderCommand.NotifyCanExecuteChanged();
        CheckServiceHealthCommand.NotifyCanExecuteChanged();
    }

    // -------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------
    public async Task ActivateAsync()
    {
        if (!_activated)
        {
            _activated = true;
            StartAutoRefresh();
        }

        await ForceBackendRecheckAsync();
    }

    public void StartAutoRefresh()
    {
        if (!_snapshotTimer.IsEnabled)
            _snapshotTimer.Start();
    }

    public void StopAutoRefresh()
    {
        if (_snapshotTimer.IsEnabled)
            _snapshotTimer.Stop();
    }

    // -------------------------------------------------
    // SNAPSHOT
    // -------------------------------------------------
    private async void RefreshSnapshot()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            var installed = _svc.IsServiceInstalled();
            ServiceVersionText = _svc.GetServiceVersion();
            BackendStatusText = _backend.StatusMessage;

            if (!installed)
            {
                SetState(ServiceStateKind.NotInstalled, AppStrings.error_service_not_installed_title);
                SnapshotTimestamp = "-";
                SessionStateText = "-";
                AppsSummaryText = "-";
                ServiceAccountText = "-";
                HasProtocolMismatch = false;
                ProtocolMismatchText = "";
                return;
            }

            if (!IsServiceRunning())
            {
                SetState(ServiceStateKind.Stopped, AppStrings.service_stopped);
                SnapshotTimestamp = "-";
                SessionStateText = "-";
                AppsSummaryText = "-";
                ServiceAccountText = "-";
                HasProtocolMismatch = false;
                ProtocolMismatchText = "";
                return;
            }

            ServiceSnapshot snap;
            try
            {
                snap = await Task.Run(() => _pipe.GetStatus());
                if (snap == null)
                    throw new Exception(AppStrings.pipe_not_available_ex);
            }
            catch (Exception ex)
            {
                _backend.SetOffline(string.Format(AppStrings.pipe_not_available_ex, ex.Message));
                StatusLine = string.Format(AppStrings.pipe_not_available_ex, ex.Message);
                SetState(ServiceStateKind.Stopped, AppStrings.service_stopped);
                SnapshotTimestamp = "-";
                SessionStateText = "-";
                AppsSummaryText = "-";
                ServiceAccountText = "-";
                return;
            }

            _backend.SetReady(AppStrings.service_connected);

            StatusLine = $"Snapshot: {snap.Timestamp:HH:mm:ss} | Session: {snap.SessionState}";

            SnapshotTimestamp = snap.Timestamp.ToString("HH:mm:ss");
            SessionStateText = snap.SessionState.ToString();

            var total = snap.Apps.Count;
            var enabled = snap.Apps.Count(a => a.Enabled);
            var down = snap.Apps.Count(a => a.Enabled && !a.IsRunning);

            AppsSummaryText = $"{enabled}/{total} enabled, {down} down";

            UpdateSystemInfo(snap.SystemInfo);
            UpdateEnabledApps(snap.Apps);

            ServiceAccountText = snap.SystemInfo.UserName;
            HasProtocolMismatch = snap.SystemInfo.PipeProtocol != PipeProtocol.ProtocolVersion;
            ProtocolMismatchText = HasProtocolMismatch
                ? AppStrings.error_service_update_required_text
                : "";

            SetState(HasProtocolMismatch ? ServiceStateKind.UpdateRequired : ServiceStateKind.Running,
                HasProtocolMismatch ? AppStrings.error_service_update_required : "Running");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // -------------------------------------------------
    // UPDATE HELPERS
    // -------------------------------------------------
    private void UpdateSystemInfo(SystemInfo sys)
    {
        MachineName = sys.MachineName;
        OsVersion = sys.OsVersion;
        DotNetVersion = sys.DotNetVersion;
        Uptime = sys.Uptime.ToString(@"dd\.hh\:mm\:ss");
        MemoryInfo = string.Format(
            AppStrings.service_system_info_memory,
            sys.AvailableMemoryMb,
            sys.TotalMemoryMb);

        ClientProtocolVersion = $"V. {PipeProtocol.ProtocolVersion}";
        ServiceProtocolVersion = $"V. {sys.PipeProtocol}";
        Username = sys.UserName;
        Corecount = sys.ProcessorCount;
    }

    private void UpdateEnabledApps(IEnumerable<AppStatus> apps)
    {
        var enabled = apps.Where(a => a.Enabled).ToList();

        for (int i = EnabledApps.Count - 1; i >= 0; i--)
        {
            if (!enabled.Any(a => a.Name == EnabledApps[i].Name))
                EnabledApps.RemoveAt(i);
        }

        foreach (var app in enabled)
        {
            var existing = EnabledApps.FirstOrDefault(a => a.Name == app.Name);
            if (existing == null)
            {
                EnabledApps.Add(app);
            }
            else
            {
                existing.IsRunning = app.IsRunning;
                existing.LastStartError = app.LastStartError;
                existing.ExePath = app.ExePath;
            }
        }

        HasEnabledApps = EnabledApps.Count > 0;
        OnPropertyChanged(nameof(HasNoEnabledApps));
    }

    private async Task ForceBackendRecheckAsync()
    {
        await Task.Delay(1500);
        RefreshSnapshot();
    }

    private bool IsServiceRunning()
    {
        try { return _svc.IsServiceRunning(); }
        catch { return false; }
    }

    private static bool IsAdmin()
    {
        try
        {
            var wp = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private void SetState(ServiceStateKind state, string label)
    {
        _currentState = state;
        ServiceStateChipText = label;
        ServiceStateText = label;

        switch (state)
        {
            case ServiceStateKind.Running:
                ServiceStateSymbol = SymbolRegular.CheckmarkCircle24;
                ServiceStateBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                PrimaryActionText = AppStrings.service_button_stop;
                PrimaryActionSymbol = SymbolRegular.Stop24;
                PrimaryActionAppearance = ControlAppearance.Danger;
                break;

            case ServiceStateKind.Stopped:
                ServiceStateSymbol = SymbolRegular.PauseCircle24;
                ServiceStateBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                PrimaryActionText = AppStrings.service_button_start;
                PrimaryActionSymbol = SymbolRegular.Play24;
                PrimaryActionAppearance = ControlAppearance.Success;
                break;

            case ServiceStateKind.NotInstalled:
                ServiceStateSymbol = SymbolRegular.Wrench24;
                ServiceStateBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                PrimaryActionText = AppStrings.install;
                PrimaryActionSymbol = SymbolRegular.Wrench24;
                PrimaryActionAppearance = ControlAppearance.Primary;
                break;

            case ServiceStateKind.UpdateRequired:
                ServiceStateSymbol = SymbolRegular.Warning24;
                ServiceStateBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                PrimaryActionText = AppStrings.reinstall;
                PrimaryActionSymbol = SymbolRegular.ArrowSync24;
                PrimaryActionAppearance = ControlAppearance.Caution;
                break;

            default:
                ServiceStateSymbol = SymbolRegular.Info24;
                ServiceStateBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                PrimaryActionText = AppStrings.service_button_start;
                PrimaryActionSymbol = SymbolRegular.Play24;
                PrimaryActionAppearance = ControlAppearance.Primary;
                break;
        }
    }

    private bool CanRunAction()
        => !IsActionRunning;

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task PrimaryActionAsync()
    {
        switch (_currentState)
        {
            case ServiceStateKind.Running:
                await StopServiceAsync();
                break;
            case ServiceStateKind.NotInstalled:
                await InstallServiceAsync();
                break;
            case ServiceStateKind.UpdateRequired:
                await ReinstallServiceAsync(confirm: true);
                break;
            default:
                await StartServiceAsync();
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task RestartServiceAsync()
        => await RunServiceActionAsync(
            () => _svc.RestartService(),
            AppStrings.service_succesfully_started,
            requiresConfirmation: false,
            actionLabel: AppStrings.service_button_start);

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ReinstallServiceAsync()
        => await ReinstallServiceAsync(confirm: true);

    private async Task ReinstallServiceAsync(bool confirm)
    {
        if (confirm && !await _dialog.ShowConfirmAsync(
                AppStrings.reinstall,
                AppStrings.error_service_update_required_text,
                AppStrings.reinstall,
                AppStrings.cancel))
        {
            return;
        }

        await RunServiceActionAsync(
            () => _svc.ReinstallService(),
            AppStrings.service_succesfully_installed,
            requiresConfirmation: false,
            actionLabel: AppStrings.reinstall);
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private void OpenLogsFolder()
    {
        try
        {
            var path = _pipe.GetLogPath()?.Path;
            if (!string.IsNullOrWhiteSpace(path))
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private void OpenServiceFolder()
    {
        try
        {
            var path = _svc.GetInstallDirectory();
            if (!string.IsNullOrWhiteSpace(path))
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task CheckServiceHealthAsync()
    {
        try
        {
            IsActionRunning = true;
            ActionStatusText = AppStrings.service_action_in_progress;

            await Task.Run(() => _pipe.Ping());

            _snackbar.Show(
                AppStrings.service_title,
                AppStrings.service_connected,
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28),
                TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            _snackbar.Show(
                AppStrings.service_error,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24, 28),
                TimeSpan.FromSeconds(6));
        }
        finally
        {
            IsActionRunning = false;
            ActionStatusText = "";
        }
    }

    private async Task StartServiceAsync()
        => await RunServiceActionAsync(
            () => _svc.StartService(),
            AppStrings.service_succesfully_started,
            requiresConfirmation: false,
            actionLabel: AppStrings.service_button_start);

    private async Task StopServiceAsync()
        => await RunServiceActionAsync(
            () => _svc.StopService(),
            AppStrings.service_succesfully_stopped,
            requiresConfirmation: false,
            actionLabel: AppStrings.service_button_stop);

    private async Task InstallServiceAsync()
        => await RunServiceActionAsync(
            () => _svc.InstallServiceFromLocalExe(),
            AppStrings.service_succesfully_installed,
            requiresConfirmation: false,
            actionLabel: AppStrings.install);

    private async Task<bool> EnsureElevationAsync(string actionLabel)
    {
        if (IsAdmin())
            return true;

        return await _dialog.ShowConfirmAsync(
            AppStrings.warn,
            $"Administrator privileges are required to {actionLabel.ToLowerInvariant()}. Continue?",
            AppStrings.ok,
            AppStrings.cancel);
    }

    private async Task RunServiceActionAsync(Action action, string? successText, bool requiresConfirmation, string actionLabel)
    {
        if (requiresConfirmation)
        {
            var confirm = await _dialog.ShowConfirmAsync(
                AppStrings.warn,
                $"{actionLabel}?",
                AppStrings.ok,
                AppStrings.cancel);
            if (!confirm)
                return;
        }

        if (!await EnsureElevationAsync(actionLabel))
            return;

        try
        {
            IsActionRunning = true;
            ActionStatusText = actionLabel;

            action();
            await ForceBackendRecheckAsync();

            LastActionText = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (!string.IsNullOrWhiteSpace(successText))
            {
                _snackbar.Show(
                    AppStrings.service_title,
                    successText,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28),
                    TimeSpan.FromSeconds(4));
            }
        }
        catch (Exception ex)
        {
            _snackbar.Show(
                AppStrings.service_error,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24, 28),
                TimeSpan.FromSeconds(6));
        }
        finally
        {
            IsActionRunning = false;
            ActionStatusText = "";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ExportConfigAsync()
    {
        try
        {
            var json = await Task.Run(() => _pipe.ExportConfig());
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Config export failed.");

            var dlg = new SaveFileDialog
            {
                Title = AppStrings.service_export_config,
                Filter = "AppWatchdog config (*.awcfg)|*.awcfg|JSON (*.json)|*.json|All files (*.*)|*.*",
                FileName = "appwatchdog-config.awcfg"
            };

            if (dlg.ShowDialog() != true)
                return;

            File.WriteAllText(dlg.FileName, json);

            _snackbar.Show(
                AppStrings.service_title,
                AppStrings.service_config_exported,
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28),
                TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            _snackbar.Show(
                AppStrings.service_error,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24, 28),
                TimeSpan.FromSeconds(6));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ImportConfigAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = AppStrings.service_import_config,
            Filter = "AppWatchdog config (*.awcfg;*.json)|*.awcfg;*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dlg.ShowDialog() != true)
            return;

        var confirm = await _dialog.ShowConfirmAsync(
            AppStrings.service_import_config,
            AppStrings.service_import_config_confirm,
            AppStrings.ok,
            AppStrings.cancel);

        if (!confirm)
            return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            await Task.Run(() => _pipe.ImportConfig(json));
            await ForceBackendRecheckAsync();

            _snackbar.Show(
                AppStrings.service_title,
                AppStrings.service_config_imported,
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28),
                TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            _snackbar.Show(
                AppStrings.service_error,
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24, 28),
                TimeSpan.FromSeconds(6));
        }
    }
}
