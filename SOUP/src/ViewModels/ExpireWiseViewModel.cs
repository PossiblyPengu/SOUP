using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Core.Interfaces;
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
    private string _dateDisplayFormat = "MMMM yyyy";
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _items = new();

    /// <summary>
    /// Gets or sets the filtered items for the current month view.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _filteredItems = new();

    [ObservableProperty]
    private ObservableCollection<MonthGroup> _availableMonths = new();

    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _notifications = new();

    [ObservableProperty]
    private ExpireWiseAnalyticsViewModel _analytics = new();

    /// <summary>
    /// Gets or sets the available months that have expiring items.
    /// </summary>
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
    /// The looked-up dictionary item for Quick Add (cached).
    /// </summary>
    private Data.Entities.DictionaryItemEntity? _quickAddLookedUpItem;

    /// <summary>
    /// Available stores for dropdown selection.
    /// </summary>
    public ObservableCollection<Data.Entities.StoreEntity> AvailableStores { get; } = new();

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

        // Load available stores from dictionary
        LoadAvailableStores();

        // Subscribe to SKU changes for debounced lookup
        PropertyChanged += OnQuickAddSkuChanged;

        UpdateMonthDisplay();
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
            Notifications.Clear();
            foreach (var it in list)
                Notifications.Add(it);

            if (Notifications.Count > 0)
            {
                StatusMessage = $"{Notifications.Count} upcoming expiration(s)";
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
        Notifications.Remove(item);
    }

    [RelayCommand]
    private void ClearNotifications()
    {
        Notifications.Clear();
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

    [RelayCommand]
    private void GoToMonth(MonthGroup? monthGroup)
    {
        if (monthGroup != null)
        {
            _navigationService.NavigateToMonth(monthGroup.Month.Year, monthGroup.Month.Month);
            CurrentMonth = _navigationService.CurrentMonth;
        }
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

    [RelayCommand]
    private async Task AddItem()
    {
        _logger?.LogInformation("Add item clicked");

        try
        {
            // Show dialog
            var dialogViewModel = new ExpirationItemDialogViewModel();
            dialogViewModel.InitializeForAdd();

            // Apply sticky settings if available
            if (_settings != null)
            {
                dialogViewModel.RememberSettings = _settings.RememberLastLocation || _settings.RememberLastExpiryDate;

                if (_settings.RememberLastLocation && !string.IsNullOrEmpty(_settings.LastSelectedStore))
                {
                    dialogViewModel.SelectedStore = dialogViewModel.AvailableStores
                        .FirstOrDefault(s => s.Code == _settings.LastSelectedStore);
                }

                if (_settings.RememberLastExpiryDate && _settings.LastExpiryMonth.HasValue && _settings.LastExpiryYear.HasValue)
                {
                    dialogViewModel.ExpiryMonth = _settings.LastExpiryMonth.Value;
                    dialogViewModel.ExpiryYear = _settings.LastExpiryYear.Value;
                }

                dialogViewModel.DefaultUnits = _settings.DefaultUnits;
            }

            var dialog = new ExpirationItemDialog
            {
                DataContext = dialogViewModel
            };

            var result = await _dialogService.ShowContentDialogAsync<List<ExpirationItem>?>(dialog);

            if (result == null || result.Count == 0)
            {
                _logger?.LogInformation("Add item cancelled or no items provided");
                return;
            }

            // Show loading indicator
            IsBusy = true;
            BusyMessage = $"Adding {result.Count} item(s)...";
            IsLoading = true;

            var addedCount = 0;
            var failedCount = 0;
            var errors = new List<string>();

            try
            {
                // Add items one by one with error handling
                foreach (var item in result)
                {
                    try
                    {
                        // Validate item before adding
                        var isValid = ValidateItem(item, out var errorMessage);
                        if (!isValid)
                        {
                            failedCount++;
                            errors.Add($"{item.ItemNumber}: {errorMessage}");
                            _logger?.LogWarning("Validation failed for item {ItemNumber}: {Error}",
                                item.ItemNumber, errorMessage);
                            continue;
                        }

                        // Add to database
                        var addedItem = await _itemService.AddAsync(item);

                        // Verify it was actually added
                        if (addedItem != null && addedItem.Id != Guid.Empty)
                        {
                            Items.Add(addedItem);
                            addedCount++;
                            _logger?.LogInformation("Successfully added item {ItemNumber} (ID: {Id})",
                                addedItem.ItemNumber, addedItem.Id);
                        }
                        else
                        {
                            failedCount++;
                            errors.Add($"{item.ItemNumber}: Failed to add (no item returned)");
                            _logger?.LogWarning("Item service returned null or empty ID for {ItemNumber}",
                                item.ItemNumber);
                        }
                    }
                    catch (Exception itemEx)
                    {
                        failedCount++;
                        errors.Add($"{item.ItemNumber}: {itemEx.Message}");
                        _logger?.LogError(itemEx, "Failed to add item {ItemNumber}", item.ItemNumber);
                    }
                }

                // Recalculate counts and refresh display
                if (addedCount > 0)
                {
                    TotalMonths = Items
                        .Select(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1))
                        .Distinct()
                        .Count();
                    TotalItems = Items.Count;

                    BuildAvailableMonths();
                    ApplyFilters();
                    UpdateLastSaved();

                    // Save sticky settings if RememberSettings is enabled
                    if (_settings != null && dialogViewModel.RememberSettings)
                    {
                        if (dialogViewModel.SelectedStore != null)
                        {
                            _settings.LastSelectedStore = dialogViewModel.SelectedStore.Code;
                        }
                        _settings.LastExpiryMonth = dialogViewModel.ExpiryMonth;
                        _settings.LastExpiryYear = dialogViewModel.ExpiryYear;
                        _settings.DefaultUnits = dialogViewModel.DefaultUnits;

                        // Persist to disk
                        await SaveQuickAddSettings();
                    }
                }

                // Show results
                if (failedCount == 0)
                {
                    // All succeeded
                    if (addedCount == 1)
                    {
                        StatusMessage = $"✓ Added item: {result[0].ItemNumber}";
                        ShowSuccessToast($"Added {result[0].ItemNumber}");
                    }
                    else
                    {
                        StatusMessage = $"✓ Added {addedCount} items successfully";
                        ShowSuccessToast($"Added {addedCount} items");
                    }
                    _logger?.LogInformation("Successfully added {Count} items", addedCount);
                }
                else if (addedCount == 0)
                {
                    // All failed
                    StatusMessage = $"✗ Failed to add {failedCount} item(s)";
                    var errorSummary = string.Join("\n", errors.Take(3));
                    if (errors.Count > 3)
                        errorSummary += $"\n... and {errors.Count - 3} more";

                    _dialogService.ShowError($"Failed to add items:\n\n{errorSummary}", "Add Items Failed");
                    _logger?.LogError("Failed to add all {Count} items", failedCount);
                }
                else
                {
                    // Partial success
                    StatusMessage = $"✓ Added {addedCount}, ✗ Failed {failedCount}";
                    var errorSummary = string.Join("\n", errors.Take(3));
                    if (errors.Count > 3)
                        errorSummary += $"\n... and {errors.Count - 3} more";

                    _dialogService.ShowWarning(
                        $"Added {addedCount} item(s) successfully.\n\n" +
                        $"Failed to add {failedCount} item(s):\n{errorSummary}",
                        "Partial Success");
                    _logger?.LogWarning("Partial success: {Added} added, {Failed} failed",
                        addedCount, failedCount);
                }
            }
            finally
            {
                IsLoading = false;
                IsBusy = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding items: {ex.Message}";
            _logger?.LogError(ex, "Exception while adding items");
            _dialogService.ShowError($"An error occurred while adding items:\n\n{ex.Message}", "Error");

            IsLoading = false;
            IsBusy = false;
        }
    }

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
            // Show dialog
            var dialogViewModel = new ExpirationItemDialogViewModel();
            dialogViewModel.InitializeForEdit(SelectedItem);

            var dialog = new ExpirationItemDialog
            {
                DataContext = dialogViewModel
            };

            var result = await _dialogService.ShowContentDialogAsync<List<ExpirationItem>?>(dialog);

            if (result == null || result.Count == 0)
            {
                _logger?.LogInformation("Edit item cancelled");
                return;
            }

            // Show loading indicator
            IsBusy = true;
            BusyMessage = "Updating item...";
            IsLoading = true;

            try
            {
                var itemToUpdate = result[0];

                // Validate item before updating
                var isValid = ValidateItem(itemToUpdate, out var errorMessage);
                if (!isValid)
                {
                    _dialogService.ShowError($"Validation failed:\n\n{errorMessage}", "Invalid Item");
                    _logger?.LogWarning("Validation failed for item {ItemNumber}: {Error}",
                        itemToUpdate.ItemNumber, errorMessage);
                    return;
                }

                // Update in database
                var updatedItem = await _itemService.UpdateAsync(itemToUpdate);

                // Verify it was updated
                if (updatedItem != null && updatedItem.Id != Guid.Empty)
                {
                    // Update in collection
                    var index = Items.IndexOf(SelectedItem);
                    if (index >= 0)
                    {
                        Items[index] = updatedItem;
                        SelectedItem = updatedItem; // Update selection
                    }
                    else
                    {
                        _logger?.LogWarning("Could not find item in collection at index {Index}", index);
                    }

                    BuildAvailableMonths();
                    ApplyFilters();
                    UpdateLastSaved();

                    StatusMessage = $"✓ Updated item: {updatedItem.ItemNumber}";
                    ShowSuccessToast($"Updated {updatedItem.ItemNumber}");
                    _logger?.LogInformation("Successfully updated item {ItemNumber} (ID: {Id})",
                        updatedItem.ItemNumber, updatedItem.Id);
                }
                else
                {
                    _dialogService.ShowError("Failed to update item (no item returned from database).",
                        "Update Failed");
                    _logger?.LogWarning("Item service returned null or empty ID for {ItemNumber}",
                        originalItemNumber);
                }
            }
            finally
            {
                IsLoading = false;
                IsBusy = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating item: {ex.Message}";
            _logger?.LogError(ex, "Exception while updating item {ItemNumber}", originalItemNumber);
            _dialogService.ShowError($"An error occurred while updating the item:\n\n{ex.Message}", "Error");

            IsLoading = false;
            IsBusy = false;
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
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnStatusFilterChanged(string value)
    {
        ApplyFilters();
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

        // Final sort and populate
        FilteredItems.Clear();
        foreach (var item in filtered.OrderBy(i => i.Location ?? string.Empty).ThenBy(i => i.ExpiryDate))
        {
            FilteredItems.Add(item);
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
            // Add a default "(No Store)" option
            AvailableStores.Add(new Data.Entities.StoreEntity { Code = "", Name = "(No Store)" });

            // Order by store number (Code) instead of alphabetically by Name
            foreach (var store in stores.OrderBy(s => s.Code))
            {
                AvailableStores.Add(store);
            }

            _logger?.LogInformation("Loaded {Count} stores for Quick Add", stores.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load stores");
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
                QuickAddItemPreview = string.Empty;
                QuickAddIsValid = false; // Last - triggers command notification
                return;
            }

            // Start validation indicator
            QuickAddIsValidating = true;
            QuickAddItemPreview = string.Empty;
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
    /// Perform the actual SKU lookup after debounce
    /// </summary>
    private void DebouncedLookupSku()
    {
        var sku = QuickAddSku?.Trim();
        if (string.IsNullOrWhiteSpace(sku))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;
                _quickAddLookedUpItem = null;
                QuickAddItemPreview = string.Empty;
                QuickAddIsValid = false; // Last - triggers command notification
            });
            return;
        }

        try
        {
            var result = _itemLookupService.Search(sku);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;

                if (result.Found && result.Item != null)
                {
                    // IMPORTANT: Set _quickAddLookedUpItem BEFORE QuickAddIsValid
                    // because setting QuickAddIsValid triggers OnQuickAddIsValidChanged which checks _quickAddLookedUpItem
                    _quickAddLookedUpItem = result.Item;
                    QuickAddItemPreview = $"{result.Item.Description} ({result.Item.Number})";
                    QuickAddIsValid = true; // This triggers command notification - must be last
                    _logger?.LogDebug("Quick Add SKU lookup success: {Sku} → {Item}", sku, result.Item.Number);
                }
                else
                {
                    _quickAddLookedUpItem = null;
                    QuickAddItemPreview = string.Empty;
                    QuickAddIsValid = false; // Last - triggers command notification
                    _logger?.LogDebug("Quick Add SKU not found: {Sku}", sku);
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Quick Add SKU lookup error: {Sku}", sku);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;
                _quickAddLookedUpItem = null;
                QuickAddItemPreview = string.Empty;
                QuickAddIsValid = false; // Last - triggers command notification
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
        StatusMessage = QuickAddExpanded ? "Quick Add panel opened (Ctrl+Shift+Q)" : "Quick Add panel closed";
    }

    /// <summary>
    /// Check if item can be added to queue
    /// </summary>
    private bool CanAddToQueue() => QuickAddIsValid && _quickAddLookedUpItem != null;

    /// <summary>
    /// Add item to queue (not saved to database yet)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddToQueue))]
    private void AddToQueue()
    {
        if (_quickAddLookedUpItem == null) return;

        try
        {
            // Create expiration item (not saved yet)
            var expiryDate = new DateTime(QuickAddYear, QuickAddMonth, DateTime.DaysInMonth(QuickAddYear, QuickAddMonth));

            var queuedItem = new ExpirationItem
            {
                ItemNumber = _quickAddLookedUpItem.Number,
                Description = _quickAddLookedUpItem.Description,
                Location = QuickAddStore?.Code ?? string.Empty,
                Quantity = QuickAddUnits,
                ExpiryDate = expiryDate
            };

            // Add to queue
            QuickAddQueue.Add(queuedItem);
            HasQueuedItems = QuickAddQueue.Count > 0;

            // Show feedback
            StatusMessage = $"Added to queue: {queuedItem.ItemNumber} ({QuickAddQueue.Count} items)";
            _logger?.LogInformation("Added to queue: {Item}", queuedItem.ItemNumber);

            // Clear SKU input for next entry
            QuickAddSku = string.Empty;
            _quickAddLookedUpItem = null;
            QuickAddItemPreview = string.Empty;
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
