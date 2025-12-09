using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SAP.Core.Entities.ExpireWise;
using SAP.Core.Interfaces;
using SAP.Services;
using SAP.Views.ExpireWise;
using SAP.Views;
using SAP.Infrastructure.Services.Parsers;

namespace SAP.ViewModels;

/// <summary>
/// ViewModel for the ExpireWise module, tracking product expiration dates with month-based navigation.
/// </summary>
/// <remarks>
/// <para>
/// This module provides comprehensive expiration tracking including:
/// <list type="bullet">
///   <item>Month-by-month navigation of expiring items</item>
///   <item>Visual indicators for critical (≤7 days), warning (≤30 days), and expired items</item>
///   <item>Import of expiration data from Excel or CSV files</item>
///   <item>Manual entry of expiration items</item>
///   <item>Analytics dashboard with expiration trends</item>
///   <item>Persistent storage of data between sessions</item>
/// </list>
/// </para>
/// </remarks>
public partial class ExpireWiseViewModel : ObservableObject
{
    #region Private Fields

    private readonly IExpireWiseRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly ExpireWiseParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpireWiseViewModel>? _logger;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the full collection of expiration items.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _items = new();

    /// <summary>
    /// Gets or sets the filtered items for the current month view.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _filteredItems = new();

    /// <summary>
    /// Gets or sets the available months that have expiring items.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MonthGroup> _availableMonths = new();

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
    /// Gets or sets the currently displayed month.
    /// </summary>
    [ObservableProperty]
    private DateTime _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

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
    /// Gets or sets the analytics ViewModel for the analytics dashboard.
    /// </summary>
    [ObservableProperty]
    private ExpireWiseAnalyticsViewModel _analytics = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpireWiseViewModel"/> class.
    /// </summary>
    /// <param name="repository">The repository for persisting expiration data.</param>
    /// <param name="fileService">The service for file import/export operations.</param>
    /// <param name="dialogService">The service for displaying dialogs.</param>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ExpireWiseViewModel(
        IExpireWiseRepository repository,
        IFileImportExportService fileService,
        DialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<ExpireWiseViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _parser = new ExpireWiseParser(null);
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        UpdateMonthDisplay();
    }

    /// <summary>
    /// Initialize the view model and load data
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadItems();
        UpdateAnalytics();
    }

    private void UpdateAnalytics()
    {
        Analytics.UpdateAnalytics(Items);
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
        // Build the visible month pills around the current month
        RebuildVisibleMonths();
    }

    /// <summary>
    /// Rebuilds the visible month pills centered around the current month
    /// Shows 6 months before and 6 months after current selection
    /// </summary>
    private void RebuildVisibleMonths()
    {
        AvailableMonths.Clear();

        // Show 6 months before and 6 months after current month (13 total)
        var startMonth = CurrentMonth.AddMonths(-6);
        var endMonth = CurrentMonth.AddMonths(6);

        var currentMonthIter = startMonth;
        while (currentMonthIter <= endMonth)
        {
            var itemsInMonth = Items.Where(i =>
                i.ExpiryDate.Year == currentMonthIter.Year &&
                i.ExpiryDate.Month == currentMonthIter.Month).ToList();

            AvailableMonths.Add(new MonthGroup
            {
                Month = currentMonthIter,
                DisplayName = currentMonthIter.ToString("MMMM yyyy"),
                ItemCount = itemsInMonth.Count,
                TotalUnits = itemsInMonth.Sum(i => i.Units),
                CriticalCount = itemsInMonth.Count(i => i.Status == ExpirationStatus.Critical),
                ExpiredCount = itemsInMonth.Count(i => i.Status == ExpirationStatus.Expired),
                IsCurrentMonth = currentMonthIter == CurrentMonth
            });

            currentMonthIter = currentMonthIter.AddMonths(1);
        }
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
    }

    [RelayCommand]
    private void NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
    }

    [RelayCommand]
    private void GoToMonth(MonthGroup? monthGroup)
    {
        if (monthGroup != null)
        {
            CurrentMonth = monthGroup.Month;
        }
    }

    partial void OnCurrentMonthChanged(DateTime value)
    {
        UpdateMonthDisplay();
        RebuildVisibleMonths();
        ApplyFilters();
    }

    private void UpdateMonthDisplay()
    {
        CurrentMonthDisplay = CurrentMonth.ToString("MMMM yyyy");
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
                    var addedItem = await _repository.AddAsync(item);
                    Items.Add(addedItem);
                }
                
                // Recalculate total months
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
                var updatedItem = await _repository.UpdateAsync(result[0]);
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
            foreach (var item in toDelete)
            {
                var success = await _repository.DeleteAsync(item.Id);
                if (success)
                {
                    Items.Remove(item);
                    deleted++;
                }
            }

            // Refresh views
            BuildAvailableMonths();
            ApplyFilters();
            UpdateLastSaved();

            StatusMessage = deleted > 0 ? $"Deleted {deleted} item(s)" : "No items were deleted";
            SelectedItems.Clear();
            SelectedItem = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting items: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting selected items");
        }
    }

    [RelayCommand]
    private async Task ImportFromExcel()
    {
        _logger?.LogInformation("Import from Excel clicked");

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select Excel file to import",
                "Excel Files (*.xlsx)|*.xlsx");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                StatusMessage = "Importing from Excel...";

                var result = await _parser.ParseExcelAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    var existing = await _repository.GetAllAsync();
                    foreach (var item in existing)
                    {
                        await _repository.DeleteAsync(item.Id);
                    }

                    foreach (var item in result.Value)
                    {
                        await _repository.AddAsync(item);
                    }

                    await LoadItems();
                    StatusMessage = $"Imported {result.Value.Count} items";
                    UpdateLastSaved();
                    _logger?.LogInformation("Imported {Count} items from Excel", result.Value.Count);
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

                var result = await _parser.ParseCsvAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    var existing = await _repository.GetAllAsync();
                    foreach (var item in existing)
                    {
                        await _repository.DeleteAsync(item.Id);
                    }

                    foreach (var item in result.Value)
                    {
                        await _repository.AddAsync(item);
                    }

                    await LoadItems();
                    StatusMessage = $"Imported {result.Value.Count} items";
                    UpdateLastSaved();
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
            IsLoading = true;
            StatusMessage = "Exporting to Excel...";

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"ExpireWise_Export_{timestamp}.xlsx";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var result = await _fileService.ExportToExcelAsync(Items.ToList(), filePath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {fileName}";
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
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
            IsLoading = true;
            StatusMessage = "Exporting to CSV...";

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"ExpireWise_Export_{timestamp}.csv";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var result = await _fileService.ExportToCsvAsync(Items.ToList(), filePath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {fileName}";
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        // Filter by current month
        var filtered = Items.Where(i =>
            i.ExpiryDate.Year == CurrentMonth.Year &&
            i.ExpiryDate.Month == CurrentMonth.Month);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText;
            filtered = filtered.Where(i =>
                i.ItemNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.Location?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredItems.Clear();
        // When showing filtered items for a month, sort by Location (store) then expiry date
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
        var monthItems = Items.Where(i =>
            i.ExpiryDate.Year == CurrentMonth.Year &&
            i.ExpiryDate.Month == CurrentMonth.Month).ToList();

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
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel);
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

    private static string GetDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SAP", "ExpireWise");
    }

    private static string GetDataFilePath() => Path.Combine(GetDataPath(), "session-data.json");

    /// <summary>
    /// Saves current data on application shutdown.
    /// </summary>
    public async Task SaveDataOnShutdownAsync()
    {
        if (Items.Count == 0) return;

        try
        {
            var dataPath = GetDataPath();
            Directory.CreateDirectory(dataPath);

            var data = new ExpireWiseData
            {
                SavedAt = DateTime.Now,
                Items = Items.Select(i => new SavedExpirationItem
                {
                    ItemNumber = i.ItemNumber,
                    Upc = i.Upc,
                    Description = i.Description,
                    Location = i.Location,
                    Units = i.Units,
                    ExpiryDate = i.ExpiryDate,
                    Notes = i.Notes,
                    Category = i.Category
                }).ToList()
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(GetDataFilePath(), json);

            _logger?.LogInformation("Saved ExpireWise data: {Count} items", Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save ExpireWise data");
        }
    }

    /// <summary>
    /// Loads persisted data on startup.
    /// </summary>
    public async Task LoadPersistedDataAsync()
    {
        try
        {
            var filePath = GetDataFilePath();
            if (!File.Exists(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<ExpireWiseData>(json);

            if (data?.Items == null || data.Items.Count == 0) return;

            Items.Clear();
            foreach (var saved in data.Items)
            {
                Items.Add(new ExpirationItem
                {
                    ItemNumber = saved.ItemNumber,
                    Upc = saved.Upc,
                    Description = saved.Description,
                    Location = saved.Location,
                    Units = saved.Units,
                    ExpiryDate = saved.ExpiryDate,
                    Notes = saved.Notes,
                    Category = saved.Category
                });
            }

            RebuildVisibleMonths();
            ApplyFilters();
            HasData = Items.Count > 0;
            TotalItems = Items.Count;
            TotalMonths = Items
                .Select(i => new DateTime(i.ExpiryDate.Year, i.ExpiryDate.Month, 1))
                .Distinct()
                .Count();
            BuildAvailableMonths();
            UpdateMonthStats();
            StatusMessage = $"Restored {Items.Count} items from previous session";
            _logger?.LogInformation("Loaded ExpireWise persisted data: {Count} items", Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load ExpireWise persisted data");
        }
    }

    private class ExpireWiseData
    {
        public DateTime SavedAt { get; set; }
        public List<SavedExpirationItem> Items { get; set; } = new();
    }

    private class SavedExpirationItem
    {
        public string ItemNumber { get; set; } = string.Empty;
        public string Upc { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Location { get; set; }
        public int Units { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string? Notes { get; set; }
        public string? Category { get; set; }
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
