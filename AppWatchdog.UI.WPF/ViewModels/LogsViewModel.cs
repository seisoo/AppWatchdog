using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class LogsViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly BackendStateService _backend;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    [ObservableProperty]
    private string _searchText = "";

    private string _rawLogText = "";

    [ObservableProperty]
    private int _searchMatchCount;

    public string SearchMatchText =>
        string.Format(AppStrings.logs_search_matches, SearchMatchCount);

    private const int SearchContextLines = 5;
    private const int RefreshIntervalSeconds = 3;

    [ObservableProperty]
    private bool _isContentEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isAutoRefreshEnabled = true;

    public ObservableCollection<string> Days { get; } = new();

    [ObservableProperty] 
    private string? _selectedDay;
    
    [ObservableProperty] 
    private string _logText = "";
    
    [ObservableProperty] 
    private string _header = "Select a log to view";

    private bool _activated;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    public LogsViewModel(
        PipeFacade pipe,
        BackendStateService backend)
    {
        _pipe = pipe;
        _backend = backend;

        _dispatcher = Dispatcher.CurrentDispatcher;
        _backend.PropertyChanged += OnBackendStateChanged;
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
            // Lade verfügbare Tage im Hintergrund ohne zu warten
            _ = LoadAvailableDaysAsync();
        }

        IsContentEnabled = true;
    }

    public void Deactivate()
    {
        IsContentEnabled = false;
        StopAutoRefresh();
    }

    private async Task LoadAvailableDaysAsync()
    {
        try
        {
            var days = await Task.Run(() =>
            {
                var resp = _pipe.ListLogDays();
                if (resp?.Days == null)
                    return new List<string>();

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
                    Header = AppStrings.error_service_notavailable_text;
                    return;
                }

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                SelectedDay = Days.Contains(today) ? today : Days.First();
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                Header = $"Error loading logs: {ex.Message}";
            });
        }
    }

    partial void OnSelectedDayChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            StopAutoRefresh();
            _ = LoadSelectedLogAsync(value);
        }
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        if (!value)
        {
            StopAutoRefresh();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedDay))
            StartAutoRefresh(SelectedDay);
    }

    private async Task LoadSelectedLogAsync(string day)
    {
        IsLoading = true;
        try
        {
            var resp = await Task.Run(() => _pipe.GetLogDay(day));
            await _dispatcher.InvokeAsync(() =>
            {
                if (resp == null)
                {
                    Header = AppStrings.error_service_notavailable_text;
                    LogText = "";
                    _rawLogText = "";
                    IsLoading = false;
                    return;
                }

                Header = string.Format(AppStrings.logs_last_update_text, resp.Day, DateTime.Now.ToString("HH:mm:ss"));

                if (string.IsNullOrWhiteSpace(resp.Content))
                {
                    LogText = "";
                    _rawLogText = "";
                    IsLoading = false;
                    return;
                }

                var reversed = string.Join(
                    Environment.NewLine,
                    resp.Content
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Reverse()
                );

                _rawLogText = reversed;
                ApplySearchFilter();

                if (IsAutoRefreshEnabled)
                    StartAutoRefresh(day);

                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                Header = $"Error loading log: {ex.Message}";
                LogText = "";
                _rawLogText = "";
                IsLoading = false;
            });
        }
    }

    private void StartAutoRefresh(string day)
    {
        if (!IsAutoRefreshEnabled)
            return;

        StopAutoRefresh();

        _refreshCts = new CancellationTokenSource();
        _refreshTask = Task.Run(async () =>
        {
            while (!_refreshCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshIntervalSeconds * 1000, _refreshCts.Token);

                    var resp = await Task.Run(() => _pipe.GetLogDay(day), _refreshCts.Token);
                    if (resp == null)
                        continue;

                    await _dispatcher.InvokeAsync(() =>
                    {
                        Header = string.Format(AppStrings.logs_last_update_text, resp.Day, DateTime.Now.ToString("HH:mm:ss"));

                        if (string.IsNullOrWhiteSpace(resp.Content))
                        {
                            LogText = "";
                            _rawLogText = "";
                            return;
                        }

                        var reversed = string.Join(
                            Environment.NewLine,
                            resp.Content
                                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                                .Reverse()
                        );

                        _rawLogText = reversed;
                        ApplySearchFilter();
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }, _refreshCts.Token);
    }

    private void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
        _refreshTask = null;
    }

    [RelayCommand]
    private async Task RefreshLogs()
    {
        if (string.IsNullOrWhiteSpace(SelectedDay))
        {
            await LoadAvailableDaysAsync();
            return;
        }

        await LoadSelectedLogAsync(SelectedDay);
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        var cfg = _pipe.GetLogPath();
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.Path))
            return;

        Process.Start(new ProcessStartInfo()
        {
            FileName = cfg.Path,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(_rawLogText))
        {
            LogText = "";
            SearchMatchCount = 0;
            return;
        }

        var allLines = _rawLogText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            LogText = _rawLogText;
            SearchMatchCount = allLines.Length;
            return;
        }

        var resultLines = new List<string>();
        var addedIndices = new HashSet<int>();

        int matchCount = 0;

        for (int i = 0; i < allLines.Length; i++)
        {
            if (!allLines[i].Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            matchCount++;

            int start = Math.Max(0, i - SearchContextLines);
            int end = Math.Min(allLines.Length - 1, i + SearchContextLines);

            for (int j = start; j <= end; j++)
            {
                if (addedIndices.Add(j))
                    resultLines.Add(allLines[j]);
            }
        }

        SearchMatchCount = matchCount;
        LogText = string.Join(Environment.NewLine, resultLines);
    }

    partial void OnSearchMatchCountChanged(int value)
    {
        OnPropertyChanged(nameof(SearchMatchText));
    }

    private async void OnBackendStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BackendStateService.State))
            return;

        if (_backend.IsReady)
            await ActivateAsync();
        else
        {
            Deactivate();
        }
    }
}

