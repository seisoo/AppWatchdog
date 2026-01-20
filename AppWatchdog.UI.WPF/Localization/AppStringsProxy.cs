using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AppWatchdog.UI.WPF.Localization
{
    public class AppStringsProxy : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string this[string key]
        {
            get
            {
                try
                {
                    var value = AppStrings.ResourceManager.GetString(
                        key,
                        CultureInfo.CurrentUICulture);

                    return string.IsNullOrEmpty(value)
                        ? $"##{key}##"
                        : value;
                }
                catch (MissingManifestResourceException)
                {
                    return $"##{key}##";
                }
                catch (CultureNotFoundException)
                {
                    return $"##{key}##";
                }
            }
        }


        public void Refresh()
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
                }));
        }

    }
}
