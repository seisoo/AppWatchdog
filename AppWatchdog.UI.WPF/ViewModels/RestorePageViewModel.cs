using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AppWatchdog.UI.WPF.ViewModels;

public sealed class BackupArtifactItem
{
    public string Name { get; init; } = "";
    public DateTimeOffset CreatedUtc { get; init; }

    public string DisplayCreatedLocal
        => CreatedUtc == DateTimeOffset.MinValue ? "unknown" : CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string DisplayInfo => "awdb";
}

public sealed class RestoreChainItem
{
    public string ArtifactName { get; init; } = "";
    public string Mode { get; init; } = "";
    public DateTimeOffset CreatedUtc { get; init; }

    public SymbolRegular Icon => string.Equals(Mode, "Full", StringComparison.OrdinalIgnoreCase)
        ? SymbolRegular.Database24
        : SymbolRegular.ArrowRepeatAll24;

    public string Title => $"{Mode}";
    public string Subtitle
        => CreatedUtc == DateTimeOffset.MinValue
            ? ArtifactName
            : $"{CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm} • {ArtifactName}";
}

public partial class ManifestTreeNode : ObservableObject
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsFolder { get; init; }

    public ObservableCollection<ManifestTreeNode> Children { get; } = new();

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                ApplyToChildren(value);
        }
    }

    public SymbolRegular Icon => IsFolder ? SymbolRegular.Folder24 : GetIconForFile(Name);

    private void ApplyToChildren(bool value)
    {
        if (Children.Count == 0)
            return;

        foreach (var c in Children)
            c.IsSelected = value;
    }

    private static SymbolRegular GetIconForFile(string name)
    {
        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".json" => SymbolRegular.DocumentText24,
            ".txt" => SymbolRegular.DocumentText24,
            ".log" => SymbolRegular.DocumentText24,
            ".xml" => SymbolRegular.DocumentText24,
            ".ini" => SymbolRegular.DocumentText24,
            ".dll" => SymbolRegular.Library24,
            ".exe" => SymbolRegular.AppGeneric24,
            ".bak" => SymbolRegular.Database24,
            ".db" => SymbolRegular.Database24,
            ".sqlite" => SymbolRegular.Database24,
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => SymbolRegular.Image24,
            ".zip" => SymbolRegular.FolderZip24,
            ".7z" or ".rar" => SymbolRegular.FolderZip24,
            ".pdf" => SymbolRegular.DocumentPdf24,
            _ => SymbolRegular.Document24
        };
    }
}

