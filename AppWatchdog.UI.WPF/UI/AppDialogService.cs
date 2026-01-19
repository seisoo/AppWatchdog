using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace AppWatchdog.UI.WPF.Dialogs;

public sealed class AppDialogService
{
    private readonly IContentDialogService _contentDialogService;

    public AppDialogService(IContentDialogService contentDialogService)
    {
        _contentDialogService = contentDialogService;
    }

    public async Task<bool> ShowConfirmAsync(
        string title,
        string message,
        string confirmText = "OK",
        string cancelText = "Abbrechen",
        CancellationToken cancellationToken = default)
    {
        var content = BuildContent(message);

        var result = await _contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = content,
                PrimaryButtonText = confirmText,
                CloseButtonText = cancelText
            },
            cancellationToken);

        return result == ContentDialogResult.Primary;
    }

    public async Task ShowInfoAsync(
        string title,
        string message,
        string closeText = "OK",
        CancellationToken cancellationToken = default)
    {
        var content = BuildContent(message);

        await _contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = content,
                CloseButtonText = closeText
            },
            cancellationToken);
    }

    public async Task ShowErrorAsync(
        string title,
        string message,
        string closeText = "OK",
        CancellationToken cancellationToken = default)
    {
        var content = BuildContent(message, isError: true);

        await _contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = content,
                CloseButtonText = closeText
            },
            cancellationToken);
    }

    private static UIElement BuildContent(string message, bool isError = false)
    {
        return new Grid
        {
            Margin = new Thickness(24),
            Width = 500,
            HorizontalAlignment = HorizontalAlignment.Center,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                new Wpf.Ui.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = isError ? 1.0 : 0.85
                }
            }
        };
    }
}
