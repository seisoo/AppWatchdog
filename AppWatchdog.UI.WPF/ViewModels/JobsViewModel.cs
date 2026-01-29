using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace AppWatchdog.UI.WPF.ViewModels;

public sealed partial class JobsViewModel : ObservableObject, IDisposable
{
    private readonly Timer _timer;
    private DateTime _busySince;
    private bool _hasError;

    public ObservableCollection<JobRow> Jobs { get; } = new();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public JobsViewModel()
    {
        _timer = new Timer(2000);
        _timer.Elapsed += (_, _) => Refresh();
        _timer.Start();

        // Perform first refresh immediately
        Refresh();
    }

    [RelayCommand]
    private async Task RebuildJobs()
    {
        if (IsBusy)
            return;

        var shouldRefresh = false;

        try
        {
            IsBusy = true;
            await Task.Run(() => PipeClient.RebuildJobs());
            shouldRefresh = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"JobsViewModel RebuildJobs failed: {ex.GetType().Name}: {ex.Message}");
            if (App.Current?.Dispatcher != null)
                App.Current.Dispatcher.Invoke(() => Jobs.Clear());
        }
        finally
        {
            IsBusy = false;
        }

        if (shouldRefresh)
            Refresh();
    }

    private void Refresh()
    {
        if (IsBusy)
            return;

        try
        {
            SetBusy(true);

            JobSnapshotsResponse? response = null;
            try
            {
                response = PipeClient.GetJobs();
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JobsViewModel Refresh timeout: {ex.Message}");
                if (!_hasError)
                {
                    _hasError = true;
                    if (App.Current?.Dispatcher != null)
                        App.Current.Dispatcher.Invoke(() => Jobs.Clear());
                }
                return;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JobsViewModel Refresh error: {ex.Message}");
                if (!_hasError)
                {
                    _hasError = true;
                    if (App.Current?.Dispatcher != null)
                        App.Current.Dispatcher.Invoke(() => Jobs.Clear());
                }
                return;
            }

            if (response?.Jobs == null)
            {
                if (App.Current?.Dispatcher != null)
                    App.Current.Dispatcher.Invoke(() => Jobs.Clear());
                _hasError = false;
                return;
            }

            var snapshots = response.Jobs;
            _hasError = false;

            if (App.Current?.Dispatcher != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    var existing = Jobs.ToDictionary(j => j.JobId, StringComparer.OrdinalIgnoreCase);
                    var unchanged = Jobs.Count == snapshots.Count
                        && snapshots.All(s => existing.TryGetValue(s.JobId, out var row) && row.IsSameSnapshot(s));

                    if (unchanged)
                        return;

                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var targetIndex = 0;

                    foreach (var snap in snapshots)
                    {
                        if (existing.TryGetValue(snap.JobId, out var row))
                        {
                            if (!row.IsSameSnapshot(snap))
                                row.UpdateSnapshot(snap);

                            var currentIndex = Jobs.IndexOf(row);
                            if (currentIndex != targetIndex && currentIndex >= 0)
                                Jobs.Move(currentIndex, targetIndex);
                        }
                        else
                        {
                            row = new JobRow(snap);
                            Jobs.Insert(targetIndex, row);
                        }

                        seen.Add(snap.JobId);
                        targetIndex++;
                    }

                    for (var i = Jobs.Count - 1; i >= 0; i--)
                    {
                        if (!seen.Contains(Jobs[i].JobId))
                            Jobs.RemoveAt(i);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // Catch any unexpected errors
            System.Diagnostics.Debug.WriteLine($"JobsViewModel Refresh failed (unexpected): {ex.GetType().Name}: {ex.Message}");
            if (App.Current?.Dispatcher != null)
                App.Current.Dispatcher.Invoke(() => Jobs.Clear());
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        if (value)
        {
            _busySince = DateTime.UtcNow;
            IsBusy = true;
        }
        else
        {
            var elapsed = DateTime.UtcNow - _busySince;
            if (elapsed < TimeSpan.FromMilliseconds(500))
            {
                Task.Delay(TimeSpan.FromMilliseconds(500) - elapsed)
                    .ContinueWith(_ =>
                    {
                        if (App.Current?.Dispatcher != null)
                            App.Current.Dispatcher.Invoke(() => IsBusy = false);
                    });
            }
            else
            {
                IsBusy = false;
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
