using AppWatchdog.UI.WPF.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class AboutPage : Page
{
    public AboutPage(AboutViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
