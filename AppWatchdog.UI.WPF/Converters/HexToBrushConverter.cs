using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppWatchdog.UI.WPF.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public Brush FallbackBrush { get; set; } = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return FallbackBrush;

        try
        {
            var normalized = text.StartsWith('#') ? text : "#" + text;
            var color = (Color)ColorConverter.ConvertFromString(normalized);
            return new SolidColorBrush(color);
        }
        catch
        {
            return FallbackBrush;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
