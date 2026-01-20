using System.Text.Json;

namespace AppWatchdog.Shared;

public static class ConfigStore
{
    public static string GetDefaultConfigPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AppWatchdog");

        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    public static WatchdogConfig LoadOrCreateDefault(string path)
    {
        WatchdogConfig cfg;

        if (!File.Exists(path))
        {
            cfg = CreateDefaultConfig();
            Save(path, cfg);
        }
        else
        {
            var json = File.ReadAllText(path);
            cfg = JsonSerializer.Deserialize<WatchdogConfig>(json, PipeProtocol.JsonOptions)
                  ?? CreateDefaultConfig();
        }

        NormalizeConfig(cfg);
        DecryptSecrets(cfg);

        return cfg;
    }

    public static void Save(string path, WatchdogConfig cfg)
    {
        var persist = DeepClone(cfg);

        NormalizeConfig(persist);
        EncryptSecrets(persist);

        var json = JsonSerializer.Serialize(
            persist,
            new JsonSerializerOptions(PipeProtocol.JsonOptions)
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }

    private static WatchdogConfig CreateDefaultConfig()
    {
        return new WatchdogConfig
        {
            Apps = new List<WatchedApp>
            {
                new WatchedApp
                {
                    Name = "Beispiel",
                    ExePath = @"C:\Pfad\zu\deiner\app.exe",
                    Arguments = "",
                    Enabled = false
                }
            },
            Smtp = new SmtpSettings(),
            Ntfy = new NtfySettings()
        };
    }

    private static void NormalizeConfig(WatchdogConfig cfg)
    {
        cfg.Smtp ??= new SmtpSettings();
        cfg.Ntfy ??= new NtfySettings();
        cfg.Apps ??= new List<WatchedApp>();
    }

    private static void EncryptSecrets(WatchdogConfig cfg)
    {
        cfg.Smtp.Password = EncryptIfNotEmpty(cfg.Smtp.Password);
        cfg.Ntfy.Token = EncryptIfNotEmpty(cfg.Ntfy.Token);

        foreach (var app in cfg.Apps)
        {
            if (app.UptimeKuma != null)
            {
                app.UptimeKuma.PushToken =
                    EncryptIfNotEmpty(app.UptimeKuma.PushToken);
            }
        }
    }

    private static void DecryptSecrets(WatchdogConfig cfg)
    {
        cfg.Smtp.Password = DecryptIfNotEmpty(cfg.Smtp.Password);
        cfg.Ntfy.Token = DecryptIfNotEmpty(cfg.Ntfy.Token);

        foreach (var app in cfg.Apps)
        {
            if (app.UptimeKuma != null)
            {
                app.UptimeKuma.PushToken =
                    DecryptIfNotEmpty(app.UptimeKuma.PushToken);
            }
        }
    }

    private static string EncryptIfNotEmpty(string value)
        => string.IsNullOrWhiteSpace(value)
            ? value
            : ConfigCrypto.Encrypt(value);

    private static string DecryptIfNotEmpty(string value)
        => string.IsNullOrWhiteSpace(value)
            ? value
            : ConfigCrypto.Decrypt(value);

    private static WatchdogConfig DeepClone(WatchdogConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, PipeProtocol.JsonOptions);
        return JsonSerializer.Deserialize<WatchdogConfig>(json, PipeProtocol.JsonOptions)
               ?? new WatchdogConfig();
    }
}
