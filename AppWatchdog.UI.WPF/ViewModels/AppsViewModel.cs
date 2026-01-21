using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Dialogs;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class AppsViewModel : DirtyViewModelBase
{
    private readonly AppDialogService _dialog;
    private readonly PipeFacade _pipe;
    private readonly BackendStateService _backend;
    private bool _activated;

    public ObservableCollection<WatchedAppItemViewModel> Apps { get; } = new();

    [ObservableProperty]
    private bool _isContentEnabled;

    [ObservableProperty]
    private WatchedAppItemViewModel? _selectedApp;

    public bool HasSelectedApp => SelectedApp != null;

    [ObservableProperty]
    private int _checkIntervalSeconds = 5;

    [ObservableProperty]
    private int _mailIntervalHours = 12;

    [ObservableProperty]
    private bool _kumaEnabled;

    [ObservableProperty]
    private string _kumaBaseUrl = "";

    [ObservableProperty]
    private string _kumaPushToken = "";

    [ObservableProperty]
    private int _kumaIntervalSeconds = 60;

    public string HintText => AppStrings.apps_hint_text;

    public AppsViewModel(
        PipeFacade pipe,
        AppDialogService dialog,
        BackendStateService backend)
    {
        _pipe = pipe;
        _dialog = dialog;
        _backend = backend;

        Apps.CollectionChanged += (_, __) => MarkDirty();
        _backend.PropertyChanged += OnBackendStateChanged;
    }

    public async Task ActivateAsync()
    {
        if (!_backend.IsReady)
        {
            IsContentEnabled = false;
            return;
        }

        if (!_activated)
        {
            _activated = true;
            await LoadAsync();
        }

        IsContentEnabled = true;
    }

    public void Deactivate()
    {
        IsContentEnabled = false;
    }

    private void OnBackendStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BackendStateService.State))
            return;

        IsContentEnabled = _backend.IsReady;
    }

    private async Task LoadAsync()
    {
        using var _ = SuppressDirty();

        var cfg = await Task.Run(() => _pipe.GetConfig());

        CheckIntervalSeconds = cfg.CheckIntervalSeconds;
        MailIntervalHours = cfg.MailIntervalHours;

        SyncApps(cfg.Apps);

        SelectedApp ??= Apps.FirstOrDefault();

        ClearDirty();
    }


    [RelayCommand]
    private async Task ReloadAsync()
    {
        await LoadAsync();
    }

    private void SyncApps(System.Collections.Generic.IEnumerable<WatchedApp> models)
    {
        // 1️⃣ Entfernen, was es nicht mehr gibt
        for (int i = Apps.Count - 1; i >= 0; i--)
        {
            var vm = Apps[i];
            if (!models.Any(m => m.ExePath == vm.ExePath))
                Apps.RemoveAt(i);
        }

        // 2️⃣ Updaten / Hinzufügen
        foreach (var model in models)
        {
            var vm = Apps.FirstOrDefault(a => a.ExePath == model.ExePath);

            if (vm == null)
            {
                Apps.Add(WatchedAppItemViewModel.FromModel(model, MarkDirty));
            }
            else
            {
                // 🔁 bestehendes VM aktualisieren
                vm.Name = model.Name;
                vm.Enabled = model.Enabled;
                vm.Arguments = model.Arguments;
                vm.ExePath = model.ExePath;

                if (model.UptimeKuma != null)
                {
                    vm.KumaEnabled = model.UptimeKuma.Enabled;
                    vm.KumaBaseUrl = model.UptimeKuma.BaseUrl;
                    vm.KumaPushToken = model.UptimeKuma.PushToken;
                    vm.KumaIntervalSeconds = model.UptimeKuma.IntervalSeconds;
                }
                else
                {
                    vm.KumaEnabled = false;
                }
            }
        }
    }


    [RelayCommand]
    private void Add()
    {
        var vm = new WatchedAppItemViewModel(MarkDirty)
        {
            Name = AppStrings.apps_new_application,
            Enabled = true
        };

        Apps.Add(vm);
        SelectedApp = vm;

        MarkDirty();
        SaveStateText = AppStrings.config_not_saved;
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedApp == null)
            return;

        var confirm = await _dialog.ShowConfirmAsync(
            AppStrings.apps_remove_application,
            string.Format(AppStrings.apps_remove_question, SelectedApp.Name),
            AppStrings.delete,
            AppStrings.abort);

        if (!confirm)
            return;

        Apps.Remove(SelectedApp);
        SelectedApp = Apps.FirstOrDefault();

        MarkDirty();
        SaveStateText = AppStrings.config_not_saved;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            var cfg = await Task.Run(() => _pipe.GetConfig());

            cfg.CheckIntervalSeconds = CheckIntervalSeconds;
            cfg.MailIntervalHours = MailIntervalHours;

            cfg.Apps.Clear();
            foreach (var vm in Apps)
                cfg.Apps.Add(vm.ToModel());

            await Task.Run(() => _pipe.SaveConfig(cfg));
            ClearDirty();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                AppStrings.config_saving_failed,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanSave() => IsDirty;

    protected override void OnIsDirtyChanged(bool value)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            SaveCommand.NotifyCanExecuteChanged();
        else
            Application.Current.Dispatcher.Invoke(SaveCommand.NotifyCanExecuteChanged);
    }

    partial void OnSelectedAppChanged(WatchedAppItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedApp));
    }

    partial void OnCheckIntervalSecondsChanged(int value) => MarkDirty();
    partial void OnMailIntervalHoursChanged(int value) => MarkDirty();
}
