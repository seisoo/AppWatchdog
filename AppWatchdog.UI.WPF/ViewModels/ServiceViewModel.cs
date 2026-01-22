using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class ServiceViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly ServiceControlFacade _svc;
    private readonly DispatcherTimer _snapshotTimer;
    private bool _isRefreshing;
    private readonly BackendStateService _backend;
    public LanguageSelectorViewModel LanguageSelector { get; }

    private readonly ISnackbarService _snackbar;

    public bool HasNoEnabledApps => !HasEnabledApps;
    [ObservableProperty] private string _statusLine = "";

    public ObservableCollection<AppStatus> EnabledApps { get; } = new();
    [ObservableProperty] private bool _hasEnabledApps;

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

    

    public ServiceViewModel(
        PipeFacade pipe, 
        ServiceControlFacade svc,
        BackendStateService backend,
        LanguageSelectorViewModel _languageSelector, 
        ISnackbarService snackbar)
    {
        _pipe = pipe;
        _svc = svc;
        _backend = backend;
        _snackbar = snackbar;

        LanguageSelector = _languageSelector;

        IsAdminText = IsAdmin() ? AppStrings.yes : AppStrings.no;

        _snapshotTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _snapshotTimer.Tick += (_, _) => RefreshSnapshot();
    }


    private bool _activated;

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

    private static bool IsAdmin()
    {
        try
        {
            var wp = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private async void RefreshSnapshot()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            if (!IsServiceRunning())
            {
                _backend.SetOffline(AppStrings.service_stopped);
                StatusLine = AppStrings.service_stopped;
                return;   
            }

                ServiceSnapshot snap;
            try
            {
                snap = await Task.Run(() => _pipe.GetStatus());

                if(snap == null)
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                _backend.SetOffline(string.Format(AppStrings.pipe_not_available_ex, ex.Message));
                StatusLine = string.Format(AppStrings.pipe_not_available_ex, ex.Message);
                return;
            }


            _backend.SetReady(AppStrings.service_connected);

            StatusLine =
                $"Snapshot: {snap.Timestamp:HH:mm:ss} | Session: {snap.SessionState}";

            UpdateSystemInfo(snap.SystemInfo);
            UpdateEnabledApps(snap.Apps);
        }
        finally
        {
            _isRefreshing = false;
        }
    }


    private void UpdateSystemInfo(SystemInfo sys)
    {
        MachineName = sys.MachineName;
        OsVersion = sys.OsVersion;
        DotNetVersion = sys.DotNetVersion;
        Uptime = sys.Uptime.ToString(@"dd\.hh\:mm\:ss");
        MemoryInfo = string.Format(AppStrings.service_system_info_memory, sys.AvailableMemoryMb, sys.TotalMemoryMb);
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

    [RelayCommand]
    private async Task StartService()
       => await RunServiceActionAsync(
            () => _svc.StartService(),
            AppStrings.service_succesfully_started);

    [RelayCommand]
    private async Task StopService()
        => await RunServiceActionAsync(
            () => _svc.StopService(),
            AppStrings.service_succesfully_stopped);

    [RelayCommand]
    private async Task InstallService()
        => await RunServiceActionAsync(
            () => _svc.InstallServiceFromLocalExe(),
            AppStrings.service_succesfully_installed);

    [RelayCommand]
    private async Task UninstallService()
        => await RunServiceActionAsync(
            () => _svc.UninstallService(),
            AppStrings.service_succesfully_uninstalled);

    private async Task ForceBackendRecheckAsync()
    {
        await Task.Delay(1500);
        RefreshSnapshot();
    }


    private async Task RunServiceActionAsync(Action action, string? successText)
    {
        try
        {
            action();
            await ForceBackendRecheckAsync();

            if (!string.IsNullOrWhiteSpace(successText))
            {
                RunOnUiThread(() =>
                {
                    _snackbar.Show(
                        AppStrings.service_title,
                        successText,
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                        TimeSpan.FromSeconds(4));
                });
            }
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.service_error,
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            });
        }
    }



    private bool IsServiceRunning()
    {
        try
        {
            return _svc.IsServiceRunning();
        }
        catch
        {
            return false;
        }
    }



}
