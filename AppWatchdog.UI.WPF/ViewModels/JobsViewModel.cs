using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Timers;

namespace AppWatchdog.UI.WPF.ViewModels;

public sealed class JobsViewModel : ObservableObject, IDisposable
{
    private readonly Timer _timer;
    private DateTime _busySince;

    public ObservableCollection<JobRow> Jobs { get; } = new();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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
            var minDuration = TimeSpan.FromMilliseconds(500);

            if (elapsed < minDuration)
            {
                var remaining = minDuration - elapsed;

                Task.Delay(remaining).ContinueWith(_ =>
                    App.Current.Dispatcher.Invoke(() => IsBusy = false));
            }
            else
            {
                IsBusy = false;
            }
        }
    }

    public JobsViewModel()
    {
        _timer = new Timer(2000);
        _timer.Elapsed += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void Refresh()
    {
        if (IsBusy)
            return;

        try
        {
            SetBusy(true);

            var jobs = PipeClient.GetJobs();

            App.Current.Dispatcher.Invoke(() =>
            {
                Jobs.Clear();
                foreach (var j in jobs)
                    Jobs.Add(new JobRow(j));
            });
        }
        catch
        {
            // bewusst silent
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void Dispose()
        => _timer.Dispose();
}
