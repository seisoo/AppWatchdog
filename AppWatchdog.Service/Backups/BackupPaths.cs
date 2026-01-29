namespace AppWatchdog.Service.Backups;

/// <summary>
/// Provides standard backup storage paths.
/// </summary>
public static class BackupPaths
{
    /// <summary>
    /// Gets the root backup directory.
    /// </summary>
    public static string Root
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AppWatchdog",
                "backups");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Gets the staging directory for temporary artifacts.
    /// </summary>
    public static string Staging
    {
        get
        {
            var dir = Path.Combine(Root, "staging");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Gets the state directory for backup state data.
    /// </summary>
    public static string State
    {
        get
        {
            var dir = Path.Combine(Root, "state");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
