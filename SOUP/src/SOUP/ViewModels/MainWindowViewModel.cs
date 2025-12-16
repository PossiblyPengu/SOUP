using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SOUP.Models;
using SOUP.Services;
using SOUP.Views;
using SOUP.Windows;

namespace SOUP.ViewModels;

/// <summary>
/// Main window ViewModel - Launcher with module navigation
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindowViewModel>? _logger;
    private readonly ThemeService _themeService;
    private readonly NavOrderService _navOrderService;
    private bool _disposed;

    [ObservableProperty]
    private string _title = "S.A.P";

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        // Sync IsSelected on all items
        foreach (var item in NavItems)
        {
            item.IsSelected = item == value;
        }
    }

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public LauncherViewModel LauncherViewModel { get; }
    public NavigationService NavigationService { get; }

    public MainWindowViewModel(
        LauncherViewModel launcherViewModel,
        NavigationService navigationService,
        ThemeService themeService,
        NavOrderService navOrderService,
        IServiceProvider serviceProvider,
        ILogger<MainWindowViewModel>? logger = null)
    {
        LauncherViewModel = launcherViewModel;
        NavigationService = navigationService;
        _themeService = themeService;
        _navOrderService = navOrderService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Initialize nav items
        InitializeNavItems();

        // Sync with theme service
        IsDarkMode = _themeService.IsDarkMode;
        _themeService.ThemeChanged += OnThemeChanged;

        // Update title when navigating to modules
        NavigationService.ModuleChanged += OnModuleChanged;
    }

    private void InitializeNavItems()
    {
        var defaultItems = new[]
        {
            new NavItem
            {
                Id = "ExpireWise",
                Name = "ExpireWise",
                Description = "Expiry tracking",
                IconKey = "ExpireWiseIcon",
                SplashIconKey = "ExpireWiseSplashIcon",
                SplashGradientKey = "ExpireWiseSplashGradient",
                IconGradientKey = "ExpireWiseIconGradient",
                IconShadowColorKey = "ExpireWiseIconShadowColor",
                SplashSubtitle = "Track product expiration dates and manage inventory lifecycle",
                Features = new[] { "📅 Expiry Tracking", "📊 Analytics", "🔔 Smart Alerts" },
                ShortcutHint = "Alt+1",
                IsVisible = LauncherViewModel.IsExpireWiseEnabled,
                Order = 0
            },
            new NavItem
            {
                Id = "AllocationBuddy",
                Name = "Allocation Buddy",
                Description = "Store allocations",
                IconKey = "AllocationBuddyIcon",
                SplashIconKey = "AllocationBuddySplashIcon",
                SplashGradientKey = "AllocationBuddySplashGradient",
                IconGradientKey = "AllocationBuddyIconGradient",
                IconShadowColorKey = "AllocationBuddyIconShadowColor",
                SplashSubtitle = "Smart inventory allocation and store distribution management",
                Features = new[] { "🏪 Multi-Store", "📋 BC Ready", "📥 Excel Import" },
                ShortcutHint = "Alt+2",
                IsVisible = LauncherViewModel.IsAllocationBuddyEnabled,
                Order = 1
            },
            new NavItem
            {
                Id = "EssentialsBuddy",
                Name = "Essentials Buddy",
                Description = "Inventory tracking",
                IconKey = "EssentialsBuddyIcon",
                SplashIconKey = "EssentialsBuddySplashIcon",
                SplashGradientKey = "EssentialsBuddySplashGradient",
                IconGradientKey = "EssentialsBuddyIconGradient",
                IconShadowColorKey = "EssentialsBuddyIconShadowColor",
                SplashSubtitle = "Track essential inventory items and manage stock levels",
                Features = new[] { "📦 Stock Tracking", "📋 BC Ready", "🔔 Low Stock Alerts" },
                ShortcutHint = "Alt+3",
                IsVisible = LauncherViewModel.IsEssentialsBuddyEnabled,
                Order = 2
            },
            new NavItem
            {
                Id = "SwiftLabel",
                Name = "Swift Label",
                Description = "Label generator",
                IconKey = "SwiftLabelIcon",
                SplashIconKey = "SwiftLabelSplashIcon",
                SplashGradientKey = "SwiftLabelSplashGradient",
                IconGradientKey = "SwiftLabelIconGradient",
                IconShadowColorKey = "SwiftLabelIconShadowColor",
                SplashSubtitle = "Generate and print labels quickly with customizable templates",
                Features = new[] { "🏷️ Quick Labels", "🖨️ Print Ready", "📐 Custom Sizes" },
                ShortcutHint = "Alt+4",
                IsVisible = LauncherViewModel.IsSwiftLabelEnabled,
                Order = 3
            },
            new NavItem
            {
                Id = "OrderLog",
                Name = "Order Log",
                Description = "Track transfers & shipments",
                IconKey = "OrderLogIcon",
                SplashIconKey = "OrderLogSplashIcon",
                SplashGradientKey = "OrderLogSplashGradient",
                IconGradientKey = "OrderLogIconGradient",
                IconShadowColorKey = "OrderLogIconShadowColor",
                SplashSubtitle = "Track transfer groups, mark orders complete, and colour-code entries",
                Features = new[] { "📋 Transfer Groups", "🎨 Colour Coding", "✅ Status Tracking" },
                ShortcutHint = "Alt+5",
                IsVisible = LauncherViewModel.IsOrderLogEnabled,
                Order = 4
            }
        };

        // Apply saved order
        foreach (var item in defaultItems)
        {
            item.Order = _navOrderService.GetOrder(item.Id, item.Order);
            item.PropertyChanged += OnNavItemPropertyChanged;
        }

        // Sort by order and add to collection
        foreach (var item in defaultItems.OrderBy(i => i.Order))
        {
            NavItems.Add(item);
        }

        // Select first visible item
        SelectedNavItem = NavItems.FirstOrDefault(i => i.IsVisible);
    }

    private void OnNavItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When a nav item's IsSelected changes to true, update SelectedNavItem
        if (e.PropertyName == nameof(NavItem.IsSelected) && sender is NavItem item && item.IsSelected)
        {
            if (SelectedNavItem != item)
            {
                SelectedNavItem = item;
            }
        }
    }

    /// <summary>
    /// Action delegate for the drag-drop behavior to call when items are reordered.
    /// </summary>
    public Action SaveNavOrderAction => SaveNavOrder;

    /// <summary>
    /// Called when nav items are reordered via drag-drop.
    /// </summary>
    public void SaveNavOrder()
    {
        _navOrderService.UpdateOrder(NavItems.Select(i => i.Id));
        _logger?.LogInformation("Saved nav order");
    }

    private void OnThemeChanged(object? sender, bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
    }

    private void OnModuleChanged(object? sender, string moduleName)
    {
        Title = moduleName == "Launcher"
            ? "S.A.P"
            : $"S.A.P - {moduleName}";
    }

    /// <summary>
    /// Toggles between light and dark themes (Ctrl+T shortcut).
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        _logger?.LogInformation("Theme toggled to {Theme}", IsDarkMode ? "Dark" : "Light");
    }

    /// <summary>
    /// Shows the About dialog with version and module information (F1 shortcut).
    /// </summary>
    [RelayCommand]
    private void ShowAbout()
    {
        try
        {
            var aboutWindow = new AboutWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
            _logger?.LogInformation("Opened About dialog");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open About dialog");
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel);
            settingsWindow.ShowDialog();
            _logger?.LogInformation("Opened unified settings window");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open unified settings window");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Unsubscribe from events to prevent memory leaks
                NavigationService.ModuleChanged -= OnModuleChanged;
                _themeService.ThemeChanged -= OnThemeChanged;
            }
            _disposed = true;
        }
    }
}
