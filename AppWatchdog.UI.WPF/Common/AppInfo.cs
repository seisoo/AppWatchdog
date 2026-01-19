using System.Reflection;

namespace AppWatchdog.UI.WPF.Common;

public static class AppInfo
{
    public static string Version
        => Assembly.GetExecutingAssembly()
                   .GetName()
                   .Version?
                   .ToString() ?? "unknown";
}
