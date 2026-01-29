using Microsoft.Win32;
using System;

namespace AppWatchdog.UI.WPF.Services;

public sealed class FolderPickerService
{
    public string? Pick(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder",
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect = false
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }
}
