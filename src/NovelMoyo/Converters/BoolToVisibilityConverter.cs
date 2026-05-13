using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NovelMoyo.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                return visibility != Visibility.Visible;
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
