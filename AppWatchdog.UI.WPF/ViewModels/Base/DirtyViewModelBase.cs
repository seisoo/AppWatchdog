using AppWatchdog.UI.WPF.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Windows;

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
    }

    public void ClearDirty()
    {
        IsDirty = false;
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

    public static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

}
