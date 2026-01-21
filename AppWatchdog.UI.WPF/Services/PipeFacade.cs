using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Localization;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AppWatchdog.UI.WPF.Services;

public sealed class PipeFacade
{
    private readonly GlobalErrorDialogService _errors;
    private readonly BackendStateService _backend;

    public PipeFacade(
        GlobalErrorDialogService errors,
        BackendStateService backend)
    {
        _errors = errors;
        _backend = backend;
    }

    // =========================
    // PUBLIC API (SAFE)
    // =========================

    public WatchdogConfig? GetConfig()
        => Execute(PipeClient.GetConfig);

    public ServiceSnapshot? GetStatus()
        => Execute(PipeClient.GetStatus);

    public void SaveConfig(WatchdogConfig cfg)
        => Execute(() => PipeClient.SaveConfig(cfg));

    public void TriggerCheck()
        => Execute(PipeClient.TriggerCheck);

    public void Ping()
        => Execute(PipeClient.Ping);

    public LogDaysResponse? ListLogDays()
        => Execute(PipeClient.ListLogDays);

    public LogDayResponse? GetLogDay(string day)
        => Execute(() => PipeClient.GetLogDay(day));

    public LogPathResponse? GetLogPath()
        => Execute(PipeClient.GetLogPath);

    public void TestSmtp()
        => Execute(PipeClient.TestSmtp);

    public void TestNtfy()
        => Execute(PipeClient.TestNtfy);

    public void TestDiscord()
        => Execute(PipeClient.TestDiscord);

    public void TestTelegram()
        => Execute(PipeClient.TestTelegram);

    // =========================
    // CORE EXECUTION LOGIC
    // =========================

    private T? Execute<T>(Func<T> action)
    {
        try
        {
            var result = action();

            // Success → backend online
            _backend.SetReady(AppStrings.service_connected);

            return result;
        }
        catch (TimeoutException ex)
        {
            HandleException(
                new PipeTimeoutException(
                    AppStrings.error_service_timeout_text,
                    ex));

            return default;
        }
        catch (IOException ex)
        {
            HandleException(
                new PipeUnavailableException(
                    AppStrings.error_service_notavailable_text,
                    ex));

            return default;
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return default;
        }
    }

    private void Execute(Action action)
    {
        try
        {
            action();

            // Success → backend online
            _backend.SetReady(AppStrings.service_connected);
        }
        catch (TimeoutException ex)
        {
            HandleException(
                new PipeTimeoutException(
                    AppStrings.error_service_timeout_text,
                    ex));
        }
        catch (IOException ex)
        {
            HandleException(
                new PipeUnavailableException(
                    AppStrings.error_service_notavailable_text,
                    ex));
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    // =========================
    // CENTRAL ERROR HANDLING
    // =========================

    private void HandleException(Exception ex)
    {
        // Backend offline immediately
        _backend.SetOffline(ex.Message);

        // Fire & forget – never block caller
        _ = Task.Run(async () =>
        {
            await _errors.ShowExceptionAsync(ex);
        });
    }
}
