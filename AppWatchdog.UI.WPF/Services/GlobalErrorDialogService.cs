using AppWatchdog.UI.WPF.Common;
using CommunityToolkit.Mvvm.DependencyInjection;
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

namespace AppWatchdog.UI.WPF.Services;

public sealed class GlobalErrorDialogService
{
    private readonly IContentDialogService _dialogService;

    public GlobalErrorDialogService(IContentDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task ShowExceptionAsync(Exception ex)
    {
        if (MainWindow.GlobalDialogHost != null)
        {
            _dialogService.SetDialogHost(MainWindow.GlobalDialogHost);
        }
        else
        {
            if (ex is PipeProtocolMismatchException pmEx)
            {
                var result = System.Windows.MessageBox.Show(
        ex.Message + "\n\n" +
        "Der Dienst ist nicht kompatibel.\n\n" +
        "Die Anwendung kann den Dienst jetzt automatisch aktualisieren.\n" +
        "Dazu wird sie ggf. als Administrator neu gestartet.\n\n" +
        "Möchtest du fortfahren?",
        "Dienst-Aktualisierung erforderlich",
        System.Windows.MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                try
                {
                    if (!IsRunningAsAdministrator())
                    {
                        RestartAsAdministrator("--service-update");
                        Application.Current.Shutdown();
                        return;
                    }

                    RunServiceUpdate();
                    RestartNormally();
                    Application.Current.Shutdown();
                }
                catch (Exception updateEx)
                {
                    System.Windows.MessageBox.Show(
                        "Die automatische Dienst-Aktualisierung ist fehlgeschlagen:\n\n" +
                        updateEx,
                        "Kritischer Fehler",
                        System.Windows.MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return;
            }
            else if (ex is PipeTimeoutException timeoutEx)
            {
                var result = System.Windows.MessageBox.Show(
                    timeoutEx.Message + "\n\n" +
                    "Der Dienst ist momentan nicht erreichbar.\n\n" +
                    "Die Anwendung kann prüfen, ob der Dienst installiert ist,\n" +
                    "ihn starten oder bei Bedarf installieren.\n\n" +
                    "Möchtest du fortfahren?",
                    "Dienst nicht erreichbar",
                    System.Windows.MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                try
                {
                    if (!IsRunningAsAdministrator())
                    {
                        RestartAsAdministrator("--service-ensure");
                        Application.Current.Shutdown();
                        return;
                    }

                    EnsureServiceRunning();
                    RestartNormally();
                    Application.Current.Shutdown();
                }
                catch (Exception ensureEx)
                {
                    System.Windows.MessageBox.Show(
                        "Der Dienst konnte nicht gestartet oder installiert werden:\n\n" +
                        ensureEx,
                        "Kritischer Fehler",
                        System.Windows.MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return;
            }
        }

        var (title, message, icon) = MapException(ex);

        await _dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = BuildContent(icon, message, ex),
                CloseButtonText = "OK"
            },
            CancellationToken.None
        );
    }

    private static bool IsServiceInstalled(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            _ = sc.Status; // erzwingt Zugriff
            return true;
        }
        catch (InvalidOperationException)
        {
            // Dienst existiert nicht
            return false;
        }
    }


private static void EnsureServiceRunning()
    {
        var service = new ServiceControlFacade("AppWatchdog");

        if (IsServiceInstalled("AppWatchdog"))
        {
            service.StartService();
        }
        else
        {
            service.InstallServiceFromLocalExe();
            service.StartService();
        }
    }

    private static bool IsRunningAsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    private static void RestartAsAdministrator(string args)
    {
        var exePath = Process.GetCurrentProcess().MainModule!.FileName!;

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            Verb = "runas",         
            UseShellExecute = true
        });
    }

    private static void RestartNormally()
    {
        var exePath = Process.GetCurrentProcess().MainModule!.FileName!;

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true  // normaler User
        });
    }

    private static void RunServiceUpdate()
    {
        var service = new ServiceControlFacade("AppWatchdog");

        service.UninstallService();
        service.InstallServiceFromLocalExe();
        service.StartService();
    }

    private static (string title, string message, SymbolRegular icon) MapException(Exception ex)
    {
        return ex switch
        {
            PipeTimeoutException =>
                ("Timeout", "Der Watchdog-Dienst antwortet nicht rechtzeitig.", SymbolRegular.Clock24),

            PipeUnavailableException =>
                ("Dienst nicht erreichbar", "Der Watchdog-Dienst läuft nicht oder ist nicht erreichbar.", SymbolRegular.CloudOff24),

            InvalidOperationException =>
                ("Ungültiger Zustand", ex.Message, SymbolRegular.Warning24),

            _ =>
                ("Unerwarteter Fehler", ex.Message, SymbolRegular.ErrorCircle24)
        };
    }

    private static StackPanel BuildContent(
     SymbolRegular icon,
     string message,
     Exception ex)
    {
        var detailsText = BuildExceptionDetails(ex);

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
                Text = message,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            },

            new Expander
            {
                Header = "Details anzeigen",
                IsExpanded = false,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new ScrollViewer
                {
                    Height = 180,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new Wpf.Ui.Controls.TextBox
                    {
                        Text = detailsText,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.NoWrap,
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
