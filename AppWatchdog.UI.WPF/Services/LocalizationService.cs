using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using System.Globalization;
using System.Threading;
using System.Windows;

public sealed class LocalizationService
{
    private readonly PipeFacade _pipe;
    private WatchdogConfig _config;

    public string CurrentCultureName { get; private set; } = "";

    public LocalizationService(PipeFacade pipe)
    {
        _pipe = pipe;
        _config = _pipe.GetConfig();

        var cultureName =
            !string.IsNullOrWhiteSpace(_config.CultureName)
                ? _config.CultureName
                : CultureInfo.CurrentUICulture.Name;

        ApplyCulture(cultureName, save: false);
    }

    public void SetLanguage(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return;

        if (cultureName == CurrentCultureName)
            return;

        ApplyCulture(cultureName, save: true);
    }

    private void ApplyCulture(string cultureName, bool save)
    {
        CurrentCultureName = cultureName;

        var culture = new CultureInfo(cultureName);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (save)
        {
            _config.CultureName = cultureName;
            _pipe.SaveConfig(_config);
        }
    }
    public static void RefreshStrings()
    {
        if (Application.Current.Resources["Strings"] is AppStringsProxy strings)
        {
            strings.Refresh();
        }
    }

}
