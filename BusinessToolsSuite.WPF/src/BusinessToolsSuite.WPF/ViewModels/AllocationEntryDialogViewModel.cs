using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using BusinessToolsSuite.Core.Entities.AllocationBuddy;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// ViewModel for Add/Edit Allocation Entry dialog
/// </summary>
public partial class AllocationEntryDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _storeId = string.Empty;

    [ObservableProperty]
    private string _storeName = string.Empty;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private StoreRank _rank = StoreRank.B;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private DateTime _allocationDate = DateTime.Today;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _dialogTitle = "Add Allocation Entry";

    [ObservableProperty]
    private bool _isEditMode;

    public Guid? EntryId { get; private set; }

    public AllocationEntryDialogViewModel()
    {
    }

    /// <summary>
    /// Initialize for adding a new entry
    /// </summary>
    public void InitializeForAdd()
    {
        EntryId = null;
        IsEditMode = false;
        DialogTitle = "Add Allocation Entry";
        ItemNumber = string.Empty;
        Description = string.Empty;
        StoreId = string.Empty;
        StoreName = string.Empty;
        Quantity = 1;
        Rank = StoreRank.B;
        Category = string.Empty;
        AllocationDate = DateTime.Today;
        Notes = string.Empty;
    }

    /// <summary>
    /// Initialize for editing an existing entry
    /// </summary>
    public void InitializeForEdit(AllocationEntry entry)
    {
        EntryId = entry.Id;
        IsEditMode = true;
        DialogTitle = "Edit Allocation Entry";
        ItemNumber = entry.ItemNumber;
        Description = entry.Description;
        StoreId = entry.StoreId;
        StoreName = entry.StoreName ?? string.Empty;
        Quantity = entry.Quantity;
        Rank = entry.Rank;
        Category = entry.Category ?? string.Empty;
        AllocationDate = entry.AllocationDate ?? DateTime.Today;
        Notes = entry.Notes ?? string.Empty;
    }

    /// <summary>
    /// Create or update the entity from the form data
    /// </summary>
    public AllocationEntry ToEntity()
    {
        var entry = new AllocationEntry
        {
            ItemNumber = ItemNumber,
            Description = Description,
            StoreId = StoreId,
            StoreName = string.IsNullOrWhiteSpace(StoreName) ? null : StoreName,
            Quantity = Quantity,
            Rank = Rank,
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category,
            AllocationDate = AllocationDate,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
            Id = EntryId ?? Guid.NewGuid()
        };
        return entry;
    }

    /// <summary>
    /// Validate the form
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ItemNumber)
            && !string.IsNullOrWhiteSpace(Description)
            && !string.IsNullOrWhiteSpace(StoreId)
            && Quantity >= 0;
    }
}
