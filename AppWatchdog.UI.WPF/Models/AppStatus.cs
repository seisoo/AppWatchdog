using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AppWatchdog.UI.WPF.Models;

public sealed class AppStatus : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = "";
    public string Name
    {
        get => _name;
        set { _name = value; OnChanged(); }
    }

    private string _exePath = "";
    public string ExePath
    {
        get => _exePath;
        set { _exePath = value; OnChanged(); }
    }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnChanged(); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnChanged(); }
    }

    private string? _lastStartError;
    public string? LastStartError
    {
        get => _lastStartError;
        set { _lastStartError = value; OnChanged(); }
    }

    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
