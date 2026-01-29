using System.Globalization;
using System.Text;

namespace AppWatchdog.Service.Helpers;

/// <summary>
/// Provides file-based logging for the service.
/// </summary>
public static class FileLogStore
{
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the log directory path.
    /// </summary>
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

    /// <summary>
    /// Appends a log entry to the current day log file.
    /// </summary>
    /// <param name="level">Log level.</param>
    /// <param name="message">Log message.</param>
    /// <param name="ex">Optional exception.</param>
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

    /// <summary>
    /// Lists available log day files.
    /// </summary>
    /// <returns>List of day strings.</returns>
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

    /// <summary>
    /// Reads the log file for a specific day.
    /// </summary>
    /// <param name="day">Day in yyyy-MM-dd format.</param>
    /// <returns>The log contents.</returns>
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
