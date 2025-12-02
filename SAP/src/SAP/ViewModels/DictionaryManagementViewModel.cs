using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SAP.Infrastructure.Services.Parsers;
using SAP.Data;

namespace SAP.ViewModels;

/// <summary>
/// ViewModel for managing dictionary items and stores (using LiteDB)
/// </summary>
public partial class DictionaryManagementViewModel : ObservableObject
{
    private readonly ILogger<DictionaryManagementViewModel>? _logger;

    // Backing lists for fast access (not bound to UI)
    private List<DictionaryItem> _allItems = new();
    private List<StoreEntry> _allStores = new();

    [ObservableProperty]
    private ObservableCollection<DictionaryItem> _items = new();

    [ObservableProperty]
    private ObservableCollection<DictionaryItem> _filteredItems = new();

    [ObservableProperty]
    private ObservableCollection<StoreEntry> _stores = new();

    [ObservableProperty]
    private ObservableCollection<StoreEntry> _filteredStores = new();

    [ObservableProperty]
    private DictionaryItem? _selectedItem;

    [ObservableProperty]
    private StoreEntry? _selectedStore;

    partial void OnSelectedItemChanged(DictionaryItem? value)
    {
        if (value != null)
        {
            // Auto-populate form fields when an item is selected
            NewItemNumber = value.Number;
            NewItemDescription = value.Description;
            NewItemSkus = value.Skus != null ? string.Join(", ", value.Skus) : string.Empty;
            StatusMessage = $"Selected item {value.Number}. Modify and click Update, or click Delete.";
        }
    }

    partial void OnSelectedStoreChanged(StoreEntry? value)
    {
        if (value != null)
        {
            // Auto-populate form fields when a store is selected
            NewStoreId = value.Code;
            NewStoreName = value.Name;
            NewStoreRank = value.Rank;
            StatusMessage = $"Selected store {value.Code}. Modify and click Update, or click Delete.";
        }
    }

    [ObservableProperty]
    private string _itemSearchText = string.Empty;

    [ObservableProperty]
    private string _storeSearchText = string.Empty;

    [ObservableProperty]
    private string _newItemNumber = string.Empty;

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private string _newItemSkus = string.Empty;

    [ObservableProperty]
    private string _newStoreId = string.Empty;

    [ObservableProperty]
    private string _newStoreName = string.Empty;

    [ObservableProperty]
    private string _newStoreRank = "A";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAllItems;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    private const int PageSize = 100;

    public ObservableCollection<string> StoreRanks { get; } = new() { "AA", "A", "B", "C", "D" };

    [ObservableProperty]
    private bool _isInitialized;

    public int TotalItemCount => _allItems.Count;

