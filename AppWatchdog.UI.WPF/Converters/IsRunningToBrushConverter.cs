using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppWatchdog.UI.WPF.Converters;

public sealed class IsRunningToBrushConverter : IValueConverter
{
    public Brush RunningBrush { get; set; } = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
    public Brush StoppedBrush { get; set; } = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
            return isRunning ? RunningBrush : StoppedBrush;

        return StoppedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
