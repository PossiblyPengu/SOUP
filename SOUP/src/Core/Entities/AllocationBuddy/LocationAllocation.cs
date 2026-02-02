using System.Collections.ObjectModel;

namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a store location with its allocated items.
/// </summary>
public class LocationAllocation
{
    public string Location { get; set; } = string.Empty;
    public string? LocationName { get; set; }

    public string DisplayLocation
    {
        get
        {
            // If no name or name equals code, just show code
            if (string.IsNullOrWhiteSpace(LocationName) ||
                LocationName.Equals(Location, StringComparison.OrdinalIgnoreCase))
            {
                return Location;
            }
            // Show both: "101 - Downtown Store"
            return $"{Location} - {LocationName}";
        }
    }

    public ObservableCollection<ItemAllocation> Items { get; } = new();
    public bool IsActive { get; set; } = true;
}
