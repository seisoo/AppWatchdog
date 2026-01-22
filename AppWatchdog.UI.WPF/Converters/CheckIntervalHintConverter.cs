using AppWatchdog.Shared.Monitoring;
using AppWatchdog.UI.WPF.Localization;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AppWatchdog.UI.WPF.Converters
{
    public sealed class CheckIntervalHintConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            return value switch
            {
                WatchTargetType.HttpEndpoint =>
                    AppStrings.apps_interval_hint_http,

                WatchTargetType.TcpPort =>
                    AppStrings.apps_interval_hint_tcp,

                WatchTargetType.Executable =>
                    AppStrings.apps_interval_hint_executable,

                WatchTargetType.WindowsService =>
                    AppStrings.apps_interval_hint_service,

                _ => string.Empty
            };
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            // OneWay Binding → kein Rückweg nötig
            return Binding.DoNothing;
        }
    }
}