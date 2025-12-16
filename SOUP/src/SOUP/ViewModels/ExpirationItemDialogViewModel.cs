using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Data;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.ViewModels;

/// <summary>
/// Simple store option for the location dropdown
/// </summary>
public class StoreOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Code} - {Name}";
}

/// <summary>
/// ViewModel for Add/Edit Expiration Item dialog with dictionary autocomplete
/// </summary>
public partial class ExpirationItemDialogViewModel : ObservableObject
{
    // Validation constants
    private const int MaxItemNumberLength = 50;
    private const int MaxDescriptionLength = 500;
    private const int MaxNotesLength = 1000;
    private const int MaxUnits = 999999;

    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private StoreOption? _selectedStore;

    [ObservableProperty]
    private ObservableCollection<StoreOption> _availableStores = new();

    [ObservableProperty]
    private int _units = 1;

    [ObservableProperty]
    private DateTime _expiryDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

    [ObservableProperty]
    private int _expiryMonth = DateTime.Today.AddMonths(1).Month;

    [ObservableProperty]
    private int _expiryYear = DateTime.Today.AddMonths(1).Year;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleItemMode))]
    [NotifyPropertyChangedFor(nameof(IsBulkMode))]
    private int _selectedTabIndex = 0;

    // Bulk input properties
    [ObservableProperty]
    private string _bulkSkuInput = string.Empty;

    [ObservableProperty]
    private StoreOption? _bulkSelectedStore;

    [ObservableProperty]
    private int _bulkExpiryMonth = DateTime.Today.AddMonths(1).Month;

    [ObservableProperty]
    private int _bulkExpiryYear = DateTime.Today.AddMonths(1).Year;

    [ObservableProperty]
    private int _bulkUnits = 1;

    [ObservableProperty]
    private int _bulkItemCount = 0;

    [ObservableProperty]
    private string _bulkLookupStatus = string.Empty;

    [ObservableProperty]
    private bool _bulkVerified = false;

    [ObservableProperty]
    private ObservableCollection<BulkSkuResult> _bulkVerificationResults = new();

    [ObservableProperty]
    private int _bulkFoundCount = 0;

    [ObservableProperty]
    private int _bulkNotFoundCount = 0;

    [ObservableProperty]
    private bool _showVerificationResults = false;

    [ObservableProperty]
    private BulkSkuResult? _selectedNotFoundItem;

    [ObservableProperty]
    private string _newItemNumber = string.Empty;

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private string _newItemSku = string.Empty;

    [ObservableProperty]
    private bool _showAddItemPanel = false;

    [ObservableProperty]
    private ObservableCollection<DictionaryItem> _addItemSuggestions = new();

    [ObservableProperty]
    private bool _showAddItemSuggestions = false;

    [ObservableProperty]
    private string _dialogTitle = "Add Expiration Item";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private ObservableCollection<DictionaryItem> _suggestions = new();

    [ObservableProperty]
    private DictionaryItem? _selectedSuggestion;

    [ObservableProperty]
    private bool _showSuggestions;

    [ObservableProperty]
    private string _lookupStatus = string.Empty;

    [ObservableProperty]
    private bool _itemFoundInDictionary;

    public Guid? ItemId { get; private set; }

    public ExpirationItemDialogViewModel()
    {
        LoadStores();
        
        // Initialize month options
        AvailableMonths = new ObservableCollection<MonthOption>
        {
            new(1, "January"), new(2, "February"), new(3, "March"),
            new(4, "April"), new(5, "May"), new(6, "June"),
            new(7, "July"), new(8, "August"), new(9, "September"),
            new(10, "October"), new(11, "November"), new(12, "December")
        };
        
        // Initialize year options (current year to +5 years)
        AvailableYears = new ObservableCollection<int>();
        var currentYear = DateTime.Today.Year;
        for (int i = currentYear; i <= currentYear + 5; i++)
        {
            AvailableYears.Add(i);
        }
    }

    public ObservableCollection<MonthOption> AvailableMonths { get; }
    public ObservableCollection<int> AvailableYears { get; }

    partial void OnExpiryMonthChanged(int value)
    {
        UpdateExpiryDate();
    }

    partial void OnExpiryYearChanged(int value)
    {
        UpdateExpiryDate();
    }

    private void UpdateExpiryDate()
    {
        ExpiryDate = new DateTime(ExpiryYear, ExpiryMonth, 1);
    }

    partial void OnBulkSkuInputChanged(string value)
    {
        UpdateBulkItemCount();
        // Reset verification when input changes
        BulkVerified = false;
        ShowVerificationResults = false;
        BulkVerificationResults.Clear();
    }

    private void UpdateBulkItemCount()
    {
        if (string.IsNullOrWhiteSpace(BulkSkuInput))
        {
            BulkItemCount = 0;
            BulkLookupStatus = string.Empty;
            BulkFoundCount = 0;
            BulkNotFoundCount = 0;
            return;
        }

        var lines = BulkSkuInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
        
        BulkItemCount = lines.Count;
        BulkLookupStatus = "Click 'Verify SKUs' to check items";
    }

    /// <summary>
    /// Verify all bulk SKUs against the dictionary
    /// </summary>
    [RelayCommand]
    private void VerifyBulkSkus()
    {
        BulkVerificationResults.Clear();
        BulkFoundCount = 0;
        BulkNotFoundCount = 0;

        if (string.IsNullOrWhiteSpace(BulkSkuInput))
        {
            BulkLookupStatus = "No SKUs to verify";
            return;
        }

        var lines = BulkSkuInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .Distinct()
            .ToList();

        foreach (var sku in lines)
        {
            // Try to find by SKU first, then by item number
            var dictItemBySku = InternalItemDictionary.FindBySku(sku);
            var dictItemByNumber = InternalItemDictionary.FindByNumber(sku);
            var dictItem = dictItemBySku ?? dictItemByNumber;
            
            // Check if the SKU should be added to an existing item
            // This is true if we found by item number but NOT by SKU, and the SKU differs from item number
            var skuCanBeAdded = dictItem != null 
                && dictItemBySku == null 
                && !string.Equals(sku, dictItem.Number, StringComparison.OrdinalIgnoreCase)
                && (dictItem.Skus == null || !dictItem.Skus.Contains(sku, StringComparer.OrdinalIgnoreCase));
            
            var result = new BulkSkuResult
            {
                Sku = sku,
                ItemNumber = dictItem?.Number ?? sku,
                Found = dictItem != null,
                Description = dictItem?.Description ?? "Not in database",
                SkuCanBeAdded = skuCanBeAdded,
                SkuWasAdded = false
            };
            BulkVerificationResults.Add(result);

            if (dictItem != null)
                BulkFoundCount++;
            else
                BulkNotFoundCount++;
        }

        ShowVerificationResults = true;
        BulkVerified = true;

        if (BulkNotFoundCount == 0)
        {
            BulkLookupStatus = $"✓ All {BulkFoundCount} items found!";
        }
        else if (BulkFoundCount == 0)
        {
            BulkLookupStatus = $"⚠ None of the {BulkNotFoundCount} items found in database";
        }
        else
        {
            BulkLookupStatus = $"✓ {BulkFoundCount} found, ⚠ {BulkNotFoundCount} not found";
        }
    }

    /// <summary>
    /// Start adding a missing item to the dictionary
    /// </summary>
    [RelayCommand]
    private void StartAddToDictionary(BulkSkuResult item)
    {
        if (item == null || item.Found) return;
        
        SelectedNotFoundItem = item;
        NewItemNumber = string.Empty;
        NewItemDescription = string.Empty;
        NewItemSku = item.Sku;
        AddItemSuggestions.Clear();
        ShowAddItemSuggestions = false;
        ShowAddItemPanel = true;
    }

    /// <summary>
    /// Add SKU to an existing item in the dictionary
    /// </summary>
    [RelayCommand]
    private void AddSkuToExistingItem(BulkSkuResult item)
    {
        if (item == null || !item.Found || !item.SkuCanBeAdded || item.SkuWasAdded) return;
        
        // Get the existing item from dictionary
        var existingItem = InternalItemDictionary.FindByNumber(item.ItemNumber);
        if (existingItem == null) return;
        
        // Add the new SKU
        var skus = existingItem.Skus?.ToList() ?? new List<string>();
        if (!skus.Contains(item.Sku, StringComparer.OrdinalIgnoreCase))
        {
            skus.Add(item.Sku);
            existingItem.Skus = skus;
            InternalItemDictionary.UpsertItem(existingItem);
            
            // Update the result to show it was added
            item.SkuCanBeAdded = false;
            item.SkuWasAdded = true;
            
            // Force UI refresh
            var index = BulkVerificationResults.IndexOf(item);
            if (index >= 0)
            {
                var updated = new BulkSkuResult
                {
                    Sku = item.Sku,
                    ItemNumber = item.ItemNumber,
                    Description = item.Description,
                    Found = true,
                    SkuCanBeAdded = false,
                    SkuWasAdded = true
                };
                BulkVerificationResults[index] = updated;
            }
        }
    }

    /// <summary>
    /// Cancel adding item to dictionary
    /// </summary>
    [RelayCommand]
    private void CancelAddToDictionary()
    {
        ShowAddItemPanel = false;
        SelectedNotFoundItem = null;
        NewItemNumber = string.Empty;
        NewItemDescription = string.Empty;
        NewItemSku = string.Empty;
        AddItemSuggestions.Clear();
        ShowAddItemSuggestions = false;
    }

    partial void OnNewItemNumberChanged(string value)
    {
        SearchForAddItemSuggestions(value);
    }

    private void SearchForAddItemSuggestions(string searchTerm)
    {
        AddItemSuggestions.Clear();
        
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            ShowAddItemSuggestions = false;
            return;
        }

        // Search by item number first
        var byNumber = InternalItemDictionary.FindByNumber(searchTerm);
        if (byNumber != null)
        {
            AddItemSuggestions.Add(byNumber);
        }

        // Then search by partial match
        var partial = InternalItemDictionary.FindItem(searchTerm);
        if (partial != null && !AddItemSuggestions.Any(s => s.Number == partial.Number))
        {
            AddItemSuggestions.Add(partial);
        }

        // Search by description
        var byDescription = InternalItemDictionary.SearchByDescription(searchTerm, 5);
        foreach (var item in byDescription)
        {
            if (!AddItemSuggestions.Any(s => s.Number == item.Number))
            {
                AddItemSuggestions.Add(item);
            }
        }

        ShowAddItemSuggestions = AddItemSuggestions.Count > 0;
    }

    /// <summary>
    /// Select an existing item to add the SKU to
    /// </summary>
    [RelayCommand]
    private void SelectAddItemSuggestion(DictionaryItem item)
    {
        if (item == null) return;
        
        NewItemNumber = item.Number;
        NewItemDescription = item.Description;
        ShowAddItemSuggestions = false;
    }

    /// <summary>
    /// Confirm adding item to dictionary
    /// </summary>
    [RelayCommand]
    private void ConfirmAddToDictionary()
    {
        if (string.IsNullOrWhiteSpace(NewItemNumber) || string.IsNullOrWhiteSpace(NewItemDescription))
            return;

        // Add to dictionary
        var newItem = new DictionaryItem
        {
            Number = NewItemNumber.Trim(),
            Description = NewItemDescription.Trim(),
            Skus = string.IsNullOrWhiteSpace(NewItemSku) 
                ? new List<string>() 
                : new List<string> { NewItemSku.Trim() }
        };
        InternalItemDictionary.UpsertItem(newItem);

        // Update the verification result
        if (SelectedNotFoundItem != null)
        {
            SelectedNotFoundItem.Found = true;
            SelectedNotFoundItem.ItemNumber = newItem.Number;
            SelectedNotFoundItem.Description = newItem.Description;
            
            // Recalculate counts
            BulkFoundCount = BulkVerificationResults.Count(r => r.Found);
            BulkNotFoundCount = BulkVerificationResults.Count(r => !r.Found);
            
            // Update status
            if (BulkNotFoundCount == 0)
            {
                BulkLookupStatus = $"✓ All {BulkFoundCount} items found!";
            }
            else
            {
                BulkLookupStatus = $"✓ {BulkFoundCount} found, ⚠ {BulkNotFoundCount} not found";
            }
            
            // Force UI refresh by replacing the item
            var index = BulkVerificationResults.IndexOf(SelectedNotFoundItem);
            if (index >= 0)
            {
                var updated = new BulkSkuResult
                {
                    Sku = SelectedNotFoundItem.Sku,
                    ItemNumber = newItem.Number,
                    Description = newItem.Description,
                    Found = true
                };
                BulkVerificationResults[index] = updated;
            }
        }

        ShowAddItemPanel = false;
        SelectedNotFoundItem = null;
        NewItemNumber = string.Empty;
        NewItemDescription = string.Empty;
        NewItemSku = string.Empty;
    }

    private void LoadStores()
    {
        AvailableStores.Clear();
        
        // Add empty option for "no location"
        AvailableStores.Add(new StoreOption { Code = "", Name = "" });
        
        var stores = InternalStoreDictionary.GetStores();
        foreach (var store in stores.OrderBy(s => s.Code))
        {
            AvailableStores.Add(new StoreOption
            {
                Code = store.Code,
                Name = store.Name
            });
        }
    }

    partial void OnSelectedStoreChanged(StoreOption? value)
    {
        if (value != null)
        {
            Location = value.Code;
        }
    }

    /// <summary>
    /// Initialize for adding a new item
    /// </summary>
    public void InitializeForAdd()
    {
        ItemId = null;
        IsEditMode = false;
        DialogTitle = "Add Expiration Item";
        SelectedTabIndex = 0;
        
        // Single item fields
        ItemNumber = string.Empty;
        Description = string.Empty;
        Location = string.Empty;
        SelectedStore = AvailableStores.FirstOrDefault();
        Units = 1;
        ExpiryMonth = DateTime.Today.AddMonths(1).Month;
        ExpiryYear = DateTime.Today.AddMonths(1).Year;
        ExpiryDate = new DateTime(ExpiryYear, ExpiryMonth, 1);
        Notes = string.Empty;
        LookupStatus = string.Empty;
        ItemFoundInDictionary = false;
        Suggestions.Clear();
        ShowSuggestions = false;
        
        // Bulk input fields
        BulkSkuInput = string.Empty;
        BulkSelectedStore = AvailableStores.FirstOrDefault();
        BulkExpiryMonth = DateTime.Today.AddMonths(1).Month;
        BulkExpiryYear = DateTime.Today.AddMonths(1).Year;
        BulkUnits = 1;
        BulkItemCount = 0;
        BulkLookupStatus = string.Empty;
        BulkVerified = false;
        BulkVerificationResults.Clear();
        BulkFoundCount = 0;
        BulkNotFoundCount = 0;
        ShowVerificationResults = false;
        ShowAddItemPanel = false;
        SelectedNotFoundItem = null;
        NewItemNumber = string.Empty;
        NewItemDescription = string.Empty;
        NewItemSku = string.Empty;
    }

    /// <summary>
    /// Initialize for editing an existing item
    /// </summary>
    public void InitializeForEdit(ExpirationItem item)
    {
        ItemId = item.Id;
        IsEditMode = true;
        DialogTitle = "Edit Expiration Item";
        SelectedTabIndex = 0; // Force single item tab for editing
        ItemNumber = item.ItemNumber;
        Description = item.Description;
        Location = item.Location ?? string.Empty;
        SelectedStore = AvailableStores.FirstOrDefault(s => s.Code == item.Location) ?? AvailableStores.FirstOrDefault();
        Units = item.Units;
        ExpiryMonth = item.ExpiryDate.Month;
        ExpiryYear = item.ExpiryDate.Year;
        ExpiryDate = new DateTime(ExpiryYear, ExpiryMonth, 1);
        Notes = item.Notes ?? string.Empty;
        LookupStatus = string.Empty;
        ItemFoundInDictionary = false;
        Suggestions.Clear();
        ShowSuggestions = false;
    }

    partial void OnItemNumberChanged(string value)
    {
        if (IsEditMode) return; // Don't search in edit mode
        
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            Suggestions.Clear();
            ShowSuggestions = false;
            LookupStatus = string.Empty;
            ItemFoundInDictionary = false;
            return;
        }

        SearchDictionary(value);
    }

    /// <summary>
    /// Search the dictionary for matching items
    /// </summary>
    private void SearchDictionary(string searchTerm)
    {
        Suggestions.Clear();

        // First try exact item number match
        var exactMatch = InternalItemDictionary.FindByNumber(searchTerm);
        if (exactMatch != null)
        {
            Suggestions.Add(exactMatch);
            LookupStatus = "✓ Item found in dictionary";
            ItemFoundInDictionary = true;
            ShowSuggestions = true;
            return;
        }

        // Try partial number match and description search
        var byNumber = InternalItemDictionary.FindItem(searchTerm);
        if (byNumber != null)
        {
            Suggestions.Add(byNumber);
        }

        // Search by description
        var byDescription = InternalItemDictionary.SearchByDescription(searchTerm, 10);
        foreach (var item in byDescription)
        {
            if (!Suggestions.Any(s => s.Number == item.Number))
            {
                Suggestions.Add(item);
            }
        }

        if (Suggestions.Count > 0)
        {
            LookupStatus = $"Found {Suggestions.Count} matching item(s)";
            ItemFoundInDictionary = true;
            ShowSuggestions = true;
        }
        else
        {
            LookupStatus = "No matches found - enter details manually";
            ItemFoundInDictionary = false;
            ShowSuggestions = false;
        }
    }

    partial void OnSelectedSuggestionChanged(DictionaryItem? value)
    {
        if (value == null) return;

        // Auto-fill the form with selected item
        IsEditMode = true; // Temporarily prevent re-search
        ItemNumber = value.Number;
        Description = value.Description;
        IsEditMode = false;
        ShowSuggestions = false;
        LookupStatus = $"✓ {value.Number} - {value.Description}";
        ItemFoundInDictionary = true;
    }

    /// <summary>
    /// Command to select a suggestion from the list
    /// </summary>
    [RelayCommand]
    private void SelectSuggestion(DictionaryItem item)
    {
        if (item == null) return;
        
        // Temporarily set edit mode to prevent re-triggering search
        var wasEditMode = IsEditMode;
        IsEditMode = true;
        ItemNumber = item.Number;
        Description = item.Description;
        IsEditMode = wasEditMode;
        ShowSuggestions = false;
        LookupStatus = $"✓ {item.Number} - {item.Description}";
        ItemFoundInDictionary = true;
    }

    /// <summary>
    /// Command to clear the form and start fresh
    /// </summary>
    [RelayCommand]
    private void ClearForm()
    {
        if (SelectedTabIndex == 0)
        {
            // Clear single item form
            ItemNumber = string.Empty;
            Description = string.Empty;
            Location = string.Empty;
            SelectedStore = AvailableStores.FirstOrDefault();
            Units = 1;
            ExpiryMonth = DateTime.Today.AddMonths(1).Month;
            ExpiryYear = DateTime.Today.AddMonths(1).Year;
            ExpiryDate = new DateTime(ExpiryYear, ExpiryMonth, 1);
            Notes = string.Empty;
            LookupStatus = string.Empty;
            ItemFoundInDictionary = false;
            Suggestions.Clear();
            ShowSuggestions = false;
        }
        else
        {
            // Clear bulk input
            BulkSkuInput = string.Empty;
            BulkSelectedStore = AvailableStores.FirstOrDefault();
            BulkExpiryMonth = DateTime.Today.AddMonths(1).Month;
            BulkExpiryYear = DateTime.Today.AddMonths(1).Year;
            BulkUnits = 1;
            BulkItemCount = 0;
            BulkLookupStatus = string.Empty;
            BulkVerified = false;
            BulkVerificationResults.Clear();
            BulkFoundCount = 0;
            BulkNotFoundCount = 0;
            ShowVerificationResults = false;
            ShowAddItemPanel = false;
            SelectedNotFoundItem = null;
        }
    }

    /// <summary>
    /// Create or update the entity from the form data
    /// </summary>
    public ExpirationItem ToEntity()
    {
        var item = new ExpirationItem
        {
            ItemNumber = ItemNumber,
            Description = Description,
            Location = string.IsNullOrWhiteSpace(Location) ? null : Location,
            Units = Units,
            ExpiryDate = ExpiryDate,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
            Id = ItemId ?? Guid.NewGuid()
        };
        return item;
    }

    /// <summary>
    /// Get bulk items to add - only returns items that were found in dictionary
    /// </summary>
    public List<ExpirationItem> GetBulkItems()
    {
        var items = new List<ExpirationItem>();
        if (!BulkVerified || BulkVerificationResults.Count == 0) return items;

        var expiryDate = new DateTime(BulkExpiryYear, BulkExpiryMonth, 1);
        var location = BulkSelectedStore?.Code;

        // Only add items that were found in the dictionary
        foreach (var result in BulkVerificationResults.Where(r => r.Found))
        {
            items.Add(new ExpirationItem
            {
                Id = Guid.NewGuid(),
                ItemNumber = result.ItemNumber,  // Use the actual item number from dictionary
                Description = result.Description,
                Location = string.IsNullOrWhiteSpace(location) ? null : location,
                Units = BulkUnits,
                ExpiryDate = expiryDate
            });
        }

        return items;
    }

    /// <summary>
    /// Get the list of SKUs that were not found in the dictionary
    /// </summary>
    public List<string> GetNotFoundSkus()
    {
        return BulkVerificationResults
            .Where(r => !r.Found)
            .Select(r => r.Sku)
            .ToList();
    }

    /// <summary>
    /// Check if bulk input is ready to save (verified and has found items)
    /// </summary>
    public bool CanSaveBulk => BulkVerified && BulkFoundCount > 0;

    /// <summary>
    /// Check if in bulk mode
    /// </summary>
    public bool IsBulkMode => SelectedTabIndex == 1;

    /// <summary>
    /// Check if in single item mode
    /// </summary>
    public bool IsSingleItemMode => SelectedTabIndex == 0;

    /// <summary>
    /// Validate the form
    /// </summary>
    public bool IsValid()
    {
        return GetValidationErrors().Count == 0;
    }

    /// <summary>
    /// Gets a list of validation errors
    /// </summary>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        // Item Number validation
        if (string.IsNullOrWhiteSpace(ItemNumber))
            errors.Add("Item Number is required");
        else if (ItemNumber.Length > MaxItemNumberLength)
            errors.Add($"Item Number must be {MaxItemNumberLength} characters or less");

        // Description validation
        if (string.IsNullOrWhiteSpace(Description))
            errors.Add("Description is required");
        else if (Description.Length > MaxDescriptionLength)
            errors.Add($"Description must be {MaxDescriptionLength} characters or less");

        // Units validation
        if (Units < 0)
            errors.Add("Units cannot be negative");
        else if (Units > MaxUnits)
            errors.Add($"Units cannot exceed {MaxUnits:N0}");

        // Notes validation (optional but has max length)
        if (!string.IsNullOrEmpty(Notes) && Notes.Length > MaxNotesLength)
            errors.Add($"Notes must be {MaxNotesLength} characters or less");

        // Expiry date validation
        if (ExpiryYear < DateTime.Today.Year - 1)
            errors.Add("Expiry year is too far in the past");
        else if (ExpiryYear > DateTime.Today.Year + 10)
            errors.Add("Expiry year is too far in the future");

        return errors;
    }

    /// <summary>
    /// Gets the first validation error message, or null if valid
    /// </summary>
    public string? ValidationError
    {
        get
        {
            var errors = GetValidationErrors();
            return errors.Count > 0 ? errors[0] : null;
        }
    }
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

/// <summary>
/// Result of a bulk SKU verification
/// </summary>
public class BulkSkuResult
{
    public string Sku { get; set; } = string.Empty;
    public string ItemNumber { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// True if the SKU was found via item number but isn't registered as a SKU for that item
    /// </summary>
    public bool SkuCanBeAdded { get; set; }
    /// <summary>
    /// True if SKU has been added to the item
    /// </summary>
    public bool SkuWasAdded { get; set; }
}
