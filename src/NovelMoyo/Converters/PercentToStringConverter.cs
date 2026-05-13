using System.Globalization;
using System.Windows.Data;

namespace NovelMoyo.Converters;

[ValueConversion(typeof(double), typeof(string))]
public class PercentToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            d = Math.Max(0, Math.Min(100, d));
            return $"{d:F0}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && double.TryParse(s.TrimEnd('%'), NumberStyles.Float, culture, out var result))
            return result;
        return 0.0;
    }
}
