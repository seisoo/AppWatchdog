namespace AppWatchdog.Service.Jobs;

public interface IJob : IDisposable
{
    string Id { get; }
    TimeSpan Interval { get; }
    Task ExecuteAsync(CancellationToken ct);
}
