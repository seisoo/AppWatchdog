using AppWatchdog.Shared;
using AppWatchdog.Shared.Monitoring;
using AppWatchdog.UI.WPF.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.IO;
using System.Xml.Linq;

public partial class WatchedAppItemViewModel : ObservableObject
{
    private readonly Action _markDirty;
    [ObservableProperty]
    private WatchTargetType _type = WatchTargetType.Executable;

    // Windows Service
    [ObservableProperty]
    private string _serviceName = "";

    // HTTP
    [ObservableProperty]
    private string _url = "";
    [ObservableProperty]
    private int _expectedStatusCode = 200;
    [ObservableProperty]
    private int _checkIntervalSeconds = 60;

    public bool IsUrlValid =>
    string.IsNullOrWhiteSpace(Url) ||
    Uri.TryCreate(Url, UriKind.Absolute, out _);

    // TCP
    [ObservableProperty]
    private string _host = "";
    [ObservableProperty]
    private int _port;

    public WatchedAppItemViewModel(Action markDirty)
    {
        _markDirty = markDirty;
        LocalizationService.LanguageChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(Type));
        };
    }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _exePath = "";
    [ObservableProperty] private string _arguments = "";
    [ObservableProperty] private bool _enabled;

    [ObservableProperty] private bool _kumaEnabled;
    [ObservableProperty] private string _kumaBaseUrl = "";
    [ObservableProperty] private string _kumaPushToken = "";
    [ObservableProperty] private int _kumaIntervalSeconds = 60;

    partial void OnNameChanged(string value) => _markDirty();
    partial void OnExePathChanged(string value) => _markDirty();
    partial void OnArgumentsChanged(string value) => _markDirty();
    partial void OnEnabledChanged(bool value) => _markDirty();

    partial void OnKumaEnabledChanged(bool value) => _markDirty();
    partial void OnKumaBaseUrlChanged(string value) => _markDirty();
    partial void OnKumaPushTokenChanged(string value) => _markDirty();
    partial void OnKumaIntervalSecondsChanged(int value) => _markDirty();

    partial void OnTypeChanged(WatchTargetType value) => _markDirty();
    partial void OnServiceNameChanged(string value) => _markDirty();
    partial void OnUrlChanged(string value) => _markDirty();
    partial void OnExpectedStatusCodeChanged(int value) => _markDirty();
    partial void OnHostChanged(string value) => _markDirty();
    partial void OnPortChanged(int value) => _markDirty();

    partial void OnCheckIntervalSecondsChanged(int value) => _markDirty();


    public static WatchedAppItemViewModel FromModel(WatchedApp model, Action markDirty)
    {
        var vm = new WatchedAppItemViewModel(markDirty);
        vm.UpdateFromModel(model);
        return vm;
    }

    public WatchedApp ToModel()
    {
        return new WatchedApp
        {
            Name = Name,
            Type = Type,
            Enabled = Enabled,

            CheckIntervalSeconds = CheckIntervalSeconds,

            ExePath = ExePath,
            Arguments = Arguments,

            ServiceName = string.IsNullOrWhiteSpace(ServiceName) ? null : ServiceName,
            Url = string.IsNullOrWhiteSpace(Url) ? null : Url,
            ExpectedStatusCode = ExpectedStatusCode,
            Host = string.IsNullOrWhiteSpace(Host) ? null : Host,
            Port = Port <= 0 ? null : Port,

            UptimeKuma = KumaEnabled
                ? new UptimeKumaSettings
                {
                    Enabled = true,
                    BaseUrl = KumaBaseUrl,
                    PushToken = KumaPushToken,
                    IntervalSeconds = KumaIntervalSeconds
                }
                : null
        };
    }



    public void UpdateFromModel(WatchedApp model)
    {
        Name = model.Name;
        Type = model.Type;

        Enabled = model.Enabled;

        ExePath = model.ExePath;
        Arguments = model.Arguments;

        ServiceName = model.ServiceName ?? "";

        Url = model.Url ?? "";
        ExpectedStatusCode = model.ExpectedStatusCode;

        Host = model.Host ?? "";
        Port = model.Port ?? 0;

        CheckIntervalSeconds =
        model.CheckIntervalSeconds > 0
            ? model.CheckIntervalSeconds
            : 60;


        if (model.UptimeKuma != null)
        {
            KumaEnabled = model.UptimeKuma.Enabled;
            KumaBaseUrl = model.UptimeKuma.BaseUrl;
            KumaPushToken = model.UptimeKuma.PushToken;
            KumaIntervalSeconds = model.UptimeKuma.IntervalSeconds;
        }
        else
        {
            KumaEnabled = false;
            KumaBaseUrl = "";
            KumaPushToken = "";
            KumaIntervalSeconds = 60;
        }
    }


    [RelayCommand]
    private void BrowseExe()
    {
        var dlg = new OpenFileDialog
        {
            Title = AppStrings.apps_select_executable,
            Filter = AppStrings.apps_select_file_filter,
            CheckFileExists = true,
            CheckPathExists = true
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(ExePath))
            {
                var dir = Path.GetDirectoryName(ExePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    dlg.InitialDirectory = dir;
            }
        }
        catch
        {
            // ignore
        }

        if (dlg.ShowDialog() == true)
        {
            ExePath = dlg.FileName;
            _markDirty();
        }
    }

}
