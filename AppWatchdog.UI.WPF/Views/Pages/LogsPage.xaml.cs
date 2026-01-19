using AppWatchdog.UI.WPF.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class LogsPage : Page
{
    public LogsPage(LogsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, __) => await vm.ActivateAsync();
        Unloaded += (_, __) => vm.Deactivate();
    }
}
