using System.Collections.ObjectModel;

namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// View model for an item with its store allocations (item-sorted view).
/// </summary>
public class ItemAllocationView
{
    public string ItemNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public ObservableCollection<StoreAllocation> StoreAllocations { get; set; } = new();

    public string DisplayItem => string.IsNullOrWhiteSpace(Description)
        ? ItemNumber
        : $"{ItemNumber} - {Description}";

    public int StoreCount => StoreAllocations.Count;
}
