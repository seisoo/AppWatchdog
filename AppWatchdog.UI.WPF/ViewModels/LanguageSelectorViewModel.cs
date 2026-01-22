using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.ViewModels;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Wpf.Ui;
using Wpf.Ui.Controls;

public partial class LanguageSelectorViewModel : DirtyViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<MainWindowViewModel.LanguageItem> Languages { get; }

    [ObservableProperty]
    private MainWindowViewModel.LanguageItem? selectedLanguage;

    public LanguageSelectorViewModel(
        LocalizationService localization,
        ISnackbarService snackbar)
    {
        _localization = localization;
        _snackbar = snackbar;

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

        RunOnUiThread(() =>
        {
            _snackbar.Show(
                AppStrings.language,
                AppStrings.language_changed,
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                TimeSpan.FromSeconds(4));
        });
    }
}
