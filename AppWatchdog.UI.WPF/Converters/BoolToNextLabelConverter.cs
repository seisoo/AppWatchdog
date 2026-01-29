using System;
using System.Globalization;
using System.Windows.Data;

namespace AppWatchdog.UI.WPF.Converters;

public sealed class BoolToNextLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "Next planned" : "Next check";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
