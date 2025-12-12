using System.Collections.Generic;
using LiteDB;

namespace SAP.Data.Entities;

/// <summary>
/// LiteDB entity for dictionary items (item number to description mapping)
/// </summary>
public class DictionaryItemEntity
{
    [BsonId]
    public string Number { get; set; } = "";
    
    public string Description { get; set; } = "";
    
    public List<string> Skus { get; set; } = new();
    
    /// <summary>
    /// Whether this item is marked as an essential (for EssentialsBuddy filtering)
    /// </summary>
    public bool IsEssential { get; set; }
    
    /// <summary>
    /// Whether this item is a private label/store brand item
    /// </summary>
    public bool IsPrivateLabel { get; set; }
    
    /// <summary>
    /// Optional tags for categorization (e.g., "essential", "seasonal", "new")
    /// </summary>
    public List<string> Tags { get; set; } = new();
}
