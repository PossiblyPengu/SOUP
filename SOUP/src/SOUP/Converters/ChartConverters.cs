using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SOUP.Converters;

/// <summary>
/// Converts a percentage value to a height for bar charts (max 160px)
/// </summary>
public class PercentageToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            // Scale to max height of 160
            return Math.Max(4, percentage * 1.6);
        }
        return 4.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts a percentage value to a width for horizontal bars (based on container width)
/// </summary>
public class PercentageToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            // Return as percentage for use with MaxWidth binding or fixed max
            return Math.Max(4, percentage * 1.5); // Scale to ~150px max
        }
        return 4.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts a hex color string to a Color for use in SolidColorBrush
/// </summary>
public class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorString && !string.IsNullOrEmpty(colorString))
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                return Colors.Gray;
            }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts critical count to a proportional height for the critical overlay
/// </summary>
public class CriticalToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int criticalCount)
        {
            // Simple scaling - each critical item adds some height (max 80px)
            return Math.Min(80, criticalCount * 12);
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
