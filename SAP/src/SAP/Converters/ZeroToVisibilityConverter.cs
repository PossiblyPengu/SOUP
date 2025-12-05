using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SAP.Converters;

/// <summary>
/// Converts zero values to Visibility.Visible, non-zero to Collapsed
/// Used to show empty state messages when collections are empty
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // If parameter is "Invert", hide when zero, show when not zero
        bool invert = parameter?.ToString() == "Invert";

        bool isZero = false;
        if (value is int intVal)
            isZero = intVal == 0;
        else if (value is long longVal)
            isZero = longVal == 0;
        else if (value is double doubleVal)
            isZero = doubleVal == 0;
        else if (value is decimal decVal)
            isZero = decVal == 0;

        if (invert)
        {
            return isZero ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            return isZero ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
