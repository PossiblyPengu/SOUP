using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using BusinessToolsSuite.Core.Entities.EssentialsBuddy;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// ViewModel for Add/Edit Inventory Item dialog
/// </summary>
public partial class InventoryItemDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _binCode = string.Empty;

    [ObservableProperty]
    private int _quantityOnHand;

    [ObservableProperty]
    private int? _minimumThreshold;

    [ObservableProperty]
    private int? _maximumThreshold;

    [ObservableProperty]
    private decimal? _unitCost;

    [ObservableProperty]
    private decimal? _unitPrice;

    [ObservableProperty]
    private string _category = string.Empty;

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
        UnitCost = null;
        UnitPrice = null;
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
        UnitCost = item.UnitCost;
        UnitPrice = item.UnitPrice;
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
            UnitCost = UnitCost,
            UnitPrice = UnitPrice,
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
        return !string.IsNullOrWhiteSpace(ItemNumber)
            && !string.IsNullOrWhiteSpace(Description)
            && QuantityOnHand >= 0;
    }
}
