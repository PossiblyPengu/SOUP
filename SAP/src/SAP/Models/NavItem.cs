using CommunityToolkit.Mvvm.ComponentModel;

namespace SAP.Models;

/// <summary>
/// Represents a navigation item in the sidebar.
/// </summary>
public partial class NavItem : ObservableObject
{
    /// <summary>
    /// Unique identifier for the nav item (e.g., "ExpireWise", "AllocationBuddy").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name shown in the sidebar.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Short description shown below the name.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Resource key for the icon (e.g., "ExpireWiseIcon").
    /// </summary>
    public required string IconKey { get; init; }

    /// <summary>
    /// Resource key for the splash icon (e.g., "ExpireWiseSplashIcon").
    /// </summary>
    public required string SplashIconKey { get; init; }

    /// <summary>
    /// Resource key for the splash gradient (e.g., "ExpireWiseSplashGradient").
    /// </summary>
    public required string SplashGradientKey { get; init; }

    /// <summary>
    /// Resource key for the icon gradient (e.g., "ExpireWiseIconGradient").
    /// </summary>
    public required string IconGradientKey { get; init; }

    /// <summary>
    /// Resource key for the icon shadow color (e.g., "ExpireWiseIconShadowColor").
    /// </summary>
    public required string IconShadowColorKey { get; init; }

    /// <summary>
    /// Subtitle shown on the splash screen.
    /// </summary>
    public required string SplashSubtitle { get; init; }

    /// <summary>
    /// Feature badges shown on the splash screen.
    /// </summary>
    public required string[] Features { get; init; }

    /// <summary>
    /// Keyboard shortcut hint (e.g., "Alt+1").
    /// </summary>
    public required string ShortcutHint { get; init; }

    /// <summary>
    /// Whether this nav item is currently visible (based on module configuration).
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Whether this nav item is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Order index for sorting (persisted).
    /// </summary>
    public int Order { get; set; }
}
