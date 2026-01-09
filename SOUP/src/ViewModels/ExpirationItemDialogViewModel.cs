using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Data;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.ViewModels;

/// <summary>
/// Unified ViewModel for Add/Edit Expiration Item dialog.
/// Supports single items, bulk paste, and Excel paste (SKU + Quantity tab-separated).
/// </summary>
public partial class ExpirationItemDialogViewModel : ObservableObject
{
    private const int MaxItemNumberLength = 50;
    private const int MaxDescriptionLength = 500;
    private const int MaxUnits = 999999;

    #region Input Properties

    /// <summary>
    /// Main input field - supports single SKU or multiple lines.
    /// Accepts formats: "SKU" or "SKU[tab]Qty" per line.
    /// </summary>
    [ObservableProperty]
    private string _skuInput = string.Empty;

    [ObservableProperty]
    private StoreOption? _selectedStore;

    [ObservableProperty]
    private ObservableCollection<StoreOption> _availableStores = new();

    [ObservableProperty]
    private int _defaultUnits = 1;

    [ObservableProperty]
    private int _expiryMonth = DateTime.Today.Month;

    [ObservableProperty]
    private int _expiryYear = DateTime.Today.AddMonths(1).Year;

    #endregion

    #region Parsed Items

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private ObservableCollection<ParsedSkuEntry> _parsedItems = new();

    [ObservableProperty]
    private int _totalItemCount;

    [ObservableProperty]
    private int _foundCount;

    [ObservableProperty]
    private int _notFoundCount;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool _isVerified;

    #endregion

    #region Edit Mode

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _dialogTitle = "Add Expiration Items";

    public Guid? EditItemId { get; private set; }

    #endregion

    #region Add to Dictionary Panel

    [ObservableProperty]
    private bool _showAddToDictionaryPanel;

    [ObservableProperty]
    private ParsedSkuEntry? _itemToAddToDict;

    [ObservableProperty]
    private string _newDictItemNumber = string.Empty;

    [ObservableProperty]
    private string _newDictDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DictionaryItem> _dictSuggestions = new();

    [ObservableProperty]
    private bool _showDictSuggestions;

    #endregion

    public ObservableCollection<MonthOption> AvailableMonths { get; } = new()
    {
        new(1, "January"), new(2, "February"), new(3, "March"),
        new(4, "April"), new(5, "May"), new(6, "June"),
        new(7, "July"), new(8, "August"), new(9, "September"),
        new(10, "October"), new(11, "November"), new(12, "December")
    };

    public ObservableCollection<int> AvailableYears { get; }

    public ExpirationItemDialogViewModel()
    {
        AvailableYears = new ObservableCollection<int>(
            Enumerable.Range(DateTime.Today.Year, 6));
        LoadStores();
    }

    private void LoadStores()
    {
        AvailableStores.Clear();
        AvailableStores.Add(new StoreOption { Code = "", Name = "(No Store)" });

        try
        {
            var stores = DictionaryDbContext.Instance.GetAllStores();
            foreach (var store in stores.OrderBy(s => s.Code))
            {
                AvailableStores.Add(new StoreOption { Code = store.Code, Name = store.Name });
            }
        }
        catch { }
    }

    /// <summary>
    /// Initialize for adding new items
    /// </summary>
    public void InitializeForAdd()
    {
        IsEditMode = false;
        DialogTitle = "Add Expiration Items";
        SkuInput = string.Empty;
        ParsedItems.Clear();
        ShowResults = false;
        IsVerified = false;
        StatusMessage = "Enter SKUs (one per line, or paste SKU + Qty from Excel)";
    }

    /// <summary>
    /// Initialize for editing an existing item
    /// </summary>
    public void InitializeForEdit(ExpirationItem item)
    {
        IsEditMode = true;
        DialogTitle = "Edit Expiration Item";
        EditItemId = item.Id;
        
        SkuInput = item.ItemNumber;
        DefaultUnits = item.Units;
        ExpiryMonth = item.ExpiryDate.Month;
        ExpiryYear = item.ExpiryDate.Year;
        SelectedStore = AvailableStores.FirstOrDefault(s => s.Code == item.Location) 
                       ?? AvailableStores.FirstOrDefault();

        // Auto-verify for edit mode
        ParseAndVerify();
    }

    partial void OnSkuInputChanged(string value)
    {
        // Reset verification when input changes
        IsVerified = false;
        UpdateItemCount();
    }

