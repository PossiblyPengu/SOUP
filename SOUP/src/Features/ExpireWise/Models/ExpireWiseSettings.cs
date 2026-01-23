using System;

namespace SOUP.Features.ExpireWise.Models;

/// <summary>
/// Settings for ExpireWise feature including sticky preferences and Quick Add state.
/// Persisted using SettingsService.
/// </summary>
public class ExpireWiseSettings
{
    #region Sticky Settings

    /// <summary>
    /// Last selected store location (saved between sessions if RememberLastLocation is true)
    /// </summary>
    public string? LastSelectedStore { get; set; }

    /// <summary>
    /// Last selected expiry month (1-12)
    /// </summary>
    public int? LastExpiryMonth { get; set; }

    /// <summary>
    /// Last selected expiry year
    /// </summary>
    public int? LastExpiryYear { get; set; }

    /// <summary>
    /// Default units/quantity for new items
    /// </summary>
    public int DefaultUnits { get; set; } = 1;

    #endregion

    #region Quick Add Preferences

    /// <summary>
    /// Whether the Quick Add panel is expanded or collapsed
    /// </summary>
    public bool QuickAddExpanded { get; set; } = false;

    /// <summary>
    /// Remember last selected location between adds
    /// </summary>
    public bool RememberLastLocation { get; set; } = true;

    /// <summary>
    /// Remember last selected expiry date between adds
    /// </summary>
    public bool RememberLastExpiryDate { get; set; } = true;

    #endregion

    #region Validation Preferences

    /// <summary>
    /// Block adding items not found in Business Central dictionary
    /// </summary>
    public bool BlockUnknownItems { get; set; } = true;

    /// <summary>
    /// Show warning dialog when attempting to add unknown items
    /// </summary>
    public bool ShowWarningForUnknownItems { get; set; } = true;

    #endregion

    #region UI Preferences

    /// <summary>
    /// Show toast notifications after adding items
    /// </summary>
    public bool ShowToastNotifications { get; set; } = true;

    /// <summary>
    /// Auto-focus search box after adding item via Quick Add
    /// </summary>
    public bool AutoFocusSearchAfterAdd { get; set; } = false;

    #endregion

    /// <summary>
    /// Create default settings instance
    /// </summary>
    public static ExpireWiseSettings CreateDefault()
    {
        var now = DateTime.Now;
        return new ExpireWiseSettings
        {
            DefaultUnits = 1,
            QuickAddExpanded = false,
            RememberLastLocation = true,
            RememberLastExpiryDate = true,
            BlockUnknownItems = true,
            ShowWarningForUnknownItems = true,
            ShowToastNotifications = true,
            AutoFocusSearchAfterAdd = false,
            // Initialize with next month as default
            LastExpiryMonth = now.Month == 12 ? 1 : now.Month + 1,
            LastExpiryYear = now.Month == 12 ? now.Year + 1 : now.Year
        };
    }
}
