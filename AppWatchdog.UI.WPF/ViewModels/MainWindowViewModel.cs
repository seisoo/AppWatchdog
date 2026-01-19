using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppsViewModel _apps;
    private readonly NotificationsViewModel _notifications;
    private readonly BackendStateService _backend;

    public ObservableCollection<object> MenuItems { get; }
    public ObservableCollection<object> FooterMenuItems { get; }

    [ObservableProperty] private string _headerText = "Service";
    [ObservableProperty] private string _statusText = "";

    public bool BackendReady => _backend.IsReady;
    public string BackendStatus => _backend.StatusMessage;


    public bool AnyDirty =>
       _apps.IsDirty || _notifications.IsDirty;

    public MainWindowViewModel(
        AppsViewModel apps,
        NotificationsViewModel notifications,
        BackendStateService backend)
    {
        _apps = apps;
        _notifications = notifications;
        _backend = backend;

        _backend.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(BackendReady));
            OnPropertyChanged(nameof(BackendStatus));
        };


        _apps.PropertyChanged += OnChildDirtyChanged;
        _notifications.PropertyChanged += OnChildDirtyChanged;

        MenuItems = new ObservableCollection<object>
        {
            new NavigationViewItem { Content = "Service", Icon = new SymbolIcon { Symbol = SymbolRegular.Shield24 }, TargetPageType = typeof(Views.Pages.ServicePage) },
            new NavigationViewItem { Content = "Apps",    Icon = new SymbolIcon { Symbol = SymbolRegular.AppFolder24 }, TargetPageType = typeof(Views.Pages.AppsPage) },
            new NavigationViewItem { Content = "Mail",    Icon = new SymbolIcon { Symbol = SymbolRegular.Mail24 }, TargetPageType = typeof(Views.Pages.NotificationsPage) },
            new NavigationViewItem { Content = "Logs",    Icon = new SymbolIcon { Symbol = SymbolRegular.DocumentText24 }, TargetPageType = typeof(Views.Pages.LogsPage) },
        };

        var about = new NavigationViewItem { Content = "Über", Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 }, TargetPageType = typeof(Views.Pages.ServicePage) };
        about.Tag = "about";

        FooterMenuItems = new ObservableCollection<object> { about };
    }

    private void OnChildDirtyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DirtyViewModelBase.IsDirty))
        {
            OnPropertyChanged(nameof(AnyDirty));
        }
    }


    public void SaveAll()
    {
        if (_apps.IsDirty && _apps.SaveCommand.CanExecute(null))
            _apps.SaveCommand.Execute(null);

        if (_notifications.IsDirty && _notifications.SaveCommand.CanExecute(null))
            _notifications.SaveCommand.Execute(null);
    }

    public void DiscardAll()
    {
        _apps.ClearDirty();
        _notifications.ClearDirty();
    }
}
