using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SAP.Core.Entities.EssentialsBuddy;
using SAP.Core.Interfaces;
using SAP.Data;
using SAP.Services;
using SAP.Views.EssentialsBuddy;
using SAP.Views;
using SAP.Infrastructure.Services.Parsers;

namespace SAP.ViewModels;

public partial class EssentialsBuddyViewModel : ObservableObject
{
    private readonly IEssentialsBuddyRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly EssentialsBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
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
    private bool _essentialsOnly;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

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
        IServiceProvider serviceProvider,
        ILogger<EssentialsBuddyViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _parser = new EssentialsBuddyParser(null); // Uses specialized parser with exact JS logic
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await LoadItems();
    }

    /// <summary>
    /// Match items against the dictionary database to get descriptions and essential status.
    /// Also adds ALL essential items from the dictionary that aren't already in the list.
    /// Returns a list of unmatched items.
    /// </summary>
    private List<InventoryItem> MatchItemsAgainstDictionary(List<InventoryItem> items)
    {
        var matchedCount = 0;
        var essentialCount = 0;
        var unmatchedItems = new List<InventoryItem>();
        var existingItemNumbers = new HashSet<string>(items.Select(i => i.ItemNumber));

        foreach (var item in items)
        {
            // Try exact match
            var dictEntity = InternalItemDictionary.GetEntity(item.ItemNumber);
            
            if (dictEntity != null)
            {
                item.DictionaryMatched = true;
                item.DictionaryDescription = dictEntity.Description;
                item.IsEssential = dictEntity.IsEssential;
                matchedCount++;
                if (dictEntity.IsEssential) essentialCount++;
            }
            else
            {
                item.DictionaryMatched = false;
                unmatchedItems.Add(item);
            }
        }

        // Add ALL essential items from dictionary that aren't already in the list
        var allEssentials = InternalItemDictionary.GetAllEssentialItems();
        var addedEssentialsCount = 0;
        
        foreach (var essential in allEssentials)
        {
            if (!existingItemNumbers.Contains(essential.Number))
            {
                var newItem = new InventoryItem
                {
                    ItemNumber = essential.Number,
                    Description = essential.Description,
                    DictionaryDescription = essential.Description,
                    DictionaryMatched = true,
                    IsEssential = true,
                    QuantityOnHand = 0,
                    BinCode = "Not in bins"
                };
                items.Add(newItem);
                addedEssentialsCount++;
            }
        }

        _logger?.LogInformation("Dictionary matching: {Matched}/{Total} items matched, {Essentials} marked as essential, {Unmatched} unmatched, {AddedEssentials} essential items added from dictionary",
            matchedCount, items.Count - addedEssentialsCount, essentialCount, unmatchedItems.Count, addedEssentialsCount);
        
        return unmatchedItems;
    }

    /// <summary>
    /// Prompt user to add unmatched items to the dictionary with a detailed dialog
    /// </summary>
    private async Task PromptToAddUnmatchedItems(List<InventoryItem> unmatchedItems)
    {
        if (unmatchedItems.Count == 0)
            return;

        // Show dialog for user to edit items before adding
        var dialog = new Views.EssentialsBuddy.AddToDictionaryDialog(unmatchedItems);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        
        var result = dialog.ShowDialog();
        
        if (result == true && dialog.WasConfirmed)
        {
            var addedCount = 0;
            var essentialCount = 0;
            
            foreach (var item in dialog.Items)
            {
                // Add to dictionary with user-provided description and essential status
                var dictItem = new Infrastructure.Services.Parsers.DictionaryItem
                {
                    Number = item.ItemNumber,
                    Description = !string.IsNullOrEmpty(item.Description) ? item.Description : item.ItemNumber,
                    Skus = new List<string>()
                };
                
                InternalItemDictionary.UpsertItem(dictItem);
                
                // Set essential status
                if (item.IsEssential)
                {
                    InternalItemDictionary.SetEssential(item.ItemNumber, true);
                    essentialCount++;
                }
                
                // Update the item to show it's now matched
                item.DictionaryMatched = true;
                item.DictionaryDescription = dictItem.Description;
                addedCount++;
            }
            
            StatusMessage = $"Added {addedCount} items to dictionary ({essentialCount} essentials)";
            _logger?.LogInformation("Added {Count} items to dictionary, {Essentials} marked as essential", addedCount, essentialCount);
            
            // Refresh the view to show updated match status
            await LoadItems();
        }
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

            ApplyFilters();
            HasNoData = Items.Count == 0;
            StatusMessage = $"Loaded {Items.Count} items";
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
                    // Convert to list so we can add essential items from dictionary
                    var items = result.Value.ToList();
                    
                    // Match against dictionary for descriptions and essential status
                    var unmatchedItems = MatchItemsAgainstDictionary(items);
                    
                    // Clear existing and add new (replacing data like JS version)
                    var existing = await _repository.GetAllAsync();
                    foreach (var item in existing)
                    {
                        await _repository.DeleteAsync(item.Id);
                    }

                    foreach (var item in items)
                    {
                        await _repository.AddAsync(item);
                    }

                    await LoadItems();
                    
                    var essentialCount = items.Count(i => i.IsEssential);
                    var matchedCount = items.Count(i => i.DictionaryMatched);
                    StatusMessage = $"Imported {items.Count} items ({matchedCount} matched, {essentialCount} essentials)";
                    _logger?.LogInformation("Imported {Count} items from Excel, {Matched} matched dictionary, {Essentials} essentials", 
                        items.Count, matchedCount, essentialCount);
                    
                    // Prompt to add unmatched items to dictionary
                    if (unmatchedItems.Count > 0)
                    {
                        IsLoading = false;
                        await PromptToAddUnmatchedItems(unmatchedItems);
                    }
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
                    // Convert to list so we can add essential items from dictionary
                    var items = result.Value.ToList();
                    
                    // Match against dictionary for descriptions and essential status
                    var unmatchedItems = MatchItemsAgainstDictionary(items);
                    
                    // Clear existing and add new
                    var existing = await _repository.GetAllAsync();
                    foreach (var item in existing)
                    {
                        await _repository.DeleteAsync(item.Id);
                    }

                    foreach (var item in items)
                    {
                        await _repository.AddAsync(item);
                    }

                    await LoadItems();
                    
                    var essentialCount = items.Count(i => i.IsEssential);
                    var matchedCount = items.Count(i => i.DictionaryMatched);
                    StatusMessage = $"Imported {items.Count} items ({matchedCount} matched, {essentialCount} essentials)";
                    _logger?.LogInformation("Imported {Count} items from CSV, {Matched} matched dictionary, {Essentials} essentials", 
                        items.Count, matchedCount, essentialCount);
                    
                    // Prompt to add unmatched items to dictionary
                    if (unmatchedItems.Count > 0)
                    {
                        IsLoading = false;
                        await PromptToAddUnmatchedItems(unmatchedItems);
                    }
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

            var result = await _dialogService.ShowContentDialogAsync<InventoryItem?>(dialog);

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

    [RelayCommand]
    private void ClearData()
    {
        try
        {
            Items.Clear();
            FilteredItems.Clear();
            SearchText = string.Empty;
            StatusFilter = "All";
            StatusMessage = "All data cleared";
            _logger?.LogInformation("Cleared all EssentialsBuddy data");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear error: {ex.Message}";
            _logger?.LogError(ex, "Exception during data clear");
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

    partial void OnEssentialsOnlyChanged(bool value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = Items.AsEnumerable();

        // Hide non-essential items with zero quantity
        filtered = filtered.Where(i => i.IsEssential || i.QuantityOnHand > 0);

        // Essentials filter
        if (EssentialsOnly)
        {
            filtered = filtered.Where(i => i.IsEssential);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText;
            filtered = filtered.Where(i =>
                i.ItemNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.DictionaryDescription?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.BinCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Category?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
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
