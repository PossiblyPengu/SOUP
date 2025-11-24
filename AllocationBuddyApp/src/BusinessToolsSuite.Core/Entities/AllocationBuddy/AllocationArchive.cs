using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents an archived allocation file
/// </summary>
public class AllocationArchive : BaseEntity
{
    public required string ArchiveName { get; set; }
    public required string FileName { get; set; }
    public DateTime ArchiveDate { get; set; }
    public int TotalEntries { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public List<AllocationEntry> Entries { get; set; } = new();
}
