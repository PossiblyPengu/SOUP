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
}
