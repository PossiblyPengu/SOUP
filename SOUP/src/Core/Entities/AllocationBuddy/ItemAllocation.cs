using CommunityToolkit.Mvvm.ComponentModel;

namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents an item allocation with quantity, description, and SKU information.
/// </summary>
public partial class ItemAllocation : ObservableObject
{
    [ObservableProperty]
    private string _itemNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private string? _sKU;

    [ObservableProperty]
    private bool _isUpdated;

    /// <summary>Total quantity of this item across all locations (not including pool).</summary>
    [ObservableProperty]
    private int _totalInLocations;

    // Notify GrandTotal when TotalInLocations changes
    partial void OnTotalInLocationsChanged(int value)
    {
        OnPropertyChanged(nameof(GrandTotal));
    }

    // Notify GrandTotal when Quantity changes
    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(GrandTotal));
    }

    /// <summary>Grand total = pool quantity + all locations.</summary>
    public int GrandTotal => Quantity + TotalInLocations;
}
