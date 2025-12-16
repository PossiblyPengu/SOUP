using SOUP.Core.Common;

namespace SOUP.Core.Entities.EssentialsBuddy;

public class MasterListItem : BaseEntity
{
    public required string Upc { get; set; }
    public required string Description { get; set; }
    public int MinThreshold { get; set; }
    public int MaxThreshold { get; set; }
    public string? Category { get; set; }
}
