using System;
using SOUP.Core.Common;

namespace SOUP.Core.Entities.ExpireWise;

/// <summary>
/// Represents an archived expiration item that has already expired.
/// Maintains historical record of expired items organized by store.
/// </summary>
public class ArchivedExpirationItem : BaseEntity
{
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
    /// Gets or sets the storage location (store).
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the number of units that were archived.
    /// </summary>
    public int Units { get; set; }

    /// <summary>
    /// Gets or sets the original expiration date.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the date when this item was archived.
    /// </summary>
    public DateTime ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the item.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the item category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets the actual expiration date (last day of the expiry month).
    /// </summary>
    public DateTime ActualExpiryDate => new DateTime(ExpiryDate.Year, ExpiryDate.Month, DateTime.DaysInMonth(ExpiryDate.Year, ExpiryDate.Month));
}
