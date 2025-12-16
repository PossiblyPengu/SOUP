using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SOUP.Converters;

/// <summary>
/// Converts a boolean value to a success/error color
/// </summary>
public class BoolToSuccessColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Green for success, orange for warning/not found
            return boolValue 
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))   // Green 
                : new SolidColorBrush(Color.FromRgb(234, 179, 8));  // Yellow/amber
        }
        return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray default
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}


