using SAP.Core.Common;
using System;

namespace SAP.Core.Entities.ExpireWise;

/// <summary>
/// Represents an item with expiration tracking
/// </summary>
public class ExpirationItem : BaseEntity
{
    public string ItemNumber { get; set; } = string.Empty;
    public string Upc { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int Units { get; set; }
    public int Quantity { get => Units; set => Units = value; }
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
            <= 7 => ExpirationStatus.ExpiringSoon,
            <= 30 => ExpirationStatus.ExpiringMonth,
            _ => ExpirationStatus.Fresh
        };
    }

    public int DaysUntilExpiry => (ExpiryDate - DateTime.UtcNow).Days;
}

public enum ExpirationStatus
{
    Fresh,
    ExpiringMonth,
    ExpiringSoon,
    Expired
}
