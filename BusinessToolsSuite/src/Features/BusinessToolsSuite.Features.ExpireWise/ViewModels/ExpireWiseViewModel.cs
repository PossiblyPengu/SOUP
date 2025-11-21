using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BusinessToolsSuite.Core.Entities.ExpireWise;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Shared.Services;
using BusinessToolsSuite.Features.ExpireWise.Views;
using BusinessToolsSuite.Infrastructure.Services.Parsers;

namespace BusinessToolsSuite.Features.ExpireWise.ViewModels;

/// <summary>
/// ViewModel for ExpireWise expiration tracking module
/// </summary>
public partial class ExpireWiseViewModel : ObservableObject
{
    private readonly IExpireWiseRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly ExpireWiseParser _parser;
    private readonly DialogService _dialogService;
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
        DialogService dialogService,
        ILogger<ExpireWiseViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _parser = new ExpireWiseParser(null); // Uses specialized parser with exact JS logic
        _dialogService = dialogService;
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

            var result = await _dialogService.ShowDialogAsync<ExpirationItem?>(dialog);

            if (result != null)
            {
                var addedItem = await _repository.AddAsync(result);
                Items.Add(addedItem);
                ApplyFilters();
                StatusMessage = $"Added item {addedItem.ItemNumber}";
                _logger?.LogInformation("Added new item {ItemNumber}", addedItem.ItemNumber);
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

            var result = await _dialogService.ShowDialogAsync<ExpirationItem?>(dialog);

            if (result != null)
            {
                var updatedItem = await _repository.UpdateAsync(result);

                // Update the item in the collection
                var index = Items.IndexOf(SelectedItem);
                if (index >= 0)
                {
                    Items[index] = updatedItem;
                }

                ApplyFilters();
                StatusMessage = $"Updated item {updatedItem.ItemNumber}";
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
        _logger?.LogInformation("Import from Excel clicked - using specialized parser (Excel date conversion)");

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select Excel file to import",
                "Excel Files", "xlsx");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                StatusMessage = "Importing from Excel (smart date detection)...";

                // Use specialized parser with exact JS logic
                var result = await _parser.ParseExcelAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    // Clear existing and add new (replacing data like JS version)
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
                    StatusMessage = $"Imported {result.Value.Count} expiration items";
                    _logger?.LogInformation("Imported {Count} items from Excel using specialized parser", result.Value.Count);
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                    _logger?.LogError("Failed to import from Excel: {Error}", result.ErrorMessage);
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
        _logger?.LogInformation("Import from CSV clicked - using specialized parser");

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select CSV file to import",
                "CSV Files", "csv");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                StatusMessage = "Importing from CSV...";

                // Use specialized parser with exact JS logic
                var result = await _parser.ParseCsvAsync(files[0]);

                if (result.IsSuccess && result.Value != null)
                {
                    // Clear existing and add new
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
                    StatusMessage = $"Imported {result.Value.Count} expiration items";
                    _logger?.LogInformation("Imported {Count} items from CSV using specialized parser", result.Value.Count);
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                    _logger?.LogError("Failed to import from CSV: {Error}", result.ErrorMessage);
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
