using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PeerChat
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visible = value is bool && (bool)value;
            if (parameter is string && ((string)parameter).Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                visible = !visible;
            }

            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }
}
