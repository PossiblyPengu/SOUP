using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MechaRogue.ViewModels;

namespace MechaRogue;

/// <summary>
/// Converts boolean to its inverse.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

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
/// Converts boolean to inverse Visibility.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts GameScreen enum to Visibility for specific screen.
/// </summary>
public class ScreenToVisibilityConverter : IValueConverter
{
    public static readonly ScreenToVisibilityConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GameScreen current && parameter is string target)
        {
            var targetScreen = Enum.Parse<GameScreen>(target);
            return current == targetScreen ? Visibility.Visible : Visibility.Collapsed;
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

/// <summary>
/// Shows element only when TutorialStep matches parameter index.
/// </summary>
public class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string paramStr && int.TryParse(paramStr, out int targetStep))
        {
            return currentStep == targetStep ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns accent color for current step, gray for others (tutorial dots).
/// </summary>
public class IndexToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(233, 69, 96));   // #e94560
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(100, 100, 100));
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string paramStr && int.TryParse(paramStr, out int targetStep))
        {
            return currentStep == targetStep ? AccentBrush : GrayBrush;
        }
        return GrayBrush;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts current/max durability to a width for the health bar.
/// </summary>
public class HealthBarWidthConverter : IMultiValueConverter
{
    public static readonly HealthBarWidthConverter Instance = new();
    
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 
            && values[0] is int current 
            && values[1] is int max 
            && max > 0)
        {
            var maxWidth = values[2] is string s && double.TryParse(s, out var w) ? w : 40.0;
            return Math.Max(0, (double)current / max * maxWidth);
        }
        return 0.0;
    }
    
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
