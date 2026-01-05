using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Converters;

/// <summary>
/// Converts OrderItem.OrderStatus to a Color or Brush resource.
/// Eliminates repetitive DataTriggers in XAML.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    // Cache brushes to avoid repeated resource lookups
    private static Brush? _dangerBrush;
    private static Brush? _warningBrush;
    private static Brush? _successBrush;
    private static Brush? _tertiaryBrush;
    private static bool _cacheInitialized;

    private static void EnsureCacheInitialized()
    {
        if (_cacheInitialized) return;
        
        var app = Application.Current;
        _dangerBrush = app?.TryFindResource("DangerBrush") as Brush;
        _warningBrush = app?.TryFindResource("WarningBrush") as Brush;
        _successBrush = app?.TryFindResource("SuccessBrush") as Brush;
        _tertiaryBrush = app?.TryFindResource("TextTertiaryBrush") as Brush;
        _cacheInitialized = true;
    }

    /// <summary>Clears cached brushes (call on theme change)</summary>
    public static void InvalidateCache() => _cacheInitialized = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OrderItem.OrderStatus status)
            return DependencyProperty.UnsetValue;

        EnsureCacheInitialized();

        var brush = status switch
        {
            OrderItem.OrderStatus.NotReady => _dangerBrush,
            OrderItem.OrderStatus.OnDeck => _warningBrush,
            OrderItem.OrderStatus.InProgress => _successBrush,
            OrderItem.OrderStatus.Done => _tertiaryBrush,
            _ => _dangerBrush
        };

        if (brush != null)
        {
            return targetType == typeof(Color) && brush is SolidColorBrush solidBrush
                ? solidBrush.Color
                : brush;
        }

        // Fallback colors if resources not found
        return status switch
        {
            OrderItem.OrderStatus.NotReady => targetType == typeof(Color) ? Colors.Red : Brushes.Red,
            OrderItem.OrderStatus.OnDeck => targetType == typeof(Color) ? Colors.Orange : Brushes.Orange,
            OrderItem.OrderStatus.InProgress => targetType == typeof(Color) ? Colors.Green : Brushes.Green,
            OrderItem.OrderStatus.Done => targetType == typeof(Color) ? Colors.Gray : Brushes.Gray,
            _ => targetType == typeof(Color) ? Colors.Red : Brushes.Red
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts OrderItem.OrderStatus to a card background brush resource.
/// </summary>
public class StatusToCardBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OrderItem.OrderStatus status)
            return DependencyProperty.UnsetValue;

        var resourceKey = status switch
        {
            OrderItem.OrderStatus.NotReady => "OrderLogCardNotReadyBrush",
            OrderItem.OrderStatus.OnDeck => "OrderLogCardOnDeckBrush",
            OrderItem.OrderStatus.InProgress => "OrderLogCardInProgressBrush",
            OrderItem.OrderStatus.Done => "OrderLogCardDoneBrush",
            _ => "OrderLogCardNotReadyBrush"
        };

        return Application.Current.TryFindResource(resourceKey) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
