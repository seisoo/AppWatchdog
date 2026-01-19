using AppWatchdog.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Linq;

public partial class WatchedAppItemViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public WatchedAppItemViewModel(Action markDirty)
    {
        _markDirty = markDirty;
    }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _exePath = "";
    [ObservableProperty] private string _arguments = "";
    [ObservableProperty] private bool _enabled;

    [ObservableProperty] private bool _kumaEnabled;
    [ObservableProperty] private string _kumaBaseUrl = "";
    [ObservableProperty] private string _kumaPushToken = "";
    [ObservableProperty] private int _kumaIntervalSeconds = 60;

    // -------------------- Dirty forwarding --------------------

    partial void OnNameChanged(string value) => _markDirty();
    partial void OnExePathChanged(string value) => _markDirty();
    partial void OnArgumentsChanged(string value) => _markDirty();
    partial void OnEnabledChanged(bool value) => _markDirty();

    partial void OnKumaEnabledChanged(bool value) => _markDirty();
    partial void OnKumaBaseUrlChanged(string value) => _markDirty();
    partial void OnKumaPushTokenChanged(string value) => _markDirty();
    partial void OnKumaIntervalSecondsChanged(int value) => _markDirty();

    // -------------------- Mapping --------------------

    public static WatchedAppItemViewModel FromModel(
        WatchedApp model,
        Action markDirty)
    {
        var vm = new WatchedAppItemViewModel(markDirty)
        {
            Name = model.Name,
            ExePath = model.ExePath,
            Arguments = model.Arguments,
            Enabled = model.Enabled
        };

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
            vm.KumaIntervalSeconds = 60;
        }

        return vm;
    }

    public WatchedApp ToModel()
    {
        return new WatchedApp
        {
            Name = Name,
            ExePath = ExePath,
            Arguments = Arguments,
            Enabled = Enabled,
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
}
