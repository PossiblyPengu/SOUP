using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace BusinessToolsSuite.Shared.Converters
{
    public class BoolToHighlightBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                var b = value as bool? ?? false;
                if (b)
                    return new SolidColorBrush(Color.FromRgb(255, 249, 196)); // light highlight
                return Avalonia.Media.Brushes.Transparent;
            }
            catch
            {
                return Avalonia.Media.Brushes.Transparent;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
