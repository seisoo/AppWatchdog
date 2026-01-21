namespace AppWatchdog.Service.Notifiers;

public abstract class NotifierBase<TSettings>
{
    protected readonly TSettings Settings;

    protected NotifierBase(TSettings settings)
    {
        Settings = settings;
    }
    public abstract bool IsConfigured(out string? error);
    public abstract string Name { get; }
}
