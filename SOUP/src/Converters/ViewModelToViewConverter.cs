using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SOUP.ViewModels;
using SOUP.Views.AllocationBuddy;
using SOUP.Views.EssentialsBuddy;
using SOUP.Views.ExpireWise;
using SOUP.Views.SwiftLabel;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Views;

namespace SOUP.Converters;

/// <summary>
/// Converts a ViewModel instance to its corresponding View using convention-based mapping.
/// </summary>
/// <remarks>
/// <para>
/// This converter supports the MVVM pattern by allowing the ContentPresenter to automatically
/// display the correct View based on the bound ViewModel type.
/// </para>
/// <para>
/// Supported mappings:
/// <list type="bullet">
/// <item><see cref="AllocationBuddyRPGViewModel"/> → <see cref="AllocationBuddyRPGView"/></item>
/// <item><see cref="EssentialsBuddyViewModel"/> → <see cref="EssentialsBuddyView"/></item>
/// <item><see cref="ExpireWiseViewModel"/> → <see cref="ExpireWiseView"/></item>
/// <item><see cref="SwiftLabelViewModel"/> → <see cref="SwiftLabelView"/></item>
/// </list>
/// </para>
/// </remarks>
public class ViewModelToViewConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            AllocationBuddyRPGViewModel vm => new AllocationBuddyRPGView { DataContext = vm },
            EssentialsBuddyViewModel vm => new EssentialsBuddyView { DataContext = vm },
            ExpireWiseViewModel vm => new ExpireWiseView { DataContext = vm },
            SwiftLabelViewModel vm => new SwiftLabelView { DataContext = vm },
            OrderLogViewModel vm => new OrderLogView { DataContext = vm },
            null => null,
            _ => throw new ArgumentException($"Unknown ViewModel type: {value.GetType().Name}")
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
