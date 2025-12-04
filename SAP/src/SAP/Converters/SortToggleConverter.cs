using System;
using System.Globalization;
using System.Windows.Data;

namespace SAP.Converters;

/// <summary>
/// Converter that toggles between ascending and descending sort modes.
/// Takes current sort mode and returns the next mode for the specified column.
/// </summary>
public class SortToggleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string currentMode || parameter is not string column)
            return $"{parameter}-desc";

        // If clicking the same column, toggle direction
        if (currentMode.StartsWith(column))
        {
            return currentMode.EndsWith("-asc") ? $"{column}-desc" : $"{column}-asc";
        }

        // If clicking a different column, start with descending (or ascending for name)
        return column == "name" ? $"{column}-asc" : $"{column}-desc";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
