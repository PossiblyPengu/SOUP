using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Core.Interfaces;
using SOUP.Features.ExpireWise.Models;
using SOUP.Infrastructure.Services.Parsers;
using SOUP.Services;
using SOUP.Views;
using SOUP.Views.ExpireWise;

namespace SOUP.ViewModels;

public partial class ExpireWiseViewModel : ObservableObject, IDisposable
{
    #region Fields

    // Core services and dependencies
    private readonly IExpireWiseRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly SOUP.Infrastructure.Services.Parsers.ExpireWiseParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpireWiseViewModel>? _logger;

    // Feature services
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseSearchService _searchService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseMonthNavigationService _navigationService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseImportExportService _importExportService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseNotificationService _notificationService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseItemService _itemService;
    private readonly SOUP.Features.ExpireWise.Services.ItemLookupService _itemLookupService;

    // Settings / timers
    private Infrastructure.Services.SettingsService? _settingsService;
    private SOUP.Features.ExpireWise.Models.ExpireWiseSettings? _settings;
    private int _autoRefreshMinutes = 0;
    private DispatcherTimer? _notificationTimer;
    private EventHandler? _notificationTimerHandler;
    private System.Threading.Timer? _searchDebounceTimer;
    private string _dateDisplayFormat = "MMMM yyyy";
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _items = new();

    /// <summary>
    /// Gets the collection view for efficient filtering and grouping.
    /// </summary>
    private ICollectionView? _itemsView;
    public ICollectionView? ItemsView
    {
        get => _itemsView;
        private set => SetProperty(ref _itemsView, value);
    }

    [ObservableProperty]
    private ObservableCollection<MonthGroup> _availableMonths = new();

    /// <summary>
    /// Timeline view - months with their items for vertical scrolling
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MonthWithItems> _timelineMonths = new();

    /// <summary>
    /// Notifications list (populated by CheckNotifications, used internally)
    /// </summary>
    private readonly List<ExpirationItem> _notificationsList = new();

    [ObservableProperty]
    private ExpireWiseAnalyticsViewModel _analytics = new();

    /// <summary>
    /// Gets or sets the currently selected expiration item.
    /// </summary>
    [ObservableProperty]
    private ExpirationItem? _selectedItem;

    /// <summary>
    /// Gets or sets the currently selected items (supports multi-select).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _selectedItems = new();

    /// <summary>
    /// Gets or sets the search text for filtering items.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Gets or sets the status filter: "All", "Good", "Warning", "Critical", or "Expired".
    /// </summary>
    [ObservableProperty]
    private string _statusFilter = "All";

