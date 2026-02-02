namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Archived location data.
/// </summary>
public class ArchivedLocation
{
    public string Location { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public List<ArchivedItem> Items { get; set; } = new();
}
