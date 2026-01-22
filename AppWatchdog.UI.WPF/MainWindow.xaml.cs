using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels;
using AppWatchdog.UI.WPF.Views.Pages;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
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
    public LanguageSelectorViewModel LanguageSelector { get; }
    public BackendStateService BackendState { get; }

    private readonly INavigationService _navigationService;
    private readonly INavigationViewPageProvider _pageProvider;
    private readonly IContentDialogService _contentDialogService;
    private readonly IContentDialogService _dialogService;
    private bool _isClosingConfirmed;

    // 
    public MainWindow(
         MainWindowViewModel viewModel,
         INavigationService navigationService,
         LanguageSelectorViewModel languageSelector,
         BackendStateService backendStateService,
         INavigationViewPageProvider pageProvider,
         IContentDialogService contentDialogService,
         IContentDialogService dialogService,
         ISnackbarService snackbarService)
    {
        InitializeComponent();
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        DataContext = this;
        ViewModel = viewModel;
        _navigationService = navigationService;
        _pageProvider = pageProvider;
        _contentDialogService = contentDialogService;
        _dialogService = dialogService;
        BackendState = backendStateService;

        _contentDialogService.SetDialogHost(RootContentDialogHost);
        GlobalDialogHost = RootContentDialogHost;

        _navigationService.SetNavigationControl(RootNavigationView);
        RootNavigationView.SetPageProviderService(_pageProvider);

        LanguageSelector = languageSelector;
        LanguageSelector.Initialize(
            CultureInfo.CurrentUICulture.Name
        );


        Loaded += (_, __) => _navigationService.Navigate(typeof(ServicePage));
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_isClosingConfirmed)
            return;

        if (!ViewModel.AnyDirty)
            return;

        e.Cancel = true;

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
                Text = AppStrings.config_not_saved_changes,
                TextWrapping = TextWrapping.Wrap
            }
        }
        };

        var result = await _dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = AppStrings.warn,
                Content = content,
                PrimaryButtonText = AppStrings.save,
                SecondaryButtonText = AppStrings.discard,
                CloseButtonText = AppStrings.abort,
            },
            CancellationToken.None
        );

        switch (result)
        {
            case ContentDialogResult.Primary:
                ViewModel.SaveAll();
                _isClosingConfirmed = true;
                Close();
                break;

            case ContentDialogResult.Secondary:
                ViewModel.DiscardAll();
                _isClosingConfirmed = true;
                Close();
                break;

            case ContentDialogResult.None:
                break;
        }
    }
}
