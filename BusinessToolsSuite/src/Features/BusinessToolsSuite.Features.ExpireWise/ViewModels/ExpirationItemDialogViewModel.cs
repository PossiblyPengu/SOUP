using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using BusinessToolsSuite.Core.Entities.ExpireWise;

namespace BusinessToolsSuite.Features.ExpireWise.ViewModels;

/// <summary>
/// ViewModel for Add/Edit Expiration Item dialog
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
        ItemNumber = string.Empty;
        Description = string.Empty;
        Location = string.Empty;
        Units = 1;
        ExpiryDate = DateTime.Today.AddMonths(1);
        Notes = string.Empty;
        Category = string.Empty;
    }

    /// <summary>
    /// Initialize for editing an existing item
    /// </summary>
    public void InitializeForEdit(ExpirationItem item)
    {
        ItemId = item.Id;
        IsEditMode = true;
        DialogTitle = "Edit Expiration Item";
        ItemNumber = item.ItemNumber;
        Description = item.Description;
        Location = item.Location ?? string.Empty;
        Units = item.Units;
        ExpiryDate = item.ExpiryDate;
        Notes = item.Notes ?? string.Empty;
        Category = item.Category ?? string.Empty;
    }

    /// <summary>
    /// Create or update the entity from the form data
    /// </summary>
public ExpirationItem ToEntity()    {        var item = new ExpirationItem        {            ItemNumber = ItemNumber,            Description = Description,            Location = string.IsNullOrWhiteSpace(Location) ? null : Location,            Units = Units,            ExpiryDate = ExpiryDate,            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,            Category = string.IsNullOrWhiteSpace(Category) ? null : Category,            Id = ItemId ?? Guid.NewGuid()        };        return item;
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
