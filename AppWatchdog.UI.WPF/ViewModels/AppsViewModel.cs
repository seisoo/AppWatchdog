using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Common;
using AppWatchdog.UI.WPF.Dialogs;
using AppWatchdog.UI.WPF.Localization;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class AppsViewModel : DirtyViewModelBase
{
    private readonly AppDialogService _dialog;
    private readonly PipeFacade _pipe;
    private readonly BackendStateService _backend;

    public ObservableCollection<WatchedAppItemViewModel> Apps { get; } = new();

    [ObservableProperty]
    private bool _isContentEnabled;

    [ObservableProperty]
    private WatchedAppItemViewModel? _selectedApp;

    [ObservableProperty]
    
    private int _checkIntervalMinutes = 5;

    [ObservableProperty]
    
    private int _mailIntervalHours = 12;

    [ObservableProperty]
    
    private bool _kumaEnabled;

    [ObservableProperty]
    
    private string _kumaBaseUrl = string.Empty;

    [ObservableProperty]
    
    private string _kumaPushToken = string.Empty;

    [ObservableProperty]
    
    private int _kumaIntervalSeconds = 60;

    private bool _activated;


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
            await Task.Run(Load);   
        }

        IsContentEnabled = true; 
    }

    public void Deactivate()
    {
        IsContentEnabled = false;
    }

    private async void OnBackendStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BackendStateService.State))
            return;

        if (_backend.IsReady)
            await ActivateAsync();
        else
            IsContentEnabled = false;
    }


    private void Load()
    {
        Apps.Clear();

        var cfg = _pipe.GetConfig();
        CheckIntervalMinutes = cfg.CheckIntervalMinutes;
        MailIntervalHours = cfg.MailIntervalHours;


        foreach (var app in cfg.Apps)
        {
            var vm = WatchedAppItemViewModel.FromModel(app, MarkDirty);
            Apps.Add(vm);
        }

        SelectedApp = Apps.FirstOrDefault();

        ClearDirty();
    }


    [RelayCommand]
    private void Reload()
    {
        Load();
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

        IsDirty = true;
        SaveStateText = AppStrings.config_not_saved;
    }


    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedApp == null)
            return;

        var confirm = await _dialog.ShowConfirmAsync(
            title: AppStrings.apps_remove_application,
            message: string.Format(AppStrings.apps_remove_question, SelectedApp.Name),
            confirmText: AppStrings.delete,
            cancelText: AppStrings.abort);

        if (!confirm)
            return;

        Apps.Remove(SelectedApp);
        SelectedApp = null;

        IsDirty = true;
        SaveStateText = AppStrings.config_not_saved;
    }

    


    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        try
        {
            var cfg = _pipe.GetConfig();

            cfg.CheckIntervalMinutes = CheckIntervalMinutes;
            cfg.MailIntervalHours = MailIntervalHours;


            cfg.Apps.Clear();
            foreach (var vm in Apps)
                cfg.Apps.Add(vm.ToModel());

            _pipe.SaveConfig(cfg);

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

    private bool CanSave()
        => IsDirty;

    protected override void OnIsDirtyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    #region Dirty-Events
    partial void OnCheckIntervalMinutesChanged(int value)
    => MarkDirty();

    partial void OnMailIntervalHoursChanged(int value)
        => MarkDirty();

    partial void OnKumaEnabledChanged(bool value)
        => MarkDirty();

    partial void OnKumaBaseUrlChanged(string value)
        => MarkDirty();

    partial void OnKumaPushTokenChanged(string value)
        => MarkDirty();

    partial void OnKumaIntervalSecondsChanged(int value)
        => MarkDirty();

    #endregion


}
