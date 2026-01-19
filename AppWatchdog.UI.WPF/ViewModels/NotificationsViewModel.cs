using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using Wpf.Ui;
using Wpf.Ui.Controls;
using AppWatchdog.UI.WPF.Common;
using System.Threading.Tasks;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class NotificationsViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly IContentDialogService _dialogService;
    private readonly BackendStateService _backend;
    private bool _activated;

    [ObservableProperty]
    private bool _isContentEnabled;
    [ObservableProperty]
    
    private string _smtpServer = "";

    [ObservableProperty]
    
    private int _smtpPortText = 587;

    [ObservableProperty]
    
    private bool _smtpEnableSsl = true;

    [ObservableProperty]
    
    private string _smtpUser = "";

    [ObservableProperty]
    
    private string _smtpPassword = "";

    [ObservableProperty]
    
    private string _smtpFrom = "";

    [ObservableProperty]
    
    private string _smtpTo = "";

    [ObservableProperty]
    
    private bool _ntfyEnabled;

    [ObservableProperty]
    
    private string _ntfyBaseUrl = "";

    [ObservableProperty]
    
    private string _ntfyTopic = "";

    [ObservableProperty]
    
    private string _ntfyToken = "";

    [ObservableProperty]
    
    private int _ntfyPriorityText = 3;

    public NotificationsViewModel(
    PipeFacade pipe,
    IContentDialogService dialogService,
    BackendStateService backend)
    {
        _pipe = pipe;
        _dialogService = dialogService;
        _backend = backend;

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
            await Task.Run(Load);   // oder Logs: LoadDaysAndAutoSelectToday()
        }

        IsContentEnabled = true;    // 🔥 IMMER setzen
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
        using var _ = SuppressDirty();

        var cfg = _pipe.GetConfig();

        SmtpServer = cfg.Smtp.Server;
        SmtpPortText = cfg.Smtp.Port;
        SmtpEnableSsl = cfg.Smtp.EnableSsl;
        SmtpUser = cfg.Smtp.User;
        SmtpPassword = cfg.Smtp.Password;
        SmtpFrom = cfg.Smtp.From;
        SmtpTo = cfg.Smtp.To;

        NtfyEnabled = cfg.Ntfy.Enabled;
        NtfyBaseUrl = cfg.Ntfy.BaseUrl;
        NtfyTopic = cfg.Ntfy.Topic;
        NtfyToken = cfg.Ntfy.Token;
        NtfyPriorityText = cfg.Ntfy.Priority;

        SaveStateText = "Konfig geladen.";
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var cfg = _pipe.GetConfig();

        cfg.Smtp.Server = SmtpServer;
        cfg.Smtp.Port = SmtpPortText;
        cfg.Smtp.EnableSsl = SmtpEnableSsl;
        cfg.Smtp.User = SmtpUser;
        cfg.Smtp.Password = SmtpPassword;
        cfg.Smtp.From = SmtpFrom;
        cfg.Smtp.To = SmtpTo;

        cfg.Ntfy.Enabled = NtfyEnabled;
        cfg.Ntfy.BaseUrl = NtfyBaseUrl;
        cfg.Ntfy.Topic = NtfyTopic;
        cfg.Ntfy.Token = NtfyToken;
        cfg.Ntfy.Priority = NtfyPriorityText;

        _pipe.SaveConfig(cfg);
        ClearDirty();
    }

    protected override void OnIsDirtyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave()
        => IsDirty;

    [RelayCommand]
    private async Task TestSmtpAsync()
    {
        try
        {
            _pipe.TestSmtp();
            SaveStateText = "SMTP-Test erfolgreich";

            await UiDialogHelper.ShowInfoAsync(
                _dialogService,
                "SMTP-Test",
                "SMTP-Test erfolgreich abgeschlossen.",
                SymbolRegular.CheckmarkCircle24
            );
        }
        catch (Exception)
        {
            SaveStateText = "SMTP-Test fehlgeschlagen";
            throw; // 👉 globaler Exception-Dialog übernimmt
        }
    }


    [RelayCommand]
    private async Task TestNtfyAsync()
    {
        try
        {
            _pipe.TestNtfy();
            SaveStateText = "NTFY-Test erfolgreich";

            await UiDialogHelper.ShowInfoAsync(
                _dialogService,
                "NTFY-Test",
                "NTFY-Test erfolgreich abgeschlossen.",
                SymbolRegular.CheckmarkCircle24
            );
        }
        catch
        {
            SaveStateText = "NTFY-Test fehlgeschlagen";
            throw;
        }
    }

    #region Dirty-Events
    partial void OnSmtpServerChanged(string value)
    => MarkDirty();

    partial void OnSmtpPortTextChanged(int value)
        => MarkDirty();

    partial void OnSmtpEnableSslChanged(bool value)
        => MarkDirty();

    partial void OnSmtpUserChanged(string value)
        => MarkDirty();

    partial void OnSmtpPasswordChanged(string value)
        => MarkDirty();

    partial void OnSmtpFromChanged(string value)
        => MarkDirty();

    partial void OnSmtpToChanged(string value)
        => MarkDirty();


    partial void OnNtfyEnabledChanged(bool value)
        => MarkDirty();

    partial void OnNtfyBaseUrlChanged(string value)
        => MarkDirty();

    partial void OnNtfyTopicChanged(string value)
        => MarkDirty();

    partial void OnNtfyTokenChanged(string value)
        => MarkDirty();

    partial void OnNtfyPriorityTextChanged(int value)
        => MarkDirty();


    #endregion


}
