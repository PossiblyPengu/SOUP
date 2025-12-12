using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SAP.Converters;

/// <summary>
/// Converts a boolean value to <see cref="Visibility"/>.
/// </summary>
/// <remarks>
/// <c>true</c> converts to <see cref="Visibility.Visible"/>,
/// <c>false</c> converts to <see cref="Visibility.Collapsed"/>.
/// </remarks>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts a boolean value to <see cref="Visibility"/> with inverse logic.
/// </summary>
/// <remarks>
/// <c>true</c> converts to <see cref="Visibility.Collapsed"/>,
/// <c>false</c> converts to <see cref="Visibility.Visible"/>.
/// </remarks>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Collapsed;
        }
        return false;
    }
}

/// <summary>
/// Converts a boolean to a sort arrow character.
/// </summary>
/// <remarks>
/// <c>true</c> (descending) converts to "↓",
/// <c>false</c> (ascending) converts to "↑".
/// </remarks>
public class BoolToSortArrowConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDescending)
        {
            return isDescending ? "↓" : "↑";
        }
        return "↑";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == "↓";
    }
}
