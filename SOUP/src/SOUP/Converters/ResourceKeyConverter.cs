using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SOUP.Converters;

/// <summary>
/// Converts a resource key string to the actual resource value.
/// </summary>
public class ResourceKeyConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrEmpty(key))
        {
            return Application.Current.TryFindResource(key);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
