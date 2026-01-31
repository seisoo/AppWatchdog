using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Dialogs;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using AppWatchdog.UI.WPF.Localization;
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
    private readonly AppDialogService _dialogService;

    private bool _activated;
    private bool _suppressBreakingChange;
    private readonly System.Collections.Generic.Dictionary<string, BackupPlanChangeSnapshot> _planSnapshots = new(StringComparer.OrdinalIgnoreCase);

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
        FilePickerService filePicker,
        AppDialogService dialogService)
    {
        _pipe = pipe;
        _backend = backend;
        _snackbar = snackbar;
        _folderPicker = folderPicker;
        _filePicker = filePicker;
        _dialogService = dialogService;

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

            _planSnapshots.Clear();
            foreach (var plan in Plans)
                StoreSnapshot(plan);

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
            {
                Plans.RemoveAt(i);
                _planSnapshots.Remove(vm.Id);
            }
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

    private async void OnPlanVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkDirty();

        if (_suppressBreakingChange || sender is not BackupPlanItemViewModel vm)
            return;

        if (!BackupPlanChangeSnapshot.BreakingProperties.Contains(e.PropertyName ?? string.Empty))
            return;

        if (!_planSnapshots.TryGetValue(vm.Id, out var snapshot))
            return;

        if (!snapshot.HasBreakingChange(vm))
            return;

        _suppressBreakingChange = true;
        try
        {
            var confirmed = await ConfirmBreakingChangeAsync(vm);
            if (!confirmed)
            {
                snapshot.ApplyTo(vm);
                return;
            }

            var purged = await PurgeRestorePointsAsync(vm);
            if (!purged)
            {
                snapshot.ApplyTo(vm);
                return;
            }

            StoreSnapshot(vm);
        }
        finally
        {
            _suppressBreakingChange = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            var cfg = await Task.Run(() => _pipe.GetConfig());
            if (cfg == null)
                return;

            var breakingPlans = Plans
                .Where(p => _planSnapshots.TryGetValue(p.Id, out var snap) && snap.HasBreakingChange(p))
                .ToList();

            if (breakingPlans.Count > 0)
            {
                var confirmed = await ConfirmBreakingChangeAsync(breakingPlans);
                if (!confirmed)
                    return;

                foreach (var plan in breakingPlans)
                {
                    var ok = await PurgeRestorePointsAsync(plan);
                    if (!ok)
                        return;
                    StoreSnapshot(plan);
                }
            }

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
        StoreSnapshot(vm);

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
        _planSnapshots.Remove(SelectedPlan.Id);

        Plans.Remove(SelectedPlan);
        SelectedPlan = Plans.FirstOrDefault();

        MarkDirty();
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(CanRunNow));
        RunNowCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

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

    private void StoreSnapshot(BackupPlanItemViewModel plan)
    {
        _planSnapshots[plan.Id] = new BackupPlanChangeSnapshot(plan);
    }

    private async Task<bool> ConfirmBreakingChangeAsync(BackupPlanItemViewModel plan)
    {
        return await _dialogService.ShowConfirmAsync(
            AppStrings.backup_breaking_change_title,
            string.Format(AppStrings.backup_breaking_change_text, plan.Name));
    }

    private async Task<bool> ConfirmBreakingChangeAsync(System.Collections.Generic.IReadOnlyCollection<BackupPlanItemViewModel> plans)
    {
        var names = string.Join(", ", plans.Select(p => p.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        var label = string.IsNullOrWhiteSpace(names) ? AppStrings.backup_breaking_change_default_plan : names;

        return await _dialogService.ShowConfirmAsync(
            AppStrings.backup_breaking_change_title,
            string.Format(AppStrings.backup_breaking_change_text, label));
    }

    private async Task<bool> PurgeRestorePointsAsync(BackupPlanItemViewModel plan)
    {
        try
        {
            await Task.Run(() => _pipe.PurgeBackupArtifacts(plan.Id));
            return true;
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
            return false;
        }
    }

    private sealed class BackupPlanChangeSnapshot
    {
        public static readonly System.Collections.Generic.HashSet<string> BreakingProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(BackupPlanItemViewModel.SourceType),
            nameof(BackupPlanItemViewModel.TargetType),
            nameof(BackupPlanItemViewModel.CompressionCompress),
            nameof(BackupPlanItemViewModel.CompressionLevel),
            nameof(BackupPlanItemViewModel.CryptoEncrypt),
            nameof(BackupPlanItemViewModel.CryptoPassword),
            nameof(BackupPlanItemViewModel.CryptoIterations)
        };

        public BackupSourceType SourceType { get; }
        public BackupTargetType TargetType { get; }
        public bool CompressionCompress { get; }
        public int CompressionLevel { get; }
        public bool CryptoEncrypt { get; }
        public string CryptoPassword { get; }
        public int CryptoIterations { get; }

        public BackupPlanChangeSnapshot(BackupPlanItemViewModel plan)
        {
            SourceType = plan.SourceType;
            TargetType = plan.TargetType;
            CompressionCompress = plan.CompressionCompress;
            CompressionLevel = plan.CompressionLevel;
            CryptoEncrypt = plan.CryptoEncrypt;
            CryptoPassword = plan.CryptoPassword;
            CryptoIterations = plan.CryptoIterations;
        }

        public bool HasBreakingChange(BackupPlanItemViewModel plan)
            => SourceType != plan.SourceType
               || TargetType != plan.TargetType
               || CompressionCompress != plan.CompressionCompress
               || CompressionLevel != plan.CompressionLevel
               || CryptoEncrypt != plan.CryptoEncrypt
               || !string.Equals(CryptoPassword ?? "", plan.CryptoPassword ?? "", StringComparison.Ordinal)
               || CryptoIterations != plan.CryptoIterations;

        public void ApplyTo(BackupPlanItemViewModel plan)
        {
            plan.SourceType = SourceType;
            plan.TargetType = TargetType;
            plan.CompressionCompress = CompressionCompress;
            plan.CompressionLevel = CompressionLevel;
            plan.CryptoEncrypt = CryptoEncrypt;
            plan.CryptoPassword = CryptoPassword;
            plan.CryptoIterations = CryptoIterations;
        }
    }

    private bool CanSave() => IsDirty;
}