    private void UpdateItemCount()
    {
        if (string.IsNullOrWhiteSpace(SkuInput))
        {
            TotalItemCount = 0;
            StatusMessage = "Enter SKUs (one per line, or paste SKU + Qty from Excel)";
            return;
        }

        var lines = ParseInputLines();
        TotalItemCount = lines.Count;
        
        if (TotalItemCount == 1)
            StatusMessage = "1 item - Press Verify to check";
        else
            StatusMessage = $"{TotalItemCount} items - Press Verify to check";
    }

    /// <summary>
    /// Parse input lines, handling tab-separated SKU+Qty from Excel
    /// </summary>
    private List<(string Sku, int? Qty)> ParseInputLines()
    {
        var results = new List<(string Sku, int? Qty)>();
        
        if (string.IsNullOrWhiteSpace(SkuInput))
            return results;

        var lines = SkuInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Check for tab-separated (Excel paste) or multiple spaces
            var parts = trimmed.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length >= 2)
            {
                // Last part might be quantity - check if it's a number
                var lastPart = parts[^1].Trim();
                if (int.TryParse(lastPart, out var qty))
                {
                    // Everything except last part is the SKU
                    var skuPart = string.Join(" ", parts[..^1]);
                    results.Add((skuPart, qty));
                }
                else
                {
                    // No quantity found, whole thing is SKU
                    results.Add((parts[0], null));
                }
            }
            else
            {
                // Just SKU, use default units
                results.Add((trimmed, null));
            }
        }

        return results;
    }

    /// <summary>
    /// Verify all SKUs against the dictionary
    /// </summary>
    [RelayCommand]
    private void ParseAndVerify()
    {
        ParsedItems.Clear();
        FoundCount = 0;
        NotFoundCount = 0;

        var lines = ParseInputLines();
        
        foreach (var (sku, qty) in lines)
        {
            var dictItemBySku = InternalItemDictionary.FindBySku(sku);
            var dictItemByNumber = InternalItemDictionary.FindByNumber(sku);
            var dictItem = dictItemBySku ?? dictItemByNumber;

            var entry = new ParsedSkuEntry
            {
                InputSku = sku,
                ItemNumber = dictItem?.Number ?? sku,
                Description = dictItem?.Description ?? "Not in database",
                Units = qty ?? DefaultUnits,
                Found = dictItem != null,
                CanAddSkuToItem = dictItem != null && dictItemBySku == null 
                    && !string.Equals(sku, dictItem.Number, StringComparison.OrdinalIgnoreCase)
            };

            ParsedItems.Add(entry);

            if (entry.Found)
                FoundCount++;
            else
                NotFoundCount++;
        }

        ShowResults = ParsedItems.Count > 0;
        IsVerified = true;

        if (NotFoundCount == 0)
            StatusMessage = $"✓ All {FoundCount} items found in database";
        else if (FoundCount == 0)
            StatusMessage = $"⚠ {NotFoundCount} items not found";
        else
            StatusMessage = $"✓ {FoundCount} found, ⚠ {NotFoundCount} not found";
    }

    /// <summary>
    /// Build the final list of ExpirationItems to add
    /// </summary>
    public List<ExpirationItem> BuildItems()
    {
        var items = new List<ExpirationItem>();
        var expiryDate = new DateTime(ExpiryYear, ExpiryMonth, 1);
        var location = SelectedStore?.Code ?? "";

        foreach (var parsed in ParsedItems)
        {
            items.Add(new ExpirationItem
            {
                Id = IsEditMode && EditItemId.HasValue ? EditItemId.Value : Guid.NewGuid(),
                ItemNumber = parsed.ItemNumber,
                Description = parsed.Description != "Not in database" ? parsed.Description : parsed.InputSku,
                Units = parsed.Units,
                ExpiryDate = expiryDate,
                Location = location,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return items;
    }

    /// <summary>
    /// Check if the dialog can be submitted
    /// </summary>
    public bool CanSubmit => IsVerified && ParsedItems.Count > 0;

    #region Add to Dictionary

    [RelayCommand]
    private void StartAddToDict(ParsedSkuEntry item)
    {
        if (item == null || item.Found) return;
        
        ItemToAddToDict = item;
        NewDictItemNumber = string.Empty;
        NewDictDescription = string.Empty;
        DictSuggestions.Clear();
        ShowDictSuggestions = false;
        ShowAddToDictionaryPanel = true;
    }

    [RelayCommand]
    private void CancelAddToDict()
    {
        ShowAddToDictionaryPanel = false;
        ItemToAddToDict = null;
    }

    partial void OnNewDictItemNumberChanged(string value)
    {
        SearchDictSuggestions(value);
    }

    private void SearchDictSuggestions(string search)
    {
        DictSuggestions.Clear();
        if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
        {
            ShowDictSuggestions = false;
            return;
        }

        var results = InternalItemDictionary.SearchByDescription(search, 5);
        foreach (var item in results)
        {
            DictSuggestions.Add(item);
        }

        var byNumber = InternalItemDictionary.FindByNumber(search);
        if (byNumber != null && !DictSuggestions.Any(d => d.Number == byNumber.Number))
        {
            DictSuggestions.Insert(0, byNumber);
        }

        ShowDictSuggestions = DictSuggestions.Count > 0;
    }

    [RelayCommand]
    private void SelectDictSuggestion(DictionaryItem item)
    {
        if (item == null || ItemToAddToDict == null) return;

        // Link the SKU to the existing item
        var skus = item.Skus?.ToList() ?? new List<string>();
        if (!skus.Contains(ItemToAddToDict.InputSku, StringComparer.OrdinalIgnoreCase))
        {
            skus.Add(ItemToAddToDict.InputSku);
            item.Skus = skus;
            InternalItemDictionary.UpsertItem(item);
        }

        // Update the parsed entry
        ItemToAddToDict.ItemNumber = item.Number;
        ItemToAddToDict.Description = item.Description;
        ItemToAddToDict.Found = true;
        ItemToAddToDict.CanAddSkuToItem = false;

        // Refresh counts
        RefreshCounts();
        
        ShowAddToDictionaryPanel = false;
        ShowDictSuggestions = false;
    }

    [RelayCommand]
    private void SaveNewDictItem()
    {
        if (ItemToAddToDict == null) return;
        if (string.IsNullOrWhiteSpace(NewDictItemNumber) || string.IsNullOrWhiteSpace(NewDictDescription))
            return;

        var newItem = new DictionaryItem
        {
            Number = NewDictItemNumber,
            Description = NewDictDescription,
            Skus = new List<string> { ItemToAddToDict.InputSku }
        };

        InternalItemDictionary.UpsertItem(newItem);

        // Update parsed entry
        ItemToAddToDict.ItemNumber = NewDictItemNumber;
        ItemToAddToDict.Description = NewDictDescription;
        ItemToAddToDict.Found = true;

        RefreshCounts();
        ShowAddToDictionaryPanel = false;
    }

    [RelayCommand]
    private void AddSkuToItem(ParsedSkuEntry entry)
    {
        if (entry == null || !entry.Found || !entry.CanAddSkuToItem) return;

        var existingItem = InternalItemDictionary.FindByNumber(entry.ItemNumber);
        if (existingItem == null) return;

        var skus = existingItem.Skus?.ToList() ?? new List<string>();
        if (!skus.Contains(entry.InputSku, StringComparer.OrdinalIgnoreCase))
        {
            skus.Add(entry.InputSku);
            existingItem.Skus = skus;
            InternalItemDictionary.UpsertItem(existingItem);
        }

        entry.CanAddSkuToItem = false;
        entry.SkuWasAdded = true;
    }

    private void RefreshCounts()
    {
        FoundCount = ParsedItems.Count(p => p.Found);
        NotFoundCount = ParsedItems.Count(p => !p.Found);

        if (NotFoundCount == 0)
            StatusMessage = $"✓ All {FoundCount} items found in database";
        else if (FoundCount == 0)
            StatusMessage = $"⚠ {NotFoundCount} items not found";
        else
            StatusMessage = $"✓ {FoundCount} found, ⚠ {NotFoundCount} not found";
    }

    #endregion
}

/// <summary>
/// Represents a parsed SKU entry from input
/// </summary>
public partial class ParsedSkuEntry : ObservableObject
{
    [ObservableProperty]
    private string _inputSku = string.Empty;

    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private int _units = 1;

    [ObservableProperty]
    private bool _found;

    [ObservableProperty]
    private bool _canAddSkuToItem;

    [ObservableProperty]
    private bool _skuWasAdded;
}

/// <summary>
/// Simple store option for dropdown
/// </summary>
public class StoreOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Code} - {Name}";
}

/// <summary>
/// Month option for dropdown
/// </summary>
public class MonthOption
{
    public int Value { get; }
    public string Name { get; }
    
    public MonthOption(int value, string name)
    {
        Value = value;
        Name = name;
    }
}
