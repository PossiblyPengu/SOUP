using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SOUP.Models;

namespace SOUP.Converters;

/// <summary>
/// Converts a NavItem or string to Visibility based on whether its Id/value matches the parameter.
/// </summary>
public class NavItemIdToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string expectedId)
            return Visibility.Collapsed;

        // Handle NavItem
        if (value is NavItem navItem)
        {
            return navItem.Id == expectedId ? Visibility.Visible : Visibility.Collapsed;
        }

        // Handle string (e.g., CurrentModuleName)
        if (value is string stringValue)
        {
            return stringValue == expectedId ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
