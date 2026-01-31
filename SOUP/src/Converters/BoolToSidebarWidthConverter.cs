using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SOUP.Converters;

/// <summary>
/// Converts a boolean (collapsed state) to sidebar width.
/// Collapsed: 60px, Expanded: 200px
/// </summary>
public class BoolToSidebarWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCollapsed)
        {
            return new GridLength(isCollapsed ? 60 : 200);
        }
        return new GridLength(200);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
