using AppWatchdog.Service;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

if (!EventLog.SourceExists("AppWatchdog"))
{
    EventLog.CreateEventSource("AppWatchdog", "Application");
}

EventLog.WriteEntry(
    "AppWatchdog",
    "Service START erreicht",
    EventLogEntryType.Information);



IHost host = Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureLogging(logging =>
            {
                logging.AddEventLog(o =>
                {
                    o.SourceName = "AppWatchdog";
                    o.LogName = "Application";
                    o.Filter = (category, level) => level >= LogLevel.Information;
                });
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<Worker>();
            })
            .Build();

await host.RunAsync();
