using LiteDB;

namespace SOUP.Data.Entities;

/// <summary>
/// LiteDB entity for store entries (store code to name/rank mapping)
/// </summary>
public class StoreEntity
{
    [BsonId]
    public string Code { get; set; } = "";
    
    public string Name { get; set; } = "";
    
    public string Rank { get; set; } = "";
}
