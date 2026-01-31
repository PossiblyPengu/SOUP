using System;
using System.Globalization;
using System.Windows.Data;

namespace SOUP.Converters;

/// <summary>
/// Converts an integer to a boolean based on a parameter value.
/// Used for binding integer properties to RadioButton IsChecked.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string strParam && int.TryParse(strParam, out int paramValue))
        {
            return intValue == paramValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is string strParam && int.TryParse(strParam, out int paramValue))
        {
            return paramValue;
        }
        return Binding.DoNothing;
    }
}
