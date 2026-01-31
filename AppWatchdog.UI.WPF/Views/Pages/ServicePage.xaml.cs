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

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        if (element.ContextMenu == null)
            return;

        element.ContextMenu.PlacementTarget = element;
        element.ContextMenu.IsOpen = true;
    }
}
