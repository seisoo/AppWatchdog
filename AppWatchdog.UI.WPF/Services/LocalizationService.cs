using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using System.Globalization;
using System.Threading;
using System.Windows;

public sealed class LocalizationService
{
    private readonly PipeFacade _pipe;
    private readonly BackendStateService _backend;


    private const string DefaultCulture = "en-GB";

    public string CurrentCultureName { get; private set; } = DefaultCulture;

    public LocalizationService(
        PipeFacade pipe,
        BackendStateService backend)
    {
        _pipe = pipe;
        _backend = backend;

        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var cfg = _pipe.GetConfig();

            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.CultureName))
            {
                ApplyCulture(cfg.CultureName);
                _backend.SetReady(AppStrings.service_connected);
                return;
            }

            ApplyCulture(CultureInfo.CurrentUICulture.Name);
            _backend.SetReady(AppStrings.service_connected);
        }
        catch
        {
            ApplyCulture(DefaultCulture);
            _backend.SetOffline(AppStrings.error_service_notavailable_text);
        }
    }

    public void SetLanguage(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return;

        if (cultureName == CurrentCultureName)
            return;

        ApplyCulture(cultureName);

        try
        {
            var cfg = _pipe.GetConfig();   // 🔑 WICHTIG
            if (cfg != null)
            {
                cfg.CultureName = cultureName;
                _pipe.SaveConfig(cfg);
            }
        }
        catch
        {
            // optional logging
        }
    }


    private void ApplyCulture(string cultureName)
    {
        CultureInfo culture;
        try
        {
            culture = new CultureInfo(cultureName);
        }
        catch
        {
            culture = new CultureInfo(DefaultCulture);
            cultureName = DefaultCulture;
        }

        CurrentCultureName = cultureName;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        RefreshStrings();
    }

    // =====================================================
    // RESOURCE REFRESH
    // =====================================================
    public static void RefreshStrings()
    {
        if (Application.Current.Resources["Strings"] is AppStringsProxy strings)
        {
            strings.Refresh();
        }
    }
}
