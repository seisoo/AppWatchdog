using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Localization;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using static System.Net.Mime.MediaTypeNames;

namespace AppWatchdog.UI.WPF.Services;

public sealed class GlobalErrorDialogService
{
    private readonly IContentDialogService _dialogService;
    private readonly BackendStateService _backend;
    private static readonly SemaphoreSlim _dialogGate = new(1, 1);

    public GlobalErrorDialogService(
        IContentDialogService dialogService,
        BackendStateService backend)
    {
        _dialogService = dialogService;
        _backend = backend;
    }

    public async Task ShowExceptionAsync(Exception ex)
    {
        if (ex is null)
            return;

        _backend.SetOffline(ex.Message);

        await WaitForDialogHostAsync();
        await RunOnUIAsync(() =>
        {
            _dialogService.SetDialogHost(MainWindow.GlobalDialogHost!);
            return Task.CompletedTask;
        });

        await _dialogGate.WaitAsync();
        try
        {
            // Spezialfälle zuerst
            if (ex is PipeProtocolMismatchException)
            {
                await HandleProtocolMismatchAsync(ex);
                return;
            }

            if (ex is PipeTimeoutException || ex is PipeUnavailableException || ex is System.TimeoutException)
            {
                await HandlePipeNotReachableAsync(ex);
                return;
            }

            var (title, message, icon) = MapException(ex);

            await RunOnUIAsync(async () =>
            {
                await _dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = title,
                        Content = BuildContent(icon, message, ex),
                        CloseButtonText = AppStrings.ok
                    },
                    CancellationToken.None
                );
            });
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    private async Task HandlePipeNotReachableAsync(Exception ex)
    {
        var serviceName = "AppWatchdog";

        bool installed = IsServiceInstalled(serviceName);
        if (!installed)
        {
            var result = await RunOnUIAsync(() =>
                _dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = AppStrings.error_service_not_installed_title,
                        Content = BuildContent(
                            SymbolRegular.Wrench24,
                            AppStrings.error_service_not_installed_text,
                            ex),
                        PrimaryButtonText = AppStrings.install,
                        SecondaryButtonText = AppStrings.cancel,
                        CloseButtonText = AppStrings.close
                    },
                    CancellationToken.None
                )
            );

            if (result == ContentDialogResult.Primary)
            {
                await ExecuteServiceOperationAsync(ServiceOperation.Install);
            }

