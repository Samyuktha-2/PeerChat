using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PeerChat.Helpers
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        // true → Collapsed
        // false → Visible
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
                return flag ? Visibility.Collapsed : Visibility.Visible;

            return Visibility.Visible;
        }

        // Not required
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}