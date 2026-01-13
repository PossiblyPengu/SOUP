using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    private readonly UpdateService _updateService;
    private readonly Timer _updateCheckTimer;
    private bool _disposed;

    [ObservableProperty]
    private string _title = "S.O.U.P";

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateVersion = "";

    [ObservableProperty]
    private UpdateInfo? _availableUpdate;

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
        UpdateService updateService,
        IServiceProvider serviceProvider,
        ILogger<MainWindowViewModel>? logger = null)
    {
        LauncherViewModel = launcherViewModel;
        NavigationService = navigationService;
        _themeService = themeService;
        _navOrderService = navOrderService;
        _updateService = updateService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Initialize nav items
        InitializeNavItems();

        // Sync with theme service
        IsDarkMode = _themeService.IsDarkMode;
        _themeService.ThemeChanged += OnThemeChanged;

        // Update title when navigating to modules
        NavigationService.ModuleChanged += OnModuleChanged;

        // Start periodic update checking (check every 30 minutes, initial check after 5 seconds)
        _updateCheckTimer = new Timer(async _ => await CheckForUpdatesAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(30));
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();

            // Update on UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (updateInfo != null)
                {
                    AvailableUpdate = updateInfo;
                    UpdateVersion = updateInfo.Version;
                    IsUpdateAvailable = true;
                    _logger?.LogInformation("Update available: v{Version}", updateInfo.Version);
                }
                else
                {
                    IsUpdateAvailable = false;
                    AvailableUpdate = null;
                    UpdateVersion = "";
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check for updates");
        }
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
            ? "S.O.U.P"
            : $"S.O.U.P - {moduleName}";
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
            var aboutWindow = new AboutWindow();
            // Only set owner if MainWindow is visible (don't block widget when main window is hidden)
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow)
            {
                aboutWindow.Owner = mainWindow;
            }
            aboutWindow.ShowDialog();
            _logger?.LogInformation("Opened About dialog");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open About dialog");
        }
    }

    /// <summary>
    /// Opens the About dialog and triggers an update check.
    /// </summary>
    [RelayCommand]
    private void ShowUpdate()
    {
        // Trigger update check in background (fire-and-forget)
        _ = CheckForUpdatesAsync();
        ShowAbout();
    }

    /// <summary>
    /// Dismisses the update notification banner.
    /// </summary>
    [RelayCommand]
    private void DismissUpdate()
    {
        IsUpdateAvailable = false;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            _logger?.LogDebug("OpenSettings command invoked");
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel);

            // Find the main window - Application.Current.MainWindow may not be reliable
            var mainWindow = System.Windows.Application.Current?.Windows
                .OfType<MainWindow>()
                .FirstOrDefault(w => w.IsVisible);

            if (mainWindow != null)
            {
                settingsWindow.Owner = mainWindow;
            }

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
                // Stop update checking timer
                _updateCheckTimer?.Dispose();

                // Unsubscribe from events to prevent memory leaks
                NavigationService.ModuleChanged -= OnModuleChanged;
                _themeService.ThemeChanged -= OnThemeChanged;

                // Unsubscribe from nav item property changes
                foreach (var item in NavItems)
                {
                    item.PropertyChanged -= OnNavItemPropertyChanged;
                }
            }
            _disposed = true;
        }
    }
}
