namespace AppWatchdog.Service.Notifiers;

/// <summary>
/// Base class for notification channel implementations.
/// </summary>
/// <typeparam name="TSettings">Settings type for the notifier.</typeparam>
public abstract class NotifierBase<TSettings>
{
    /// <summary>
    /// Gets the settings for the notifier.
    /// </summary>
    protected readonly TSettings Settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifierBase{TSettings}"/> class.
    /// </summary>
    /// <param name="settings">Notifier settings.</param>
    protected NotifierBase(TSettings settings)
    {
        Settings = settings;
    }

    /// <summary>
    /// Validates whether the notifier is configured.
    /// </summary>
    /// <param name="error">Error message if not configured.</param>
    /// <returns><c>true</c> when configured.</returns>
    public abstract bool IsConfigured(out string? error);

    /// <summary>
    /// Gets the display name for the notifier.
    /// </summary>
    public abstract string Name { get; }
}