    /// <summary>
    /// Available status filter options for the ComboBox.
    /// </summary>
    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "All", "Good", "Warning", "Critical", "Expired"
    };

    /// <summary>
    /// Gets or sets the currently displayed month.
    /// </summary>
    [ObservableProperty]
    private DateTime _currentMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);

    /// <summary>
    /// Gets or sets the formatted display string for the current month.
    /// </summary>
    [ObservableProperty]
    private string _currentMonthDisplay = string.Empty;

    /// <summary>
    /// Gets or sets whether data is currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Gets or sets whether a long-running operation is in progress (shows loading overlay).
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Gets or sets the message to display during busy operations.
    /// </summary>
    [ObservableProperty]
    private string _busyMessage = "Loading...";

    /// <summary>
    /// Gets or sets the current status message displayed to the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Gets or sets whether there is data loaded.
    /// </summary>
    [ObservableProperty]
    private bool _hasData;

    #endregion

    #region Month Statistics

    /// <summary>
    /// Gets or sets the count of items expiring in the current month.
    /// </summary>
    [ObservableProperty]
    private int _monthItemCount;

    /// <summary>
    /// Gets or sets the total units expiring in the current month.
    /// </summary>
    [ObservableProperty]
    private int _monthTotalUnits;

    /// <summary>
    /// Gets or sets the count of items in critical status (≤7 days until expiry).
    /// </summary>
    [ObservableProperty]
    private int _criticalCount;

    /// <summary>
    /// Gets or sets the count of expired items.
    /// </summary>
    [ObservableProperty]
    private int _expiredCount;

    /// <summary>
    /// Gets or sets the count of items expiring soon (within warning threshold).
    /// </summary>
    [ObservableProperty]
    private int _expiringSoonCount;

    /// <summary>
    /// Gets or sets the total count of all items.
    /// </summary>
    [ObservableProperty]
    private int _totalItems;

    /// <summary>
    /// Gets or sets the count of distinct months with expiring items.
    /// </summary>
    [ObservableProperty]
    private int _totalMonths;

    /// <summary>
    /// Gets or sets the sort option for items within each month column.
    /// Options: "Date", "Description", "Location", "Quantity"
    /// </summary>
    [ObservableProperty]
    private string _timelineSortBy = "Date";

    /// <summary>
    /// Available sort options for the timeline view.
    /// </summary>
    public string[] TimelineSortOptions { get; } = ["Date", "Description", "Location", "Quantity"];

    #endregion

    #region Page Navigation

    /// <summary>
    /// Gets or sets the current page: "Timeline" or "Archive".
    /// </summary>
    [ObservableProperty]
    private string _currentPage = "Timeline";

    /// <summary>
    /// Archive items collection (expired items before current month).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _archiveItems = new();

    /// <summary>
    /// Archive items grouped by month for display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MonthWithItems> _archiveMonths = new();

    /// <summary>
    /// Total count of archived items.
    /// </summary>
    [ObservableProperty]
    private int _archiveItemCount;

    /// <summary>
    /// Total units in archive.
    /// </summary>
    [ObservableProperty]
    private int _archiveTotalUnits;

    /// <summary>
    /// Search text for archive filtering.
    /// </summary>
    [ObservableProperty]
    private string _archiveSearchText = string.Empty;

    /// <summary>
    /// Sort option for archive view.
    /// </summary>
    [ObservableProperty]
    private string _archiveSortBy = "Date";

    #endregion

    #region Kanban Urgency Collections

    /// <summary>
    /// Count of overdue/expired items (used in UI summary).
    /// </summary>
    [ObservableProperty]
    private int _overdueCount;

    #endregion

    #region UI State

    /// <summary>
    /// Gets or sets the display text for the last save time.
    /// </summary>
    [ObservableProperty]
    private string _lastSavedDisplay = string.Empty;

    /// <summary>
    /// Gets or sets the index of the selected tab.
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// Gets or sets whether items should be grouped by store location.
    /// </summary>
    [ObservableProperty]
    private bool _groupByStore = true;

    /// <summary>
    /// Gets or sets the currently selected store for filtering.
    /// Null means "All Stores".
    /// </summary>
    [ObservableProperty]
    private Data.Entities.StoreEntity? _selectedStore;

    /// <summary>
    /// Event raised when the SelectedStore property changes.
    /// </summary>
    public event Action<Data.Entities.StoreEntity?>? SelectedStoreChanged;

    /// <summary>
    /// Event raised when the GroupByStore property changes.
    /// </summary>
    public event Action<bool>? GroupByStoreChanged;

    /// <summary>
    /// Gets or sets the analytics ViewModel for the analytics dashboard.
    /// </summary>
    [ObservableProperty]

    private bool _isInitialized;

    #endregion

    #region Quick Add Properties

    /// <summary>
    /// Gets or sets the SKU/item number for quick add.
    /// </summary>
    [ObservableProperty]
    private string _quickAddSku = string.Empty;

    /// <summary>
    /// Gets or sets the quick add status message.
    /// </summary>
    [ObservableProperty]
    private string _quickAddStatus = string.Empty;

    /// <summary>
    /// Gets or sets whether the quick add panel is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _quickAddExpanded = false;

    /// <summary>
    /// Gets or sets the month for quick add (defaults to next month).
    /// </summary>
    [ObservableProperty]
    private int _quickAddMonth = DateTime.Today.AddMonths(1).Month;

    /// <summary>
    /// Gets or sets the year for quick add.
    /// </summary>
    [ObservableProperty]
    private int _quickAddYear = DateTime.Today.AddMonths(1).Year;

    /// <summary>
    /// Gets or sets the units for quick add.
    /// </summary>
    [ObservableProperty]
    private int _quickAddUnits = 1;

    /// <summary>
    /// Available months for quick add dropdown.
    /// </summary>
    public ObservableCollection<MonthOption> QuickAddMonths { get; } = new()
    {
        new(1, "Jan"), new(2, "Feb"), new(3, "Mar"),
        new(4, "Apr"), new(5, "May"), new(6, "Jun"),
        new(7, "Jul"), new(8, "Aug"), new(9, "Sep"),
        new(10, "Oct"), new(11, "Nov"), new(12, "Dec")
    };

    /// <summary>
    /// Available years for quick add dropdown.
    /// </summary>
    public ObservableCollection<int> QuickAddYears { get; } = new(
        Enumerable.Range(DateTime.Today.Year, 6));

    /// <summary>
    /// Gets or sets the store for quick add.
    /// </summary>
    [ObservableProperty]
    private Data.Entities.StoreEntity? _quickAddStore;

    /// <summary>
    /// Gets or sets whether Quick Add is currently validating the SKU.
    /// </summary>
    [ObservableProperty]
    private bool _quickAddIsValidating;

    /// <summary>
    /// Gets or sets whether the Quick Add SKU is valid.
    /// </summary>
    [ObservableProperty]
    private bool _quickAddIsValid;

    /// <summary>
    /// Gets or sets the item preview text for Quick Add (shown when valid).
    /// </summary>
    [ObservableProperty]
    private string _quickAddItemPreview = string.Empty;

    /// <summary>
    /// Gets or sets whether Quick Add has validation errors (some items not found).
    /// </summary>
    [ObservableProperty]
    private bool _quickAddHasErrors;

    /// <summary>
    /// The looked-up dictionary item for Quick Add (cached).
    /// Used when single SKU is entered.
    /// </summary>
    private Data.Entities.DictionaryItemEntity? _quickAddLookedUpItem;

    /// <summary>
    /// Parsed items from multi-line input (SKU, Qty, Found item).
    /// </summary>
    private List<(string Sku, int Qty, Data.Entities.DictionaryItemEntity? Item)> _quickAddParsedItems = new();

    /// <summary>
    /// Available stores for dropdown selection (includes "No Store" option).
    /// </summary>
    public ObservableCollection<Data.Entities.StoreEntity> AvailableStores { get; } = new();

    /// <summary>
    /// Stores for the tab navigation with item counts.
    /// </summary>
    public ObservableCollection<StoreTabModel> StoreTabModels { get; } = new();

    /// <summary>
    /// Items for the store dropdown selector (includes "All Stores" option).
    /// </summary>
    public ObservableCollection<StoreDropdownItem> StoreDropdownItems { get; } = new();

    /// <summary>
    /// The currently selected store dropdown item.
    /// </summary>
    [ObservableProperty]
    private StoreDropdownItem? _selectedStoreDropdownItem;

    /// <summary>
    /// Queue of items pending confirmation (not yet saved to database).
    /// </summary>
    public ObservableCollection<ExpirationItem> QuickAddQueue { get; } = new();

    /// <summary>
    /// Gets whether there are items in the queue.
    /// </summary>
    [ObservableProperty]
    private bool _hasQueuedItems;

    /// <summary>
    /// Debounce timer for SKU lookup (waits 300ms after user stops typing).
    /// </summary>
    private System.Threading.Timer? _skuLookupDebounceTimer;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpireWiseViewModel"/> class.
    /// </summary>
    /// <param name="repository">The repository for persisting expiration data.</param>
    /// <param name="fileService">The service for file import/export operations.</param>
    /// <param name="dialogService">The service for displaying dialogs.</param>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="searchService">The search/filter service for ExpireWise.</param>
    /// <param name="navigationService">The month navigation service for ExpireWise.</param>
    /// <param name="importExportService">The import/export helper service for ExpireWise.</param>
    /// <param name="notificationService">The notification service for ExpireWise.</param>
    /// <param name="itemService">The item management service for ExpireWise.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ExpireWiseViewModel(
        IExpireWiseRepository repository,
        IFileImportExportService fileService,
        DialogService dialogService,
        IServiceProvider serviceProvider,
        SOUP.Features.ExpireWise.Services.ExpireWiseSearchService searchService,
        SOUP.Features.ExpireWise.Services.ExpireWiseMonthNavigationService navigationService,
        SOUP.Features.ExpireWise.Services.ExpireWiseImportExportService importExportService,
        SOUP.Features.ExpireWise.Services.ExpireWiseNotificationService notificationService,
        SOUP.Features.ExpireWise.Services.ExpireWiseItemService itemService,
        ILogger<ExpireWiseViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _parser = new ExpireWiseParser(null);
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _searchService = searchService;
        _navigationService = navigationService;
        _importExportService = importExportService;
        _notificationService = notificationService;
        _itemService = itemService;
        _itemLookupService = new SOUP.Features.ExpireWise.Services.ItemLookupService();

        // Subscribe to settings changes for dynamic updates
        _settingsService = serviceProvider.GetService<Infrastructure.Services.SettingsService>();
        if (_settingsService != null)
        {
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

        // Subscribe to dictionary data changes (e.g., stores/items imported)
        DictionaryManagementViewModel.DictionaryDataChanged += OnDictionaryDataChanged;

        // Load available stores from dictionary
        LoadAvailableStores();

        // Subscribe to SKU changes for debounced lookup
        PropertyChanged += OnQuickAddSkuChanged;

        UpdateMonthDisplay();
    }

    /// <summary>
    /// Handles dictionary data changes (items/stores imported) to refresh cached data.
    /// </summary>
    private void OnDictionaryDataChanged(object? sender, EventArgs e)
    {
        // Reload stores on UI thread
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            LoadAvailableStores();
            _logger?.LogInformation("Reloaded stores after dictionary data changed");
        });
    }

    /// <summary>
    /// Handles settings change notifications to apply settings dynamically.
    /// </summary>
    private void OnSettingsChanged(object? sender, Infrastructure.Services.SettingsChangedEventArgs e)
    {
        if (e.AppName == "ExpireWise")
        {
            _ = LoadAndApplySettingsAsync();
        }
    }

    /// <summary>
    /// Initialize the view model and load data
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        IsInitialized = true;

        // Load and apply settings for expiration thresholds
        await LoadAndApplySettingsAsync();
        // Load Quick Add settings (sticky preferences)
        await LoadQuickAddSettings();
        await LoadItems();

        // Initialize ICollectionView for efficient filtering
        InitializeItemsView();

        UpdateAnalytics();

        // Set up periodic notification checks if configured
        try
        {
            if (_autoRefreshMinutes > 0)
            {
                _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(_autoRefreshMinutes) };
                _notificationTimerHandler = async (s, e) => await CheckNotifications();
                _notificationTimer.Tick += _notificationTimerHandler;
                _notificationTimer.Start();

                // Perform an initial check
                await CheckNotifications();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to start notification timer");
        }
    }

    /// <summary>
    /// Loads settings from the settings service and applies expiration thresholds globally.
    /// </summary>
    private async Task LoadAndApplySettingsAsync()
    {
        try
        {
            var settingsService = _serviceProvider.GetService<Infrastructure.Services.SettingsService>();
            if (settingsService != null)
            {
                var settings = await settingsService.LoadSettingsAsync<Core.Entities.Settings.ExpireWiseSettings>("ExpireWise");

                // Apply thresholds to the static properties on ExpirationItem
                ExpirationItem.CriticalDaysThreshold = settings.CriticalThresholdDays;
                ExpirationItem.WarningDaysThreshold = settings.WarningThresholdDays;

                // Apply default filter settings
                StatusFilter = settings.DefaultStatusFilter;

                // Cache date display format
                _dateDisplayFormat = settings.DateDisplayFormat ?? _dateDisplayFormat;
                // Cache auto-refresh interval
                _autoRefreshMinutes = settings.AutoRefreshIntervalMinutes;

                // Refresh month display to apply new date format
                UpdateMonthDisplay();

                _logger?.LogInformation("Applied ExpireWise settings: Critical={Critical} days, Warning={Warning} days, Filter={Filter}",
                    settings.CriticalThresholdDays, settings.WarningThresholdDays, settings.DefaultStatusFilter);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load ExpireWise settings, using defaults");
        }
    }

    private string FormatExpirationDate(DateTime date)
    {
        return _dateDisplayFormat switch
        {
            "Short" => date.ToString("MM/yyyy"),
            "Long" => date.ToString("MMMM yyyy"),
            _ => date.ToString("MMMM yyyy"),
        };
    }

    /// <summary>
    /// Validates an expiration item for required fields and reasonable date ranges.
    /// </summary>
    private bool ValidateItem(ExpirationItem item, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(item.ItemNumber) && string.IsNullOrWhiteSpace(item.Description))
        {
            errorMessage = "Item must have either an Item Number or Description.";
            return false;
        }

        if (item.ExpiryDate < DateTime.Today.AddYears(-1))
        {
            errorMessage = "Expiration date cannot be more than 1 year in the past.\n\nPlease verify the date is correct.";
            return false;
        }

        if (item.ExpiryDate > DateTime.Today.AddYears(10))
        {
            errorMessage = "Expiration date cannot be more than 10 years in the future.\n\nPlease verify the date is correct.";
            return false;
        }

        if (item.Units < 0)
        {
            errorMessage = "Units cannot be negative.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void UpdateAnalytics()
    {
        Analytics.UpdateAnalytics(Items);
    }

    /// <summary>
    /// Event raised when search box should be focused (triggered by Ctrl+F)
    /// </summary>
    public event Action? FocusSearchRequested;

    [RelayCommand]
    private async Task CheckNotifications()
    {
        try
        {
            var settingsService = _serviceProvider.GetService<Infrastructure.Services.SettingsService>();
            var settings = settingsService != null ? await settingsService.LoadSettingsAsync<Core.Entities.Settings.ExpireWiseSettings>("ExpireWise") : null;
            var enabled = settings?.ShowExpirationNotifications ?? true;
            var threshold = settings?.WarningThresholdDays ?? ExpirationItem.WarningDaysThreshold;

            var list = await _notificationService.GetNotificationsAsync(Items, threshold, enabled);
            _notificationsList.Clear();
            _notificationsList.AddRange(list);

            if (_notificationsList.Count > 0)
            {
                StatusMessage = $"{_notificationsList.Count} upcoming expiration(s)";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check notifications");
            StatusMessage = "Failed to check notifications";
        }
    }

    [RelayCommand]
    private void RemoveNotification(ExpirationItem? item)
    {
        if (item == null) return;
        _notificationsList.Remove(item);
    }

    [RelayCommand]
    private void ClearNotifications()
    {
        _notificationsList.Clear();
    }

    [RelayCommand]
    private void FocusSearch()
    {
        FocusSearchRequested?.Invoke();
    }

    [RelayCommand]
    private async Task LoadItems()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading items...";

            var allItems = await _repository.GetAllAsync();
            var activeItems = allItems.Where(i => !i.IsDeleted).ToList();

            Items.Clear();
            // Order items by Location (store) then by expiry date for consistent display
            foreach (var item in activeItems.OrderBy(i => i.Location ?? string.Empty).ThenBy(i => i.ExpiryDate))
            {
                Items.Add(item);
            }

            HasData = Items.Count > 0;
            TotalItems = Items.Count;

            // Update item counts per store
            UpdateStoreItemCounts();

            // Calculate total months with data (distinct months across all items)
            TotalMonths = Items
                .Select(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1))
                .Distinct()
                .Count();

            // Build available months from data
            BuildAvailableMonths();

            // Navigate to the first month with data, or current month
            if (AvailableMonths.Any())
            {
                var firstWithItems = AvailableMonths.FirstOrDefault(m => m.ItemCount > 0);
                if (firstWithItems != null)
                {
                    CurrentMonth = firstWithItems.Month;
                }
            }

            ApplyFilters();
            StatusMessage = $"Loaded {Items.Count} items across {TotalMonths} months";
            _logger?.LogInformation("Loaded {Count} expiration items", Items.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading items: {ex.Message}";
            _logger?.LogError(ex, "Exception while loading items");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildAvailableMonths()
    {
        // Build the visible month pills around the current month via navigation service
        var groups = _navigationService.BuildMonthGroups(Items, CurrentMonth, _dateDisplayFormat);
        AvailableMonths.Clear();
        foreach (var g in groups)
            AvailableMonths.Add(g);
        
        // Also build the timeline view
        BuildTimelineView();
    }

    /// <summary>
    /// Builds the timeline view showing Archive (expired) + current month + next 5 months.
    /// </summary>
    private void BuildTimelineView()
    {
        TimelineMonths.Clear();
        
        // Get items filtered by store if selected
        var items = Items.Where(i => !i.IsDeleted).AsEnumerable();
        _logger?.LogDebug("BuildTimelineView: Total items={Count}, SelectedStore={Store}", 
            Items.Count(i => !i.IsDeleted), SelectedStore?.Code ?? "ALL");
        
        if (SelectedStore != null && !string.IsNullOrEmpty(SelectedStore.Code))
        {
            // Match by either Code or Name (imported data may use either)
            items = items.Where(i => 
                string.Equals(i.Location, SelectedStore.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Location, SelectedStore.Name, StringComparison.OrdinalIgnoreCase));
        }
        
        // Apply text search if any
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            items = _searchService.Search(items, SearchText);
        }
        
        // Apply status filter
        if (StatusFilter != "All")
        {
            var status = StatusFilter switch
            {
                "Good" => ExpirationStatus.Good,
                "Warning" => ExpirationStatus.Warning,
                "Critical" => ExpirationStatus.Critical,
                "Expired" => ExpirationStatus.Expired,
                _ => (ExpirationStatus?)null
            };
            if (status.HasValue)
            {
                items = items.Where(i => i.Status == status.Value);
            }
        }
        
        var itemsList = items.ToList();
        _logger?.LogDebug("BuildTimelineView: After filter items={Count}", itemsList.Count);
        
        var today = DateTime.Today;
        var currentMonthStart = new DateTime(today.Year, today.Month, 1);
        
        // Separate expired items (before current month) from future items
        var expiredItems = itemsList.Where(i => i.ExpiryDate < currentMonthStart).ToList();
        var futureItems = itemsList.Where(i => i.ExpiryDate >= currentMonthStart).ToList();
        _logger?.LogDebug("BuildTimelineView: Expired={ExpiredCount}, Future={FutureCount}", 
            expiredItems.Count, futureItems.Count);
        
        // Update overdue count (for the badge on Archive tab)
        OverdueCount = Items.Count(i => !i.IsDeleted && i.ExpiryDate < currentMonthStart);
        
        // Group future items by month for lookup
        var itemsByMonth = futureItems
            .GroupBy(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1))
            .ToDictionary(g => g.Key, g => g.ToList());
        
        // Add current month + next 5 months (6 total) - NO archive in timeline anymore
        for (int i = 0; i < 6; i++)
        {
            var month = currentMonthStart.AddMonths(i);
            var monthItems = itemsByMonth.TryGetValue(month, out var list) ? list : new List<ExpirationItem>();
            var sortedItems = SortItems(monthItems);
            
            var monthWithItems = new MonthWithItems
            {
                Month = month,
                SortBy = TimelineSortBy,
                Items = new ObservableCollection<ExpirationItem>(sortedItems)
            };
            TimelineMonths.Add(monthWithItems);
        }
    }
    
    private IEnumerable<ExpirationItem> SortItems(IEnumerable<ExpirationItem> items)
    {
        return TimelineSortBy switch
        {
            "Description" => items.OrderBy(i => i.Description),
            "Location" => items.OrderBy(i => i.Location ?? "").ThenBy(i => i.ExpiryDate),
            "Quantity" => items.OrderByDescending(i => i.Quantity).ThenBy(i => i.ExpiryDate),
            _ => items.OrderBy(i => i.ExpiryDate) // Default: Date
        };
    }

    /// <summary>
    /// Rebuilds the visible month pills centered around the current month
    /// Shows 6 months before and 6 months after current selection
    /// </summary>
    private void RebuildVisibleMonths()
    {
        var groups = _navigationService.BuildMonthGroups(Items, CurrentMonth, _dateDisplayFormat);
        AvailableMonths.Clear();
        foreach (var g in groups)
            AvailableMonths.Add(g);
        
        // Also rebuild timeline
        BuildTimelineView();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        _navigationService.NavigatePrevious();
        CurrentMonth = _navigationService.CurrentMonth;
    }

    [RelayCommand]
    private void NextMonth()
    {
        _navigationService.NavigateNext();
        CurrentMonth = _navigationService.CurrentMonth;
    }

    /// <summary>
    /// Navigates to the current month (today).
    /// </summary>
    [RelayCommand]
    private void Today()
    {
        var now = DateTime.Now;
        _navigationService.NavigateToMonth(now.Year, now.Month);
        CurrentMonth = _navigationService.CurrentMonth;
    }

    [RelayCommand]
    private void GoToMonth(MonthGroup? monthGroup)
    {
        if (monthGroup != null)
        {
            _navigationService.NavigateToMonth(monthGroup.Month.Year, monthGroup.Month.Month);
            CurrentMonth = _navigationService.CurrentMonth;
        }
    }

    /// <summary>
    /// Collapses all months and store groups in the timeline view.
    /// </summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var month in TimelineMonths)
        {
            month.IsExpanded = false;
        }
    }

    /// <summary>
    /// Expands all months and store groups in the timeline view.
    /// </summary>
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var month in TimelineMonths)
        {
            month.IsExpanded = true;
            // Also expand all store groups within each month
            foreach (var store in month.ItemsByStore)
            {
                store.IsExpanded = true;
            }
        }
    }

    /// <summary>
    /// Navigates to the Timeline page.
    /// </summary>
    [RelayCommand]
    private void GoToTimeline()
    {
        CurrentPage = "Timeline";
    }

    /// <summary>
    /// Navigates to the Archive page.
    /// </summary>
    [RelayCommand]
    private void GoToArchive()
    {
        CurrentPage = "Archive";
        BuildArchiveView();
    }

    /// <summary>
    /// Builds the archive view with expired items grouped by month.
    /// </summary>
    private void BuildArchiveView()
    {
        ArchiveMonths.Clear();
        
        var today = DateTime.Today;
        var currentMonthStart = new DateTime(today.Year, today.Month, 1);
        
        // Get all expired items (before current month)
        var expiredItems = Items
            .Where(i => !i.IsDeleted && i.ExpiryDate < currentMonthStart)
            .AsEnumerable();
        
        // Apply store filter if selected
        if (SelectedStore != null && !string.IsNullOrEmpty(SelectedStore.Code))
        {
            expiredItems = expiredItems.Where(i => 
                string.Equals(i.Location, SelectedStore.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Location, SelectedStore.Name, StringComparison.OrdinalIgnoreCase));
        }
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(ArchiveSearchText))
        {
            expiredItems = _searchService.Search(expiredItems, ArchiveSearchText);
        }
        
        var expiredList = expiredItems.ToList();
        ArchiveItems = new ObservableCollection<ExpirationItem>(expiredList);
        ArchiveItemCount = expiredList.Count;
        ArchiveTotalUnits = expiredList.Sum(i => i.Units);
        
        // Group by month (newest first)
        var groupedByMonth = expiredList
            .GroupBy(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1))
            .OrderByDescending(g => g.Key);
        
        foreach (var group in groupedByMonth)
        {
            var sortedItems = SortArchiveItems(group);
            var monthWithItems = new MonthWithItems
            {
                Month = group.Key,
                IsArchive = true,
                SortBy = ArchiveSortBy,
                Items = new ObservableCollection<ExpirationItem>(sortedItems)
            };
            ArchiveMonths.Add(monthWithItems);
        }
    }
    
    private IEnumerable<ExpirationItem> SortArchiveItems(IEnumerable<ExpirationItem> items)
    {
        return ArchiveSortBy switch
        {
            "Description" => items.OrderBy(i => i.Description),
            "Location" => items.OrderBy(i => i.Location ?? "").ThenBy(i => i.ExpiryDate),
            "Quantity" => items.OrderByDescending(i => i.Quantity).ThenBy(i => i.ExpiryDate),
            _ => items.OrderByDescending(i => i.ExpiryDate) // Default: Date (newest first for archive)
        };
    }

    partial void OnArchiveSearchTextChanged(string value)
    {
        if (CurrentPage == "Archive")
        {
            BuildArchiveView();
        }
    }

    partial void OnArchiveSortByChanged(string value)
    {
        if (CurrentPage == "Archive")
        {
            BuildArchiveView();
        }
    }

    /// <summary>
    /// Exports archive items to CSV.
    /// </summary>
    [RelayCommand]
    private async Task ExportArchiveToCsvAsync()
    {
        if (ArchiveItems.Count == 0)
        {
            StatusMessage = "No archive items to export";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = $"ExpireWise_Archive_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Exporting archive...";
                
                var lines = new List<string>
                {
                    "Description,Location,Quantity,Units,Expiry Date,Status"
                };
                
                foreach (var item in ArchiveItems.OrderByDescending(i => i.ExpiryDate))
                {
                    var desc = item.Description?.Replace(",", ";") ?? "";
                    var loc = item.Location?.Replace(",", ";") ?? "";
                    lines.Add($"\"{desc}\",\"{loc}\",{item.Quantity},{item.Units},{item.ExpiryDate:yyyy-MM-dd},{item.Status}");
                }
                
                await File.WriteAllLinesAsync(dialog.FileName, lines);
                StatusMessage = $"Exported {ArchiveItems.Count} archive items to CSV";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export archive to CSV");
                StatusMessage = "Export failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Exports archive items to Excel.
    /// </summary>
    [RelayCommand]
    private async Task ExportArchiveToExcelAsync()
    {
        if (ArchiveItems.Count == 0)
        {
            StatusMessage = "No archive items to export";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            DefaultExt = ".xlsx",
            FileName = $"ExpireWise_Archive_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Exporting archive to Excel...";
                
                await _importExportService.ExportToExcelAsync(
                    dialog.FileName, 
                    ArchiveItems.OrderByDescending(i => i.ExpiryDate).ToList());
                    
                StatusMessage = $"Exported {ArchiveItems.Count} archive items to Excel";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export archive to Excel");
                StatusMessage = "Export failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Deletes all items in the archive (with confirmation).
    /// </summary>
    [RelayCommand]
    private async Task ClearArchiveAsync()
    {
        if (ArchiveItems.Count == 0)
        {
            StatusMessage = "Archive is empty";
            return;
        }
        
        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to permanently delete {ArchiveItems.Count} archived items?\n\nThis action cannot be undone.",
            "Clear Archive",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
            
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Clearing archive...";
                
                var itemsToDelete = ArchiveItems.ToList();
                foreach (var item in itemsToDelete)
                {
                    Items.Remove(item);
                    await _repository.DeleteAsync(item.Id);
                }
                
                StatusMessage = $"Deleted {itemsToDelete.Count} archived items";
                BuildArchiveView();
                BuildTimelineView(); // Refresh timeline too
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to clear archive");
                StatusMessage = "Failed to clear archive: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Command to select a store for filtering.
    /// </summary>
    [RelayCommand]
    private void SelectStore(object? storeParam)
    {
        // Handle both StoreEntity and StoreTabModel parameters
        if (storeParam is StoreTabModel tabModel)
        {
            SelectedStore = AvailableStores.FirstOrDefault(s => s.Code == tabModel.Code);
        }
        else if (storeParam is Data.Entities.StoreEntity entity)
        {
            SelectedStore = entity;
        }
        else
        {
            SelectedStore = null; // All stores
        }
    }

    /// <summary>
    /// Navigate to the previous store in the carousel.
    /// Cycles from first store to "All Stores" (null), then to last store.
    /// </summary>
    [RelayCommand]
    private void PreviousStore()
    {
        // Use StoreTabModels for navigation (excludes "No Store" option)
        if (StoreTabModels.Count == 0) return;
        
        if (SelectedStore == null)
        {
            // Currently on "All Stores" - go to last store
            var lastTab = StoreTabModels.LastOrDefault();
            SelectedStore = lastTab != null ? AvailableStores.FirstOrDefault(s => s.Code == lastTab.Code) : null;
        }
        else
        {
            var currentTab = StoreTabModels.FirstOrDefault(t => t.Code == SelectedStore.Code);
            var currentIndex = currentTab != null ? StoreTabModels.IndexOf(currentTab) : -1;
            
            if (currentIndex <= 0)
            {
                // At first store - go to "All Stores"
                SelectedStore = null;
            }
            else
            {
                var prevTab = StoreTabModels[currentIndex - 1];
                SelectedStore = AvailableStores.FirstOrDefault(s => s.Code == prevTab.Code);
            }
        }
    }

    /// <summary>
    /// Navigate to the next store in the carousel.
    /// Cycles from last store to "All Stores" (null), then to first store.
    /// </summary>
    [RelayCommand]
    private void NextStore()
    {
        // Use StoreTabModels for navigation (excludes "No Store" option)
        if (StoreTabModels.Count == 0) return;
        
        if (SelectedStore == null)
        {
            // Currently on "All Stores" - go to first store
            var firstTab = StoreTabModels.FirstOrDefault();
            SelectedStore = firstTab != null ? AvailableStores.FirstOrDefault(s => s.Code == firstTab.Code) : null;
        }
        else
        {
            var currentTab = StoreTabModels.FirstOrDefault(t => t.Code == SelectedStore.Code);
            var currentIndex = currentTab != null ? StoreTabModels.IndexOf(currentTab) : -1;
            
            if (currentIndex < 0 || currentIndex >= StoreTabModels.Count - 1)
            {
                // At last store or not found - go to "All Stores"
                SelectedStore = null;
            }
            else
            {
                var nextTab = StoreTabModels[currentIndex + 1];
                SelectedStore = AvailableStores.FirstOrDefault(s => s.Code == nextTab.Code);
            }
        }
    }

    /// <summary>
    /// Command to navigate directly to a specific month.
    /// </summary>
    [RelayCommand]
    private void GoToMonthDirect(DateTime month)
    {
        _navigationService.NavigateToMonth(month.Year, month.Month);
        CurrentMonth = _navigationService.CurrentMonth;
    }

    partial void OnCurrentMonthChanged(DateTime value)
    {
           // Keep navigation service in sync and refresh UI
           _navigationService.NavigateToMonth(value.Year, value.Month);
           UpdateMonthDisplay();
           RebuildVisibleMonths();
           ApplyFilters();
    }

    private void UpdateMonthDisplay()
    {
        CurrentMonthDisplay = FormatExpirationDate(CurrentMonth);
    }

    // Navigation is always available - endless carousel
    public bool CanGoToPreviousMonth => true;
    public bool CanGoToNextMonth => true;

    /// <summary>
    /// Shows a brief success toast notification (non-blocking)
    /// </summary>
    private void ShowSuccessToast(string message)
    {
        if (_settings?.ShowToastNotifications == false)
            return;

        StatusMessage = $"✓ {message}";

        // Auto-clear status message after 3 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, e) =>
        {
            if (StatusMessage == $"✓ {message}")
                StatusMessage = string.Empty;
            timer.Stop();
        };
        timer.Start();
    }

    [RelayCommand]
    private async Task EditItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to edit";
            _dialogService.ShowWarning("Please select an item to edit.", "No Item Selected");
            return;
        }

        var originalItemNumber = SelectedItem.ItemNumber;
        _logger?.LogInformation("Edit item clicked for {ItemNumber}", originalItemNumber);

        try
        {
            // Open Quick Add panel with selected item's values pre-filled
            QuickAddExpanded = true;

            // Set the SKU to the selected item's item number
            QuickAddSku = SelectedItem.ItemNumber;

            // Set store and date
            QuickAddStore = AvailableStores.FirstOrDefault(s => s.Code == SelectedItem.Location);
            QuickAddMonth = SelectedItem.ExpiryDate.Month;
            QuickAddYear = SelectedItem.ExpiryDate.Year;

            StatusMessage = $"Edit '{originalItemNumber}' - modify values and add to queue, then delete the original";
            _logger?.LogInformation("Pre-filled Quick Add for editing {ItemNumber}", originalItemNumber);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error editing item: {ex.Message}";
            _logger?.LogError(ex, "Exception while editing item {ItemNumber}", originalItemNumber);
        }
    }

    [RelayCommand]
    private async Task DeleteItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to delete";
            return;
        }

        // Confirmation dialog
        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete item {SelectedItem.ItemNumber}?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            var itemNumber = SelectedItem.ItemNumber;
            var success = await _repository.DeleteAsync(SelectedItem.Id);

            if (success)
            {
                Items.Remove(SelectedItem);
                TotalItems = Items.Count;
                UpdateStoreItemCounts();
                BuildAvailableMonths();
                ApplyFilters();
                StatusMessage = $"Deleted item {itemNumber}";
                UpdateLastSaved();
                _logger?.LogInformation("Deleted item {ItemNumber}", itemNumber);
                SelectedItem = null;
            }
            else
            {
                StatusMessage = $"Error deleting item: Item not found";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting item");
        }
    }

    /// <summary>
    /// Deletes a specific item (used by Kanban card quick actions).
    /// </summary>
    [RelayCommand]
    private async Task DeleteItemByParameter(ExpirationItem? item)
    {
        if (item == null) return;

        try
        {
            var success = await _repository.DeleteAsync(item.Id);
            if (success)
            {
                Items.Remove(item);
                TotalItems = Items.Count;
                UpdateStoreItemCounts();
                BuildAvailableMonths();
                ApplyFilters();
                StatusMessage = $"Removed {item.Description}";
                UpdateLastSaved();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting item {ItemNumber}", item.ItemNumber);
        }
    }

    /// <summary>
    /// Marks an item as handled (removes it from tracking).
    /// </summary>
    [RelayCommand]
    private async Task MarkHandled(ExpirationItem? item)
    {
        if (item == null) return;

        try
        {
            // Soft delete (mark as handled)
            var success = await _repository.DeleteAsync(item.Id);
            if (success)
            {
                Items.Remove(item);
                TotalItems = Items.Count;
                UpdateStoreItemCounts();
                BuildAvailableMonths();
                ApplyFilters();
                StatusMessage = $"Marked {item.Description} as handled";
                UpdateLastSaved();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Exception while marking item handled {ItemNumber}", item.ItemNumber);
        }
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        try
        {
            var toDelete = SelectedItems?.ToList() ?? new List<ExpirationItem>();
            if (toDelete.Count == 0)
            {
                StatusMessage = "Please select one or more items to delete";
                return;
            }

            var confirm = await (_dialogService?.ShowConfirmationAsync("Confirm delete", $"Delete {toDelete.Count} item(s)? This cannot be undone.") ?? Task.FromResult(false));
            if (!confirm) return;

            var deleted = 0;
                deleted = await _itemService.DeleteRangeAsync(toDelete);
                foreach (var item in toDelete.Where(i => Items.Contains(i)))
                {
                    Items.Remove(item);
                }

            // Refresh views
            TotalItems = Items.Count;
            UpdateStoreItemCounts();
            BuildAvailableMonths();
            ApplyFilters();
            UpdateLastSaved();

            StatusMessage = deleted > 0 ? $"Deleted {deleted} item(s)" : "No items were deleted";
            SelectedItems?.Clear();
            SelectedItem = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting items: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting selected items");
        }
        _logger?.LogInformation("Import from Excel clicked");

        try
        {
            if (_dialogService == null)
            {
                StatusMessage = "Import failed: dialog service unavailable";
                return;
            }

            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select Excel file to import",
                "Excel Files (*.xlsx)|*.xlsx");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                StatusMessage = "Importing from Excel...";

                if (_dialogService == null)
                {
                    StatusMessage = "Import failed: dialog service unavailable";
                    IsLoading = false;
                    return;
                }

                var result = await _parser.ParseExcelAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    var items = result.Value.ToList();
                    var importResult = await _importExportService.ImportItemsAsync(items);

                    if (importResult.IsSuccess)
                    {
                        await LoadItems();
                        StatusMessage = $"Imported {importResult.Value} items";
                        UpdateLastSaved();
                        _logger?.LogInformation("Imported {Count} items from Excel (transactional)", importResult.Value);
                    }
                    else
                    {
                        StatusMessage = $"Import failed: {importResult.ErrorMessage}";
                        _logger?.LogWarning("Excel import failed: {Error}", importResult.ErrorMessage);
                    }
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                }

                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            _logger?.LogError(ex, "Exception during Excel import");
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportFromCsv()
    {
        _logger?.LogInformation("Import from CSV clicked");

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select CSV file to import",
                "CSV Files (*.csv)|*.csv");

                if (files != null && files.Length > 0)
                {
                    IsBusy = true;
                    BusyMessage = "Importing items from CSV...";
                    IsLoading = true;
                    StatusMessage = "Importing from CSV...";

                    if (_dialogService == null)
                    {
                        StatusMessage = "Import failed: dialog service unavailable";
                        IsLoading = false;
                        IsBusy = false;
                        return;
                    }

                    var result = await _parser.ParseCsvAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    var items = result.Value.ToList();
                    BusyMessage = $"Importing {items.Count} items...";
                    var importResult = await _importExportService.ImportItemsAsync(items);

                    if (importResult.IsSuccess)
                    {
                        BusyMessage = "Refreshing data...";
                        await LoadItems();
                        StatusMessage = $"Imported {importResult.Value} items";
                        UpdateLastSaved();
                        _logger?.LogInformation("Imported {Count} items from CSV (transactional)", importResult.Value);
                    }
                    else
                    {
                        StatusMessage = $"Import failed: {importResult.ErrorMessage}";
                        _logger?.LogWarning("CSV import failed: {Error}", importResult.ErrorMessage);
                    }
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                }

                IsLoading = false;
                IsBusy = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            _logger?.LogError(ex, "Exception during CSV import");
            IsLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        try
        {
            if (Items.Count == 0)
            {
                StatusMessage = "No items to export";
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"ExpireWise_Export_{timestamp}.xlsx";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to Excel",
                defaultFileName,
                "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                StatusMessage = "Export cancelled";
                return;
            }

            IsBusy = true;
            BusyMessage = $"Exporting {Items.Count} items to Excel...";
            IsLoading = true;
            StatusMessage = "Exporting to Excel...";

            var result = await _importExportService.ExportToExcelAsync(filePath, Items);

            if (result.IsSuccess)
            {
                var fileName = System.IO.Path.GetFileName(filePath);
                StatusMessage = $"Exported {Items.Count} item(s)";
                _dialogService.ShowExportSuccessDialog(fileName, filePath, Items.Count);
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _dialogService.ShowExportErrorDialog(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _dialogService.ShowExportErrorDialog(ex.Message);
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        try
        {
            if (Items.Count == 0)
            {
                StatusMessage = "No items to export";
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"ExpireWise_Export_{timestamp}.csv";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to CSV",
                defaultFileName,
                "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                StatusMessage = "Export cancelled";
                return;
            }

            IsBusy = true;
            BusyMessage = $"Exporting {Items.Count} items to CSV...";
            IsLoading = true;
            StatusMessage = "Exporting to CSV...";

            var result = await _importExportService.ExportToCsvAsync(filePath, Items);

            if (result.IsSuccess)
            {
                var fileName = System.IO.Path.GetFileName(filePath);
                StatusMessage = $"Exported {Items.Count} item(s)";
                _dialogService.ShowExportSuccessDialog(fileName, filePath, Items.Count);
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _dialogService.ShowExportErrorDialog(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _dialogService.ShowExportErrorDialog(ex.Message);
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    partial void OnGroupByStoreChanged(bool value)
    {
        GroupByStoreChanged?.Invoke(value);

        // Update ItemsView grouping
        if (_itemsView != null)
        {
            _itemsView.GroupDescriptions.Clear();
            if (value)
            {
                _itemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExpirationItem.Location)));
            }
        }
    }

    partial void OnSelectedStoreChanged(Data.Entities.StoreEntity? value)
    {
        SelectedStoreChanged?.Invoke(value);
        
        // Update IsSelected on StoreTabModels
        foreach (var storeTab in StoreTabModels)
        {
            storeTab.IsSelected = value != null && storeTab.Code == value.Code;
        }
        
        // Sync dropdown selection (avoid re-entrancy)
        var targetDropdownItem = value == null 
            ? StoreDropdownItems.FirstOrDefault(x => x.IsAllStores)
            : StoreDropdownItems.FirstOrDefault(x => x.Code == value.Code);
        if (_selectedStoreDropdownItem != targetDropdownItem)
        {
            _selectedStoreDropdownItem = targetDropdownItem;
            OnPropertyChanged(nameof(SelectedStoreDropdownItem));
        }
        
        // Re-apply filter when store changes
        ApplyFilters();
        
        // Rebuild timeline for selected store
        BuildTimelineView();
    }

    partial void OnSelectedStoreDropdownItemChanged(StoreDropdownItem? value)
    {
        // When dropdown changes, update the actual SelectedStore
        if (value == null || value.IsAllStores)
        {
            if (SelectedStore != null)
                SelectedStore = null;
        }
        else
        {
            var store = AvailableStores.FirstOrDefault(s => s.Code == value.Code);
            if (SelectedStore != store)
                SelectedStore = store;
        }
    }

    partial void OnTimelineSortByChanged(string value)
    {
        BuildTimelineView();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce search to avoid filtering on every keystroke
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new System.Threading.Timer(_ =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _itemsView?.Refresh();
                ApplyFilters(); // Updates stats
                BuildTimelineView(); // Rebuild timeline with search filter
            });
        }, null, 300, System.Threading.Timeout.Infinite);
    }

    partial void OnStatusFilterChanged(string value)
    {
        _itemsView?.Refresh();
        ApplyFilters(); // Updates stats
        BuildTimelineView(); // Rebuild timeline with status filter
    }

    /// <summary>
    /// Initializes the ICollectionView for efficient filtering and grouping.
    /// Uses CollectionViewSource for efficient filtering and virtualization.
    /// </summary>
    private void InitializeItemsView()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        if (ItemsView != null)
        {
            ItemsView.Filter = FilterPredicate;

            // Set up grouping by Location
            if (GroupByStore)
            {
                ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExpirationItem.Location)));
            }

            // Set up sorting
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(ExpirationItem.Location), ListSortDirection.Ascending));
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(ExpirationItem.ExpiryDate), ListSortDirection.Ascending));
        }
    }

    /// <summary>
    /// Filter predicate for ICollectionView - determines which items to show.
    /// This is much more efficient than rebuilding the collection on every change.
    /// </summary>
    private bool FilterPredicate(object obj)
    {
        if (obj is not ExpirationItem item) return false;

        // Exclude deleted items
        if (item.IsDeleted) return false;

        // Store filter - when a specific store is selected
        if (SelectedStore != null && !string.IsNullOrEmpty(SelectedStore.Code))
        {
            if (!string.Equals(item.Location, SelectedStore.Code, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        // Month filter - use navigation service range
        var start = CurrentMonth;
        var end = CurrentMonth.AddMonths(1).AddDays(-1);
        if (_navigationService != null)
        {
            var range = _navigationService.GetMonthRange();
            start = range.start;
            end = range.end;
        }

        if (item.ExpiryDate < start || item.ExpiryDate > end)
            return false;

        // Status filter
        if (StatusFilter != "All")
        {
            var statusMatches = StatusFilter switch
            {
                "Good" => item.Status == ExpirationStatus.Good,
                "Warning" => item.Status == ExpirationStatus.Warning,
                "Critical" => item.Status == ExpirationStatus.Critical,
                "Expired" => item.Status == ExpirationStatus.Expired,
                _ => true
            };

            if (!statusMatches) return false;
        }

        // Text search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            // Use search service for consistent matching
            return _searchService.Matches(item, SearchText);
        }

        return true;
    }

    partial void OnQuickAddIsValidChanged(bool value)
    {
        // Notify the AddToQueue command to re-check its CanExecute state
        // Ensure this runs on UI thread
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            AddToQueueCommand.NotifyCanExecuteChanged();
        });
    }

    private void ApplyFilters()
    {
        // Start from all active items
        var all = Items.Where(i => !i.IsDeleted).AsEnumerable();

        // Filter by selected store (primary filter for store-by-store navigation)
        if (SelectedStore != null)
        {
            all = all.Where(i => string.Equals(i.Location, SelectedStore.Code, StringComparison.OrdinalIgnoreCase));
        }

        // Filter to current month via navigation service range
        var start = CurrentMonth;
        var end = CurrentMonth.AddMonths(1).AddDays(-1);
        if (_navigationService != null)
        {
            var range = _navigationService.GetMonthRange();
            start = range.start;
            end = range.end;
        }
        var filtered = all.Where(i => i.ExpiryDate >= start && i.ExpiryDate <= end);

        // Apply status filter
        if (StatusFilter != "All")
        {
            var status = StatusFilter switch
            {
                "Good" => ExpirationStatus.Good,
                "Warning" => ExpirationStatus.Warning,
                "Critical" => ExpirationStatus.Critical,
                "Expired" => ExpirationStatus.Expired,
                _ => (ExpirationStatus?)null
            };

            filtered = _searchService.FilterByExpirationStatus(filtered, status, ExpirationItem.WarningDaysThreshold);
        }

        // Apply text search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = _searchService.Search(filtered, SearchText);
        }

        // Update month stats
        UpdateMonthStats();

        // Update analytics
        UpdateAnalytics();

        // Notify navigation buttons
        OnPropertyChanged(nameof(CanGoToPreviousMonth));
        OnPropertyChanged(nameof(CanGoToNextMonth));
    }

    private void UpdateLastSaved()
    {
        LastSavedDisplay = $"Saved at {DateTime.Now:h:mm tt}";
    }

    private void UpdateMonthStats()
    {
        var monthItems = _navigationService.GetItemsForCurrentMonth(Items).ToList();

        MonthItemCount = monthItems.Count;
        MonthTotalUnits = monthItems.Sum(i => i.Units);
        CriticalCount = monthItems.Count(i => i.Status == ExpirationStatus.Critical);
        ExpiredCount = monthItems.Count(i => i.Status == ExpirationStatus.Expired);

        // Update global statistics
        UpdateGlobalStatistics();
    }

    /// <summary>
    /// Updates global statistics across all items (not just current month).
    /// </summary>
    private void UpdateGlobalStatistics()
    {
        var today = DateTime.Today;
        var warningDate = today.AddDays(ExpirationItem.WarningDaysThreshold);

        TotalItems = Items.Count;
        ExpiredCount = Items.Count(i => i.ExpiryDate < today);
        ExpiringSoonCount = Items.Count(i => i.ExpiryDate >= today && i.ExpiryDate <= warningDate);
        
        // Update overdue count for summary display
        UpdateOverdueCount();
    }

    /// <summary>
    /// Updates the count of overdue/expired items for summary display.
    /// </summary>
    private void UpdateOverdueCount()
    {
        var today = DateTime.Today;

        // Start with all active items
        var items = Items.Where(i => !i.IsDeleted).AsEnumerable();

        // Filter by store if selected
        if (SelectedStore != null && !string.IsNullOrEmpty(SelectedStore.Code))
        {
            items = items.Where(i => 
                string.Equals(i.Location, SelectedStore.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Location, SelectedStore.Name, StringComparison.OrdinalIgnoreCase));
        }

        // Apply search filter if any
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            items = _searchService.Search(items, SearchText);
        }

        // Count items that have expired (before today)
        OverdueCount = items.Count(i => i.ExpiryDate < today);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel, "expirewise");
            // Only set owner if MainWindow is visible (don't block widget)
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow)
            {
                settingsWindow.Owner = mainWindow;
            }
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open settings window");
            StatusMessage = "Failed to open settings";
        }
    }

    #endregion

    #region Quick Add Logic

    /// <summary>
    /// Load available stores from the dictionary database
    /// </summary>
    private void LoadAvailableStores()
    {
        try
        {
            var db = Data.DictionaryDbContext.Instance;
            var stores = db.GetAllStores();

            AvailableStores.Clear();
            StoreTabModels.Clear();
            StoreDropdownItems.Clear();

            // Add a default "(No Store)" option for Quick Add dropdown
            AvailableStores.Add(new Data.Entities.StoreEntity { Code = "", Name = "(No Store)" });

            // Add "All Stores" option to dropdown
            StoreDropdownItems.Add(StoreDropdownItem.CreateAllStores());

            // Order by store number (Code) instead of alphabetically by Name
            foreach (var store in stores.OrderBy(s => s.Code))
            {
                AvailableStores.Add(store);
                var tabModel = StoreTabModel.FromEntity(store);
                StoreTabModels.Add(tabModel);
                StoreDropdownItems.Add(StoreDropdownItem.FromStoreTab(tabModel));
            }

            // Set default selection to "All Stores"
            SelectedStoreDropdownItem = StoreDropdownItems.FirstOrDefault();

            // Update item counts for each store
            UpdateStoreItemCounts();

            _logger?.LogInformation("Loaded {Count} stores for store tabs", stores.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load stores");
        }
    }

    /// <summary>
    /// Updates the item counts for each store tab based on current Items collection.
    /// </summary>
    private void UpdateStoreItemCounts()
    {
        if (Items == null || Items.Count == 0)
        {
            foreach (var store in StoreTabModels)
            {
                store.ItemCount = 0;
            }
            foreach (var item in StoreDropdownItems)
            {
                item.ItemCount = 0;
            }
            return;
        }

        // Get all unique locations from items
        var activeItems = Items.Where(i => !i.IsDeleted).ToList();
        var totalCount = activeItems.Count;

        foreach (var store in StoreTabModels)
        {
            // Match by either Code or Name (imported data may use either)
            store.ItemCount = activeItems.Count(i => 
                string.Equals(i.Location, store.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Location, store.Name, StringComparison.OrdinalIgnoreCase));
        }

        // Sync dropdown items
        foreach (var dropdownItem in StoreDropdownItems)
        {
            if (dropdownItem.IsAllStores)
            {
                dropdownItem.ItemCount = totalCount;
            }
            else
            {
                var matchingTab = StoreTabModels.FirstOrDefault(t => t.Code == dropdownItem.Code);
                dropdownItem.ItemCount = matchingTab?.ItemCount ?? 0;
            }
        }
    }

    /// <summary>
    /// Handle QuickAddSku property changes with debouncing
    /// </summary>
    private void OnQuickAddSkuChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickAddSku))
        {
            // Cancel previous timer
            _skuLookupDebounceTimer?.Dispose();

            if (string.IsNullOrWhiteSpace(QuickAddSku))
            {
                // Clear validation state if SKU is empty
                QuickAddIsValidating = false;
                _quickAddLookedUpItem = null;
                _quickAddParsedItems.Clear();
                QuickAddItemPreview = string.Empty;
                QuickAddHasErrors = false;
                QuickAddIsValid = false; // Last - triggers command notification
                return;
            }

            // Start validation indicator
            QuickAddIsValidating = true;
            QuickAddItemPreview = string.Empty;
            QuickAddHasErrors = false;
            QuickAddIsValid = false; // Last - triggers command notification

            // Set up debounce timer (300ms)
            _skuLookupDebounceTimer = new System.Threading.Timer(
                _ => DebouncedLookupSku(),
                null,
                300,
                System.Threading.Timeout.Infinite
            );
        }
    }

    /// <summary>
    /// Parse input lines, handling tab-separated SKU+Qty from Excel.
    /// Returns list of (SKU, Quantity) tuples.
    /// </summary>
    private List<(string Sku, int Qty)> ParseSkuInputLines()
    {
        var results = new List<(string Sku, int Qty)>();

        if (string.IsNullOrWhiteSpace(QuickAddSku))
            return results;

        var lines = QuickAddSku.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Check for tab-separated (Excel paste) or multiple spaces
            var parts = trimmed.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                // Last part might be quantity - check if it's a number
                var lastPart = parts[^1].Trim();
                if (int.TryParse(lastPart, out var qty) && qty > 0)
                {
                    // Everything except last part is the SKU
                    var skuPart = string.Join(" ", parts[..^1]).Trim();
                    results.Add((skuPart, qty));
                }
                else
                {
                    // No quantity found, whole thing is SKU, use default
                    results.Add((parts[0].Trim(), 1));
                }
            }
            else
            {
                // Just SKU, use default quantity of 1
                results.Add((trimmed, 1));
            }
        }

        return results;
    }

    /// <summary>
    /// Perform the actual SKU lookup after debounce.
    /// Supports multi-line input with tab-separated quantities.
    /// </summary>
    private void DebouncedLookupSku()
    {
        if (string.IsNullOrWhiteSpace(QuickAddSku))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;
                _quickAddLookedUpItem = null;
                _quickAddParsedItems.Clear();
                QuickAddItemPreview = string.Empty;
                QuickAddHasErrors = false;
                QuickAddIsValid = false;
            });
            return;
        }

        try
        {
            var parsedLines = ParseSkuInputLines();
            var parsedItems = new List<(string Sku, int Qty, Data.Entities.DictionaryItemEntity? Item)>();
            var foundCount = 0;
            var notFoundCount = 0;

            foreach (var (sku, qty) in parsedLines)
            {
                var result = _itemLookupService.Search(sku);
                if (result.Found && result.Item != null)
                {
                    parsedItems.Add((sku, qty, result.Item));
                    foundCount++;
                }
                else
                {
                    parsedItems.Add((sku, qty, null));
                    notFoundCount++;
                }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;
                _quickAddParsedItems = parsedItems;

                // For backwards compatibility with single-item entry
                _quickAddLookedUpItem = parsedItems.Count == 1 && parsedItems[0].Item != null 
                    ? parsedItems[0].Item 
                    : null;

                // Build preview text
                if (parsedItems.Count == 0)
                {
                    QuickAddItemPreview = string.Empty;
                    QuickAddHasErrors = false;
                    QuickAddIsValid = false;
                }
                else if (notFoundCount == 0)
                {
                    // All items found
                    if (parsedItems.Count == 1)
                    {
                        var item = parsedItems[0].Item!;
                        QuickAddItemPreview = $"✓ {item.Description} ({item.Number})";
                    }
                    else
                    {
                        QuickAddItemPreview = $"✓ All {foundCount} items found in Business Central";
                    }
                    QuickAddHasErrors = false;
                    QuickAddIsValid = true;
                }
                else if (foundCount == 0)
                {
                    // No items found
                    QuickAddItemPreview = parsedItems.Count == 1
                        ? $"❌ SKU not found: {parsedItems[0].Sku}"
                        : $"❌ {notFoundCount} item(s) not found in Business Central";
                    QuickAddHasErrors = true;
                    QuickAddIsValid = false;
                }
                else
                {
                    // Mixed results
                    var notFoundSkus = parsedItems.Where(p => p.Item == null).Select(p => p.Sku);
                    QuickAddItemPreview = $"✓ {foundCount} found, ❌ {notFoundCount} not found: {string.Join(", ", notFoundSkus.Take(3))}";
                    QuickAddHasErrors = true;
                    QuickAddIsValid = false;
                }

                _logger?.LogDebug("Quick Add parsed {Total} SKUs: {Found} found, {NotFound} not found", 
                    parsedItems.Count, foundCount, notFoundCount);
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Quick Add SKU lookup error");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;
                _quickAddLookedUpItem = null;
                _quickAddParsedItems.Clear();
                QuickAddItemPreview = string.Empty;
                QuickAddHasErrors = false;
                QuickAddIsValid = false;
            });
        }
    }

    /// <summary>
    /// Toggle Quick Add panel (replaces old Ctrl+N modal dialog)
    /// </summary>
    [RelayCommand]
    private void ToggleQuickAdd()
    {
        QuickAddExpanded = !QuickAddExpanded;
        
        if (QuickAddExpanded)
        {
            // Auto-select current store from main view if one is selected
            if (SelectedStore != null)
            {
                var matchingStore = AvailableStores.FirstOrDefault(s => s.Code == SelectedStore.Code);
                if (matchingStore != null)
                {
                    QuickAddStore = matchingStore;
                }
            }
        }
        
        StatusMessage = QuickAddExpanded ? "Quick Add panel opened (Ctrl+N)" : "Quick Add panel closed";
    }

    /// <summary>
    /// Check if item can be added to queue.
    /// Supports both single item and multi-item input.
    /// </summary>
    private bool CanAddToQueue() => QuickAddIsValid && (_quickAddLookedUpItem != null || _quickAddParsedItems.Any(p => p.Item != null));

    /// <summary>
    /// Add item(s) to queue (not saved to database yet).
    /// Supports multi-line input with tab-separated quantities.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddToQueue))]
    private void AddToQueue()
    {
        try
        {
            var expiryDate = new DateTime(QuickAddYear, QuickAddMonth, DateTime.DaysInMonth(QuickAddYear, QuickAddMonth));
            var location = QuickAddStore?.Code ?? string.Empty;
            var addedCount = 0;

            // Check if we have multiple parsed items
            if (_quickAddParsedItems.Count > 0)
            {
                foreach (var (sku, qty, item) in _quickAddParsedItems)
                {
                    if (item == null) continue; // Skip not-found items

                    var queuedItem = new ExpirationItem
                    {
                        ItemNumber = item.Number,
                        Description = item.Description,
                        Location = location,
                        Quantity = qty,
                        ExpiryDate = expiryDate
                    };

                    QuickAddQueue.Add(queuedItem);
                    addedCount++;
                }
            }
            else if (_quickAddLookedUpItem != null)
            {
                // Legacy single-item support
                var queuedItem = new ExpirationItem
                {
                    ItemNumber = _quickAddLookedUpItem.Number,
                    Description = _quickAddLookedUpItem.Description,
                    Location = location,
                    Quantity = 1,
                    ExpiryDate = expiryDate
                };

                QuickAddQueue.Add(queuedItem);
                addedCount = 1;
            }

            HasQueuedItems = QuickAddQueue.Count > 0;

            // Show feedback
            if (addedCount == 1)
            {
                StatusMessage = $"Added to queue: {QuickAddQueue.Last().ItemNumber} ({QuickAddQueue.Count} items total)";
            }
            else
            {
                StatusMessage = $"Added {addedCount} items to queue ({QuickAddQueue.Count} items total)";
            }
            _logger?.LogInformation("Added {Count} items to queue", addedCount);

            // Clear input for next entry
            QuickAddSku = string.Empty;
            _quickAddLookedUpItem = null;
            _quickAddParsedItems.Clear();
            QuickAddItemPreview = string.Empty;
            QuickAddHasErrors = false;
            QuickAddIsValid = false;
            QuickAddIsValidating = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Add to queue failed");
            StatusMessage = $"Failed to add to queue: {ex.Message}";
        }
    }

    /// <summary>
    /// Remove an item from the queue
    /// </summary>
    [RelayCommand]
    private void RemoveFromQueue(ExpirationItem? item)
    {
        if (item == null) return;

        QuickAddQueue.Remove(item);
        HasQueuedItems = QuickAddQueue.Count > 0;
        StatusMessage = $"Removed from queue: {item.ItemNumber}";
    }

    /// <summary>
    /// Increase quantity of a queued item
    /// </summary>
    [RelayCommand]
    private void IncreaseQueueItemQuantity(ExpirationItem? item)
    {
        if (item == null) return;
        item.Quantity++;
    }

    /// <summary>
    /// Decrease quantity of a queued item (minimum 1)
    /// </summary>
    [RelayCommand]
    private void DecreaseQueueItemQuantity(ExpirationItem? item)
    {
        if (item == null || item.Quantity <= 1) return;
        item.Quantity--;
    }

    /// <summary>
    /// Clear the entire queue
    /// </summary>
    [RelayCommand]
    private void ClearQueue()
    {
        var count = QuickAddQueue.Count;
        QuickAddQueue.Clear();
        HasQueuedItems = false;
        StatusMessage = $"Cleared {count} items from queue";
    }

    /// <summary>
    /// Confirm and save all queued items to database
    /// </summary>
    [RelayCommand]
    private async Task ConfirmQueue()
    {
        if (QuickAddQueue.Count == 0) return;

        try
        {
            IsBusy = true;
            BusyMessage = $"Saving {QuickAddQueue.Count} items...";

            int successCount = 0;
            int failCount = 0;

            foreach (var queuedItem in QuickAddQueue.ToList())
            {
                try
                {
                    // Save to repository
                    var savedItem = await _itemService.QuickAddAsync(
                        queuedItem.ItemNumber,
                        queuedItem.ExpiryDate.Month,
                        queuedItem.ExpiryDate.Year,
                        queuedItem.Quantity,
                        queuedItem.Description
                    );

                    // Set the location
                    savedItem.Location = queuedItem.Location;
                    await _repository.UpdateAsync(savedItem);

                    // Add to local collection
                    Items.Add(savedItem);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to save queued item: {Item}", queuedItem.ItemNumber);
                    failCount++;
                }
            }

            // Clear queue
            QuickAddQueue.Clear();
            HasQueuedItems = false;

            // Save settings (sticky preferences)
            await SaveQuickAddSettings();

            // Refresh UI
            await LoadItems();
            UpdateAnalytics();

            // Show success message
            if (failCount == 0)
            {
                StatusMessage = $"✓ Successfully added {successCount} items";
            }
            else
            {
                StatusMessage = $"Added {successCount} items, {failCount} failed";
            }

            _logger?.LogInformation("Queue confirmed: {Success} success, {Fail} failed", successCount, failCount);

            // Collapse panel if all successful
            if (failCount == 0)
            {
                QuickAddExpanded = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Confirm queue failed");
            StatusMessage = $"Failed to save items: {ex.Message}";
            _ = _dialogService.ShowErrorAsync("Confirm Queue Error", $"Failed to save items: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Clear the Quick Add form
    /// </summary>
    private void ClearQuickAddForm()
    {
        QuickAddSku = string.Empty;
        _quickAddLookedUpItem = null;
        QuickAddItemPreview = string.Empty;
        QuickAddIsValid = false;
        QuickAddIsValidating = false;

        // Don't reset store/date if using sticky settings
        if (_settings?.RememberLastLocation != true)
        {
            QuickAddStore = AvailableStores.FirstOrDefault();
        }

        if (_settings?.RememberLastExpiryDate != true)
        {
            var nextMonth = DateTime.Today.AddMonths(1);
            QuickAddMonth = nextMonth.Month;
            QuickAddYear = nextMonth.Year;
        }
    }

    /// <summary>
    /// Save Quick Add settings (sticky preferences)
    /// </summary>
    private async Task SaveQuickAddSettings()
    {
        if (_settingsService == null || _settings == null) return;

        try
        {
            if (_settings.RememberLastLocation)
            {
                _settings.LastSelectedStore = QuickAddStore?.Code;
            }

            if (_settings.RememberLastExpiryDate)
            {
                _settings.LastExpiryMonth = QuickAddMonth;
                _settings.LastExpiryYear = QuickAddYear;
            }

            _settings.QuickAddExpanded = QuickAddExpanded;

            await _settingsService.SaveSettingsAsync("ExpireWise", _settings);
            _logger?.LogDebug("Quick Add settings saved");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save Quick Add settings");
        }
    }

    /// <summary>
    /// Load settings and apply to Quick Add form
    /// </summary>
    private async Task LoadQuickAddSettings()
    {
        if (_settingsService == null) return;

        try
        {
            _settings = await _settingsService.LoadSettingsAsync<SOUP.Features.ExpireWise.Models.ExpireWiseSettings>("ExpireWise");
            if (_settings == null)
            {
                _settings = SOUP.Features.ExpireWise.Models.ExpireWiseSettings.CreateDefault();
            }

            // Apply settings to Quick Add form
            QuickAddExpanded = _settings.QuickAddExpanded;

            if (_settings.RememberLastLocation && !string.IsNullOrEmpty(_settings.LastSelectedStore))
            {
                QuickAddStore = AvailableStores.FirstOrDefault(s => s.Code == _settings.LastSelectedStore)
                    ?? AvailableStores.FirstOrDefault();
            }
            else
            {
                QuickAddStore = AvailableStores.FirstOrDefault();
            }

            if (_settings.RememberLastExpiryDate && _settings.LastExpiryMonth.HasValue && _settings.LastExpiryYear.HasValue)
            {
                QuickAddMonth = _settings.LastExpiryMonth.Value;
                QuickAddYear = _settings.LastExpiryYear.Value;
            }

            QuickAddUnits = _settings.DefaultUnits;

            _logger?.LogInformation("Quick Add settings loaded");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load Quick Add settings");
            _settings = SOUP.Features.ExpireWise.Models.ExpireWiseSettings.CreateDefault();
        }
    }

    #endregion

    #region Data Persistence

    /// <summary>
    /// JSON session persistence has been removed. The SQLite repository is the source
    /// of truth and handles persistence; this method is a no-op to keep the shutdown
    /// lifecycle stable where callers expect an async save method.
    /// </summary>
    public Task SaveDataOnShutdownAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    /// <summary>
    /// Releases resources used by the ViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Unsubscribe from settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }

            // Unsubscribe from dictionary data changes
            DictionaryManagementViewModel.DictionaryDataChanged -= OnDictionaryDataChanged;

            // Dispose managed resources
            (_repository as IDisposable)?.Dispose();
            if (_notificationTimer != null)
            {
                if (_notificationTimerHandler != null)
                    _notificationTimer.Tick -= _notificationTimerHandler;
                _notificationTimer.Stop();
                _notificationTimer = null;
                _notificationTimerHandler = null;
            }
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = null;
        }
        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Represents a month with aggregated item data
/// </summary>
public class MonthGroup
{
    public DateTime Month { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public int TotalUnits { get; set; }
    public int CriticalCount { get; set; }
    public int ExpiredCount { get; set; }
    public bool IsCurrentMonth { get; set; }

    public bool HasCriticalOrExpired => CriticalCount > 0 || ExpiredCount > 0;
}

/// <summary>
/// Represents a month with its items for the timeline view
/// </summary>
public partial class MonthWithItems : ObservableObject
{
    public DateTime Month { get; set; }
    public bool IsArchive { get; set; }
    
    /// <summary>
    /// The current sort option (Date, Description, Location, Quantity).
    /// Used to sort items within store groups.
    /// </summary>
    public string SortBy { get; set; } = "Date";
    
    /// <summary>
    /// Whether this month section is expanded (showing items).
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;
    
    public string DisplayName => IsArchive ? "📦 Archive" : Month.ToString("MMMM yyyy");
    public string ShortName => IsArchive ? "Archive" : Month.ToString("MMM yyyy");
    public ObservableCollection<ExpirationItem> Items { get; set; } = new();
    public int ItemCount => Items.Count;
    public int TotalUnits => Items.Sum(i => i.Units);
    public int CriticalCount => Items.Count(i => i.Status == ExpirationStatus.Critical);
    public int ExpiredCount => Items.Count(i => i.Status == ExpirationStatus.Expired);
    public bool HasCriticalOrExpired => CriticalCount > 0 || ExpiredCount > 0;
    
    /// <summary>
    /// Returns true if this is the current calendar month.
    /// </summary>
    public bool IsCurrentMonth => !IsArchive && Month.Year == DateTime.Today.Year && Month.Month == DateTime.Today.Month;
    
    /// <summary>
    /// Returns true if this month is in the past (but not archive).
    /// </summary>
    public bool IsPastMonth => !IsArchive && Month < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    /// <summary>
    /// Gets items grouped by store location for the "All Stores" view.
    /// Respects the current sort option.
    /// </summary>
    public ObservableCollection<StoreGroup> ItemsByStore => new(Items
        .GroupBy(i => i.Location ?? "Unknown")
        .OrderBy(g => g.Key)
        .Select(g => new StoreGroup 
        { 
            StoreName = g.Key, 
            Items = new ObservableCollection<ExpirationItem>(SortGroupItems(g)),
            ItemCount = g.Count(),
            TotalUnits = g.Sum(i => i.Units)
        }));
    
    private IEnumerable<ExpirationItem> SortGroupItems(IEnumerable<ExpirationItem> items)
    {
        return SortBy switch
        {
            "Description" => items.OrderBy(i => i.Description),
            "Location" => items.OrderBy(i => i.Location ?? "").ThenBy(i => i.ExpiryDate),
            "Quantity" => items.OrderByDescending(i => i.Quantity).ThenBy(i => i.ExpiryDate),
            _ => items.OrderBy(i => i.ExpiryDate) // Default: Date
        };
    }
}

/// <summary>
/// Represents a group of items for a specific store within a month.
/// </summary>
public partial class StoreGroup : ObservableObject
{
    public string StoreName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this store group is expanded (showing items).
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;
    public ObservableCollection<ExpirationItem> Items { get; set; } = new();
    public int ItemCount { get; set; }
    public int TotalUnits { get; set; }
}

/// <summary>
/// Month option for dropdown selection.
/// </summary>
public class MonthOption
{
    public int Value { get; }
    public string Name { get; }

    public MonthOption(int value, string name)
    {
        Value = value;
        Name = name;
    }
}
