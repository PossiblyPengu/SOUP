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
using SOUP.Infrastructure.Services.Parsers;
using SOUP.Services;
using SOUP.Views;
using SOUP.Views.ExpireWise;

namespace SOUP.ViewModels;

public partial class ExpireWiseViewModel : ObservableObject, IDisposable
{
    private readonly IExpireWiseRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly SOUP.Infrastructure.Services.Parsers.ExpireWiseParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpireWiseViewModel>? _logger;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseSearchService _searchService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseMonthNavigationService _navigationService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseImportExportService _importExportService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseNotificationService _notificationService;
    private readonly SOUP.Features.ExpireWise.Services.ExpireWiseItemService _itemService;
    private readonly SOUP.Features.ExpireWise.Services.ItemLookupService _itemLookupService;
    private Infrastructure.Services.SettingsService? _settingsService;
    private SOUP.Features.ExpireWise.Models.ExpireWiseSettings? _settings;
    private int _autoRefreshMinutes = 0;
    private DispatcherTimer? _notificationTimer;
    private EventHandler? _notificationTimerHandler;
    private System.Threading.Timer? _searchDebounceTimer;
    private string _dateDisplayFormat = "Long";
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _items = new();
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _filteredItems = new();

    private ICollectionView? _itemsView;
    public ICollectionView? ItemsView
    {
        get => _itemsView;
        private set => SetProperty(ref _itemsView, value);
    }

    [ObservableProperty]
    private ObservableCollection<MonthGroup> _availableMonths = new();
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _notifications = new();
    [ObservableProperty]
    private ExpireWiseAnalyticsViewModel _analytics = new();
    [ObservableProperty]
    private ExpirationItem? _selectedItem;
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _selectedItems = new();
    [ObservableProperty]
    private string _searchText = string.Empty;
    [ObservableProperty]
    private string _statusFilter = "All";

    public ObservableCollection<string> StatusFilters { get; } = new() { "All", "Good", "Warning", "Critical", "Expired" };

    [ObservableProperty]
    private DateTime _currentMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    [ObservableProperty]
    private string _currentMonthDisplay = string.Empty;
    
    [ObservableProperty]
    private string? _selectedStore;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableStoreNames = new();
    
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private string _busyMessage = "Loading...";
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private bool _hasData;

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
    [ObservableProperty]
    private int _expiredCount;
    [ObservableProperty]
    private int _expiringSoonCount;
    [ObservableProperty]
    private int _totalItems;
    [ObservableProperty]
    private int _totalMonths;
    [ObservableProperty]
    private string _lastSavedDisplay = string.Empty;
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// Collection of archived items
    /// </summary>
    public ObservableCollection<ArchivedExpirationItem> ArchivedItems { get; } = new();
    
    /// <summary>
    /// Gets or sets whether archived items view is shown.
    /// </summary>
    [ObservableProperty]
    private bool _showArchive;
    
    /// <summary>
    /// Gets or sets the count of archived items for the selected store.
    /// </summary>
    [ObservableProperty]
    private int _archivedItemsCount;
    [ObservableProperty]
    private bool _groupByStore = true;
    public event Action<bool>? GroupByStoreChanged;
    [ObservableProperty]
    private bool _isInitialized;
    [ObservableProperty]
    private string _quickAddSku = string.Empty;
    [ObservableProperty]
    private string _quickAddStatus = string.Empty;
    [ObservableProperty]
    private bool _quickAddExpanded = false;
    [ObservableProperty]
    private int _selectedViewIndex = 1;
    [ObservableProperty]
    private bool _sidebarCollapsed = false;
    [ObservableProperty]
    private int _quickAddMonth = DateTime.Today.AddMonths(1).Month;
    [ObservableProperty]
    private int _quickAddYear = DateTime.Today.AddMonths(1).Year;
    [ObservableProperty]
    private int _quickAddUnits = 1;
    public ObservableCollection<MonthOption> QuickAddMonths { get; } = new()
    {
        new(1, "January"), new(2, "February"), new(3, "March"),
        new(4, "April"), new(5, "May"), new(6, "June"),
        new(7, "July"), new(8, "August"), new(9, "September"),
        new(10, "October"), new(11, "November"), new(12, "December")
    };
    public ObservableCollection<int> QuickAddYears { get; } = new(Enumerable.Range(DateTime.Today.Year, 6));
    [ObservableProperty]
    private Data.Entities.StoreEntity? _quickAddStore;
    [ObservableProperty]
    private bool _quickAddIsValidating;
    [ObservableProperty]
    private bool _quickAddIsValid;
    [ObservableProperty]
    private string _quickAddItemPreview = string.Empty;
    private Data.Entities.DictionaryItemEntity? _quickAddLookedUpItem;
    public ObservableCollection<Data.Entities.StoreEntity> AvailableStores { get; } = new();
    public ObservableCollection<ExpirationItem> QuickAddQueue { get; } = new();
    [ObservableProperty]
    private bool _hasQueuedItems;
    private System.Threading.Timer? _skuLookupDebounceTimer;
    public ExpireWiseViewModel(IExpireWiseRepository repository, IFileImportExportService fileService, DialogService dialogService, IServiceProvider serviceProvider, SOUP.Features.ExpireWise.Services.ExpireWiseSearchService searchService, SOUP.Features.ExpireWise.Services.ExpireWiseMonthNavigationService navigationService, SOUP.Features.ExpireWise.Services.ExpireWiseImportExportService importExportService, SOUP.Features.ExpireWise.Services.ExpireWiseNotificationService notificationService, SOUP.Features.ExpireWise.Services.ExpireWiseItemService itemService, ILogger<ExpireWiseViewModel>? logger = null)
    {
        (_repository, _fileService, _parser, _dialogService, _serviceProvider, _logger, _searchService, _navigationService, _importExportService, _notificationService, _itemService, _itemLookupService) = (repository, fileService, new ExpireWiseParser(null), dialogService, serviceProvider, logger, searchService, navigationService, importExportService, notificationService, itemService, new SOUP.Features.ExpireWise.Services.ItemLookupService());
        _settingsService = serviceProvider.GetService<Infrastructure.Services.SettingsService>();
        if (_settingsService != null) _settingsService.SettingsChanged += OnSettingsChanged;
        LoadAvailableStores();
        PropertyChanged += OnQuickAddSkuChanged;
        UpdateMonthDisplay();
    }

