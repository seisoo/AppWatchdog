using System;

namespace AppWatchdog.UI.WPF.Common;

public class CommonResources
{
    // Links (string → Uri sauber gekapselt)
    public Uri GitHub => new(AppLinks.GitHub);
    public Uri Steam => new(AppLinks.Steam);
    public Uri Mail => new(AppLinks.Mail);
    public Uri WpfUi => new(AppLinks.WpfUi);
    public Uri Unsplash => new(AppLinks.Unsplash);

    // Version
    public string Version => AppInfo.Version;
}
