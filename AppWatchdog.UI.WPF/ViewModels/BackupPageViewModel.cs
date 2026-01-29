using AppWatchdog.Shared;
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
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class BackupPageViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly BackendStateService _backend;
    private readonly ISnackbarService _snackbar;
    private readonly FolderPickerService _folderPicker;
    private readonly FilePickerService _filePicker;

    private bool _activated;

    public Array BackupSourceTypes => Enum.GetValues(typeof(BackupSourceType));
    public Array BackupTargetTypes => Enum.GetValues(typeof(BackupTargetType));

    public ObservableCollection<BackupPlanItemViewModel> Plans { get; } = new();

    [ObservableProperty]
    private bool _isContentEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private BackupPlanItemViewModel? _selectedPlan;

    public bool HasSelected => SelectedPlan != null;

    public bool CanRunNow => SelectedPlan != null && SelectedPlan.Enabled;

    public BackupPageViewModel(
        PipeFacade pipe,
        BackendStateService backend,
        ISnackbarService snackbar,
        FolderPickerService folderPicker,
        FilePickerService filePicker)
    {
        _pipe = pipe;
        _backend = backend;
        _snackbar = snackbar;
        _folderPicker = folderPicker;
        _filePicker = filePicker;

        Plans.CollectionChanged += (_, __) => MarkDirty();
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

    public void Deactivate() => IsContentEnabled = false;

    private async void OnBackendStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BackendStateService.State) && _backend.IsReady && _activated)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            using var _ = SuppressDirty();

            var cfg = await Task.Run(() => _pipe.GetConfig());
            if (cfg == null)
                return;

            SyncPlans(cfg.Backups);

            SelectedPlan ??= Plans.FirstOrDefault();

            ClearDirty();

            RunOnUiThread(() =>
            {
                SaveCommand.NotifyCanExecuteChanged();
                RunNowCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasSelected));
                OnPropertyChanged(nameof(CanRunNow));
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SyncPlans(System.Collections.Generic.IEnumerable<BackupPlanConfig> models)
    {
        for (int i = Plans.Count - 1; i >= 0; i--)
        {
            var vm = Plans[i];
            if (!models.Any(m => string.Equals(m.Id, vm.Id, StringComparison.OrdinalIgnoreCase)))
                Plans.RemoveAt(i);
        }

        foreach (var model in models)
        {
            var vm = Plans.FirstOrDefault(p => string.Equals(p.Id, model.Id, StringComparison.OrdinalIgnoreCase));
            if (vm == null)
            {
                var created = BackupPlanItemViewModel.FromModel(model, MarkDirty, _folderPicker, _filePicker);
                created.PropertyChanged += OnPlanVmChanged;
                Plans.Add(created);
            }
            else
            {
                vm.UpdateFromModel(model);
            }
        }

        foreach (var vm in Plans)
        {
            vm.PropertyChanged -= OnPlanVmChanged;
            vm.PropertyChanged += OnPlanVmChanged;
        }
    }

    private void OnPlanVmChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    [RelayCommand]
    private async Task ReloadAsync() => await LoadAsync();

    [RelayCommand]
    private void Add()
    {
        var id = "backup_" + Guid.NewGuid().ToString("N");
        var model = new BackupPlanConfig
        {
            Enabled = true,
            Id = id,
            Name = "New Backup",
            VerifyAfterCreate = true,
            Schedule = new BackupScheduleConfig(),
            Source = new BackupSourceConfig { Type = BackupSourceType.Folder },
            Target = new BackupTargetConfig { Type = BackupTargetType.Local },
            Compression = new BackupCompressionConfig(),
            Crypto = new BackupCryptoConfig(),
            Retention = new BackupRetentionConfig()
        };

        var vm = BackupPlanItemViewModel.FromModel(model, MarkDirty, _folderPicker, _filePicker);
        vm.PropertyChanged += OnPlanVmChanged;

        Plans.Add(vm);
        SelectedPlan = vm;

        MarkDirty();
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(CanRunNow));
        RunNowCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelected))]
    private void Remove()
    {
        if (SelectedPlan == null)
            return;

        SelectedPlan.PropertyChanged -= OnPlanVmChanged;

        Plans.Remove(SelectedPlan);
        SelectedPlan = Plans.FirstOrDefault();

        MarkDirty();
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(CanRunNow));
        RunNowCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            var cfg = await Task.Run(() => _pipe.GetConfig());
            if (cfg == null)
                return;

            cfg.Backups.Clear();
            foreach (var vm in Plans)
                cfg.Backups.Add(vm.ToModel());

            await Task.Run(() => _pipe.SaveConfig(cfg));

            ClearDirty();

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    "Backup",
                    "Gespeichert",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(5));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    "Backup",
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            });
        }
    }

    private bool CanSave() => IsDirty;

    [RelayCommand(CanExecute = nameof(CanRunNow))]
    private async Task RunNowAsync()
    {
        if (SelectedPlan == null)
            return;

        try
        {
            if (IsDirty)
                await SaveAsync();

            await Task.Run(() => _pipe.TriggerBackup(SelectedPlan.Id));

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    "Backup",
                    "Backup wurde gestartet",
                    ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.Play24, 28, false),
                    TimeSpan.FromSeconds(5));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    "Backup",
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            });
        }
    }

    partial void OnSelectedPlanChanged(BackupPlanItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(CanRunNow));
        RemoveCommand.NotifyCanExecuteChanged();
        RunNowCommand.NotifyCanExecuteChanged();
    }

    protected override void OnIsDirtyChanged(bool value)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            SaveCommand.NotifyCanExecuteChanged();
        else
            Application.Current.Dispatcher.Invoke(SaveCommand.NotifyCanExecuteChanged);

        if (Application.Current.Dispatcher.CheckAccess())
            RunNowCommand.NotifyCanExecuteChanged();
        else
            Application.Current.Dispatcher.Invoke(RunNowCommand.NotifyCanExecuteChanged);
    }
}
