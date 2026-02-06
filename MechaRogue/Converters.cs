using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MechaRogue;

/// <summary>
/// Converts boolean to opacity (1.0 for true/operational, 0.4 for false/broken).
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // IsBroken == true means part is broken, so we want low opacity
        if (value is bool isBroken)
        {
            return isBroken ? 0.35 : 1.0;
        }
        return 1.0;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts IsOperational bool to a border color (green if operational).
/// </summary>
public class BoolToBorderConverter : IValueConverter
{
    public static readonly BoolToBorderConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOperational)
        {
            return isOperational 
                ? new SolidColorBrush(Color.FromRgb(74, 222, 128))  // Green
                : new SolidColorBrush(Color.FromRgb(100, 100, 100)); // Gray
        }
        return new SolidColorBrush(Colors.Gray);
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts IsOperational bool to a border color for enemies (red if operational).
/// </summary>
public class BoolToDangerBorderConverter : IValueConverter
{
    public static readonly BoolToDangerBorderConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOperational)
        {
            return isOperational 
                ? new SolidColorBrush(Color.FromRgb(248, 113, 113))  // Red
                : new SolidColorBrush(Color.FromRgb(100, 100, 100)); // Gray
        }
        return new SolidColorBrush(Colors.Gray);
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