            return;
        }

        bool running = IsServiceRunning(serviceName);
        if (!running)
        {
            var result = await RunOnUIAsync(() =>
                _dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = AppStrings.error_service_not_running_title,
                        Content = BuildContent(
                            SymbolRegular.Play24,
                            AppStrings.error_service_not_running_text,
                            ex),
                        PrimaryButtonText = AppStrings.start,
                        SecondaryButtonText = AppStrings.cancel,
                        CloseButtonText = AppStrings.close
                    },
                    CancellationToken.None
                )
            );

            if (result == ContentDialogResult.Primary)
            {
                await ExecuteServiceOperationAsync(ServiceOperation.Start);
            }

            return;
        }

        var (title, message, icon) = MapException(ex);

        await RunOnUIAsync(async () =>
        {
            await _dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = title,
                    Content = BuildContent(icon, message, ex),
                    CloseButtonText = AppStrings.ok
                },
                CancellationToken.None
            );
        });
    }

    private async Task HandleProtocolMismatchAsync(Exception ex)
    {
        var result = await RunOnUIAsync(() =>
            _dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = AppStrings.error_service_update_required,
                    Content = BuildContent(
                        SymbolRegular.Warning24,
                        AppStrings.error_service_update_required_text,
                        ex),
                    PrimaryButtonText = AppStrings.reinstall,
                    SecondaryButtonText = AppStrings.cancel,
                    CloseButtonText = AppStrings.close
                },
                CancellationToken.None
            )
        );

        if (result == ContentDialogResult.Primary)
        {
            await ExecuteServiceOperationAsync(ServiceOperation.Reinstall);
        }
    }

    private enum ServiceOperation
    {
        Start,
        Install,
        Reinstall
    }

    private async Task ExecuteServiceOperationAsync(ServiceOperation op)
    {
        _backend.SetOffline(AppStrings.service_action_in_progress);

        try
        {
            if (IsRunningAsAdministrator())
            {
                RunServiceAction(op);
            }
            else
            {
                bool ok = await RunElevatedHelperAndWaitAsync(op);
                if (!ok)
                {
                    await ShowInfoAsync(
                        AppStrings.error_uac_cancelled_title,
                        AppStrings.error_uac_cancelled_text,
                        SymbolRegular.ShieldError24);
                    return;
                }
            }

            _backend.SetReady(AppStrings.service_connected);

            await ShowInfoAsync(
                AppStrings.service_action_success_title,
                AppStrings.service_action_success_text,
                SymbolRegular.CheckmarkCircle24);
        }
        catch (Exception ex)
        {
            _backend.SetOffline(ex.Message);

            await RunOnUIAsync(async () =>
            {
                await _dialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = AppStrings.error_service_action_failed_title,
                        Content = BuildContent(SymbolRegular.ErrorCircle24, AppStrings.error_service_action_failed_text, ex),
                        CloseButtonText = AppStrings.ok
                    },
                    CancellationToken.None
                );
            });
        }
    }

    private static void RunServiceAction(ServiceOperation op)
    {
        var service = new ServiceControlFacade("AppWatchdog");

        switch (op)
        {
            case ServiceOperation.Start:
                service.StartService();
                break;

            case ServiceOperation.Install:
                service.InstallServiceFromLocalExe();
                service.StartService();
                break;

            case ServiceOperation.Reinstall:
                service.UninstallService();
                service.InstallServiceFromLocalExe();
                service.StartService();
                break;
        }
    }

    private static async Task<bool> RunElevatedHelperAndWaitAsync(ServiceOperation op)
    {
        string args = op switch
        {
            ServiceOperation.Start => "--svc-start",
            ServiceOperation.Install => "--svc-install",
            ServiceOperation.Reinstall => "--svc-reinstall",
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

        var exePath = Process.GetCurrentProcess().MainModule!.FileName!;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                Verb = "runas",            
                UseShellExecute = true
            };

            var proc = Process.Start(psi);
            if (proc == null)
                return false;

            await Task.Run(() => proc.WaitForExit());

            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsServiceInstalled(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            _ = sc.Status;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsServiceRunning(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRunningAsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task WaitForDialogHostAsync()
    {
        while (MainWindow.GlobalDialogHost == null)
            await Task.Delay(50);
    }

    private static async Task<T> RunOnUIAsync<T>(Func<Task<T>> action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted)
            return await action(); // best effort

        if (app.Dispatcher.CheckAccess())
            return await action();

        return await app.Dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private static async Task RunOnUIAsync(Func<Task> action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted)
        {
            await action();
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            await action();
            return;
        }

        await app.Dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private async Task ShowInfoAsync(string title, string message, SymbolRegular icon)
    {
        await RunOnUIAsync(async () =>
        {
            await _dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = title,
                    Content = BuildContent(icon, message, new Exception(message)),
                    CloseButtonText = AppStrings.ok
                },
                CancellationToken.None
            );
        });
    }

    private static (string title, string message, SymbolRegular icon) MapException(Exception ex)
    {
        return ex switch
        {
            PipeTimeoutException =>
                (AppStrings.error_service_timeout,
                 AppStrings.error_service_timeout_text,
                 SymbolRegular.Clock24),

            PipeUnavailableException =>
                (AppStrings.error_service_notavailable,
                 AppStrings.error_service_notavailable_text,
                 SymbolRegular.CloudOff24),

            InvalidOperationException =>
                (AppStrings.error_service_invalid_state,
                 ex.Message,
                 SymbolRegular.Warning24),

            _ =>
                (AppStrings.error_service_unexpected_error,
                 ex.Message,
                 SymbolRegular.ErrorCircle24)
        };
    }

    private static StackPanel BuildContent(SymbolRegular icon, string message, Exception ex)
    {
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 520,
            Children =
            {
                new SymbolIcon
                {
                    Symbol = icon,
                    FontSize = 64,
                    Margin = new Thickness(0, 0, 0, 16),
                    HorizontalAlignment = HorizontalAlignment.Center
                },

                new Wpf.Ui.Controls.TextBlock
                {
                    Text = (message ?? "").Replace("\\n", Environment.NewLine),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12)
                },

                new Expander
                {
                    Header = AppStrings.error_service_dialog_showdetails,
                    Content = new ScrollViewer
                    {
                        Height = 180,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = new Wpf.Ui.Controls.TextBox
                        {
                            Text = BuildExceptionDetails(ex),
                            IsReadOnly = true,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12,
                            BorderThickness = new Thickness(0),
                            Background = Brushes.Transparent
                        }
                    }
                }
            }
        };
    }

    private static string BuildExceptionDetails(Exception ex)
    {
        var sb = new StringBuilder();
        int level = 0;

        while (ex != null)
        {
            sb.AppendLine(level == 0 ? "Exception:" : $"Inner Exception {level}:");
            sb.AppendLine(ex.GetType().FullName);
            sb.AppendLine(ex.Message);
            sb.AppendLine();
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine(new string('-', 50));

            ex = ex.InnerException;
            level++;
        }

        return sb.ToString();
    }
}
