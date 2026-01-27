using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SOUP.Converters
{
    // MultiValue converter that ANDs boolean inputs and returns Visibility.
    // ConverterParameter: optional in the form "invert=0,2" to invert specific binding indexes.
    public class MultiBoolToVisConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0) return Visibility.Collapsed;

            var invertSet = new HashSet<int>();
            if (parameter is string p && p.StartsWith("invert=", StringComparison.OrdinalIgnoreCase))
            {
                var tail = p.Substring("invert=".Length);
                var parts = tail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out var idx)) invertSet.Add(idx);
                }
            }

            bool result = true;
            for (int i = 0; i < values.Length; i++)
            {
                var val = values[i];
                bool b = false;
                if (val is bool vb) b = vb;
                else if (val is bool?) b = ((bool?)val) ?? false;
                else
                {
                    if (val != null && bool.TryParse(val.ToString(), out var parsed)) b = parsed;
                }

                if (invertSet.Contains(i)) b = !b;
                result &= b;
            }

            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
