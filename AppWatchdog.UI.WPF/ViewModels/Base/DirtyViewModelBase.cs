using AppWatchdog.UI.WPF.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;

namespace AppWatchdog.UI.WPF.ViewModels.Base;

public abstract partial class DirtyViewModelBase : ObservableObject
{
    private bool _isDirty;

    public bool IsDirty
    {
        get => _isDirty;
        protected set
        {
            if (SetProperty(ref _isDirty, value))
                OnIsDirtyChanged(value);
        }
    }

    private string _saveStateText = AppStrings.config_not_saved;
    public string SaveStateText
    {
        get => _saveStateText;
        protected set => SetProperty(ref _saveStateText, value);
    }

    private int _suppressDirty;

    protected IDisposable SuppressDirty()
    {
        _suppressDirty++;
        return new ActionOnDispose(() => _suppressDirty--);
    }

    public void MarkDirty()
    {
        if (_suppressDirty > 0)
            return;

        IsDirty = true;
        SaveStateText = AppStrings.config_not_saved;
    }

    public void ClearDirty()
    {
        IsDirty = false;
        SaveStateText = AppStrings.config_saved;
    }

    /// <summary>
    /// EINZIGER erlaubter Hook für abgeleitete ViewModels
    /// </summary>
    protected virtual void OnIsDirtyChanged(bool value)
    {
    }



    private sealed class ActionOnDispose : IDisposable
    {
        private readonly Action _a;
        public ActionOnDispose(Action a) => _a = a;
        public void Dispose() => _a();
    }

    [RelayCommand]
    private void OpenSteam()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://steamcommunity.com/id/call_me_seiso/",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenMail()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "mailto:kreolen-mission9v@icloud.com",
            UseShellExecute = true
        });
    }
}
