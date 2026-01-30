using AppWatchdog.UI.WPF.Dialogs;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels;
using AppWatchdog.UI.WPF.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

namespace AppWatchdog.UI.WPF;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            try
            {
                var service = new ServiceControlFacade("AppWatchdog");

                switch (e.Args[0])
                {
                    case "--svc-start":
                        service.StartService();
                        Environment.Exit(0);
                        break;
                    case "--svc-install":
                        service.InstallServiceFromLocalExe();
                        service.StartService();
                        Environment.Exit(0);
                        break;
                    case "--svc-reinstall":
                        service.UninstallService();
                        service.InstallServiceFromLocalExe();
                        service.StartService();
                        Environment.Exit(0);
                        break;
                }
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag))
        );

        base.OnStartup(e);


        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var purple = Color.FromRgb(160, 96, 255);
        ApplicationAccentColorManager.Apply(
            systemAccent: purple,
            primaryAccent: purple,
            secondaryAccent: purple,
            tertiaryAccent: purple
        );

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<BackendStateService>();
                services.AddNavigationViewPageProvider();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IContentDialogService, ContentDialogService>();
                services.AddSingleton<GlobalErrorDialogService>();
                services.AddSingleton<AppDialogService>();
                services.AddSingleton<ISnackbarService, SnackbarService>();
                services.AddSingleton<PipeFacade>();
                services.AddSingleton(_ => new ServiceControlFacade("AppWatchdog"));
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<ServiceViewModel>();
                services.AddSingleton<FolderPickerService>();
                services.AddSingleton<FilePickerService>();
                services.AddSingleton<ColorPickerService>();
                services.AddTransient<RestorePageViewModel>();
                services.AddTransient<BackupPageViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<AboutViewModel>();
                services.AddSingleton<ServicePage>();
                services.AddSingleton<JobsPage>();
                services.AddSingleton<AppsPage>();
                services.AddSingleton<BackupPage>();
                services.AddSingleton<RestorePage>();
                services.AddSingleton<NotificationsPage>();
                services.AddSingleton<LogsPage>();
                services.AddSingleton<AboutPage>();
                services.AddSingleton<AppsViewModel>();
                services.AddSingleton<NotificationsViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<LanguageSelectorViewModel>();
                services.AddSingleton<LocalizationService>();
                //services.AddSingleton<AppStringsProxy>();
            })
            .Build();

        _host.Start();
        var localization = _host.Services.GetRequiredService<LocalizationService>();
        var languageSelector = _host.Services.GetRequiredService<LanguageSelectorViewModel>();

        languageSelector.Initialize(localization.CurrentCultureName);

        _host.Services.GetRequiredService<MainWindow>().Show();

    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private async void OnDispatcherUnhandledException(
    object sender,
    DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        await ShowGlobalErrorAsync(e.Exception);
    }

    private async void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        await ShowGlobalErrorAsync(e.Exception);
    }

    private async void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            await ShowGlobalErrorAsync(ex);
    }

    private async Task ShowGlobalErrorAsync(Exception ex)
    {
        if (_host == null)
            return;

        var dialog = _host.Services.GetRequiredService<GlobalErrorDialogService>();
        await dialog.ShowExceptionAsync(ex);
    }
}
