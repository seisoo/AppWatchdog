using AppWatchdog.UI.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace AppWatchdog.UI.WPF.Views.Pages;

public partial class AppsPage : Page
{
    private readonly IContentDialogService _contentDialogService;
    private readonly CancellationTokenSource _dialogCts = new();
    public AppsPage(
         AppsViewModel vm,
         IContentDialogService contentDialogService)
    {
        InitializeComponent();
        DataContext = vm;
        _contentDialogService = contentDialogService;
    }

    private async void AppsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not AppsViewModel vm || vm.SelectedApp == null)
            return;

        var content = new Grid
        {
            Margin = new Thickness(24),
            Width = 500, // ca. 60 %
            HorizontalAlignment = HorizontalAlignment.Center,
            RowDefinitions =
    {
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition { Height = new GridLength(12) },
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition { Height = GridLength.Auto }
    }
        };

        var description = new Wpf.Ui.Controls.TextBlock
        {
            Text = "Bitte gib einen neuen Namen für den Watch-Job dieser Anwendung ein ein.",
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        };

        var nameBox = new Wpf.Ui.Controls.TextBox
        {
            Text = vm.SelectedApp.Name,
            PlaceholderText = "Name des Jobs",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var description2 = new Wpf.Ui.Controls.TextBlock
        {
            Text = "Dieser Name hilft bei der Identifizierung aus der globaln Liste.",
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,16,0,0)
        };

        content.Children.Add(description);
        content.Children.Add(nameBox);
        content.Children.Add(description2);
        Grid.SetRow(nameBox, 2);
        Grid.SetRow(description2, 3);

        var result = await _contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "Jobnamen ändern",
                Content = content,
                PrimaryButtonText = "Speichern",
                CloseButtonText = "Abbrechen"
            },
            CancellationToken.None
        );

        if (result == ContentDialogResult.Primary)
        {
            var newName = nameBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newName))
                vm.SelectedApp.Name = newName;
        }

    }


}