    public DictionaryManagementViewModel(ILogger<DictionaryManagementViewModel>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when the settings window opens. We skip heavy loading here to avoid UI freeze.
    /// </summary>
    public Task InitializeAsync()
    {
        // Don't load dictionary on window open - defer to LoadDictionaryAsync when tab is selected
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task LoadDictionaryAsync()
    {
        if (IsLoading) return;
        
        try
        {
            IsLoading = true;
            StatusMessage = "Loading dictionary data from database...";
            _logger?.LogInformation("DictionaryManagement: Starting LoadDictionaryAsync from LiteDB");

            // Run database I/O and sorting on background thread to avoid blocking UI
            var (sortedItems, sortedStores) = await Task.Run(() =>
            {
                var loadedItems = InternalItemDictionary.GetItems();
                var loadedStores = InternalStoreDictionary.GetStores();
                // Sort on background thread
                var sorted = loadedItems.OrderBy(i => i.Number).ToList();
                var sortedS = loadedStores.OrderBy(s => s.Code).ToList();
                return (sorted, sortedS);
            }).ConfigureAwait(false);

            _logger?.LogInformation("DictionaryManagement: Loaded {ItemCount} items, {StoreCount} stores from LiteDB", sortedItems.Count, sortedStores.Count);

            // Store in backing lists (fast, not bound to UI)
            _allItems = sortedItems;
            _allStores = sortedStores;

            // Update on UI thread with pagination
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _logger?.LogInformation("DictionaryManagement: Updating UI collections");
                
                // Also keep Items/Stores synced for save operations
                Items.Clear();
                foreach (var item in _allItems) Items.Add(item);
                
                Stores.Clear();
                foreach (var store in _allStores) Stores.Add(store);

                // Calculate pages
                TotalPages = Math.Max(1, (int)Math.Ceiling(_allItems.Count / (double)PageSize));
                CurrentPage = 1;

                // Show first page of items
                UpdateDisplayedItems();
                
                // Show all stores (clear and add to existing collection)
                FilteredStores.Clear();
                foreach (var store in _allStores) FilteredStores.Add(store);
                
                _logger?.LogInformation("DictionaryManagement: FilteredItems count = {Count}, FilteredStores count = {StoreCount}", 
                    FilteredItems.Count, FilteredStores.Count);
            }, System.Windows.Threading.DispatcherPriority.Normal);

            IsInitialized = true;
            OnPropertyChanged(nameof(TotalItemCount));
            StatusMessage = $"Loaded {_allItems.Count} items and {_allStores.Count} stores from database";

            _logger?.LogInformation("DictionaryManagement: LoadDictionaryAsync complete");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading dictionary: {ex.Message}";
            _logger?.LogError(ex, "Failed to load dictionary from LiteDB");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveDictionaryAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving dictionary data to database...";

            await Task.Run(() =>
            {
                // Save items to LiteDB
                InternalItemDictionary.SaveItems(_allItems);
                
                // Save stores to LiteDB
                InternalStoreDictionary.SaveStores(_allStores);
            });

            StatusMessage = $"Saved {_allItems.Count} items and {_allStores.Count} stores to database";
            _logger?.LogInformation("Saved dictionary to LiteDB: {ItemCount} items, {StoreCount} stores", _allItems.Count, _allStores.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving dictionary: {ex.Message}";
            _logger?.LogError(ex, "Failed to save dictionary to LiteDB");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewItemNumber))
            {
                StatusMessage = "Please enter an item number";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewItemDescription))
            {
                StatusMessage = "Please enter a description";
                return;
            }

            // Check if item already exists
            if (_allItems.Any(i => i.Number.Equals(NewItemNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"Item {NewItemNumber} already exists";
                return;
            }

            var skus = string.IsNullOrWhiteSpace(NewItemSkus)
                ? new System.Collections.Generic.List<string>()
                : NewItemSkus.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

            var newItem = new DictionaryItem
            {
                Number = NewItemNumber.Trim(),
                Description = NewItemDescription.Trim(),
                Skus = skus
            };

            // Save to LiteDB immediately
            InternalItemDictionary.UpsertItem(newItem);

            _allItems.Add(newItem);
            _allItems = _allItems.OrderBy(i => i.Number).ToList();
            SyncItemsFromAllItems();
            UpdateDisplayedItems();

            StatusMessage = $"Added item {newItem.Number} (saved to database)";
            _logger?.LogInformation("Added item {Number} to LiteDB", newItem.Number);

            // Clear input fields
            NewItemNumber = string.Empty;
            NewItemDescription = string.Empty;
            NewItemSkus = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding item: {ex.Message}";
            _logger?.LogError(ex, "Failed to add item");
        }
    }

