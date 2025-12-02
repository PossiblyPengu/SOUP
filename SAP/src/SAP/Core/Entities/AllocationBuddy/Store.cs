using SAP.Core.Common;

namespace SAP.Core.Entities.AllocationBuddy;

public class Store : BaseEntity
{
    public required string Number { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}
