using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace AppWatchdog.UI.WPF.Services;

public static class UiDialogHelper
{
    public static async Task ShowInfoAsync(
        IContentDialogService dialogService,
        string title,
        string message,
        SymbolRegular icon)
    {
        var panel = BuildPanel(icon, message);

        await dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = panel,
                CloseButtonText = "OK"
            },
            CancellationToken.None
        );
    }

    private static StackPanel BuildPanel(SymbolRegular icon, string message)
    {
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 420,
            Children =
            {
                new SymbolIcon
                {
                    Symbol = icon,
                    FontSize = 64,
                    Margin = new Thickness(0, 0, 0, 16)
                },
                new Wpf.Ui.Controls.TextBlock
                {
                    Text = message,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }
}