public partial class RestorePageViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly BackendStateService _backend;
    private readonly ISnackbarService _snackbar;
    private readonly FolderPickerService _folderPicker;

    private bool _activated;
    private int _manifestLoadToken;
    private int _artifactLoadToken;

    public ObservableCollection<BackupPlanConfig> Plans { get; } = new();
    public ObservableCollection<BackupArtifactItem> Artifacts { get; } = new();
    public ObservableCollection<ManifestTreeNode> ManifestTree { get; } = new();
    public ObservableCollection<RestoreChainItem> RestoreChain { get; } = new();

    [ObservableProperty] private bool _isContentEnabled;
    [ObservableProperty] private bool _isLoadingPlans;
    [ObservableProperty] private bool _isLoadingArtifacts;
    [ObservableProperty] private bool _isLoadingManifest;
    [ObservableProperty] private BackupPlanConfig? _selectedPlan;
    [ObservableProperty] private BackupArtifactItem? _selectedArtifact;

    [ObservableProperty] private string _restoreToDirectory = "";
    [ObservableProperty] private bool _overwriteExisting = true;

    [ObservableProperty] private string _manifestJson = "";
    [ObservableProperty] private string _manifestSummary = "No manifest loaded";
    [ObservableProperty] private string _selectionSummary = "";
    [ObservableProperty] private string _chainSummary = "";
    [ObservableProperty] private bool _hasWarnings;
    [ObservableProperty] private string _warningText = "";
    [ObservableProperty] private bool _isOverwriteToggleEnabled = true;
    [ObservableProperty] private string _overwriteHint = "";

    public bool HasSelectedPlan => SelectedPlan != null;
    public bool HasSelectedArtifact => SelectedPlan != null && SelectedArtifact != null;
    public bool HasManifest => ManifestTree.Count > 0;

    public bool CanRestore
        => SelectedPlan != null
           && SelectedArtifact != null
           && !string.IsNullOrWhiteSpace(RestoreToDirectory)
           && HasManifest
           && !HasWarnings;

    public RestorePageViewModel(
        PipeFacade pipe,
        BackendStateService backend,
        ISnackbarService snackbar,
        FolderPickerService folderPicker)
    {
        _pipe = pipe;
        _backend = backend;
        _snackbar = snackbar;
        _folderPicker = folderPicker;

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
            await ReloadPlansAsync();
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

        if (_backend.IsReady && _activated)
            await ReloadPlansAsync();
        else
            IsContentEnabled = false;
    }

    [RelayCommand]
    private async Task ReloadPlansAsync()
    {
        try
        {
            IsLoadingPlans = true;

            var plans = await Task.Run(() => _pipe.ListBackups());
            using var suppressDirty = SuppressDirty();

            Plans.Clear();
            foreach (var p in plans.Plans.OrderBy(x => x.Name))
                Plans.Add(p);

            SelectedPlan ??= Plans.FirstOrDefault();

            Artifacts.Clear();
            SelectedArtifact = null;

            ClearRestoreUi();

            ClearDirty();
            RunOnUiThread(() =>
            {
                OnPropertyChanged(nameof(HasSelectedPlan));
                OnPropertyChanged(nameof(HasSelectedArtifact));
                OnPropertyChanged(nameof(CanRestore));
                TriggerRestoreCommand.NotifyCanExecuteChanged();
                LoadArtifactsCommand.NotifyCanExecuteChanged();
                LoadManifestCommand.NotifyCanExecuteChanged();
                SelectAllCommand.NotifyCanExecuteChanged();
                SelectNoneCommand.NotifyCanExecuteChanged();
            });

            // Auto-load artifacts after setting initial plan (after UI thread returns)
            if (SelectedPlan != null && !IsLoadingArtifacts)
                await LoadArtifactsAsync();
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
        finally
        {
            IsLoadingPlans = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPlan))]
    private async Task LoadArtifactsAsync()
    {
        if (SelectedPlan == null)
            return;

        var token = ++_artifactLoadToken;

        try
        {
            IsLoadingArtifacts = true;

            var artifacts = await Task.Run(() => _pipe.ListBackupArtifacts(SelectedPlan.Id));

            if (token != _artifactLoadToken)
                return;

            Artifacts.Clear();

            foreach (var name in artifacts.Artifacts)
            {
                Artifacts.Add(new BackupArtifactItem
                {
                    Name = name,
                    CreatedUtc = ParseCreatedUtcFromArtifactName(name)
                });
            }

            SelectedArtifact = Artifacts
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefault();

            ClearRestoreUi();

            RunOnUiThread(() =>
            {
                OnPropertyChanged(nameof(HasSelectedArtifact));
                OnPropertyChanged(nameof(CanRestore));
                TriggerRestoreCommand.NotifyCanExecuteChanged();
                LoadManifestCommand.NotifyCanExecuteChanged();
                SelectAllCommand.NotifyCanExecuteChanged();
                SelectNoneCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
        finally
        {
            IsLoadingArtifacts = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadManifest))]
    private async Task LoadManifestAsync()
    {
        await LoadChainAndMergedTreeAsync(force: true);
    }

    private bool CanLoadManifest()
        => SelectedPlan != null && SelectedArtifact != null && !IsLoadingManifest;

    [RelayCommand]
    private void BrowseRestoreDirectory()
    {
        var p = _folderPicker.Pick(RestoreToDirectory);
        if (!string.IsNullOrWhiteSpace(p))
            RestoreToDirectory = p;
    }

    [RelayCommand(CanExecute = nameof(HasManifest))]
    private void SelectAll()
    {
        foreach (var n in ManifestTree)
            n.IsSelected = true;

        UpdateSelectionSummary();
        TriggerRestoreCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestore));
    }

    [RelayCommand(CanExecute = nameof(HasManifest))]
    private void SelectNone()
    {
        foreach (var n in ManifestTree)
            n.IsSelected = false;

        UpdateSelectionSummary();
        TriggerRestoreCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestore));
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task TriggerRestoreAsync()
    {
        if (SelectedPlan == null || SelectedArtifact == null || string.IsNullOrWhiteSpace(RestoreToDirectory))
            return;

        try
        {
            var include = BuildIncludePathsForRestore();

            var req = new RestoreTriggerRequest
            {
                BackupPlanId = SelectedPlan.Id,
                ArtifactName = SelectedArtifact.Name,
                RestoreToDirectory = RestoreToDirectory,
                OverwriteExisting = OverwriteExisting,
                IncludePaths = include
            };

            await Task.Run(() => _pipe.TriggerRestore(req));

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    "Restore",
                    "Restore wurde gestartet",
                    ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.Play24, 28, false),
                    TimeSpan.FromSeconds(5));
            });
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
    }

    partial void OnSelectedPlanChanged(BackupPlanConfig? value)
    {
        OnPropertyChanged(nameof(HasSelectedPlan));

        Artifacts.Clear();
        SelectedArtifact = null;

        ClearRestoreUi();

        LoadArtifactsCommand.NotifyCanExecuteChanged();
        LoadManifestCommand.NotifyCanExecuteChanged();
        TriggerRestoreCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(HasSelectedArtifact));
        OnPropertyChanged(nameof(CanRestore));

        // Auto-load artifacts when plan is manually selected (run async without waiting)
        if (value != null && !IsLoadingArtifacts)
            _ = LoadArtifactsAsync();
    }

    partial void OnSelectedArtifactChanged(BackupArtifactItem? value)
    {
        LoadManifestCommand.NotifyCanExecuteChanged();
        TriggerRestoreCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(HasSelectedArtifact));
        OnPropertyChanged(nameof(CanRestore));

        if (SelectedPlan == null || value == null)
        {
            ClearRestoreUi();
            return;
        }

        // Start manifest loading async without blocking UI
        _ = LoadChainAndMergedTreeAsync(force: false);
    }

    partial void OnRestoreToDirectoryChanged(string value)
    {
        TriggerRestoreCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestore));
    }

    partial void OnOverwriteExistingChanged(bool value)
    {
        TriggerRestoreCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestore));
    }

    private async Task LoadChainAndMergedTreeAsync(bool force)
    {
        if (SelectedPlan == null || SelectedArtifact == null)
            return;

        var token = ++_manifestLoadToken;

        try
        {
            IsLoadingManifest = true;

            using var suppressDirty = SuppressDirty();

            ClearWarnings();

            var listOrdered = Artifacts
                .OrderBy(x => x.CreatedUtc == DateTimeOffset.MinValue ? DateTimeOffset.MinValue : x.CreatedUtc)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var targetIndex = listOrdered.FindIndex(x => string.Equals(x.Name, SelectedArtifact.Name, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
            {
                SetWarning("Selected artifact not found in artifacts list.");
                ClearRestoreUi(keepWarnings: true);
                return;
            }

            var chainManifests = new List<(BackupArtifactItem Artifact, BackupManifestDto Manifest)>();

            BackupManifestDto? targetManifest = null;

            for (int i = targetIndex; i >= 0; i--)
            {
                if (token != _manifestLoadToken)
                    return;

                var a = listOrdered[i];

                var json = await Task.Run(() => _pipe.GetBackupManifest(SelectedPlan.Id, a.Name));
                if (token != _manifestLoadToken)
                    return;

                if (string.IsNullOrWhiteSpace(json))
                {
                    SetWarning("Manifest could not be loaded.");
                    ClearRestoreUi(keepWarnings: true);
                    return;
                }

                var man = DeserializeManifest(json);
                if (man == null)
                {
                    SetWarning("Manifest is invalid.");
                    ClearRestoreUi(keepWarnings: true);
                    return;
                }

                if (targetManifest == null && string.Equals(a.Name, SelectedArtifact.Name, StringComparison.OrdinalIgnoreCase))
                    targetManifest = man;

                chainManifests.Add((a, man));

                if (string.Equals(man.Mode, BackupMode.Full.ToString(), StringComparison.OrdinalIgnoreCase))
                    break;
            }

            chainManifests.Reverse();

            if (chainManifests.Count == 0)
            {
                SetWarning("No manifest data available.");
                ClearRestoreUi(keepWarnings: true);
                return;
            }

            // Since we only have Full backups, no chain is necessary
            var selectedItem = chainManifests.FirstOrDefault(x => string.Equals(x.Artifact.Name, SelectedArtifact.Name, StringComparison.OrdinalIgnoreCase));
            if (selectedItem.Artifact == null)
            {
                SetWarning("Selected artifact not found in chain.");
                ClearRestoreUi(keepWarnings: true);
                return;
            }

            RestoreChain.Clear();
            RestoreChain.Add(new RestoreChainItem
            {
                ArtifactName = selectedItem.Artifact.Name,
                Mode = "Full",
                CreatedUtc = selectedItem.Manifest.CreatedUtc == DateTimeOffset.MinValue ? selectedItem.Artifact.CreatedUtc : selectedItem.Manifest.CreatedUtc
            });

            ChainSummary = "Full restore (single step)";

            IsOverwriteToggleEnabled = true;
            OverwriteHint = "Overwrite controls whether existing files will be replaced.";

            var merged = MergeManifests(chainManifests.Select(x => x.Manifest).ToList());
            ManifestJson = force && targetManifest != null ? JsonSerializer.Serialize(targetManifest, new JsonSerializerOptions { WriteIndented = true }) : "";
            ManifestSummary = BuildSummaryTextForMerged(merged, chainManifests.First().Manifest, chainManifests.Last().Manifest);

            BuildTreeFromMerged(merged);
            UpdateSelectionSummary();

            RunOnUiThread(() =>
            {
                OnPropertyChanged(nameof(HasManifest));
                OnPropertyChanged(nameof(CanRestore));
                TriggerRestoreCommand.NotifyCanExecuteChanged();
                SelectAllCommand.NotifyCanExecuteChanged();
                SelectNoneCommand.NotifyCanExecuteChanged();
                LoadManifestCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            if (token != _manifestLoadToken)
                return;

            SetWarning(ex.Message);
            ClearRestoreUi(keepWarnings: true);
            NotifyError(ex.Message);
        }
        finally
        {
            IsLoadingManifest = false;
        }
    }

    private static BackupManifestDto MergeManifests(List<BackupManifestDto> chain)
    {
        var merged = new BackupManifestDto();
        if (chain.Count == 0)
            return merged;

        var first = chain.First();
        var last = chain.Last();

        merged.PlanId = first.PlanId;
        merged.PlanName = first.PlanName;
        merged.SourceType = first.SourceType;
        merged.SourceLabel = first.SourceLabel;
        merged.Mode = last.Mode;
        merged.CreatedUtc = last.CreatedUtc == DateTimeOffset.MinValue ? first.CreatedUtc : last.CreatedUtc;

        var dict = new Dictionary<string, ManifestEntryDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in chain)
        {
            if (m.Entries == null)
                continue;

            foreach (var e in m.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.RelativePath))
                    continue;

                var p = e.RelativePath.Replace('\\', '/').TrimStart('/');
                dict[p] = new ManifestEntryDto
                {
                    RelativePath = p,
                    Size = e.Size,
                    LastWriteUtc = e.LastWriteUtc
                };
            }
        }

        merged.Entries = dict.Values
            .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return merged;
    }

    private void BuildTreeFromMerged(BackupManifestDto merged)
    {
        // Clear and build on UI thread to avoid CollectionView issues
        RunOnUiThread(() =>
        {
            ManifestTree.Clear();

            if (merged.Entries == null || merged.Entries.Count == 0)
                return;

            foreach (var e in merged.Entries)
            {
                var parts = e.RelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                AddNode(ManifestTree, parts, 0, "");
            }
        });
    }

    private static void AddNode(
        ObservableCollection<ManifestTreeNode> nodes,
        string[] parts,
        int index,
        string basePath)
    {
        var name = parts[index];
        var full = string.IsNullOrEmpty(basePath) ? name : basePath + "/" + name;

        var node = nodes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (node == null)
        {
            node = new ManifestTreeNode
            {
                Name = name,
                FullPath = full,
                IsFolder = index < parts.Length - 1
            };
            nodes.Add(node);
        }

        if (index + 1 < parts.Length)
            AddNode(node.Children, parts, index + 1, full);
    }

    private List<string> BuildIncludePathsForRestore()
    {
        if (ManifestTree.Count == 0)
            return new List<string>();

        if (AllSelected(ManifestTree))
            return new List<string>();

        var list = new List<string>();

        foreach (var n in ManifestTree)
            CollectSelectedMinimal(n, parentSelected: false, list);

        list = list
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Replace('\\', '/').TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list;
    }

    private static void CollectSelectedMinimal(ManifestTreeNode node, bool parentSelected, List<string> output)
    {
        if (!node.IsSelected)
        {
            foreach (var c in node.Children)
                CollectSelectedMinimal(c, parentSelected: false, output);
            return;
        }

        if (parentSelected)
            return;

        output.Add(node.FullPath);

        foreach (var c in node.Children)
            CollectSelectedMinimal(c, parentSelected: true, output);
    }

    private static bool AllSelected(IEnumerable<ManifestTreeNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (!n.IsSelected)
                return false;

            if (n.Children.Count > 0 && !AllSelected(n.Children))
                return false;
        }

        return true;
    }

    private void UpdateSelectionSummary()
    {
        if (ManifestTree.Count == 0)
        {
            SelectionSummary = "";
            return;
        }

        int totalFiles = 0;
        int selectedFiles = 0;

        foreach (var n in ManifestTree)
            CountFiles(n, ref totalFiles, ref selectedFiles);

        SelectionSummary = totalFiles == 0
            ? ""
            : $"Selected: {selectedFiles}/{totalFiles} files";
    }

    private static void CountFiles(ManifestTreeNode node, ref int totalFiles, ref int selectedFiles)
    {
        if (!node.IsFolder)
        {
            totalFiles++;
            if (node.IsSelected)
                selectedFiles++;
            return;
        }

        foreach (var c in node.Children)
            CountFiles(c, ref totalFiles, ref selectedFiles);
    }

    private static DateTimeOffset ParseCreatedUtcFromArtifactName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DateTimeOffset.MinValue;

        var baseName = name;
        if (baseName.EndsWith(".awdb", StringComparison.OrdinalIgnoreCase))
            baseName = baseName.Substring(0, baseName.Length - 5);

        var idx = baseName.LastIndexOf('_');
        if (idx < 0 || idx + 1 >= baseName.Length)
            return DateTimeOffset.MinValue;

        var tail = baseName.Substring(idx + 1);
        var idx2 = baseName.LastIndexOf('_', idx - 1);
        if (idx2 < 0)
            return DateTimeOffset.MinValue;

        var datePart = baseName.Substring(idx2 + 1, idx - idx2 - 1);
        var timePart = tail;

        var stamp = datePart + "_" + timePart;

        if (DateTimeOffset.TryParseExact(
            stamp,
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dt))
            return dt;

        return DateTimeOffset.MinValue;
    }

    private static BackupManifestDto? DeserializeManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var opt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        return JsonSerializer.Deserialize<BackupManifestDto>(json, opt);
    }

    private static string BuildSummaryTextForMerged(BackupManifestDto merged, BackupManifestDto first, BackupManifestDto last)
    {
        var createdFirst = first.CreatedUtc == DateTimeOffset.MinValue ? "unknown" : first.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var createdLast = last.CreatedUtc == DateTimeOffset.MinValue ? "unknown" : last.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        var entries = merged.Entries?.Count ?? 0;
        var src = string.IsNullOrWhiteSpace(merged.SourceLabel) ? merged.SourceType : merged.SourceLabel;

        var mode = RestoreModeText(first, last);

        return $"Restore: {mode} • From: {createdFirst} • To: {createdLast} • Files: {entries} • Source: {src}";
    }

    private static string RestoreModeText(BackupManifestDto first, BackupManifestDto last)
    {
        return "Full backup";
    }

    private void ClearRestoreUi(bool keepWarnings = false)
    {
        // Clear collections on UI thread to prevent CollectionView issues
        RunOnUiThread(() =>
        {
            ManifestJson = "";
            ManifestSummary = "No manifest loaded";
            SelectionSummary = "";
            ChainSummary = "";
            OverwriteHint = "";
            IsOverwriteToggleEnabled = true;
            ManifestTree.Clear();
            RestoreChain.Clear();

            if (!keepWarnings)
                ClearWarnings();

            OnPropertyChanged(nameof(HasManifest));
            TriggerRestoreCommand.NotifyCanExecuteChanged();
            SelectAllCommand.NotifyCanExecuteChanged();
            SelectNoneCommand.NotifyCanExecuteChanged();
            LoadManifestCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanRestore));
        });
    }

    private void ClearWarnings()
    {
        HasWarnings = false;
        WarningText = "";
    }

    private void SetWarning(string msg)
    {
        HasWarnings = true;
        WarningText = msg;
        TriggerRestoreCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestore));
    }

    private void NotifyError(string msg)
    {
        RunOnUiThread(() =>
        {
            _snackbar.Show(
                "Restore",
                msg,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                TimeSpan.FromSeconds(6));
        });
    }

    private sealed class BackupManifestDto
    {
        [JsonPropertyName("planId")] public string PlanId { get; set; } = "";
        [JsonPropertyName("planName")] public string PlanName { get; set; } = "";
        [JsonPropertyName("createdUtc")] public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.MinValue;
        [JsonPropertyName("sourceType")] public string SourceType { get; set; } = "";
        [JsonPropertyName("sourceLabel")] public string SourceLabel { get; set; } = "";
        [JsonPropertyName("mode")] public string Mode { get; set; } = "";
        [JsonPropertyName("entries")] public List<ManifestEntryDto> Entries { get; set; } = new();
        [JsonPropertyName("sqlBakFileName")] public string? SqlBakFileName { get; set; }
    }

    private sealed class ManifestEntryDto
    {
        [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("lastWriteUtc")] public DateTimeOffset LastWriteUtc { get; set; }
    }
}
