using System;
using System.Globalization;
using System.Windows.Data;
using SOUP.Data;

namespace SOUP.Views.ExpireWise;

/// <summary>
/// Converts a store code (Location) into a display string "CODE - Name" if available.
/// Falls back to the code if no store is found.
/// </summary>
public class StoreDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;

        var store = InternalStoreDictionary.FindByCode(code);
        if (store == null) return code;

        return string.IsNullOrWhiteSpace(store.Name) ? store.Code : $"{store.Code} - {store.Name}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // One-way conversion only
        return Binding.DoNothing;
    }
}
