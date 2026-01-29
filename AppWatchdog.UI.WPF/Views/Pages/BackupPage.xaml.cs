using AppWatchdog.UI.WPF.ViewModels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class BackupPage : Page
{
    private readonly BackupPageViewModel _vm;

    public BackupPage(BackupPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.ActivateAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.Deactivate();
    }
}
