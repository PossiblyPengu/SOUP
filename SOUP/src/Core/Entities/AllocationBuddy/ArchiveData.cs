namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Archive data structure for persisting allocation data.
/// </summary>
public class ArchiveData
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ArchivedAt { get; set; }
    public int TotalItems { get; set; }
    public int LocationCount { get; set; }
    public List<ArchivedLocation> Locations { get; set; } = new();
}
