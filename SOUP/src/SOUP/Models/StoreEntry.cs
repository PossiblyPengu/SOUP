namespace SOUP.Models;

/// <summary>
/// Represents a store entry for SwiftLabel generation.
/// </summary>
/// <remarks>
/// This model is used by SwiftLabel to generate store labels
/// with code, name, and ranking information.
/// </remarks>
public class StoreEntry
{
    /// <summary>
    /// Gets or sets the store code identifier.
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the store name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the store ranking/tier.
    /// </summary>
    public string Rank { get; set; } = string.Empty;
}
