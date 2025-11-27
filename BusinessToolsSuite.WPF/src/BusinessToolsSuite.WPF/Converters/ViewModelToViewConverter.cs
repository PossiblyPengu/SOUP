using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BusinessToolsSuite.WPF.ViewModels;
using BusinessToolsSuite.WPF.Views.AllocationBuddy;
using BusinessToolsSuite.WPF.Views.EssentialsBuddy;
using BusinessToolsSuite.WPF.Views.ExpireWise;

namespace BusinessToolsSuite.WPF.Converters;

/// <summary>
/// Converts a ViewModel instance to its corresponding View
/// </summary>
public class ViewModelToViewConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            AllocationBuddyRPGViewModel vm => new AllocationBuddyRPGView { DataContext = vm },
            EssentialsBuddyViewModel vm => new EssentialsBuddyView { DataContext = vm },
            ExpireWiseViewModel vm => new ExpireWiseView { DataContext = vm },
            null => null,
            _ => throw new ArgumentException($"Unknown ViewModel type: {value.GetType().Name}")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
