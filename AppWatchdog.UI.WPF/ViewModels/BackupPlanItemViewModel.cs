using AppWatchdog.Shared;
using AppWatchdog.UI.WPF.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class BackupPlanItemViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly FolderPickerService _folderPicker;
    private readonly FilePickerService _filePicker;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";

    [ObservableProperty] private bool _verifyAfterCreate;

    [ObservableProperty] private BackupSourceType _sourceType;
    [ObservableProperty] private string _sourcePath = "";
    [ObservableProperty] private string _sqlConnectionString = "";
    [ObservableProperty] private string _sqlDatabase = "";

    [ObservableProperty] private BackupTargetType _targetType;
    [ObservableProperty] private string _localDirectory = "";
    [ObservableProperty] private string _sftpHost = "";
    [ObservableProperty] private int _sftpPort = 22;
    [ObservableProperty] private string _sftpUser = "";
    [ObservableProperty] private string _sftpPassword = "";
    [ObservableProperty] private string _sftpRemoteDirectory = "/";
    [ObservableProperty] private string? _sftpHostKeyFingerprint;

    [ObservableProperty] private bool _compressionCompress;
    [ObservableProperty] private int _compressionLevel;

    [ObservableProperty] private bool _cryptoEncrypt;
    [ObservableProperty] private string _cryptoPassword = "";
    [ObservableProperty] private int _cryptoIterations;

    [ObservableProperty] private int _retentionKeepLast;

    [ObservableProperty] private TimeSpan? _scheduleTimeLocal;
    [ObservableProperty] private bool _dayMon;
    [ObservableProperty] private bool _dayTue;
    [ObservableProperty] private bool _dayWed;
    [ObservableProperty] private bool _dayThu;
    [ObservableProperty] private bool _dayFri;
    [ObservableProperty] private bool _daySat;
    [ObservableProperty] private bool _daySun;

    public int ScheduleHour
    {
        get => ScheduleTimeLocal?.Hours ?? 2;
        set
        {
            ScheduleTimeLocal = new TimeSpan(value, ScheduleMinute, 0);
            _markDirty();
            OnPropertyChanged();
        }
    }

    public int ScheduleMinute
    {
        get => ScheduleTimeLocal?.Minutes ?? 0;
        set
        {
            ScheduleTimeLocal = new TimeSpan(ScheduleHour, value, 0);
            _markDirty();
            OnPropertyChanged();
        }
    }


    public bool IsSourceFolder => SourceType == BackupSourceType.Folder;
    public bool IsSourceFile => SourceType == BackupSourceType.File;
    public bool IsSourceSql => SourceType == BackupSourceType.MsSql;

    public bool IsTargetLocal => TargetType == BackupTargetType.Local;
    public bool IsTargetSftp => TargetType == BackupTargetType.Sftp;

    public BackupPlanItemViewModel(Action markDirty, FolderPickerService folderPicker, FilePickerService filePicker)
    {
        _markDirty = markDirty;
        _folderPicker = folderPicker;
        _filePicker = filePicker;

        ScheduleTimeLocal = new TimeSpan(2, 0, 0);
    }

    public static BackupPlanItemViewModel FromModel(
        BackupPlanConfig model,
        Action markDirty,
        FolderPickerService folderPicker,
        FilePickerService filePicker)
    {
        var vm = new BackupPlanItemViewModel(markDirty, folderPicker, filePicker);
        vm.UpdateFromModel(model);
        return vm;
    }

    public void UpdateFromModel(BackupPlanConfig model)
    {
        Enabled = model.Enabled;
        Id = model.Id;
        Name = model.Name;

        VerifyAfterCreate = model.VerifyAfterCreate;

        SourceType = model.Source.Type;
        SourcePath = model.Source.Path ?? "";
        SqlConnectionString = model.Source.SqlConnectionString ?? "";
        SqlDatabase = model.Source.SqlDatabase ?? "";

        TargetType = model.Target.Type;
        LocalDirectory = model.Target.LocalDirectory ?? "";
        SftpHost = model.Target.SftpHost ?? "";
        SftpPort = model.Target.SftpPort <= 0 ? 22 : model.Target.SftpPort;
        SftpUser = model.Target.SftpUser ?? "";
        SftpPassword = model.Target.SftpPassword ?? "";
        SftpRemoteDirectory = string.IsNullOrWhiteSpace(model.Target.SftpRemoteDirectory) ? "/" : model.Target.SftpRemoteDirectory;
        SftpHostKeyFingerprint = model.Target.SftpHostKeyFingerprint;

        CompressionCompress = model.Compression.Compress;
        CompressionLevel = model.Compression.Level;

        CryptoEncrypt = model.Crypto.Encrypt;
        CryptoPassword = model.Crypto.Password ?? "";
        CryptoIterations = model.Crypto.Iterations;

        RetentionKeepLast = model.Retention.KeepLast;

        var t = ParseTime(model.Schedule.TimeLocal);
        ScheduleTimeLocal = ParseTime(model.Schedule.TimeLocal);


        var days = new HashSet<DayOfWeek>(model.Schedule.Days ?? new List<DayOfWeek>());
        DayMon = days.Contains(DayOfWeek.Monday);
        DayTue = days.Contains(DayOfWeek.Tuesday);
        DayWed = days.Contains(DayOfWeek.Wednesday);
        DayThu = days.Contains(DayOfWeek.Thursday);
        DayFri = days.Contains(DayOfWeek.Friday);
        DaySat = days.Contains(DayOfWeek.Saturday);
        DaySun = days.Contains(DayOfWeek.Sunday);

        OnPropertyChanged(nameof(IsSourceFolder));
        OnPropertyChanged(nameof(IsSourceFile));
        OnPropertyChanged(nameof(IsSourceSql));
        OnPropertyChanged(nameof(IsTargetLocal));
        OnPropertyChanged(nameof(IsTargetSftp));
    }

    public BackupPlanConfig ToModel()
    {
        var scheduleTime = ScheduleTimeLocal ?? new TimeSpan(2, 0, 0);
        var days = new List<DayOfWeek>();
        if (DayMon) days.Add(DayOfWeek.Monday);
        if (DayTue) days.Add(DayOfWeek.Tuesday);
        if (DayWed) days.Add(DayOfWeek.Wednesday);
        if (DayThu) days.Add(DayOfWeek.Thursday);
        if (DayFri) days.Add(DayOfWeek.Friday);
        if (DaySat) days.Add(DayOfWeek.Saturday);
        if (DaySun) days.Add(DayOfWeek.Sunday);
        if (days.Count == 0)
            days.AddRange(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday });

        return new BackupPlanConfig
        {
            Enabled = Enabled,
            Id = Id ?? "",
            Name = Name ?? "",
            VerifyAfterCreate = VerifyAfterCreate,

            Schedule = new BackupScheduleConfig
            {
                TimeLocal = scheduleTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                Days = days
            },

            Source = new BackupSourceConfig
            {
                Type = SourceType,
                Path = SourcePath ?? "",
                SqlConnectionString = SqlConnectionString ?? "",
                SqlDatabase = SqlDatabase ?? ""
            },

            Target = new BackupTargetConfig
            {
                Type = TargetType,
                LocalDirectory = LocalDirectory ?? "",
                SftpHost = SftpHost ?? "",
                SftpPort = SftpPort <= 0 ? 22 : SftpPort,
                SftpUser = SftpUser ?? "",
                SftpPassword = SftpPassword ?? "",
                SftpRemoteDirectory = string.IsNullOrWhiteSpace(SftpRemoteDirectory) ? "/" : SftpRemoteDirectory,
                SftpHostKeyFingerprint = string.IsNullOrWhiteSpace(SftpHostKeyFingerprint) ? null : SftpHostKeyFingerprint
            },

            Compression = new BackupCompressionConfig
            {
                Compress = CompressionCompress,
                Level = Math.Clamp(CompressionLevel, 1, 9)
            },

            Crypto = new BackupCryptoConfig
            {
                Encrypt = CryptoEncrypt,
                Password = CryptoPassword ?? "",
                Iterations = Math.Clamp(CryptoIterations, 10_000, 1_000_000)
            },

            Retention = new BackupRetentionConfig
            {
                KeepLast = Math.Max(1, RetentionKeepLast)
            }
        };
    }

    [RelayCommand]
    private void PickSource()
    {
        if (SourceType == BackupSourceType.File)
        {
            var p = _filePicker.Pick(null);
            if (!string.IsNullOrWhiteSpace(p))
                SourcePath = p;
            return;
        }

        if (SourceType == BackupSourceType.Folder)
        {
            var p = _folderPicker.Pick(SourcePath);
            if (!string.IsNullOrWhiteSpace(p))
                SourcePath = p;
            return;
        }
    }

    [RelayCommand]
    private void PickTargetLocal()
    {
        var p = _folderPicker.Pick(LocalDirectory);
        if (!string.IsNullOrWhiteSpace(p))
            LocalDirectory = p;
    }

    partial void OnEnabledChanged(bool value) => _markDirty();
    partial void OnIdChanged(string value) => _markDirty();
    partial void OnNameChanged(string value) => _markDirty();

    partial void OnSourceTypeChanged(BackupSourceType value)
    {
        _markDirty();
        OnPropertyChanged(nameof(IsSourceFolder));
        OnPropertyChanged(nameof(IsSourceFile));
        OnPropertyChanged(nameof(IsSourceSql));
    }

    partial void OnSourcePathChanged(string value) => _markDirty();
    partial void OnSqlConnectionStringChanged(string value) => _markDirty();
    partial void OnSqlDatabaseChanged(string value) => _markDirty();

    partial void OnTargetTypeChanged(BackupTargetType value)
    {
        _markDirty();
        OnPropertyChanged(nameof(IsTargetLocal));
        OnPropertyChanged(nameof(IsTargetSftp));
    }

    partial void OnLocalDirectoryChanged(string value) => _markDirty();
    partial void OnSftpHostChanged(string value) => _markDirty();
    partial void OnSftpPortChanged(int value) => _markDirty();
    partial void OnSftpUserChanged(string value) => _markDirty();
    partial void OnSftpPasswordChanged(string value) => _markDirty();
    partial void OnSftpRemoteDirectoryChanged(string value) => _markDirty();
    partial void OnSftpHostKeyFingerprintChanged(string? value) => _markDirty();

    partial void OnCompressionCompressChanged(bool value) => _markDirty();
    partial void OnCompressionLevelChanged(int value) => _markDirty();

    partial void OnCryptoEncryptChanged(bool value) => _markDirty();
    partial void OnCryptoPasswordChanged(string value) => _markDirty();
    partial void OnCryptoIterationsChanged(int value) => _markDirty();

    partial void OnRetentionKeepLastChanged(int value) => _markDirty();

    partial void OnScheduleTimeLocalChanged(TimeSpan? value) => _markDirty();

    partial void OnDayMonChanged(bool value) => _markDirty();
    partial void OnDayTueChanged(bool value) => _markDirty();
    partial void OnDayWedChanged(bool value) => _markDirty();
    partial void OnDayThuChanged(bool value) => _markDirty();
    partial void OnDayFriChanged(bool value) => _markDirty();
    partial void OnDaySatChanged(bool value) => _markDirty();
    partial void OnDaySunChanged(bool value) => _markDirty();

    private static TimeSpan ParseTime(string s)
    {
        if (TimeSpan.TryParseExact(s ?? "", "hh\\:mm", CultureInfo.InvariantCulture, out var t))
            return t;
        if (TimeSpan.TryParseExact(s ?? "", "h\\:mm", CultureInfo.InvariantCulture, out t))
            return t;
        if (TimeSpan.TryParse(s ?? "", CultureInfo.InvariantCulture, out t))
            return t;
        return new TimeSpan(2, 0, 0);
    }
}
