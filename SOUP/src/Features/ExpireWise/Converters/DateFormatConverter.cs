using System;
using System.Globalization;
using System.Windows.Data;

namespace SOUP.Features.ExpireWise.Converters;

/// <summary>
/// Converter for formatting dates based on a configurable format string.
/// </summary>
public class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime date)
            return string.Empty;

        var format = parameter as string ?? "MMMM yyyy";

        // Support named formats
        return format switch
        {
            "Short" => date.ToString("MM/yyyy"),
            "Long" => date.ToString("MMMM yyyy"),
            _ => date.ToString(format)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("DateFormatConverter does not support ConvertBack");
    }
}