    [RelayCommand]
    private void EditItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to edit";
            return;
        }

        // Populate edit fields
        NewItemNumber = SelectedItem.Number;
        NewItemDescription = SelectedItem.Description;
        NewItemSkus = SelectedItem.Skus != null ? string.Join(", ", SelectedItem.Skus) : string.Empty;

        StatusMessage = $"Editing item {SelectedItem.Number}. Modify fields and click Update.";
    }

    [RelayCommand]
    private void UpdateItem()
    {
        try
        {
            if (SelectedItem == null)
            {
                StatusMessage = "Please select an item to update";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewItemDescription))
            {
                StatusMessage = "Please enter a description";
                return;
            }

            var skus = string.IsNullOrWhiteSpace(NewItemSkus)
                ? new System.Collections.Generic.List<string>()
                : NewItemSkus.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

            SelectedItem.Description = NewItemDescription.Trim();
            SelectedItem.Skus = skus;

            // Save to LiteDB immediately
            InternalItemDictionary.UpsertItem(SelectedItem);

            UpdateDisplayedItems();
            StatusMessage = $"Updated item {SelectedItem.Number} (saved to database)";
            _logger?.LogInformation("Updated item {Number} in LiteDB", SelectedItem.Number);

            // Clear input fields
            NewItemNumber = string.Empty;
            NewItemDescription = string.Empty;
            NewItemSkus = string.Empty;
            SelectedItem = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating item: {ex.Message}";
            _logger?.LogError(ex, "Failed to update item");
        }
    }

    [RelayCommand]
    private void DeleteItem()
    {
        try
        {
            if (SelectedItem == null)
            {
                StatusMessage = "Please select an item to delete";
                return;
            }

            var itemNumber = SelectedItem.Number;
            
            // Delete from LiteDB immediately
            InternalItemDictionary.DeleteItem(itemNumber);
            
            _allItems.RemoveAll(i => i.Number == itemNumber);
            SyncItemsFromAllItems();
            UpdateDisplayedItems();

            StatusMessage = $"Deleted item {itemNumber} (removed from database)";
            _logger?.LogInformation("Deleted item {Number} from LiteDB", itemNumber);

            ClearItemForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting item: {ex.Message}";
            _logger?.LogError(ex, "Failed to delete item");
        }
    }

    [RelayCommand]
    private void ClearItemForm()
    {
        NewItemNumber = string.Empty;
        NewItemDescription = string.Empty;
        NewItemSkus = string.Empty;
        SelectedItem = null;
        StatusMessage = "Form cleared. Ready to add a new item.";
    }

    [RelayCommand]
    private void AddStore()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewStoreId))
            {
                StatusMessage = "Please enter a store ID";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewStoreName))
            {
                StatusMessage = "Please enter a store name";
                return;
            }

            // Check if store already exists
            if (_allStores.Any(s => s.Code.Equals(NewStoreId.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"Store {NewStoreId} already exists";
                return;
            }

            var newStore = new StoreEntry
            {
                Code = NewStoreId.Trim(),
                Name = NewStoreName.Trim(),
                Rank = NewStoreRank
            };

            // Save to LiteDB immediately
            InternalStoreDictionary.UpsertStore(newStore);

            _allStores.Add(newStore);
            _allStores = _allStores.OrderBy(s => s.Code).ToList();
            Stores = new ObservableCollection<StoreEntry>(_allStores);
            ApplyStoreFilters();

            StatusMessage = $"Added store {newStore.Code} - {newStore.Name} (saved to database)";
            _logger?.LogInformation("Added store {Code} to LiteDB", newStore.Code);

            // Clear input fields
            NewStoreId = string.Empty;
            NewStoreName = string.Empty;
            NewStoreRank = "A";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding store: {ex.Message}";
            _logger?.LogError(ex, "Failed to add store");
        }
    }

    [RelayCommand]
    private void EditStore()
    {
        if (SelectedStore == null)
        {
            StatusMessage = "Please select a store to edit";
            return;
        }

        // Populate edit fields
        NewStoreId = SelectedStore.Code;
        NewStoreName = SelectedStore.Name;
        NewStoreRank = SelectedStore.Rank;

        StatusMessage = $"Editing store {SelectedStore.Code}. Modify fields and click Update Store.";
    }

    [RelayCommand]
    private void UpdateStore()
    {
        try
        {
            if (SelectedStore == null)
            {
                StatusMessage = "Please select a store to update";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewStoreName))
            {
                StatusMessage = "Please enter a store name";
                return;
            }

            SelectedStore.Name = NewStoreName.Trim();
            SelectedStore.Rank = NewStoreRank;

            // Save to LiteDB immediately
            InternalStoreDictionary.UpsertStore(SelectedStore);

            ApplyStoreFilters();
            StatusMessage = $"Updated store {SelectedStore.Code} (saved to database)";
            _logger?.LogInformation("Updated store {Code} in LiteDB", SelectedStore.Code);

            // Clear input fields
            NewStoreId = string.Empty;
            NewStoreName = string.Empty;
            NewStoreRank = "A";
            SelectedStore = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating store: {ex.Message}";
            _logger?.LogError(ex, "Failed to update store");
        }
    }

    [RelayCommand]
    private void DeleteStore()
    {
        try
        {
            if (SelectedStore == null)
            {
                StatusMessage = "Please select a store to delete";
                return;
            }

            var storeCode = SelectedStore.Code;
            
            // Delete from LiteDB immediately
            InternalStoreDictionary.DeleteStore(storeCode);
            
            _allStores.RemoveAll(s => s.Code == storeCode);
            Stores.Remove(SelectedStore);
            ApplyStoreFilters();

            StatusMessage = $"Deleted store {storeCode} (removed from database)";
            _logger?.LogInformation("Deleted store {Code} from LiteDB", storeCode);

            ClearStoreForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting store: {ex.Message}";
            _logger?.LogError(ex, "Failed to delete store");
        }
    }

    [RelayCommand]
    private void ClearStoreForm()
    {
        NewStoreId = string.Empty;
        NewStoreName = string.Empty;
        NewStoreRank = "A";
        SelectedStore = null;
        StatusMessage = "Form cleared. Ready to add a new store.";
    }

    partial void OnItemSearchTextChanged(string value)
    {
        CurrentPage = 1;
        UpdateDisplayedItems();
    }

    partial void OnStoreSearchTextChanged(string value)
    {
        ApplyStoreFilters();
    }

    private void UpdateDisplayedItems()
    {
        IEnumerable<DictionaryItem> filtered = _allItems;

        if (!string.IsNullOrWhiteSpace(ItemSearchText))
        {
            var search = ItemSearchText.ToLower();
            filtered = _allItems.Where(i =>
                i.Number.ToLower().Contains(search) ||
                i.Description.ToLower().Contains(search) ||
                (i.Skus != null && i.Skus.Any(s => s.ToLower().Contains(search))));
        }

        var filteredList = filtered.ToList();
        TotalPages = Math.Max(1, (int)Math.Ceiling(filteredList.Count / (double)PageSize));
        
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        var pageItems = filteredList
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // Clear and repopulate instead of replacing the collection
        FilteredItems.Clear();
        foreach (var item in pageItems)
        {
            FilteredItems.Add(item);
        }

        var searchInfo = string.IsNullOrWhiteSpace(ItemSearchText) 
            ? "" 
            : $" matching '{ItemSearchText}'";
        StatusMessage = $"Page {CurrentPage} of {TotalPages} ({filteredList.Count} items{searchInfo})";
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdateDisplayedItems();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdateDisplayedItems();
        }
    }

    [RelayCommand]
    private void FirstPage()
    {
        CurrentPage = 1;
        UpdateDisplayedItems();
    }

    [RelayCommand]
    private void LastPage()
    {
        CurrentPage = TotalPages;
        UpdateDisplayedItems();
    }

    private void ApplyStoreFilters()
    {
        var filtered = _allStores.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(StoreSearchText))
        {
            var search = StoreSearchText.ToLower();
            filtered = filtered.Where(s =>
                s.Code.ToLower().Contains(search) ||
                s.Name.ToLower().Contains(search) ||
                s.Rank.ToLower().Contains(search));
        }

        // Stores are typically small, show all
        FilteredStores = new ObservableCollection<StoreEntry>(filtered.OrderBy(s => s.Code));
    }

    private void SyncItemsFromAllItems()
    {
        Items = new ObservableCollection<DictionaryItem>(_allItems);
        OnPropertyChanged(nameof(TotalItemCount));
    }

    [RelayCommand]
    private async Task ImportItemsFromExcel()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Items Excel File",
                Filter = "Excel Files|*.xlsx;*.xls|All Files|*.*",
                DefaultExt = ".xlsx"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            StatusMessage = "Importing items from Excel...";
            int imported = 0;
            int updated = 0;

            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(openFileDialog.FileName);
                var worksheet = workbook.Worksheets.First();
                
                // Find columns by header (case-insensitive)
                var headerRow = worksheet.Row(1);
                int numberCol = -1, descCol = -1, skuCol = -1;
                
                foreach (var cell in headerRow.CellsUsed())
                {
                    var headerText = cell.GetString().ToLower().Trim();
                    if (headerText.Contains("number") || headerText == "item" || headerText == "item number" || headerText == "itemnumber")
                        numberCol = cell.Address.ColumnNumber;
                    else if (headerText.Contains("desc") || headerText == "name" || headerText == "item description")
                        descCol = cell.Address.ColumnNumber;
                    else if (headerText.Contains("sku") || headerText.Contains("upc") || headerText == "skus")
                        skuCol = cell.Address.ColumnNumber;
                }

                // Default to columns A, B, C if no headers found
                if (numberCol == -1) numberCol = 1;
                if (descCol == -1) descCol = 2;
                if (skuCol == -1) skuCol = 3;

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                
                for (int row = 2; row <= lastRow; row++)
                {
                    var itemNumber = worksheet.Cell(row, numberCol).GetString().Trim();
                    var description = worksheet.Cell(row, descCol).GetString().Trim();
                    var skuValue = skuCol > 0 ? worksheet.Cell(row, skuCol).GetString().Trim() : "";
                    
                    if (string.IsNullOrWhiteSpace(itemNumber))
                        continue;

                    // Parse SKUs (comma or semicolon separated)
                    var skus = new List<string>();
                    if (!string.IsNullOrWhiteSpace(skuValue))
                    {
                        skus = skuValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }

                    var existingItem = _allItems.FirstOrDefault(i => 
                        i.Number.Equals(itemNumber, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        existingItem.Description = description;
                        existingItem.Skus = skus;
                        InternalItemDictionary.UpsertItem(existingItem);
                        updated++;
                    }
                    else
                    {
                        var newItem = new DictionaryItem
                        {
                            Number = itemNumber,
                            Description = description,
                            Skus = skus
                        };
                        InternalItemDictionary.UpsertItem(newItem);
                        _allItems.Add(newItem);
                        imported++;
                    }
                }
            });

            _allItems = _allItems.OrderBy(i => i.Number).ToList();
            SyncItemsFromAllItems();
            UpdateDisplayedItems();
            StatusMessage = $"Imported {imported} new items, updated {updated} existing items from Excel";
            _logger?.LogInformation("Imported {Imported} new, {Updated} updated items from Excel", imported, updated);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importing items: {ex.Message}";
            _logger?.LogError(ex, "Failed to import items from Excel");
        }
    }

    [RelayCommand]
    private async Task ImportStoresFromExcel()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Stores Excel File",
                Filter = "Excel Files|*.xlsx;*.xls|All Files|*.*",
                DefaultExt = ".xlsx"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            StatusMessage = "Importing stores from Excel...";
            int imported = 0;
            int updated = 0;

            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(openFileDialog.FileName);
                var worksheet = workbook.Worksheets.First();
                
                // Find columns by header (case-insensitive)
                var headerRow = worksheet.Row(1);
                int codeCol = -1, nameCol = -1, rankCol = -1;
                
                foreach (var cell in headerRow.CellsUsed())
                {
                    var headerText = cell.GetString().ToLower().Trim();
                    
                    // Check for store code/number column (e.g., "store #", "store code", "store number", "#")
                    if (headerText.Contains("#") || headerText.Contains("code") || headerText.Contains("number") || 
                        headerText == "id" || headerText == "storeid")
                        codeCol = cell.Address.ColumnNumber;
                    // Check for name column
                    else if (headerText.Contains("name"))
                        nameCol = cell.Address.ColumnNumber;
                    // Check for rank/tier column
                    else if (headerText.Contains("rank") || headerText.Contains("grade") || headerText.Contains("tier"))
                        rankCol = cell.Address.ColumnNumber;
                }

                // Default to columns A, B, C if no headers found
                if (codeCol == -1) codeCol = 1;
                if (nameCol == -1) nameCol = 2;
                if (rankCol == -1) rankCol = 3;

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                
                for (int row = 2; row <= lastRow; row++)
                {
                    var code = worksheet.Cell(row, codeCol).GetString().Trim();
                    var name = worksheet.Cell(row, nameCol).GetString().Trim();
                    var rank = rankCol > 0 ? worksheet.Cell(row, rankCol).GetString().Trim().ToUpper() : "A";
                    
                    if (string.IsNullOrWhiteSpace(code))
                        continue;

                    // Validate rank (default to A if invalid)
                    if (string.IsNullOrWhiteSpace(rank) || !new[] { "AA", "A", "B", "C", "D" }.Contains(rank))
                        rank = "A";

                    var existingStore = _allStores.FirstOrDefault(s => 
                        s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

                    if (existingStore != null)
                    {
                        existingStore.Name = name;
                        existingStore.Rank = rank;
                        InternalStoreDictionary.UpsertStore(existingStore);
                        updated++;
                    }
                    else
                    {
                        var newStore = new StoreEntry
                        {
                            Code = code,
                            Name = name,
                            Rank = rank
                        };
                        InternalStoreDictionary.UpsertStore(newStore);
                        _allStores.Add(newStore);
                        imported++;
                    }
                }
            });

            _allStores = _allStores.OrderBy(s => s.Code).ToList();
            Stores = new ObservableCollection<StoreEntry>(_allStores);
            ApplyStoreFilters();
            StatusMessage = $"Imported {imported} new stores, updated {updated} existing stores from Excel";
            _logger?.LogInformation("Imported {Imported} new, {Updated} updated stores from Excel", imported, updated);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importing stores: {ex.Message}";
            _logger?.LogError(ex, "Failed to import stores from Excel");
        }
    }
}
