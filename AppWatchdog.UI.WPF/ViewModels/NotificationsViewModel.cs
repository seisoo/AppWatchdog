using AppWatchdog.UI.WPF.Services;
using AppWatchdog.UI.WPF.ViewModels.Base;
using AppWatchdog.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Linq;
using Wpf.Ui;
using Wpf.Ui.Controls;
using AppWatchdog.UI.WPF.Common;
using System.Threading.Tasks;
using AppWatchdog.UI.WPF.Localization;
using System.Windows;

namespace AppWatchdog.UI.WPF.ViewModels;

public partial class NotificationsViewModel : DirtyViewModelBase
{
    private const string DefaultUpColor = "#15803d";
    private const string DefaultDownColor = "#b91c1c";
    private const string DefaultRestartColor = "#2563eb";

    private readonly PipeFacade _pipe;
    private readonly IContentDialogService _dialogService;
    private readonly BackendStateService _backend;
    private readonly ColorPickerService _colorPicker;
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
    ColorPickerService colorPicker,
    ISnackbarService snackbar)
    {
        _pipe = pipe;
        _dialogService = dialogService;
        _backend = backend;
        _colorPicker = colorPicker;
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


    [ObservableProperty]
    private string _upTitleTemplate = "";

    [ObservableProperty]
    private string _upSummaryTemplate = "";

    [ObservableProperty]
    private string _upBodyTemplate = "";

    [ObservableProperty]
    private string _upColor = DefaultUpColor;

    [ObservableProperty]
    private string _downTitleTemplate = "";

    [ObservableProperty]
    private string _downSummaryTemplate = "";

    [ObservableProperty]
    private string _downBodyTemplate = "";

    [ObservableProperty]
    private string _downColor = DefaultDownColor;

    [ObservableProperty]
    private string _restartTitleTemplate = "";

    [ObservableProperty]
    private string _restartSummaryTemplate = "";

    [ObservableProperty]
    private string _restartBodyTemplate = "";

    [ObservableProperty]
    private string _restartColor = DefaultRestartColor;

    public string TemplatePlaceholdersHint =>
        "$jobname, $summary, $status, $target, $error, $ping, $machine, $time";

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

        var templates = cfg.NotificationTemplates ?? NotificationTemplateSet.CreateDefault();
        ApplyTemplate(templates.Up ?? NotificationTemplate.CreateDefaultUp(), NotificationTemplate.CreateDefaultUp(), DefaultUpColor,
            v => UpTitleTemplate = v,
            v => UpSummaryTemplate = v,
            v => UpBodyTemplate = v,
            v => UpColor = v);

        ApplyTemplate(templates.Down ?? NotificationTemplate.CreateDefaultDown(), NotificationTemplate.CreateDefaultDown(), DefaultDownColor,
            v => DownTitleTemplate = v,
            v => DownSummaryTemplate = v,
            v => DownBodyTemplate = v,
            v => DownColor = v);

        ApplyTemplate(templates.Restart ?? NotificationTemplate.CreateDefaultRestart(), NotificationTemplate.CreateDefaultRestart(), DefaultRestartColor,
            v => RestartTitleTemplate = v,
            v => RestartSummaryTemplate = v,
            v => RestartBodyTemplate = v,
            v => RestartColor = v);
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

        cfg.NotificationTemplates ??= NotificationTemplateSet.CreateDefault();

        cfg.NotificationTemplates.Up.Title = UpTitleTemplate;
        cfg.NotificationTemplates.Up.Summary = UpSummaryTemplate;
        cfg.NotificationTemplates.Up.Body = UpBodyTemplate;
        cfg.NotificationTemplates.Up.Color = NormalizeColor(UpColor, DefaultUpColor);

        cfg.NotificationTemplates.Down.Title = DownTitleTemplate;
        cfg.NotificationTemplates.Down.Summary = DownSummaryTemplate;
        cfg.NotificationTemplates.Down.Body = DownBodyTemplate;
        cfg.NotificationTemplates.Down.Color = NormalizeColor(DownColor, DefaultDownColor);

        cfg.NotificationTemplates.Restart.Title = RestartTitleTemplate;
        cfg.NotificationTemplates.Restart.Summary = RestartSummaryTemplate;
        cfg.NotificationTemplates.Restart.Body = RestartBodyTemplate;
        cfg.NotificationTemplates.Restart.Color = NormalizeColor(RestartColor, DefaultRestartColor);

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

    private static void ApplyTemplate(
        NotificationTemplate template,
        NotificationTemplate defaults,
        string fallbackColor,
        Action<string> setTitle,
        Action<string> setSummary,
        Action<string> setBody,
        Action<string> setColor)
    {
        setTitle(string.IsNullOrWhiteSpace(template.Title) ? defaults.Title : template.Title);
        setSummary(string.IsNullOrWhiteSpace(template.Summary) ? defaults.Summary : template.Summary);
        setBody(string.IsNullOrWhiteSpace(template.Body) ? defaults.Body : template.Body);
        setColor(NormalizeColor(template.Color, fallbackColor));
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (!candidate.StartsWith('#'))
            candidate = "#" + candidate;

        return IsHexColor(candidate) ? candidate : fallback;
    }

    private static bool IsHexColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.StartsWith('#') ? value[1..] : value;
        if (hex.Length != 6)
            return false;

        return hex.All(Uri.IsHexDigit);
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
        var error = await Task.Run(() => _pipe.TestSmtp());

        RunOnUiThread(() =>
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                _snackbar.Show(
                    AppStrings.notific_smtp_test,
                    AppStrings.notific_smtp_test_success_finish,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                _snackbar.Show(
                    AppStrings.notific_smtp_test,
                    error,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            }
        });
    }

    [RelayCommand]
    private async Task TestNtfyAsync()
    {
        var error = await Task.Run(() => _pipe.TestNtfy());

        RunOnUiThread(() =>
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                _snackbar.Show(
                    AppStrings.notific_ntfy_test,
                    AppStrings.notific_ntfy_test_success_finish,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                _snackbar.Show(
                    AppStrings.notific_ntfy_test,
                    error,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            }
        });
    }

    [RelayCommand]
    private async Task TestTelegramAsync()
    {
        var error = await Task.Run(() => _pipe.TestTelegram());

        RunOnUiThread(() =>
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                _snackbar.Show(
                    AppStrings.notific_telegram_test,
                    AppStrings.notific_telegram_test_success,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                _snackbar.Show(
                    AppStrings.notific_telegram_test,
                    error,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            }
        });
    }

    [RelayCommand]
    private async Task TestDiscordAsync()
    {
        var error = await Task.Run(() => _pipe.TestDiscord());

        RunOnUiThread(() =>
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                _snackbar.Show(
                    AppStrings.notific_discord_test,
                    AppStrings.notific_discord_test_success,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24, 28, false),
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                _snackbar.Show(
                    AppStrings.notific_discord_test,
                    error,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24, 28, false),
                    TimeSpan.FromSeconds(6));
            }
        });
    }



    [RelayCommand]
    private async Task PickUpColorAsync()
    {
        var hex = await _colorPicker.PickAsync(UpColor);
        if (!string.IsNullOrWhiteSpace(hex))
            UpColor = hex;
    }

    [RelayCommand]
    private async Task PickDownColorAsync()
    {
        var hex = await _colorPicker.PickAsync(DownColor);
        if (!string.IsNullOrWhiteSpace(hex))
            DownColor = hex;
    }

    [RelayCommand]
    private async Task PickRestartColorAsync()
    {
        var hex = await _colorPicker.PickAsync(RestartColor);
        if (!string.IsNullOrWhiteSpace(hex))
            RestartColor = hex;
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

    partial void OnUpTitleTemplateChanged(string value) => MarkDirty();
    partial void OnUpSummaryTemplateChanged(string value) => MarkDirty();
    partial void OnUpBodyTemplateChanged(string value) => MarkDirty();
    partial void OnUpColorChanged(string value) => MarkDirty();

    partial void OnDownTitleTemplateChanged(string value) => MarkDirty();
    partial void OnDownSummaryTemplateChanged(string value) => MarkDirty();
    partial void OnDownBodyTemplateChanged(string value) => MarkDirty();
    partial void OnDownColorChanged(string value) => MarkDirty();

    partial void OnRestartTitleTemplateChanged(string value) => MarkDirty();
    partial void OnRestartSummaryTemplateChanged(string value) => MarkDirty();
    partial void OnRestartBodyTemplateChanged(string value) => MarkDirty();
    partial void OnRestartColorChanged(string value) => MarkDirty();


    #endregion


}
