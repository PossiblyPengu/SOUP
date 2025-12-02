using SAP.Core.Common;
using System;

namespace SAP.Core.Entities.AllocationBuddy;

public class AllocationArchive : BaseEntity
{
    public required string Name { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    public int EntryCount { get; set; }
    public string? Notes { get; set; }
}
