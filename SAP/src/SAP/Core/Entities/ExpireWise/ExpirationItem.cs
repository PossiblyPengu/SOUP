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

    /// <summary>
    /// Gets the actual expiration date (last day of the expiry month)
    /// </summary>
    public DateTime ActualExpiryDate => new DateTime(ExpiryDate.Year, ExpiryDate.Month, DateTime.DaysInMonth(ExpiryDate.Year, ExpiryDate.Month));

    private ExpirationStatus CalculateStatus()
    {
        var daysUntilExpiry = (ActualExpiryDate - DateTime.Today).Days;

        return daysUntilExpiry switch
        {
            < 0 => ExpirationStatus.Expired,
            <= 7 => ExpirationStatus.Critical,
            <= 30 => ExpirationStatus.Warning,
            _ => ExpirationStatus.Good
        };
    }

    public int DaysUntilExpiry => (ActualExpiryDate - DateTime.Today).Days;
    
    /// <summary>
    /// Get a human-readable status description
    /// </summary>
    public string StatusDescription => Status switch
    {
        ExpirationStatus.Expired => "Expired",
        ExpirationStatus.Critical => $"Expires in {DaysUntilExpiry} days",
        ExpirationStatus.Warning => $"Expires in {DaysUntilExpiry} days",
        ExpirationStatus.Good => $"{DaysUntilExpiry} days remaining",
        _ => "Unknown"
    };
}

/// <summary>
/// Status indicating how soon an item expires
/// </summary>
public enum ExpirationStatus
{
    /// <summary>More than 30 days until expiration</summary>
    Good,
    /// <summary>Between 8-30 days until expiration</summary>
    Warning,
    /// <summary>7 days or less until expiration</summary>
    Critical,
    /// <summary>Already expired</summary>
    Expired
}
