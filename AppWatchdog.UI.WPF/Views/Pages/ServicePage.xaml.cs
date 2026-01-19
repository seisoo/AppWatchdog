using AppWatchdog.UI.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class ServicePage : Page
{
    private readonly ServiceViewModel _vm;

    public ServicePage(ServiceViewModel vm,
        IContentDialogService contentDialogService)
    {
        InitializeComponent();
        _vm = vm;
        vm.AttachDialogService(contentDialogService);

        DataContext = vm;

        Unloaded += OnUnloaded;

        Loaded += async (_, __) =>
        {
            await _vm.ActivateAsync();
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.StopAutoRefresh();
    }
}
