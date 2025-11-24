namespace BusinessToolsSuite.Core.Common;

/// <summary>
/// Base entity class for all domain entities
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
