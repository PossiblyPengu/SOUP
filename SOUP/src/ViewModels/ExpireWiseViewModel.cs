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

    // Settings / timers
    private Infrastructure.Services.SettingsService? _settingsService;
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

        // Subscribe to settings changes for dynamic updates
        _settingsService = serviceProvider.GetService<Infrastructure.Services.SettingsService>();
        if (_settingsService != null)
        {
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

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
            var dialogViewModel = new ExpirationItemDialogViewModel();
            dialogViewModel.InitializeForAdd();

            var dialog = new ExpirationItemDialog
            {
                DataContext = dialogViewModel
            };

            var result = await _dialogService.ShowContentDialogAsync<List<ExpirationItem>?>(dialog);

                if (result != null && result.Count > 0)
                {
                    foreach (var item in result)
                    {
                        var addedItem = await _itemService.AddAsync(item);
                        Items.Add(addedItem);
                    }

                    // Recalculate counts and refresh
                    TotalMonths = Items
                        .Select(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1))
                        .Distinct()
                        .Count();
                    TotalItems = Items.Count;

                    BuildAvailableMonths();
                    ApplyFilters();

                    UpdateLastSaved();

                    if (result.Count == 1)
                    {
                        StatusMessage = $"Added item {result[0].ItemNumber}";
                        _logger?.LogInformation("Added new item {ItemNumber}", result[0].ItemNumber);
                    }
                    else
                    {
                        StatusMessage = $"Added {result.Count} items";
                        _logger?.LogInformation("Added {Count} new items", result.Count);
                    }
                }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding item: {ex.Message}";
            _logger?.LogError(ex, "Exception while adding item");
        }
    }

    [RelayCommand]
    private async Task EditItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to edit";
            return;
        }

        _logger?.LogInformation("Edit item clicked for {ItemNumber}", SelectedItem.ItemNumber);

        try
        {
            var dialogViewModel = new ExpirationItemDialogViewModel();
            dialogViewModel.InitializeForEdit(SelectedItem);

            var dialog = new ExpirationItemDialog
            {
                DataContext = dialogViewModel
            };

            var result = await _dialogService.ShowContentDialogAsync<List<ExpirationItem>?>(dialog);

                if (result != null && result.Count > 0)
                {
                    var updatedItem = await _itemService.UpdateAsync(result[0]);
                    var index = Items.IndexOf(SelectedItem);
                    if (index >= 0)
                    {
                        Items[index] = updatedItem;
                    }

                    BuildAvailableMonths();
                    ApplyFilters();
                    StatusMessage = $"Updated item {updatedItem.ItemNumber}";
                    UpdateLastSaved();
                    _logger?.LogInformation("Updated item {ItemNumber}", updatedItem.ItemNumber);
                }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating item: {ex.Message}";
            _logger?.LogError(ex, "Exception while updating item");
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
                    IsLoading = true;
                    StatusMessage = "Importing from CSV...";

                    if (_dialogService == null)
                    {
                        StatusMessage = "Import failed: dialog service unavailable";
                        IsLoading = false;
                        return;
                    }

                    var result = await _parser.ParseCsvAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    var items = result.Value.ToList();
                    var importResult = await _importExportService.ImportItemsAsync(items);

                    if (importResult.IsSuccess)
                    {
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
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            _logger?.LogError(ex, "Exception during CSV import");
            IsLoading = false;
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
