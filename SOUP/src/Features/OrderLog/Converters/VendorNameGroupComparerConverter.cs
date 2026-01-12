using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SOUP.Features.OrderLog.Converters
{
    /// <summary>
    /// Compares a member's VendorName with the group's first VendorName and
    /// returns Visibility.Collapsed when they are equal or the member name
    /// is empty; otherwise returns Visibility.Visible.
    /// Used to avoid repeating the same vendor name inside merged cards.
    /// </summary>
    public class VendorNameGroupComparerConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var memberName = values.Length > 0 ? values[0] as string : null;
                var groupFirstName = values.Length > 1 ? values[1] as string : null;

                if (string.IsNullOrWhiteSpace(memberName))
                    return Visibility.Collapsed;

                // If group first vendor exists and matches member, collapse
                if (!string.IsNullOrWhiteSpace(groupFirstName) && string.Equals(memberName?.Trim(), groupFirstName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return Visibility.Collapsed;

                return Visibility.Visible;
            }
            catch
            {
                return Visibility.Visible;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
