using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAP.Core.Entities.ExpireWise;
using SAP.Data;
using SAP.Infrastructure.Services.Parsers;

namespace SAP.ViewModels;

/// <summary>
/// ViewModel for Add/Edit Expiration Item dialog with dictionary autocomplete
/// </summary>
public partial class ExpirationItemDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private int _units = 1;

    [ObservableProperty]
    private DateTime _expiryDate = DateTime.Today.AddMonths(1);

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _dialogTitle = "Add Expiration Item";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _searchText = string.Empty;

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
    }

    /// <summary>
    /// Initialize for adding a new item
    /// </summary>
    public void InitializeForAdd()
    {
        ItemId = null;
        IsEditMode = false;
        DialogTitle = "Add Expiration Item";
        SearchText = string.Empty;
        ItemNumber = string.Empty;
        Description = string.Empty;
        Location = string.Empty;
        Units = 1;
        ExpiryDate = DateTime.Today.AddMonths(1);
        Notes = string.Empty;
        Category = string.Empty;
        LookupStatus = string.Empty;
        ItemFoundInDictionary = false;
        Suggestions.Clear();
        ShowSuggestions = false;
    }

    /// <summary>
    /// Initialize for editing an existing item
    /// </summary>
    public void InitializeForEdit(ExpirationItem item)
    {
        ItemId = item.Id;
        IsEditMode = true;
        DialogTitle = "Edit Expiration Item";
        SearchText = item.ItemNumber;
        ItemNumber = item.ItemNumber;
        Description = item.Description;
        Location = item.Location ?? string.Empty;
        Units = item.Units;
        ExpiryDate = item.ExpiryDate;
        Notes = item.Notes ?? string.Empty;
        Category = item.Category ?? string.Empty;
        LookupStatus = string.Empty;
        ItemFoundInDictionary = false;
        Suggestions.Clear();
        ShowSuggestions = false;
    }

    partial void OnSearchTextChanged(string value)
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
        ItemNumber = value.Number;
        Description = value.Description;
        SearchText = value.Number;
        ShowSuggestions = false;
        LookupStatus = $"✓ Selected: {value.Number}";
        ItemFoundInDictionary = true;
    }

    /// <summary>
    /// Command to select a suggestion from the list
    /// </summary>
    [RelayCommand]
    private void SelectSuggestion(DictionaryItem item)
    {
        if (item == null) return;
        
        ItemNumber = item.Number;
        Description = item.Description;
        SearchText = item.Number;
        ShowSuggestions = false;
        LookupStatus = $"✓ Selected: {item.Number}";
        ItemFoundInDictionary = true;
    }

    /// <summary>
    /// Command to look up an item by number
    /// </summary>
    [RelayCommand]
    private void LookupItem()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return;

        var item = InternalItemDictionary.FindItem(SearchText.Trim());
        if (item != null)
        {
            ItemNumber = item.Number;
            Description = item.Description;
            LookupStatus = $"✓ Found: {item.Number}";
            ItemFoundInDictionary = true;
            ShowSuggestions = false;
        }
        else
        {
            LookupStatus = "Item not found - enter details manually";
            ItemFoundInDictionary = false;
            // Keep the search text as item number if not found
            ItemNumber = SearchText.Trim();
        }
    }

    /// <summary>
    /// Command to clear the form and start fresh
    /// </summary>
    [RelayCommand]
    private void ClearForm()
    {
        SearchText = string.Empty;
        ItemNumber = string.Empty;
        Description = string.Empty;
        Location = string.Empty;
        Units = 1;
        ExpiryDate = DateTime.Today.AddMonths(1);
        Notes = string.Empty;
        Category = string.Empty;
        LookupStatus = string.Empty;
        ItemFoundInDictionary = false;
        Suggestions.Clear();
        ShowSuggestions = false;
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
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category,
            Id = ItemId ?? Guid.NewGuid()
        };
        return item;
    }

    /// <summary>
    /// Validate the form
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ItemNumber)
            && !string.IsNullOrWhiteSpace(Description)
            && Units >= 0;
    }
}