    private void OnSettingsChanged(object? sender, Infrastructure.Services.SettingsChangedEventArgs e) { if (e.AppName == "ExpireWise") _ = LoadAndApplySettingsAsync(); }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        IsInitialized = true;
        await LoadAndApplySettingsAsync(); await LoadQuickAddSettings(); await LoadItems();
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
                (ExpirationItem.CriticalDaysThreshold, ExpirationItem.WarningDaysThreshold, StatusFilter, _dateDisplayFormat, _autoRefreshMinutes) = (settings.CriticalThresholdDays, settings.WarningThresholdDays, settings.DefaultStatusFilter, settings.DateDisplayFormat ?? _dateDisplayFormat, settings.AutoRefreshIntervalMinutes);
                UpdateMonthDisplay();
                _logger?.LogInformation("Applied ExpireWise settings: Critical={Critical} days, Warning={Warning} days, Filter={Filter}", settings.CriticalThresholdDays, settings.WarningThresholdDays, settings.DefaultStatusFilter);
            }
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Failed to load ExpireWise settings, using defaults"); }
    }

    private string FormatExpirationDate(DateTime date) => _dateDisplayFormat switch { "Short" => date.ToString("MM/yyyy"), "Long" => date.ToString("MMMM yyyy"), _ => date.ToString("MMMM yyyy") };

    private bool ValidateItem(ExpirationItem item, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(item.ItemNumber) && string.IsNullOrWhiteSpace(item.Description)) { errorMessage = "Item must have either an Item Number or Description."; return false; }
        if (item.ExpiryDate < DateTime.Today.AddYears(-1)) { errorMessage = "Expiration date cannot be more than 1 year in the past.\n\nPlease verify the date is correct."; return false; }
        if (item.ExpiryDate > DateTime.Today.AddYears(10)) { errorMessage = "Expiration date cannot be more than 10 years in the future.\n\nPlease verify the date is correct."; return false; }
        if (item.Units < 0) { errorMessage = "Units cannot be negative."; return false; }
        (errorMessage) = (string.Empty);
        return true;
    }

    private void UpdateAnalytics() => Analytics.UpdateAnalytics(Items);

    public event Action? FocusSearchRequested;

    [RelayCommand]
    private async Task CheckNotifications()
    {
        try
        {
            var settingsService = _serviceProvider.GetService<Infrastructure.Services.SettingsService>();
            var settings = settingsService != null ? await settingsService.LoadSettingsAsync<Core.Entities.Settings.ExpireWiseSettings>("ExpireWise") : null;
            var (enabled, threshold) = (settings?.ShowExpirationNotifications ?? true, settings?.WarningThresholdDays ?? ExpirationItem.WarningDaysThreshold);
            var list = await _notificationService.GetNotificationsAsync(Items, threshold, enabled);
            Notifications.Clear();
            foreach (var it in list) Notifications.Add(it);
            if (Notifications.Count > 0) StatusMessage = $"{Notifications.Count} upcoming expiration(s)";
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to check notifications"); StatusMessage = "Failed to check notifications"; }
    }

    [RelayCommand]
    private void RemoveNotification(ExpirationItem? item) { if (item != null) Notifications.Remove(item); }

    [RelayCommand]
    private void ClearNotifications() => Notifications.Clear();

    [RelayCommand]
    private void FocusSearch() => FocusSearchRequested?.Invoke();

    [RelayCommand]
    private void ClearSearch() { SearchText = string.Empty; FocusSearchRequested?.Invoke(); }

    [RelayCommand]
    private async Task LoadItems()
    {
        try
        {
            IsLoading = true; StatusMessage = "Loading items...";
            
            // Auto-archive items expired for 2+ months
            await AutoArchiveOldExpiredItems();
            
            var allItems = await _repository.GetAllAsync();
            var activeItems = allItems.Where(i => !i.IsDeleted).ToList();
            Items.Clear();
            foreach (var item in activeItems.OrderBy(i => i.ExpiryDate)) Items.Add(item);
            (HasData, TotalItems) = (Items.Count > 0, Items.Count);
            TotalMonths = Items.Select(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1)).Distinct().Count();
            
            // Update available stores and select first if none selected
            UpdateAvailableStores();
            
            ApplyFilters();
            StatusMessage = $"Loaded {Items.Count} items from {AvailableStoreNames.Count} stores";
            _logger?.LogInformation("Loaded {Count} expiration items", Items.Count);
        }
        catch (Exception ex) { StatusMessage = $"Error loading items: {ex.Message}"; _logger?.LogError(ex, "Exception while loading items"); }
        finally { IsLoading = false; }
    }

    private async Task AutoArchiveOldExpiredItems()
    {
        try
        {
            var twoMonthsAgo = DateTime.Today.AddMonths(-2);
            var oldExpiredItems = await _repository.FindAsync(x => !x.IsDeleted && x.ExpiryDate < twoMonthsAgo);
            
            if (oldExpiredItems.Any())
            {
                var count = await _repository.ArchiveExpiredItemsAsync(null); // Archive from all stores
                if (count > 0)
                {
                    _logger?.LogInformation("Auto-archived {Count} items expired for 2+ months", count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Auto-archive failed, continuing with load");
        }
    }

    private void BuildAvailableMonths()
    {
        var groups = _navigationService.BuildMonthGroups(Items, CurrentMonth, _dateDisplayFormat);
        AvailableMonths.Clear();
        foreach (var g in groups) AvailableMonths.Add(g);
    }
    private void RebuildVisibleMonths() { var groups = _navigationService.BuildMonthGroups(Items, CurrentMonth, _dateDisplayFormat); AvailableMonths.Clear(); foreach (var g in groups) AvailableMonths.Add(g); }

    [RelayCommand]
    private void PreviousMonth() { _navigationService.NavigatePrevious(); CurrentMonth = _navigationService.CurrentMonth; }
    [RelayCommand]
    private void NextMonth() { _navigationService.NavigateNext(); CurrentMonth = _navigationService.CurrentMonth; }
    [RelayCommand]
    private void GoToMonth(MonthGroup? monthGroup) { if (monthGroup != null) { _navigationService.NavigateToMonth(monthGroup.Month.Year, monthGroup.Month.Month); CurrentMonth = _navigationService.CurrentMonth; } }
    partial void OnCurrentMonthChanged(DateTime value) { _navigationService.NavigateToMonth(value.Year, value.Month); UpdateMonthDisplay(); RebuildVisibleMonths(); _itemsView?.Refresh(); ApplyFilters(); }
    private void UpdateMonthDisplay() => CurrentMonthDisplay = CurrentMonth.ToString("MMMM yyyy");

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
            if (_settings != null)
            {
                dialogViewModel.RememberSettings = _settings.RememberLastLocation || _settings.RememberLastExpiryDate;
                if (_settings.RememberLastLocation && !string.IsNullOrEmpty(_settings.LastSelectedStore)) dialogViewModel.SelectedStore = dialogViewModel.AvailableStores.FirstOrDefault(s => s.Code == _settings.LastSelectedStore);
                if (_settings.RememberLastExpiryDate && _settings.LastExpiryMonth.HasValue && _settings.LastExpiryYear.HasValue) { dialogViewModel.ExpiryMonth = _settings.LastExpiryMonth.Value; dialogViewModel.ExpiryYear = _settings.LastExpiryYear.Value; }
                dialogViewModel.DefaultUnits = _settings.DefaultUnits;
            }
            var result = await _dialogService.ShowContentDialogAsync<List<ExpirationItem>?>(new ExpirationItemDialog { DataContext = dialogViewModel });
            if (result == null || result.Count == 0) { _logger?.LogInformation("Add item cancelled or no items provided"); return; }
            (IsBusy, BusyMessage, IsLoading) = (true, $"Adding {result.Count} item(s)...", true);
            var (addedCount, failedCount, errors) = (0, 0, new List<string>());
            try
            {
                foreach (var item in result)
                {
                    try
                    {
                        if (!ValidateItem(item, out var errorMessage)) { failedCount++; errors.Add($"{item.ItemNumber}: {errorMessage}"); _logger?.LogWarning("Validation failed for item {ItemNumber}: {Error}", item.ItemNumber, errorMessage); continue; }
                        var addedItem = await _itemService.AddAsync(item);
                        if (addedItem != null && addedItem.Id != Guid.Empty) { Items.Add(addedItem); addedCount++; _logger?.LogInformation("Successfully added item {ItemNumber} (ID: {Id})", addedItem.ItemNumber, addedItem.Id); }
                        else { failedCount++; errors.Add($"{item.ItemNumber}: Failed to add (no item returned)"); _logger?.LogWarning("Item service returned null or empty ID for {ItemNumber}", item.ItemNumber); }
                    }
                    catch (Exception itemEx) { failedCount++; errors.Add($"{item.ItemNumber}: {itemEx.Message}"); _logger?.LogError(itemEx, "Failed to add item {ItemNumber}", item.ItemNumber); }
                }
                if (addedCount > 0)
                {
                    TotalMonths = Items.Select(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1)).Distinct().Count();
                    TotalItems = Items.Count;
                    BuildAvailableMonths(); ApplyFilters(); LastSavedDisplay = $"Last saved: {DateTime.Now:h:mm tt}";
                    if (_settings != null && dialogViewModel.RememberSettings)
                    {
                        if (dialogViewModel.SelectedStore != null) _settings.LastSelectedStore = dialogViewModel.SelectedStore.Code;
                        (_settings.LastExpiryMonth, _settings.LastExpiryYear, _settings.DefaultUnits) = (dialogViewModel.ExpiryMonth, dialogViewModel.ExpiryYear, dialogViewModel.DefaultUnits);
                        await SaveQuickAddSettings();
                    }
                }
                if (failedCount == 0) { StatusMessage = addedCount == 1 ? $"✓ Added item: {result[0].ItemNumber}" : $"✓ Added {addedCount} items successfully"; ShowSuccessToast(addedCount == 1 ? $"Added {result[0].ItemNumber}" : $"Added {addedCount} items"); _logger?.LogInformation("Successfully added {Count} items", addedCount); }
                else if (addedCount == 0) { StatusMessage = $"✗ Failed to add {failedCount} item(s)"; var errorSummary = string.Join("\n", errors.Take(3)); if (errors.Count > 3) errorSummary += $"\n... and {errors.Count - 3} more"; _dialogService.ShowError($"Failed to add items:\n\n{errorSummary}", "Add Items Failed"); _logger?.LogError("Failed to add all {Count} items", failedCount); }
                else { StatusMessage = $"✓ Added {addedCount}, ✗ Failed {failedCount}"; var errorSummary = string.Join("\n", errors.Take(3)); if (errors.Count > 3) errorSummary += $"\n... and {errors.Count - 3} more"; _dialogService.ShowWarning($"Added {addedCount} item(s) successfully.\n\nFailed to add {failedCount} item(s):\n{errorSummary}", "Partial Success"); _logger?.LogWarning("Partial success: {Added} added, {Failed} failed", addedCount, failedCount); }
            }
            finally { (IsLoading, IsBusy) = (false, false); }
        }
        catch (Exception ex) { StatusMessage = $"Error adding items: {ex.Message}"; _logger?.LogError(ex, "Exception while adding items"); _dialogService.ShowError($"An error occurred while adding items:\n\n{ex.Message}", "Error"); (IsLoading, IsBusy) = (false, false); }
    }

    private void ShowSuccessToast(string message)
    {
        if (_settings?.ShowToastNotifications == false) return;
        StatusMessage = $"✓ {message}";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, e) => { if (StatusMessage == $"✓ {message}") StatusMessage = string.Empty; timer.Stop(); };
        timer.Start();
    }

    [RelayCommand]
    private async Task EditItem()
    {
        if (SelectedItem == null) { StatusMessage = "Please select an item to edit"; _dialogService.ShowWarning("Please select an item to edit.", "No Item Selected"); return; }
        var originalItemNumber = SelectedItem.ItemNumber;
        _logger?.LogInformation("Edit item clicked for {ItemNumber}", originalItemNumber);
        try
        {
            var dialogViewModel = new ExpirationItemDialogViewModel();
            dialogViewModel.InitializeForEdit(SelectedItem);
            var result = await _dialogService.ShowContentDialogAsync<List<ExpirationItem>?>(new ExpirationItemDialog { DataContext = dialogViewModel });
            if (result == null || result.Count == 0) { _logger?.LogInformation("Edit item cancelled"); return; }
            (IsBusy, BusyMessage, IsLoading) = (true, "Updating item...", true);
            try
            {
                var itemToUpdate = result[0];
                if (!ValidateItem(itemToUpdate, out var errorMessage)) { _dialogService.ShowError($"Validation failed:\n\n{errorMessage}", "Invalid Item"); _logger?.LogWarning("Validation failed for item {ItemNumber}: {Error}", itemToUpdate.ItemNumber, errorMessage); return; }
                var updatedItem = await _itemService.UpdateAsync(itemToUpdate);
                if (updatedItem != null && updatedItem.Id != Guid.Empty)
                {
                    var index = Items.IndexOf(SelectedItem);
                    if (index >= 0) { Items[index] = updatedItem; SelectedItem = updatedItem; } else _logger?.LogWarning("Could not find item in collection at index {Index}", index);
                    BuildAvailableMonths(); ApplyFilters(); LastSavedDisplay = $"Last saved: {DateTime.Now:h:mm tt}";
                    StatusMessage = $"✓ Updated item: {updatedItem.ItemNumber}"; ShowSuccessToast($"Updated {updatedItem.ItemNumber}"); _logger?.LogInformation("Successfully updated item {ItemNumber} (ID: {Id})", updatedItem.ItemNumber, updatedItem.Id);
                }
                else { _dialogService.ShowError("Failed to update item (no item returned from database).", "Update Failed"); _logger?.LogWarning("Item service returned null or empty ID for {ItemNumber}", originalItemNumber); }
            }
            finally { (IsLoading, IsBusy) = (false, false); }
        }
        catch (Exception ex) { StatusMessage = $"Error updating item: {ex.Message}"; _logger?.LogError(ex, "Exception while updating item {ItemNumber}", originalItemNumber); _dialogService.ShowError($"An error occurred while updating the item:\n\n{ex.Message}", "Error"); (IsLoading, IsBusy) = (false, false); }
    }

    [RelayCommand]
    private async Task DeleteItem()
    {
        if (SelectedItem == null) { StatusMessage = "Please select an item to delete"; return; }
        var result = System.Windows.MessageBox.Show($"Are you sure you want to delete item {SelectedItem.ItemNumber}?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var itemNumber = SelectedItem.ItemNumber;
            var success = await _repository.DeleteAsync(SelectedItem.Id);
            if (success) { Items.Remove(SelectedItem); BuildAvailableMonths(); ApplyFilters(); StatusMessage = $"Deleted item {itemNumber}"; LastSavedDisplay = $"Last saved: {DateTime.Now:h:mm tt}"; _logger?.LogInformation("Deleted item {ItemNumber}", itemNumber); SelectedItem = null; }
            else StatusMessage = $"Error deleting item: Item not found";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; _logger?.LogError(ex, "Exception while deleting item"); }
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        try
        {
            var toDelete = SelectedItems?.ToList() ?? new List<ExpirationItem>();
            if (toDelete.Count == 0) { StatusMessage = "Please select one or more items to delete"; return; }
            var confirm = await (_dialogService?.ShowConfirmationAsync("Confirm delete", $"Delete {toDelete.Count} item(s)? This cannot be undone.") ?? Task.FromResult(false));
            if (!confirm) return;
            var deleted = await _itemService.DeleteRangeAsync(toDelete);
            foreach (var item in toDelete.Where(i => Items.Contains(i))) Items.Remove(item);
            BuildAvailableMonths(); ApplyFilters(); LastSavedDisplay = $"Last saved: {DateTime.Now:h:mm tt}";
            StatusMessage = $"✓ Deleted {deleted} items"; ShowSuccessToast($"Deleted {deleted} items"); _logger?.LogInformation("Deleted {Count} items", deleted);
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; _logger?.LogError(ex, "Exception while deleting items"); _dialogService?.ShowError($"Failed to delete items: {ex.Message}", "Error"); }
    }

    [RelayCommand]
    private async Task ArchiveExpiredItems()
    {
        try
        {
            var store = SelectedStore;
            var expiredItems = Items.Where(i => i.Status == ExpirationStatus.Expired && 
                                                (string.IsNullOrEmpty(store) || i.Location == store)).ToList();
            
            if (expiredItems.Count == 0)
            {
                StatusMessage = "No expired items to archive";
                _dialogService?.ShowInfo("No expired items found for archiving.", "Archive");
                return;
            }

            var confirm = await (_dialogService?.ShowConfirmationAsync(
                "Archive Expired Items", 
                $"Archive {expiredItems.Count} expired item(s) from {(string.IsNullOrEmpty(store) ? "all stores" : store)}?\n\nArchived items will be removed from the active view but can be viewed later.") 
                ?? Task.FromResult(false));
            
            if (!confirm) return;

            (IsBusy, BusyMessage, IsLoading) = (true, "Archiving expired items...", true);
            
            var count = await _repository.ArchiveExpiredItemsAsync(store);
            
            if (count > 0)
            {
                // Remove archived items from active view
                foreach (var item in expiredItems)
                {
                    Items.Remove(item);
                }
                
                BuildAvailableMonths();
                UpdateAvailableStores();
                ApplyFilters();
                await LoadArchivedItemsAsync();
                
                StatusMessage = $"✓ Archived {count} expired items";
                ShowSuccessToast($"Archived {count} items");
                _logger?.LogInformation("Archived {Count} expired items from {Store}", count, store ?? "all stores");
            }
            else
            {
                StatusMessage = "No items were archived";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error archiving items: {ex.Message}";
            _logger?.LogError(ex, "Exception while archiving expired items");
            _dialogService?.ShowError($"Failed to archive items: {ex.Message}", "Error");
        }
        finally
        {
            (IsLoading, IsBusy) = (false, false);
        }
    }

    [RelayCommand]
    private async Task ViewArchive()
    {
        ShowArchive = !ShowArchive;
        if (ShowArchive)
        {
            await LoadArchivedItemsAsync();
            StatusMessage = "Viewing archived items";
        }
        else
        {
            StatusMessage = "Viewing active items";
        }
    }

    private async Task LoadArchivedItemsAsync()
    {
        try
        {
            var archivedItems = await _repository.GetArchivedItemsAsync(SelectedStore);
            ArchivedItems.Clear();
            foreach (var item in archivedItems.OrderByDescending(i => i.ArchivedDate))
            {
                ArchivedItems.Add(item);
            }
            ArchivedItemsCount = ArchivedItems.Count;
            _logger?.LogDebug("Loaded {Count} archived items", ArchivedItems.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading archived items");
            StatusMessage = $"Error loading archived items: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearOldArchive()
    {
        try
        {
            var sixMonthsAgo = DateTime.Today.AddMonths(-6);
            var confirm = await (_dialogService?.ShowConfirmationAsync(
                "Clear Old Archive", 
                $"Delete archived items older than {sixMonthsAgo:MMMM d, yyyy}?\n\nThis action cannot be undone.") 
                ?? Task.FromResult(false));
            
            if (!confirm) return;

            var count = await _repository.DeleteOldArchivedItemsAsync(sixMonthsAgo);
            await LoadArchivedItemsAsync();
            
            StatusMessage = $"✓ Deleted {count} old archived items";
            ShowSuccessToast($"Cleared {count} old items");
            _logger?.LogInformation("Deleted {Count} archived items older than {Date}", count, sixMonthsAgo);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing archive: {ex.Message}";
            _logger?.LogError(ex, "Exception while clearing old archive");
            _dialogService?.ShowError($"Failed to clear old archive: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private async Task ImportFromExcel()
    {
        _logger?.LogInformation("Import from Excel clicked");
        try
        {
            if (_dialogService == null) { StatusMessage = "Import failed: dialog service unavailable"; return; }
            var files = await _dialogService.ShowOpenFileDialogAsync("Select Excel file to import", "Excel Files (*.xlsx)|*.xlsx");
            if (files != null && files.Length > 0)
            {
                (IsLoading, StatusMessage) = (true, "Importing from Excel...");
                var result = await _parser.ParseExcelAsync(files[0]);
                if (result.IsSuccess && result.Value != null)
                {
                    var importResult = await _importExportService.ImportItemsAsync(result.Value.ToList());
                    if (importResult.IsSuccess) { await LoadItems(); StatusMessage = $"Imported {importResult.Value} items"; LastSavedDisplay = $"Last saved: {DateTime.Now:h:mm tt}"; _logger?.LogInformation("Imported {Count} items from Excel (transactional)", importResult.Value); }
                    else { StatusMessage = $"Import failed: {importResult.ErrorMessage}"; _logger?.LogWarning("Excel import failed: {Error}", importResult.ErrorMessage); }
                }
                else StatusMessage = $"Import failed: {result.ErrorMessage}";
                IsLoading = false;
            }
        }
        catch (Exception ex) { StatusMessage = $"Import error: {ex.Message}"; _logger?.LogError(ex, "Exception during Excel import"); IsLoading = false; }
    }

    [RelayCommand]
    private async Task ImportFromCsv()
    {
        _logger?.LogInformation("Import from CSV clicked");
        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync("Select CSV file to import", "CSV Files (*.csv)|*.csv");
            if (files != null && files.Length > 0)
            {
                (IsBusy, BusyMessage, IsLoading, StatusMessage) = (true, "Importing items from CSV...", true, "Importing from CSV...");
                if (_dialogService == null) { StatusMessage = "Import failed: dialog service unavailable"; (IsLoading, IsBusy) = (false, false); return; }
                var result = await _parser.ParseCsvAsync(files[0]);
                if (result.IsSuccess && result.Value != null)
                {
                    var items = result.Value.ToList();
                    BusyMessage = $"Importing {items.Count} items...";
                    var importResult = await _importExportService.ImportItemsAsync(items);
                    if (importResult.IsSuccess) { BusyMessage = "Refreshing data..."; await LoadItems(); StatusMessage = $"Imported {importResult.Value} items"; LastSavedDisplay = $"Last saved: {DateTime.Now:h:mm tt}"; _logger?.LogInformation("Imported {Count} items from CSV (transactional)", importResult.Value); }
                    else { StatusMessage = $"Import failed: {importResult.ErrorMessage}"; _logger?.LogWarning("CSV import failed: {Error}", importResult.ErrorMessage); }
                }
                else StatusMessage = $"Import failed: {result.ErrorMessage}";
                (IsLoading, IsBusy) = (false, false);
            }
        }
        catch (Exception ex) { StatusMessage = $"Import error: {ex.Message}"; _logger?.LogError(ex, "Exception during CSV import"); (IsLoading, IsBusy) = (false, false); }
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        try
        {
            if (Items.Count == 0) { StatusMessage = "No items to export"; return; }
            var filePath = await _dialogService.ShowSaveFileDialogAsync("Export to Excel", $"ExpireWise_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx", "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*");
            if (string.IsNullOrEmpty(filePath)) { StatusMessage = "Export cancelled"; return; }
            (IsBusy, BusyMessage, IsLoading, StatusMessage) = (true, $"Exporting {Items.Count} items to Excel...", true, "Exporting to Excel...");
            var result = await _importExportService.ExportToExcelAsync(filePath, Items);
            if (result.IsSuccess) { StatusMessage = $"Exported {Items.Count} item(s)"; _dialogService.ShowExportSuccessDialog(System.IO.Path.GetFileName(filePath), filePath, Items.Count); }
            else { StatusMessage = $"Export failed: {result.ErrorMessage}"; _dialogService.ShowExportErrorDialog(result.ErrorMessage ?? "Unknown error"); }
        }
        catch (Exception ex) { StatusMessage = $"Export error: {ex.Message}"; _dialogService.ShowExportErrorDialog(ex.Message); }
        finally { (IsLoading, IsBusy) = (false, false); }
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        try
        {
            if (Items.Count == 0) { StatusMessage = "No items to export"; return; }
            var filePath = await _dialogService.ShowSaveFileDialogAsync("Export to CSV", $"ExpireWise_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv", "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");
            if (string.IsNullOrEmpty(filePath)) { StatusMessage = "Export cancelled"; return; }
            (IsBusy, BusyMessage, IsLoading, StatusMessage) = (true, $"Exporting {Items.Count} items to CSV...", true, "Exporting to CSV...");
            var result = await _importExportService.ExportToCsvAsync(filePath, Items);
            if (result.IsSuccess) { StatusMessage = $"Exported {Items.Count} item(s)"; _dialogService.ShowExportSuccessDialog(System.IO.Path.GetFileName(filePath), filePath, Items.Count); }
            else { StatusMessage = $"Export failed: {result.ErrorMessage}"; _dialogService.ShowExportErrorDialog(result.ErrorMessage ?? "Unknown error"); }
        }
        catch (Exception ex) { StatusMessage = $"Export error: {ex.Message}"; _dialogService.ShowExportErrorDialog(ex.Message); }
        finally { (IsLoading, IsBusy) = (false, false); }
    }

    partial void OnGroupByStoreChanged(bool value)
    {
        GroupByStoreChanged?.Invoke(value);
        if (_itemsView != null) { _itemsView.GroupDescriptions.Clear(); if (value) _itemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExpirationItem.Location))); }
    }
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new System.Threading.Timer(_ => System.Windows.Application.Current?.Dispatcher.Invoke(() => { _itemsView?.Refresh(); ApplyFilters(); }), null, 300, System.Threading.Timeout.Infinite);
    }
    partial void OnStatusFilterChanged(string value) { _itemsView?.Refresh(); ApplyFilters(); }
    
    partial void OnSelectedStoreChanged(string? value)
    {
        _itemsView?.Refresh();
        ApplyFilters();
        UpdateAnalytics();
    }

    private void InitializeItemsView()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        if (ItemsView != null)
        {
            ItemsView.Filter = FilterPredicate;
            // Group by month and year, then sort by expiration date within each month
            var monthYearGroupConverter = new Converters.MonthYearGroupConverter();
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExpirationItem.ExpiryDate), monthYearGroupConverter));
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(ExpirationItem.ExpiryDate), ListSortDirection.Ascending));
        }
        
        // Populate available stores
        UpdateAvailableStores();
    }
    
    private void UpdateAvailableStores()
    {
        var stores = Items.Select(i => i.Location).Distinct().OrderBy(s => s).ToList();
        AvailableStoreNames.Clear();
        foreach (var store in stores.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            AvailableStoreNames.Add(store!);
        }
        
        // Set first store as default if none selected
        if (string.IsNullOrEmpty(SelectedStore) && AvailableStoreNames.Any())
        {
            SelectedStore = AvailableStoreNames.First();
        }
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not ExpirationItem item || item.IsDeleted) return false;
        
        // Filter by selected store
        if (!string.IsNullOrEmpty(SelectedStore) && item.Location != SelectedStore) return false;
        
        // Filter by status
        if (StatusFilter != "All" && !(StatusFilter switch { "Good" => item.Status == ExpirationStatus.Good, "Warning" => item.Status == ExpirationStatus.Warning, "Critical" => item.Status == ExpirationStatus.Critical, "Expired" => item.Status == ExpirationStatus.Expired, _ => true })) return false;
        
        // Filter by search text
        return string.IsNullOrWhiteSpace(SearchText) || _searchService.Matches(item, SearchText);
    }

    partial void OnQuickAddIsValidChanged(bool value) => System.Windows.Application.Current?.Dispatcher.Invoke(() => AddToQueueCommand.NotifyCanExecuteChanged());

    private void ApplyFilters()
    {
        var all = Items.Where(i => !i.IsDeleted).AsEnumerable();
        
        // Filter by selected store
        if (!string.IsNullOrEmpty(SelectedStore))
        {
            all = all.Where(i => i.Location == SelectedStore);
        }
        
        // Filter by status
        if (StatusFilter != "All") 
            all = _searchService.FilterByExpirationStatus(all, StatusFilter switch { "Good" => ExpirationStatus.Good, "Warning" => ExpirationStatus.Warning, "Critical" => ExpirationStatus.Critical, "Expired" => ExpirationStatus.Expired, _ => (ExpirationStatus?)null }, ExpirationItem.WarningDaysThreshold);
        
        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText)) 
            all = _searchService.Search(all, SearchText);
        
        FilteredItems.Clear();
        foreach (var item in all.OrderBy(i => i.ExpiryDate)) 
            FilteredItems.Add(item);
        
        UpdateStoreStats(); 
        UpdateAnalytics();
    }
    
    private void UpdateStoreStats()
    {
        var storeItems = FilteredItems.ToList();
        MonthItemCount = storeItems.Count;
        MonthTotalUnits = storeItems.Sum(i => i.Units);
        CriticalCount = storeItems.Count(i => i.Status == ExpirationStatus.Critical);
        ExpiredCount = storeItems.Count(i => i.Status == ExpirationStatus.Expired);
        UpdateGlobalStatistics();
    }

    private void UpdateGlobalStatistics()
    {
        var (today, warningDate) = (DateTime.Today, DateTime.Today.AddDays(ExpirationItem.WarningDaysThreshold));
        (TotalItems, ExpiredCount, ExpiringSoonCount) = (Items.Count, Items.Count(i => i.ExpiryDate < today), Items.Count(i => i.ExpiryDate >= today && i.ExpiryDate <= warningDate));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel, "expirewise");
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow) settingsWindow.Owner = mainWindow;
            settingsWindow.Show();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to open settings window"); StatusMessage = "Failed to open settings"; }
    }
    private void LoadAvailableStores()
    {
        try
        {
            var stores = Data.DictionaryDbContext.Instance.GetAllStores();
            AvailableStores.Clear();
            AvailableStores.Add(new Data.Entities.StoreEntity { Code = "", Name = "(No Store)" });
            foreach (var store in stores.OrderBy(s => s.Code)) AvailableStores.Add(store);
            _logger?.LogInformation("Loaded {Count} stores for Quick Add", stores.Count);
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to load stores"); }
    }

    private void OnQuickAddSkuChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(QuickAddSku)) return;
        _skuLookupDebounceTimer?.Dispose();
        if (string.IsNullOrWhiteSpace(QuickAddSku)) { (QuickAddIsValidating, _quickAddLookedUpItem, QuickAddItemPreview, QuickAddIsValid) = (false, null, string.Empty, false); return; }
        (QuickAddIsValidating, QuickAddItemPreview, QuickAddIsValid) = (true, string.Empty, false);
        _skuLookupDebounceTimer = new System.Threading.Timer(_ => DebouncedLookupSku(), null, 300, System.Threading.Timeout.Infinite);
    }

    private void DebouncedLookupSku()
    {
        var sku = QuickAddSku?.Trim();
        if (string.IsNullOrWhiteSpace(sku)) { System.Windows.Application.Current.Dispatcher.Invoke(() => { (QuickAddIsValidating, _quickAddLookedUpItem, QuickAddItemPreview, QuickAddIsValid) = (false, null, string.Empty, false); }); return; }
        try
        {
            var result = _itemLookupService.Search(sku);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                QuickAddIsValidating = false;
                if (result.Found && result.Item != null) { _quickAddLookedUpItem = result.Item; QuickAddItemPreview = $"{result.Item.Description} ({result.Item.Number})"; QuickAddIsValid = true; _logger?.LogDebug("Quick Add SKU lookup success: {Sku} → {Item}", sku, result.Item.Number); }
                else { (_quickAddLookedUpItem, QuickAddItemPreview, QuickAddIsValid) = (null, string.Empty, false); _logger?.LogDebug("Quick Add SKU not found: {Sku}", sku); }
            });
        }
        catch (Exception ex) { _logger?.LogError(ex, "Quick Add SKU lookup error: {Sku}", sku); System.Windows.Application.Current.Dispatcher.Invoke(() => { (QuickAddIsValidating, _quickAddLookedUpItem, QuickAddItemPreview, QuickAddIsValid) = (false, null, string.Empty, false); }); }
    }

    [RelayCommand]
    private void ToggleQuickAdd() 
    { 
        QuickAddExpanded = !QuickAddExpanded; 
        if (QuickAddExpanded && !string.IsNullOrEmpty(SelectedStore))
        {
            QuickAddStore = AvailableStores.FirstOrDefault(s => s.Code == SelectedStore || s.Name == SelectedStore);
        }
        StatusMessage = QuickAddExpanded ? "Quick Add panel opened (Ctrl+Shift+Q)" : "Quick Add panel closed"; 
    }
    [RelayCommand]
    private void ToggleSidebar() { SidebarCollapsed = !SidebarCollapsed; _logger?.LogDebug("Sidebar {State}", SidebarCollapsed ? "collapsed" : "expanded"); }
    [RelayCommand]
    private async Task ShowQuickAddDialog()
    {
        try 
        { 
            if (!string.IsNullOrEmpty(SelectedStore))
            {
                QuickAddStore = AvailableStores.FirstOrDefault(s => s.Code == SelectedStore || s.Name == SelectedStore);
            }
            await _dialogService.ShowContentDialogAsync<object?>(new SOUP.Features.ExpireWise.Views.QuickAddDialog { DataContext = this }); 
            _logger?.LogInformation("Quick Add dialog closed"); 
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to show Quick Add dialog"); _dialogService.ShowError("Failed to open Quick Add dialog", "Error"); }
    }

    private bool CanAddToQueue() => QuickAddIsValid && _quickAddLookedUpItem != null;
    [RelayCommand(CanExecute = nameof(CanAddToQueue))]
    private void AddToQueue()
    {
        if (_quickAddLookedUpItem == null) return;
        try
        {
            var queuedItem = new ExpirationItem { ItemNumber = _quickAddLookedUpItem.Number, Description = _quickAddLookedUpItem.Description, Location = QuickAddStore?.Code ?? string.Empty, Quantity = QuickAddUnits, ExpiryDate = new DateTime(QuickAddYear, QuickAddMonth, DateTime.DaysInMonth(QuickAddYear, QuickAddMonth)) };
            QuickAddQueue.Add(queuedItem);
            HasQueuedItems = QuickAddQueue.Count > 0;
            StatusMessage = $"Added to queue: {queuedItem.ItemNumber} ({QuickAddQueue.Count} items)";
            _logger?.LogInformation("Added to queue: {Item}", queuedItem.ItemNumber);
            (QuickAddSku, _quickAddLookedUpItem, QuickAddItemPreview, QuickAddIsValid, QuickAddIsValidating) = (string.Empty, null, string.Empty, false, false);
        }
        catch (Exception ex) { _logger?.LogError(ex, "Add to queue failed"); StatusMessage = $"Failed to add to queue: {ex.Message}"; }
    }
    [RelayCommand]
    private void RemoveFromQueue(ExpirationItem? item) { if (item == null) return; QuickAddQueue.Remove(item); HasQueuedItems = QuickAddQueue.Count > 0; StatusMessage = $"Removed from queue: {item.ItemNumber}"; }
    [RelayCommand]
    private void ClearQueue() { var count = QuickAddQueue.Count; QuickAddQueue.Clear(); HasQueuedItems = false; StatusMessage = $"Cleared {count} items from queue"; }

    [RelayCommand]
    private async Task ConfirmQueue()
    {
        if (QuickAddQueue.Count == 0) return;
        try
        {
            (IsBusy, BusyMessage) = (true, $"Saving {QuickAddQueue.Count} items...");
            var (successCount, failCount) = (0, 0);
            foreach (var queuedItem in QuickAddQueue.ToList())
            {
                try
                {
                    var savedItem = await _itemService.QuickAddAsync(queuedItem.ItemNumber, queuedItem.ExpiryDate.Month, queuedItem.ExpiryDate.Year, queuedItem.Quantity, queuedItem.Description);
                    savedItem.Location = queuedItem.Location;
                    await _repository.UpdateAsync(savedItem);
                    Items.Add(savedItem);
                    successCount++;
                }
                catch (Exception ex) { _logger?.LogError(ex, "Failed to save queued item: {Item}", queuedItem.ItemNumber); failCount++; }
            }
            QuickAddQueue.Clear(); HasQueuedItems = false;
            await SaveQuickAddSettings();
            await LoadItems(); UpdateAnalytics();
            StatusMessage = failCount == 0 ? $"✓ Successfully added {successCount} items" : $"Added {successCount} items, {failCount} failed";
            _logger?.LogInformation("Queue confirmed: {Success} success, {Fail} failed", successCount, failCount);
            if (failCount == 0) QuickAddExpanded = false;
        }
        catch (Exception ex) { _logger?.LogError(ex, "Confirm queue failed"); StatusMessage = $"Failed to save items: {ex.Message}"; _ = _dialogService.ShowErrorAsync("Confirm Queue Error", $"Failed to save items: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private void ClearQuickAddForm()
    {
        (QuickAddSku, _quickAddLookedUpItem, QuickAddItemPreview, QuickAddIsValid, QuickAddIsValidating) = (string.Empty, null, string.Empty, false, false);
        if (_settings?.RememberLastLocation != true) QuickAddStore = AvailableStores.FirstOrDefault();
        if (_settings?.RememberLastExpiryDate != true) { var nextMonth = DateTime.Today.AddMonths(1); (QuickAddMonth, QuickAddYear) = (nextMonth.Month, nextMonth.Year); }
    }

    private async Task SaveQuickAddSettings()
    {
        if (_settingsService == null || _settings == null) return;
        try
        {
            if (_settings.RememberLastLocation) _settings.LastSelectedStore = QuickAddStore?.Code;
            if (_settings.RememberLastExpiryDate) { _settings.LastExpiryMonth = QuickAddMonth; _settings.LastExpiryYear = QuickAddYear; }
            _settings.QuickAddExpanded = QuickAddExpanded;
            await _settingsService.SaveSettingsAsync("ExpireWise", _settings);
            _logger?.LogDebug("Quick Add settings saved");
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to save Quick Add settings"); }
    }

    private async Task LoadQuickAddSettings()
    {
        if (_settingsService == null) return;
        try
        {
            _settings = await _settingsService.LoadSettingsAsync<SOUP.Features.ExpireWise.Models.ExpireWiseSettings>("ExpireWise") ?? SOUP.Features.ExpireWise.Models.ExpireWiseSettings.CreateDefault();
            QuickAddExpanded = _settings.QuickAddExpanded;
            QuickAddStore = (_settings.RememberLastLocation && !string.IsNullOrEmpty(_settings.LastSelectedStore)) ? AvailableStores.FirstOrDefault(s => s.Code == _settings.LastSelectedStore) ?? AvailableStores.FirstOrDefault() : AvailableStores.FirstOrDefault();
            if (_settings.RememberLastExpiryDate && _settings.LastExpiryMonth.HasValue && _settings.LastExpiryYear.HasValue) { QuickAddMonth = _settings.LastExpiryMonth.Value; QuickAddYear = _settings.LastExpiryYear.Value; }
            QuickAddUnits = _settings.DefaultUnits;
            _logger?.LogInformation("Quick Add settings loaded");
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to load Quick Add settings"); _settings = SOUP.Features.ExpireWise.Models.ExpireWiseSettings.CreateDefault(); }
    }
    public Task SaveDataOnShutdownAsync() => Task.CompletedTask;
    private bool _disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            if (_settingsService != null) _settingsService.SettingsChanged -= OnSettingsChanged;
            (_repository as IDisposable)?.Dispose();
            if (_notificationTimer != null) { if (_notificationTimerHandler != null) _notificationTimer.Tick -= _notificationTimerHandler; _notificationTimer.Stop(); (_notificationTimer, _notificationTimerHandler) = (null, null); }
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = null;
        }
        _disposed = true;
    }
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
