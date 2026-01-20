using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppWatchdog.UI.WPF.ViewModels
{
    public partial class LanguageSelectorViewModel : ObservableObject
    {
        private readonly LocalizationService _localization;

        [ObservableProperty]
        private string selectedLanguage = "";

        public LanguageSelectorViewModel(LocalizationService localization)
        {
            _localization = localization;
        }

        public void Initialize(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return;

            if (SelectedLanguage == cultureName)
                return;

            SelectedLanguage = cultureName;
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (_localization.CurrentCultureName == value)
                return;

            _localization.SetLanguage(value);
            LocalizationService.RefreshStrings();
        }
    }

}
