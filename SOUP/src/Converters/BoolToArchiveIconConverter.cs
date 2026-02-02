using System;
using System.Globalization;
using System.Windows.Data;

namespace SOUP.Converters;

/// <summary>
/// Converts boolean to archive icon (📋 or ✖ for close)
/// </summary>
public class BoolToArchiveIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isShowingArchive && isShowingArchive)
            return "✖"; // Close icon when showing archive
        return "📋"; // Archive icon when not showing
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
