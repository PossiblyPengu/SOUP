using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SOUP.Models;

namespace SOUP.Converters;

/// <summary>
/// Converts a NavItem to Visibility based on whether its Id matches the parameter.
/// </summary>
public class NavItemIdToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NavItem navItem && parameter is string expectedId)
        {
            return navItem.Id == expectedId ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
