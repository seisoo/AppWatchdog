using AppWatchdog.UI.WPF.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

public partial class LanguageSelectorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    public ObservableCollection<MainWindowViewModel.LanguageItem> Languages { get; }

    [ObservableProperty]
    private MainWindowViewModel.LanguageItem? selectedLanguage;

    public LanguageSelectorViewModel(LocalizationService localization)
    {
        _localization = localization;

        Languages =
        [
            new() { Culture = "de-DE", Flag = "/Images/Flags/de.png", DisplayName="Deutsch" },
            new() { Culture = "en-GB", Flag = "/Images/Flags/gb.png", DisplayName="English" }
        ];
    }

    public void Initialize(string cultureName)
    {
        SelectedLanguage =
            Languages.FirstOrDefault(l => l.Culture == cultureName)
            ?? Languages.First();
    }

    partial void OnSelectedLanguageChanged(MainWindowViewModel.LanguageItem? value)
    {
        if (value == null)
            return;

        if (_localization.CurrentCultureName == value.Culture)
            return;

        _localization.SetLanguage(value.Culture);
        LocalizationService.RefreshStrings();
    }
}
