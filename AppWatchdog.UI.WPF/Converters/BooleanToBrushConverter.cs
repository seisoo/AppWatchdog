using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppWatchdog.UI.WPF.Converters;

public sealed class BooleanToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = Brushes.Transparent;
    public Brush FalseBrush { get; set; } = Brushes.Red;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? TrueBrush : FalseBrush;

        return FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
