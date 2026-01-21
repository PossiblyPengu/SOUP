using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using SOUP.Core.Entities.EssentialsBuddy;

namespace SOUP.ViewModels;

/// <summary>
/// ViewModel for Add/Edit Inventory Item dialog
/// </summary>
public partial class InventoryItemDialogViewModel : ObservableObject
{
    // Validation constants
    private const int MaxItemNumberLength = 50;
    private const int MaxDescriptionLength = 500;
    private const int MaxBinCodeLength = 50;
    private const int MaxCategoryLength = 100;

    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _binCode = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private int _quantityOnHand;

    [ObservableProperty]
    private int? _minimumThreshold;

    [ObservableProperty]
    private int? _maximumThreshold;

    [ObservableProperty]
    private DateTime _lastUpdated = DateTime.Today;

    [ObservableProperty]
    private string _dialogTitle = "Add Inventory Item";

    [ObservableProperty]
    private bool _isEditMode;

    public Guid? ItemId { get; private set; }

    public InventoryItemDialogViewModel()
    {
    }

    /// <summary>
    /// Initialize for adding a new item
    /// </summary>
    public void InitializeForAdd()
    {
        ItemId = null;
        IsEditMode = false;
        DialogTitle = "Add Inventory Item";
        ItemNumber = string.Empty;
        Description = string.Empty;
        BinCode = string.Empty;
        QuantityOnHand = 0;
        MinimumThreshold = null;
        MaximumThreshold = null;
        Category = string.Empty;
        LastUpdated = DateTime.Today;
    }

    /// <summary>
    /// Initialize for editing an existing item
    /// </summary>
    public void InitializeForEdit(InventoryItem item)
    {
        ItemId = item.Id;
        IsEditMode = true;
        DialogTitle = "Edit Inventory Item";
        ItemNumber = item.ItemNumber;
        Description = item.Description;
        BinCode = item.BinCode ?? string.Empty;
        QuantityOnHand = item.QuantityOnHand;
        MinimumThreshold = item.MinimumThreshold;
        MaximumThreshold = item.MaximumThreshold;
        Category = item.Category ?? string.Empty;
        LastUpdated = item.LastUpdated ?? DateTime.Today;
    }

    /// <summary>
    /// Create or update the entity from the form data
    /// </summary>
    public InventoryItem ToEntity()
    {
        var item = new InventoryItem
        {
            ItemNumber = ItemNumber,
            Description = Description,
            BinCode = string.IsNullOrWhiteSpace(BinCode) ? null : BinCode,
            QuantityOnHand = QuantityOnHand,
            MinimumThreshold = MinimumThreshold,
            MaximumThreshold = MaximumThreshold,
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category,
            LastUpdated = LastUpdated,
            Id = ItemId ?? Guid.NewGuid()
        };
        return item;
    }

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

        // Quantity validation
        if (QuantityOnHand < 0)
            errors.Add("Quantity on Hand cannot be negative");

        // Bin Code validation (optional but has max length)
        if (!string.IsNullOrEmpty(BinCode) && BinCode.Length > MaxBinCodeLength)
            errors.Add($"Bin Code must be {MaxBinCodeLength} characters or less");

        // Category validation (optional but has max length)
        if (!string.IsNullOrEmpty(Category) && Category.Length > MaxCategoryLength)
            errors.Add($"Category must be {MaxCategoryLength} characters or less");

        // Threshold validation
        if (MinimumThreshold.HasValue && MaximumThreshold.HasValue &&
            MinimumThreshold.Value > MaximumThreshold.Value)
        {
            errors.Add("Minimum threshold cannot be greater than maximum threshold");
        }

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
