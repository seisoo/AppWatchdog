using AppWatchdog.UI.WPF.ViewModels;
using System.Windows.Controls;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class RestorePage : Page
{
    private readonly RestorePageViewModel _vm;

    public RestorePage(RestorePageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += async (_, _) => await _vm.ActivateAsync();
    }
}
