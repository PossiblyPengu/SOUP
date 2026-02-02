using System.Globalization;
using System.Windows.Data;

namespace SOUP.Converters;

/// <summary>
/// Converts a DateTime to a month/year string for grouping
/// </summary>
public class MonthYearGroupConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return $"{dateTime:MMMM yyyy}";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
