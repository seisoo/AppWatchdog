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
        if (!File.Exists(path))
        {
            var cfg = new WatchdogConfig
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

            Save(path, cfg);
            return cfg;
        }

        var json = File.ReadAllText(path);
        var cfg2 = JsonSerializer.Deserialize<WatchdogConfig>(json, PipeProtocol.JsonOptions);

        cfg2 ??= new WatchdogConfig();
        cfg2.Smtp ??= new SmtpSettings();
        cfg2.Ntfy ??= new NtfySettings();
        cfg2.Apps ??= new List<WatchedApp>();

        return cfg2;
    }

    public static void Save(string path, WatchdogConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions(PipeProtocol.JsonOptions)
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }
}
