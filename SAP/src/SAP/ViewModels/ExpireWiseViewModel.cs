using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
/// ViewModel for ExpireWise expiration tracking module with month-based navigation
/// </summary>
public partial class ExpireWiseViewModel : ObservableObject
{
    private readonly IExpireWiseRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly ExpireWiseParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpireWiseViewModel>? _logger;

    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _items = new();

    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _filteredItems = new();

    [ObservableProperty]
    private ObservableCollection<MonthGroup> _availableMonths = new();

    [ObservableProperty]
    private ExpirationItem? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DateTime _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

    [ObservableProperty]
    private string _currentMonthDisplay = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasData;

    // Summary statistics for current month
    [ObservableProperty]
    private int _monthItemCount;

    [ObservableProperty]
    private int _monthTotalUnits;

    [ObservableProperty]
    private int _criticalCount;

    [ObservableProperty]
    private int _expiredCount;

    // Overall stats
    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private int _totalMonths;

    // Autosave indicator
    [ObservableProperty]
    private string _lastSavedDisplay = string.Empty;

    // Tab navigation
    [ObservableProperty]
    private int _selectedTabIndex;

    // Analytics
    [ObservableProperty]
    private ExpireWiseAnalyticsViewModel _analytics = new();

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
            foreach (var item in activeItems.OrderBy(i => i.ExpiryDate))
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
        foreach (var item in filtered.OrderBy(i => i.ExpiryDate))
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
