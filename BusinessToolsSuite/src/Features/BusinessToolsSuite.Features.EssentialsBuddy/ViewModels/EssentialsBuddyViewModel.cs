using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BusinessToolsSuite.Core.Entities.EssentialsBuddy;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Shared.Services;
using BusinessToolsSuite.Features.EssentialsBuddy.Views;
using BusinessToolsSuite.Infrastructure.Services.Parsers;

namespace BusinessToolsSuite.Features.EssentialsBuddy.ViewModels;

public partial class EssentialsBuddyViewModel : ObservableObject
{
    private readonly IEssentialsBuddyRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly EssentialsBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly ILogger<EssentialsBuddyViewModel>? _logger;

    [ObservableProperty]
    private ObservableCollection<InventoryItem> _items = new();

    [ObservableProperty]
    private ObservableCollection<InventoryItem> _filteredItems = new();

    [ObservableProperty]
    private InventoryItem? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private decimal _totalInventoryValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private bool _hasNoData = true;

    public bool HasData => !HasNoData;

    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "All",
        "Normal",
        "Low",
        "OutOfStock",
        "Overstocked"
    };

    public EssentialsBuddyViewModel(
        IEssentialsBuddyRepository repository,
        IFileImportExportService fileService,
        DialogService dialogService,
        ILogger<EssentialsBuddyViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _parser = new EssentialsBuddyParser(null); // Uses specialized parser with exact JS logic
        _dialogService = dialogService;
        _logger = logger;
    }

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
            StatusMessage = "Loading inventory items...";

            var allItems = await _repository.GetAllAsync();
            var activeItems = allItems.Where(i => !i.IsDeleted).ToList();

            Items.Clear();
            foreach (var item in activeItems.OrderBy(i => i.ItemNumber))
            {
                Items.Add(item);
            }

            TotalInventoryValue = await _repository.GetTotalInventoryValueAsync();
            ApplyFilters();
            HasNoData = Items.Count == 0;
            StatusMessage = $"Loaded {Items.Count} items | Total Value: {TotalInventoryValue:C2}";
            _logger?.LogInformation("Loaded {Count} inventory items", Items.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading items: {ex.Message}";
            _logger?.LogError(ex, "Exception while loading inventory items");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportFromExcel()
    {
        _logger?.LogInformation("Import from Excel clicked - using specialized parser (9-90* bins, aggregation)");

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select Excel file to import",
                "Excel Files", "xlsx");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                StatusMessage = "Importing from Excel (filtering 9-90* bins)...";

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
                    StatusMessage = $"Imported {result.Value.Count} unique items (9-90* bins aggregated)";
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
        _logger?.LogInformation("Import from CSV clicked - using specialized parser (9-90* bins, aggregation)");

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select CSV file to import",
                "CSV Files", "csv");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                StatusMessage = "Importing from CSV (filtering 9-90* bins)...";

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
                    StatusMessage = $"Imported {result.Value.Count} unique items (9-90* bins aggregated)";
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
            var dialogViewModel = new InventoryItemDialogViewModel();
            dialogViewModel.InitializeForEdit(SelectedItem);

            var dialog = new InventoryItemDialog
            {
                DataContext = dialogViewModel
            };

            var result = await _dialogService.ShowDialogAsync<InventoryItem?>(dialog);

            if (result != null)
            {
                var updatedItem = await _repository.UpdateAsync(result);

                // Update the item in the collection
                var index = Items.IndexOf(SelectedItem);
                if (index >= 0)
                {
                    Items[index] = updatedItem;
                }

                TotalInventoryValue = await _repository.GetTotalInventoryValueAsync();
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
                _logger?.LogInformation("Deleted inventory item {ItemNumber}", itemNumber);
                SelectedItem = null;
            }
            else
            {
                StatusMessage = $"Error deleting item: Item not found";
                _logger?.LogError("Failed to delete inventory item {ItemNumber}", itemNumber);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting inventory item");
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
            var fileName = $"EssentialsBuddy_Export_{timestamp}.xlsx";
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
            var fileName = $"EssentialsBuddy_Export_{timestamp}.csv";
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

    private void ApplyFilters()
    {
        var filtered = Items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(i =>
                i.ItemNumber.ToLower().Contains(search) ||
                i.Description.ToLower().Contains(search) ||
                (i.BinCode?.ToLower().Contains(search) ?? false) ||
                (i.Category?.ToLower().Contains(search) ?? false));
        }

        if (StatusFilter != "All")
        {
            if (Enum.TryParse<InventoryStatus>(StatusFilter, out var status))
            {
                filtered = filtered.Where(i => i.Status == status);
            }
        }

        FilteredItems.Clear();
        foreach (var item in filtered.OrderBy(i => i.ItemNumber))
        {
            FilteredItems.Add(item);
        }
    }
}
