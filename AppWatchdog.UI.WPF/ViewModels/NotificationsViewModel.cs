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
using AppWatchdog.UI.WPF.Localization;
using System.Windows;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class NotificationsViewModel : DirtyViewModelBase
{
    private readonly PipeFacade _pipe;
    private readonly IContentDialogService _dialogService;
    private readonly BackendStateService _backend;
    private bool _activated;

    [ObservableProperty]
    private bool _showSmtpPassword;

    [ObservableProperty]
    private bool _showNtfyToken;

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

    [ObservableProperty]
    private bool _discordEnabled;

    [ObservableProperty]
    private string _discordWebhookUrl = "";

    [ObservableProperty]
    private string _discordUsername = "";

    [ObservableProperty]
    private bool _telegramEnabled;

    [ObservableProperty]
    private string _telegramBotToken = "";

    [ObservableProperty]
    private string _telegramChatId = "";

    private readonly ISnackbarService _snackbar;
    public NotificationsViewModel(
    PipeFacade pipe,
    IContentDialogService dialogService,
    BackendStateService backend, 
    ISnackbarService snackbar)
    {
        _pipe = pipe;
        _dialogService = dialogService;
        _backend = backend;
        _snackbar = snackbar;

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
            await Load();
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

        if (_backend.IsReady)
            await ActivateAsync();
        else
            IsContentEnabled = false;
    }


    private async Task Load()
    {
        using var _ = SuppressDirty();

        var cfg = await Task.Run(() => _pipe.GetConfig());

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

        DiscordEnabled = cfg.Discord.Enabled;
        DiscordWebhookUrl = cfg.Discord.WebhookUrl;
        DiscordUsername = cfg.Discord.Username;

        TelegramEnabled = cfg.Telegram.Enabled;
        TelegramBotToken = cfg.Telegram.BotToken;
        TelegramChatId = cfg.Telegram.ChatId;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async void Save()
    {
        var cfg = await Task.Run(() => _pipe.GetConfig());

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

        cfg.Discord.Enabled = DiscordEnabled;
        cfg.Discord.WebhookUrl = DiscordWebhookUrl;
        cfg.Discord.Username = DiscordUsername;

        cfg.Telegram.Enabled = TelegramEnabled;
        cfg.Telegram.BotToken = TelegramBotToken;
        cfg.Telegram.ChatId = TelegramChatId;


        await Task.Run(() => _pipe.SaveConfig(cfg));
        ClearDirty();

        Application.Current.Dispatcher.Invoke(() =>
        {
            _snackbar.Show(
                AppStrings.config_saved,
                AppStrings.config_saved,
                ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24, 28, false),
                TimeSpan.FromSeconds(3));
        });
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
            await Task.Run(() => _pipe.TestSmtp());

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_smtp_test,
                    AppStrings.notific_smtp_test_success_finish,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_smtp_test,
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            });
        }
    }


    [RelayCommand]
    private async Task TestNtfyAsync()
    {
        try
        {
            await Task.Run(() => _pipe.TestNtfy());

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_ntfy_test,
                    AppStrings.notific_ntfy_test_success_finish,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_ntfy_test,
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            });
        }
    }



    [RelayCommand]
    private async Task TestTelegramAsync()
    {
        try
        {
            await Task.Run(() => _pipe.TestTelegram());

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_telegram_test,
                    AppStrings.notific_telegram_test_success,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_telegram_test,
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            });
        }
    }



    [RelayCommand]
    private async Task TestDiscordAsync()
    {
        try
        {
            await Task.Run(() => _pipe.TestDiscord());

            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_discord_test,
                    AppStrings.notific_discord_test_success,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                _snackbar.Show(
                    AppStrings.notific_discord_test,
                    ex.Message,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false), 
                    TimeSpan.FromSeconds(6));
            });
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

    partial void OnDiscordEnabledChanged(bool value) 
        => MarkDirty();
    partial void OnDiscordWebhookUrlChanged(string value) 
        => MarkDirty();
    partial void OnDiscordUsernameChanged(string value) 
        => MarkDirty();
    partial void OnTelegramEnabledChanged(bool value) 
        => MarkDirty();
    partial void OnTelegramBotTokenChanged(string value) 
        => MarkDirty();
    partial void OnTelegramChatIdChanged(string value) 
        => MarkDirty();


    #endregion


}
