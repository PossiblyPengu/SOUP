using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.ExpireWise;

/// <summary>
/// Represents an item with expiration tracking
/// </summary>
public class ExpirationItem : BaseEntity
{
    public required string ItemNumber { get; set; }
    public required string Description { get; set; }
    public string? Location { get; set; }
    public int Units { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public string? Category { get; set; }
    public ExpirationStatus Status => CalculateStatus();

    private ExpirationStatus CalculateStatus()
    {
        var daysUntilExpiry = (ExpiryDate - DateTime.UtcNow).Days;

        return daysUntilExpiry switch
        {
            < 0 => ExpirationStatus.Expired,
            <= 7 => ExpirationStatus.Critical,
            <= 30 => ExpirationStatus.Warning,
            _ => ExpirationStatus.Good
        };
    }

    public int DaysUntilExpiry => (ExpiryDate - DateTime.UtcNow).Days;
}

/// <summary>
/// Expiration status enumeration
/// </summary>
public enum ExpirationStatus
{
    Good,
    Warning,
    Critical,
    Expired
}
