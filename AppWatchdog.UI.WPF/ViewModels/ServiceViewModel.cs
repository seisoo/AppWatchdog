using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
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
    private IContentDialogService? _dialogService;
    private bool _isRefreshing;
    private readonly BackendStateService _backend;

    public void AttachDialogService(IContentDialogService dialogService)
        => _dialogService = dialogService;

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
        BackendStateService backend)
    {
        _pipe = pipe;
        _svc = svc;
        _backend = backend;

        IsAdminText = IsAdmin() ? "Ja" : "Nein";

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

        await ForceBackendRecheckAsync(); // 🔥 IMMER prüfen
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

    private void RefreshSnapshot()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            // 🔴 1. Service-Status prüfen
            if (!IsServiceRunning())
            {
                _backend.SetOffline("Service ist gestoppt");
                StatusLine = "Service ist gestoppt";
                return;   // ❌ KEIN StopAutoRefresh mehr
            }


            // 🔴 2. Pipe prüfen
            ServiceSnapshot snap;
            try
            {
                snap = _pipe.GetStatus();
            }
            catch (Exception ex)
            {
                _backend.SetOffline($"Pipe nicht erreichbar ({ex.Message})");
                StatusLine = $"Pipe nicht erreichbar ({ex.Message})";
                return;
            }

            // 🟢 BEIDES OK → READY
            _backend.SetReady("Service verbunden");

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
        MemoryInfo = $"{sys.AvailableMemoryMb:n0} MB frei / {sys.TotalMemoryMb:n0} MB gesamt";
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
            "Erfolgreich Dienst gestartet");

    [RelayCommand]
    private async Task StopService()
        => await RunServiceActionAsync(
            () => _svc.StopService(),
            "Erfolgreich Dienst gestoppt");

    [RelayCommand]
    private async Task InstallService()
        => await RunServiceActionAsync(
            () => _svc.InstallServiceFromLocalExe(),
            "Erfolgreich Dienst installiert");

    [RelayCommand]
    private async Task UninstallService()
        => await RunServiceActionAsync(
            () => _svc.UninstallService(),
            "Erfolgreich Dienst deinstalliert");

    private async Task ForceBackendRecheckAsync()
    {
        // kurze Verzögerung, damit der Service-Zustand stabil ist
        await Task.Delay(1500);

        RefreshSnapshot();
    }


    private async Task RunServiceActionAsync(Action action, string? successText)
    {
        try
        {
            action();
            await ForceBackendRecheckAsync();

            if (successText != null && _dialogService != null)
            {
                string serviceExePath =
                    Path.Combine(@"C:\AppWatchdog\Service", "AppWatchdog.Service.exe");

                var panel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                panel.Children.Add(new SymbolIcon
                {
                    Symbol = SymbolRegular.CheckmarkCircle24,
                    FontSize = 64,
                    Margin = new Thickness(0, 0, 0, 16)
                });

                panel.Children.Add(new Wpf.Ui.Controls.TextBlock
                {
                    Text = successText,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                panel.Children.Add(new Wpf.Ui.Controls.TextBlock
                {
                    Text = "Service-Executable",
                    Opacity = 0.7,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                panel.Children.Add(new Wpf.Ui.Controls.TextBlock
                {
                    Text = serviceExePath,
                    TextAlignment = TextAlignment.Center,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Opacity = 0.85
                });

                await _dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "Service",
                        Content = panel,
                        CloseButtonText = "OK"
                    },
                    CancellationToken.None
                );
            }
        }
        catch (Exception ex)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "Service-Fehler",
                        Content = ex.Message,
                        CloseButtonText = "OK"
                    },
                    CancellationToken.None
                );
            }
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
