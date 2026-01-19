using System.Globalization;
using System.Text;

namespace AppWatchdog.Service;

public static class FileLogStore
{
    private static readonly object _lock = new();

    public static string LogDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AppWatchdog",
                "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static void WriteLine(string level, string message, Exception? ex = null)
    {
        var ts = DateTimeOffset.Now;
        var day = ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var file = Path.Combine(LogDir, $"{day}.log");

        var sb = new StringBuilder();
        sb.Append(ts.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(level).Append("] ");
        sb.Append(message);

        if (ex != null)
        {
            sb.AppendLine();
            sb.Append(ex);
        }

        var line = sb.ToString();

        lock (_lock)
        {
            File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    public static List<string> ListDays()
    {
        try
        {
            if (!Directory.Exists(LogDir))
                return new List<string>();

            return Directory.EnumerateFiles(LogDir, "*.log")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderByDescending(x => x, StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static string ReadDay(string day)
    {
        if (string.IsNullOrWhiteSpace(day))
            return "";

        if (day.Length != 10 || day[4] != '-' || day[7] != '-')
            return "";

        var file = Path.Combine(LogDir, $"{day}.log");
        if (!File.Exists(file))
            return "";

        try
        {
            return File.ReadAllText(file, Encoding.UTF8);
        }
        catch
        {
            return "";
        }
    }
}
