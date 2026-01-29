using System;
using System.Globalization;
using System.Windows.Data;

namespace AppWatchdog.UI.WPF.Converters;

public sealed class BoolToLastLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "Last run" : "Last check";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
