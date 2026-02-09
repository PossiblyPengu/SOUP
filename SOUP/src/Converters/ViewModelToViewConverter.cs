using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Views;
using SOUP.ViewModels;
using SOUP.Views.AllocationBuddy;
using SOUP.Views.EssentialsBuddy;
using SOUP.Views.ExpireWise;
using SOUP.Views.SwiftLabel;

namespace SOUP.Converters;

/// <summary>
/// Converts a ViewModel instance to its corresponding View using convention-based mapping.
/// Views are cached per ViewModel instance so navigation doesn't recreate the visual tree.
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
    private static readonly ConditionalWeakTable<object, FrameworkElement> _viewCache = new();

    /// <inheritdoc/>
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return null;

        return _viewCache.GetValue(value, vm => vm switch
        {
            AllocationBuddyRPGViewModel => new AllocationBuddyRPGView { DataContext = vm },
            EssentialsBuddyViewModel => new EssentialsBuddyView { DataContext = vm },
            ExpireWiseViewModel => new ExpireWiseView { DataContext = vm },
            SwiftLabelViewModel => new SwiftLabelView { DataContext = vm },
            OrderLogViewModel => new OrderLogSettingsView { DataContext = vm },
            _ => throw new ArgumentException($"Unknown ViewModel type: {vm.GetType().Name}")
        });
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
