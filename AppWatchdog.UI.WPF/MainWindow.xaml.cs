using AppWatchdog.UI.WPF.ViewModels;
using AppWatchdog.UI.WPF.Views.Pages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace AppWatchdog.UI.WPF;

public partial class MainWindow : FluentWindow
{
    public MainWindowViewModel ViewModel { get; }
    public static ContentDialogHost? GlobalDialogHost { get; private set; }

    private readonly INavigationService _navigationService;
    private readonly INavigationViewPageProvider _pageProvider;
    private readonly IContentDialogService _contentDialogService;
    private readonly IContentDialogService _dialogService;
    private bool _isClosingConfirmed;

    public MainWindow(
         MainWindowViewModel viewModel,
         INavigationService navigationService,
         INavigationViewPageProvider pageProvider,
         IContentDialogService contentDialogService,
         IContentDialogService dialogService)
    {
        InitializeComponent();
        DataContext = this;
        ViewModel = viewModel;
        _navigationService = navigationService;
        _pageProvider = pageProvider;
        _contentDialogService = contentDialogService;
        _dialogService = dialogService;

        _contentDialogService.SetDialogHost(RootContentDialogHost);
        GlobalDialogHost = RootContentDialogHost;

        _navigationService.SetNavigationControl(RootNavigationView);
        RootNavigationView.SetPageProviderService(_pageProvider);

        Loaded += (_, __) => _navigationService.Navigate(typeof(ServicePage));
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_isClosingConfirmed)
            return;

        if (!ViewModel.AnyDirty)
            return;

        e.Cancel = true; // ⛔ Schließen stoppen

        var content = new StackPanel
        {
            Margin = new Thickness(12, 12, 12, 0),
            Children =
        {
            new SymbolIcon
            {
                Symbol = SymbolRegular.Warning24,
                FontSize = 48,
                Margin = new Thickness(0,0,0,15)
            },
            new Wpf.Ui.Controls.TextBlock
            {
                Text =
                    "Es gibt ungespeicherte Änderungen. " +
                    "Was möchtest du tun?",
                TextWrapping = TextWrapping.Wrap
            }
        }
        };

        var result = await _dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "Warnung!",
                Content = content,
                PrimaryButtonText = "Speichern",
                SecondaryButtonText = "Verwerfen",
                CloseButtonText = "Abbrechen",
            },
            CancellationToken.None
        );

        switch (result)
        {
            case ContentDialogResult.Primary:
                // 💾 speichern & schließen
                ViewModel.SaveAll();
                _isClosingConfirmed = true;
                Close();
                break;

            case ContentDialogResult.Secondary:
                // 🗑 verwerfen & schließen
                ViewModel.DiscardAll();
                _isClosingConfirmed = true;
                Close();
                break;

            case ContentDialogResult.None:
                // ❌ abbrechen
                break;
        }
    }
}
