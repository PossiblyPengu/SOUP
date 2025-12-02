using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SAP.ViewModels;
using SAP.Views.AllocationBuddy;
using SAP.Views.EssentialsBuddy;
using SAP.Views.ExpireWise;
using SAP.Views.SwiftLabel;

namespace SAP.Converters;

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
            SwiftLabelViewModel vm => new SwiftLabelView { DataContext = vm },
            null => null,
            _ => throw new ArgumentException($"Unknown ViewModel type: {value.GetType().Name}")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
