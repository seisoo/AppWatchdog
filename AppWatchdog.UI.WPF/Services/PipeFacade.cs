using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using System;
using System.IO;

namespace AppWatchdog.UI.WPF.Services;

public sealed class PipeFacade
{
    public WatchdogConfig GetConfig()
        => Execute(PipeClient.GetConfig);

    public ServiceSnapshot GetStatus()
        => Execute(PipeClient.GetStatus);

    public void SaveConfig(WatchdogConfig cfg)
        => Execute(() => PipeClient.SaveConfig(cfg));

    public void TriggerCheck()
        => Execute(PipeClient.TriggerCheck);

    public void Ping()
        => Execute(PipeClient.Ping);

    public LogDaysResponse ListLogDays()
        => Execute(PipeClient.ListLogDays);

    public LogDayResponse GetLogDay(string day)
        => Execute(() => PipeClient.GetLogDay(day));

    public void TestSmtp()
        => Execute(PipeClient.TestSmtp);

    public void TestNtfy()
        => Execute(PipeClient.TestNtfy);

    // =========================
    // ZENTRALE FEHLERBEHANDLUNG
    // =========================

    private static T Execute<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (TimeoutException ex)
        {
            throw new PipeTimeoutException(
                "Zeitüberschreitung bei der Kommunikation mit dem Watchdog-Dienst.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PipeUnavailableException(
                "Der Watchdog-Dienst ist nicht erreichbar.",
                ex);
        }
    }

    private static void Execute(Action action)
    {
        try
        {
            action();
        }
        catch (TimeoutException ex)
        {
            throw new PipeTimeoutException(
                "Zeitüberschreitung bei der Kommunikation mit dem Watchdog-Dienst.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PipeUnavailableException(
                "Der Watchdog-Dienst ist nicht erreichbar.",
                ex);
        }
    }
}
