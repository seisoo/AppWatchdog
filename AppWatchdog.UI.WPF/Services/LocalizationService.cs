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

    private WatchdogConfig? _config;

    // Default fallback language (MUST exist)
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

    // =====================================================
    // INITIALIZATION (STARTUP-SAFE)
    // =====================================================
    private void Initialize()
    {
        try
        {
            _config = _pipe.GetConfig();

            if (_config != null &&
                !string.IsNullOrWhiteSpace(_config.CultureName))
            {
                ApplyCulture(_config.CultureName, save: false);
                _backend.SetReady(AppStrings.service_connected);
                return;
            }

            // Pipe reachable but no culture stored
            ApplyCulture(CultureInfo.CurrentUICulture.Name, save: false);
            _backend.SetReady(AppStrings.service_connected);
        }
        catch
        {
            // Pipe unreachable → SAFE FALLBACK
            ApplyCulture(DefaultCulture, save: false);

            _backend.SetOffline(
                AppStrings.error_service_notavailable_text);
        }
    }

    // =====================================================
    // PUBLIC API
    // =====================================================
    public void SetLanguage(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return;

        if (cultureName == CurrentCultureName)
            return;

        ApplyCulture(cultureName, save: true);
    }

    // =====================================================
    // CORE CULTURE LOGIC
    // =====================================================
    private void ApplyCulture(string cultureName, bool save)
    {
        // Validate culture
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

        // Persist only if backend config exists
        if (save && _config != null)
        {
            _config.CultureName = cultureName;
            _pipe.SaveConfig(_config);
        }
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
