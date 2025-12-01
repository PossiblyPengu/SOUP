using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BusinessToolsSuite.Infrastructure.Services.Parsers;
using BusinessToolsSuite.WPF.Helpers;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// ViewModel for managing dictionary items and stores
/// </summary>
public partial class DictionaryManagementViewModel : ObservableObject
{
    private readonly ILogger<DictionaryManagementViewModel>? _logger;
    private readonly string _dictionaryPath;

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

    public ObservableCollection<string> StoreRanks { get; } = new() { "A", "B", "C", "D" };

    public DictionaryManagementViewModel(ILogger<DictionaryManagementViewModel>? logger = null)
    {
        _logger = logger;
        _dictionaryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "dictionaries.js");
    }

    public async Task InitializeAsync()
    {
        await LoadDictionaryAsync();
    }

    [RelayCommand]
    private async Task LoadDictionaryAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading dictionary data...";

            if (!File.Exists(_dictionaryPath))
            {
                StatusMessage = "Dictionary file not found. Creating new dictionary.";
                _logger?.LogWarning("Dictionary file not found at {Path}", _dictionaryPath);
                IsLoading = false;
                return;
            }

            // Run file I/O on background thread to avoid blocking UI
            var (items, stores) = await Task.Run(() =>
            {
                var loadedItems = DictionaryLoader.LoadItemsFromJs(_dictionaryPath);
                var loadedStores = DictionaryLoader.LoadStoresFromJs(_dictionaryPath);
                return (loadedItems, loadedStores);
            });

            // Replace collections in one operation to avoid per-item UI notifications
            Items = new ObservableCollection<DictionaryItem>(items.OrderBy(i => i.Number));
            Stores = new ObservableCollection<StoreEntry>(stores.OrderBy(s => s.Code));

            ApplyItemFilters();
            ApplyStoreFilters();

            StatusMessage = $"Loaded {Items.Count} items and {Stores.Count} stores";
            _logger?.LogInformation("Loaded {ItemCount} items and {StoreCount} stores from dictionary", Items.Count, Stores.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading dictionary: {ex.Message}";
            _logger?.LogError(ex, "Failed to load dictionary from {Path}", _dictionaryPath);
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
            StatusMessage = "Saving dictionary data...";

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_dictionaryPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder();
            sb.AppendLine("window.DICT = {");
            sb.AppendLine("  \"items\": [");

            // Write items
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"number\": \"{item.Number}\",");
                sb.AppendLine($"      \"desc\": \"{EscapeJson(item.Description)}\",");
                sb.Append("      \"sku\": [");

                if (item.Skus != null && item.Skus.Count > 0)
                {
                    sb.AppendLine();
                    for (int j = 0; j < item.Skus.Count; j++)
                    {
                        sb.Append($"        \"{item.Skus[j]}\"");
                        if (j < item.Skus.Count - 1)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }
                    sb.Append("      ]");
                }
                else
                {
                    sb.Append("]");
                }

                sb.AppendLine();
                sb.Append("    }");
                if (i < Items.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"stores\": [");

            // Write stores
            for (int i = 0; i < Stores.Count; i++)
            {
                var store = Stores[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": {store.Code},");
                sb.AppendLine($"      \"name\": \"{EscapeJson(store.Name)}\",");
                sb.AppendLine($"      \"rank\": \"{store.Rank}\"");
                sb.Append("    }");
                if (i < Stores.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("};");

            await File.WriteAllTextAsync(_dictionaryPath, sb.ToString());

            StatusMessage = $"Saved {Items.Count} items and {Stores.Count} stores successfully";
            _logger?.LogInformation("Saved dictionary to {Path}", _dictionaryPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving dictionary: {ex.Message}";
            _logger?.LogError(ex, "Failed to save dictionary to {Path}", _dictionaryPath);
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
            if (Items.Any(i => i.Number.Equals(NewItemNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
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

            Items.Add(newItem);
            ApplyItemFilters();

            StatusMessage = $"Added item {newItem.Number}";
            _logger?.LogInformation("Added item {Number}", newItem.Number);

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

            ApplyItemFilters();
            StatusMessage = $"Updated item {SelectedItem.Number}";
            _logger?.LogInformation("Updated item {Number}", SelectedItem.Number);

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
            Items.Remove(SelectedItem);
            ApplyItemFilters();

            StatusMessage = $"Deleted item {itemNumber}";
            _logger?.LogInformation("Deleted item {Number}", itemNumber);

            SelectedItem = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting item: {ex.Message}";
            _logger?.LogError(ex, "Failed to delete item");
        }
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
            if (Stores.Any(s => s.Code.Equals(NewStoreId.Trim(), StringComparison.OrdinalIgnoreCase)))
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

            Stores.Add(newStore);
            ApplyStoreFilters();

            StatusMessage = $"Added store {newStore.Code} - {newStore.Name}";
            _logger?.LogInformation("Added store {Code}", newStore.Code);

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

            ApplyStoreFilters();
            StatusMessage = $"Updated store {SelectedStore.Code}";
            _logger?.LogInformation("Updated store {Code}", SelectedStore.Code);

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
            Stores.Remove(SelectedStore);
            ApplyStoreFilters();

            StatusMessage = $"Deleted store {storeCode}";
            _logger?.LogInformation("Deleted store {Code}", storeCode);

            SelectedStore = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting store: {ex.Message}";
            _logger?.LogError(ex, "Failed to delete store");
        }
    }

    partial void OnItemSearchTextChanged(string value)
    {
        ApplyItemFilters();
    }

    partial void OnStoreSearchTextChanged(string value)
    {
        ApplyStoreFilters();
    }

    private void ApplyItemFilters()
    {
        var filtered = Items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ItemSearchText))
        {
            var search = ItemSearchText.ToLower();
            filtered = filtered.Where(i =>
                i.Number.ToLower().Contains(search) ||
                i.Description.ToLower().Contains(search) ||
                (i.Skus != null && i.Skus.Any(s => s.ToLower().Contains(search))));
        }

        // Assign a new collection to avoid many individual collection changed events
        FilteredItems = new ObservableCollection<DictionaryItem>(filtered.OrderBy(i => i.Number));
    }

    private void ApplyStoreFilters()
    {
        var filtered = Stores.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(StoreSearchText))
        {
            var search = StoreSearchText.ToLower();
            filtered = filtered.Where(s =>
                s.Code.ToLower().Contains(search) ||
                s.Name.ToLower().Contains(search) ||
                s.Rank.ToLower().Contains(search));
        }

        // Assign a new collection to avoid many individual collection changed events
        FilteredStores = new ObservableCollection<StoreEntry>(filtered.OrderBy(s => s.Code));
    }

    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
