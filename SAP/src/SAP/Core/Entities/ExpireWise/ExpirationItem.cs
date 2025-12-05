using SAP.Core.Common;
using System;

namespace SAP.Core.Entities.ExpireWise;

/// <summary>
/// Represents an item with expiration date tracking and status calculation.
/// </summary>
/// <remarks>
/// <para>
/// This entity tracks products by their expiration dates and calculates status
/// based on configurable thresholds. The actual expiry date is normalized to
/// the last day of the expiry month for consistent comparisons.
/// </para>
/// <para>
/// Status thresholds:
/// <list type="bullet">
/// <item><description>Critical: 7 days or less until expiration</description></item>
/// <item><description>Warning: 8-30 days until expiration</description></item>
/// <item><description>Good: More than 30 days until expiration</description></item>
/// <item><description>Expired: Past expiration date</description></item>
/// </list>
/// </para>
/// </remarks>
public class ExpirationItem : BaseEntity
{
    /// <summary>
    /// Number of days until expiry to show critical status (7 days or less).
    /// </summary>
    public const int CriticalDaysThreshold = 7;

    /// <summary>
    /// Number of days until expiry to show warning status (30 days or less).
    /// </summary>
    public const int WarningDaysThreshold = 30;

    /// <summary>
    /// Gets or sets the item number identifier.
    /// </summary>
    public string ItemNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the Universal Product Code (barcode).
    /// </summary>
    public string Upc { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the item description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the storage location.
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Gets or sets the number of units.
    /// </summary>
    public int Units { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity (alias for Units).
    /// </summary>
    public int Quantity { get => Units; set => Units = value; }
    
    /// <summary>
    /// Gets or sets the expiration date (month/year).
    /// </summary>
    public DateTime ExpiryDate { get; set; }
    
    /// <summary>
    /// Gets or sets optional notes about the item.
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Gets or sets the item category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets the current expiration status based on days until expiry.
    /// </summary>
    public ExpirationStatus Status => CalculateStatus();

    /// <summary>
    /// Gets the actual expiration date (last day of the expiry month).
    /// </summary>
    /// <remarks>
    /// Since products typically expire at the end of their labeled month,
    /// the actual expiry date is calculated as the last day of that month.
    /// </remarks>
    public DateTime ActualExpiryDate => new DateTime(ExpiryDate.Year, ExpiryDate.Month, DateTime.DaysInMonth(ExpiryDate.Year, ExpiryDate.Month));

    /// <summary>
    /// Calculates the expiration status based on days until expiry.
    /// </summary>
    /// <returns>The calculated expiration status.</returns>
    private ExpirationStatus CalculateStatus()
    {
        var daysUntilExpiry = (ActualExpiryDate - DateTime.Today).Days;

        return daysUntilExpiry switch
        {
            < 0 => ExpirationStatus.Expired,
            <= CriticalDaysThreshold => ExpirationStatus.Critical,
            <= WarningDaysThreshold => ExpirationStatus.Warning,
            _ => ExpirationStatus.Good
        };
    }

    /// <summary>
    /// Gets the number of days until expiration (negative if expired).
    /// </summary>
    public int DaysUntilExpiry => (ActualExpiryDate - DateTime.Today).Days;
    
    /// <summary>
    /// Gets a human-readable status description.
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
/// Represents the expiration status of an item based on days until expiry.
/// </summary>
public enum ExpirationStatus
{
    /// <summary>More than 30 days until expiration.</summary>
    Good,
    
    /// <summary>Between 8-30 days until expiration.</summary>
    Warning,
    
    /// <summary>7 days or less until expiration.</summary>
    Critical,
    
    /// <summary>Already past expiration date.</summary>
    Expired
}
