using System.Windows;
using System.Windows.Controls;
using AppWatchdog.UI.WPF.ViewModels;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class NotificationsPage : Page
{
    private readonly NotificationsViewModel _vm;

    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void SmtpPasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.SmtpPassword = ((PasswordBox)sender).Password;
        _vm.MarkDirty();
    }

    private void NtfyTokenChanged(object sender, RoutedEventArgs e)
    {
        _vm.NtfyToken = ((PasswordBox)sender).Password;
        _vm.MarkDirty();
    }
}
