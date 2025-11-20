using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BusinessToolsSuite.Core.Entities.ExpireWise;
using BusinessToolsSuite.Core.Interfaces;

namespace BusinessToolsSuite.Features.ExpireWise.ViewModels;

/// <summary>
/// ViewModel for ExpireWise expiration tracking module
/// </summary>
public partial class ExpireWiseViewModel : ObservableObject
{
    private readonly IExpireWiseRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly ILogger<ExpireWiseViewModel>? _logger;

    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _items = new();

    [ObservableProperty]
    private ObservableCollection<ExpirationItem> _filteredItems = new();

    [ObservableProperty]
    private ExpirationItem? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "All",
        "Good",
        "Warning",
        "Critical",
        "Expired"
    };

    public ExpireWiseViewModel(
        IExpireWiseRepository repository,
        IFileImportExportService fileService,
        ILogger<ExpireWiseViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the view model and load data
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadItems();
    }

    [RelayCommand]
    private async Task LoadItems()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading items...";

            var allItems = await _repository.GetAllAsync();

            // Filter out soft-deleted items
            var activeItems = allItems.Where(i => !i.IsDeleted).ToList();

            Items.Clear();
            foreach (var item in activeItems.OrderBy(i => i.ExpiryDate))
            {
                Items.Add(item);
            }

            ApplyFilters();
            StatusMessage = $"Loaded {Items.Count} items";
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

    [RelayCommand]
    private void AddItem()
    {
        // TODO: Open add item dialog
        _logger?.LogInformation("Add item clicked");
    }

    [RelayCommand]
    private void EditItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to edit";
            return;
        }

        // TODO: Open edit item dialog
        _logger?.LogInformation("Edit item clicked for {ItemNumber}", SelectedItem.ItemNumber);
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
                ApplyFilters();
                StatusMessage = $"Deleted item {itemNumber}";
                _logger?.LogInformation("Deleted item {ItemNumber}", itemNumber);
                SelectedItem = null;
            }
            else
            {
                StatusMessage = $"Error deleting item: Item not found";
                _logger?.LogError("Failed to delete item {ItemNumber}", itemNumber);
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
        // TODO: Open file picker and import
        _logger?.LogInformation("Import from Excel clicked");
        StatusMessage = "Excel import coming soon...";
    }

    [RelayCommand]
    private async Task ImportFromCsv()
    {
        // TODO: Open file picker and import
        _logger?.LogInformation("Import from CSV clicked");
        StatusMessage = "CSV import coming soon...";
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
                _logger?.LogInformation("Exported {Count} items to Excel", Items.Count);
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _logger?.LogError("Failed to export to Excel: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Exception during Excel export");
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
                _logger?.LogInformation("Exported {Count} items to CSV", Items.Count);
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _logger?.LogError("Failed to export to CSV: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Exception during CSV export");
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

    partial void OnStatusFilterChanged(string value)
    {
        ApplyFilters();
    }

    /// <summary>
    /// Apply search and status filters to items
    /// </summary>
    private void ApplyFilters()
    {
        var filtered = Items.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(i =>
                i.ItemNumber.ToLower().Contains(search) ||
                i.Description.ToLower().Contains(search) ||
                (i.Location?.ToLower().Contains(search) ?? false));
        }

        // Apply status filter
        if (StatusFilter != "All")
        {
            if (Enum.TryParse<ExpirationStatus>(StatusFilter, out var status))
            {
                filtered = filtered.Where(i => i.Status == status);
            }
        }

        FilteredItems.Clear();
        foreach (var item in filtered.OrderBy(i => i.ExpiryDate))
        {
            FilteredItems.Add(item);
        }
    }
}
