namespace SOUP.Features.OrderLog.Constants;

/// <summary>
/// Centralized color constants for OrderLog feature.
/// Eliminates magic string color codes throughout the codebase.
/// </summary>
public static class OrderLogColors
{
    // Default colors for new items
    public const string DefaultOrder = "#B56576";
    public const string DefaultNote = "#FFD700";

    // Status colors (matches OrderItem.UpdateStatusColor)
    public const string StatusNotReady = "#FF4444";       // Red
    public const string StatusOnDeck = "#FFD700";         // Yellow/Gold
    public const string StatusInProgress = "#4CAF50";     // Green

    // Additional predefined note colors
    public const string NoteYellow = "#FFD700";
    public const string NotePink = "#FFB6C1";
    public const string NoteBlue = "#87CEEB";
    public const string NoteGreen = "#90EE90";
    public const string NotePurple = "#DDA0DD";
    public const string NoteOrange = "#FFB347";
}
