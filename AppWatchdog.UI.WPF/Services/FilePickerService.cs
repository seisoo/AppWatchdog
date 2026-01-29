using Microsoft.Win32;
using System;
using System.IO;

namespace AppWatchdog.UI.WPF.Services;

public sealed class FilePickerService
{
    public string? Pick(string? initialPath = null)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select file",
            CheckFileExists = true,
            CheckPathExists = true
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                var dir = Path.GetDirectoryName(initialPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    dlg.InitialDirectory = dir;
            }
        }
        catch { }

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
