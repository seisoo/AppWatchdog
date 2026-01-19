using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class LogsViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly DispatcherTimer _timer;
    private readonly BackendStateService _backend;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;


    [ObservableProperty]
    private bool _isContentEnabled;
    public ObservableCollection<string> Days { get; } = new();

    [ObservableProperty] private string? _selectedDay;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _header = "Logs werden geladen …";

    [ObservableProperty] private bool _autoRefreshEnabled = true;
    [ObservableProperty] private string _autoRefreshStatusText = "Auto-Refresh aktiv (5 s)";

    private const int RefreshSeconds = 5;
    private bool _activated;
    public LogsViewModel(
    PipeFacade pipe,
    BackendStateService backend)
    {
        _pipe = pipe;
        _backend = backend;

        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(RefreshSeconds)
        };

        _timer.Tick += OnTimerTick;


        _backend.PropertyChanged += OnBackendStateChanged;
    }
    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (!AutoRefreshEnabled)
            return;

        if (!_backend.IsReady)
            return;

        await RefreshSelectedAsync();
    }
    private async Task RefreshSelectedAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedDay))
            return;

        try
        {
            var resp = await Task.Run(() => _pipe.GetLogDay(SelectedDay));
            await _dispatcher.InvokeAsync(() =>
            {
                Header = $"Log vom {resp.Day} – letzte Aktualisierung {DateTime.Now:HH:mm:ss}";

                if (string.IsNullOrWhiteSpace(resp.Content))
                {
                    LogText = "";
                    return;
                }

                LogText = string.Join(
                    Environment.NewLine,
                    resp.Content
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Reverse()
                );
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                Header = "Fehler beim Laden des Logs.";
                LogText = ex.Message;
            });
        }
    }


    public async Task ActivateAsync()
    {
        if (!_backend.IsReady)
        {
            IsContentEnabled = false;
            return;
        }

        if (!_activated)
        {
            _activated = true;
            var days = await Task.Run(() =>
            {
                var resp = _pipe.ListLogDays();
                return resp.Days
                           .OrderByDescending(d => d)
                           .ToList();
            });
            await _dispatcher.InvokeAsync(() =>
            {
                Days.Clear();
                foreach (var d in days)
                    Days.Add(d);

                if (Days.Count == 0)
                {
                    Header = "Keine Logs vorhanden.";
                    return;
                }

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                SelectedDay = Days.Contains(today) ? today : Days.First();
                Header = $"Logs geladen ({DateTime.Now:HH:mm:ss})";
                _timer.Start();
            });
        }

        IsContentEnabled = true;
    }


    public void Deactivate()
    {
        IsContentEnabled = false;
    }

    private async void OnBackendStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BackendStateService.State))
            return;

        if (_backend.IsReady)
            await ActivateAsync();
        else
            IsContentEnabled = false;
    }

    [RelayCommand]
    private void LoadSelected()
    {
        if (string.IsNullOrWhiteSpace(SelectedDay))
            return;

        try
        {
            var resp = _pipe.GetLogDay(SelectedDay);

            Header = $"Log vom {resp.Day} – letzte Aktualisierung {DateTime.Now:HH:mm:ss}";

            if (string.IsNullOrWhiteSpace(resp.Content))
            {
                LogText = "";
                return;
            }

            var reversed = string.Join(
                Environment.NewLine,
                resp.Content
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Reverse()
            );

            LogText = reversed;
        }
        catch (Exception ex)
        {
            Header = "Fehler beim Laden des Logs.";
            LogText = ex.Message;
        }
    }


    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        AutoRefreshStatusText = value
            ? $"Auto-Refresh aktiv ({RefreshSeconds} s)"
            : "Auto-Refresh pausiert";
    }
}
