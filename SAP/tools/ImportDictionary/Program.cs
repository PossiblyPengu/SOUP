using System.Text.Json;
using LiteDB;

Console.WriteLine("LiteDB Dictionary Importer");
Console.WriteLine("==========================");

var sharedPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "SAP",
    "Shared"
);

var dbPath = Path.Combine(sharedPath, "dictionaries.db");
var itemsJsonPath = Path.Combine(sharedPath, "items_import.json");

// Delete existing DB
if (File.Exists(dbPath))
{
    File.Delete(dbPath);
    Console.WriteLine($"Deleted existing database: {dbPath}");
}

// Read items JSON
Console.WriteLine($"Reading items from: {itemsJsonPath}");
var itemsJson = File.ReadAllText(itemsJsonPath);
var items = System.Text.Json.JsonSerializer.Deserialize<List<DictionaryItemEntity>>(itemsJson, new System.Text.Json.JsonSerializerOptions 
{ 
    PropertyNameCaseInsensitive = true 
});

if (items == null || items.Count == 0)
{
    Console.WriteLine("ERROR: No items found in JSON file!");
    return 1;
}

Console.WriteLine($"Loaded {items.Count} items from JSON");

// Import into LiteDB
using var db = new LiteDatabase(dbPath);
var collection = db.GetCollection<DictionaryItemEntity>("items");

// Create indexes
collection.EnsureIndex(x => x.Description);
collection.EnsureIndex(x => x.Skus);

// Insert all items
var count = collection.InsertBulk(items);
Console.WriteLine($"Inserted {count} items into LiteDB");

// Verify
var verifyCount = collection.Count();
Console.WriteLine($"Verification: {verifyCount} items in database");

// Also import stores from JS file
var jsPath = args.Length > 0 ? args[0] : @"D:\CODE\Cshp\SAP\src\SAP\Assets\dictionaries.js";
if (File.Exists(jsPath))
{
    Console.WriteLine($"\nImporting stores from: {jsPath}");
    var jsContent = File.ReadAllText(jsPath);
    
    // Extract stores section
    var storesMatch = System.Text.RegularExpressions.Regex.Match(jsContent, @"""stores"":\s*\[(.*?)\]", System.Text.RegularExpressions.RegexOptions.Singleline);
    if (storesMatch.Success)
    {
        var storesBlock = storesMatch.Groups[1].Value;
        var storePattern = new System.Text.RegularExpressions.Regex(@"\{\s*""id"":\s*(\d+),\s*""name"":\s*""([^""]+)"",\s*""rank"":\s*""([^""]+)""\s*\}");
        var storeMatches = storePattern.Matches(storesBlock);
        
        var storesCollection = db.GetCollection<StoreEntity>("stores");
        storesCollection.EnsureIndex(x => x.Name);
        storesCollection.EnsureIndex(x => x.Rank);
        
        var stores = new List<StoreEntity>();
        foreach (System.Text.RegularExpressions.Match m in storeMatches)
        {
            stores.Add(new StoreEntity
            {
                Code = m.Groups[1].Value,
                Name = m.Groups[2].Value,
                Rank = m.Groups[3].Value
            });
        }
        
        var storeCount = storesCollection.InsertBulk(stores);
        Console.WriteLine($"Inserted {storeCount} stores into LiteDB");
    }
}

Console.WriteLine($"\nDatabase created at: {dbPath}");
Console.WriteLine($"Size: {new FileInfo(dbPath).Length / 1024.0 / 1024.0:F2} MB");
Console.WriteLine("\nDone!");

return 0;

public class DictionaryItemEntity
{
    [BsonId]
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Skus { get; set; } = new();
}

public class StoreEntity
{
    [BsonId]
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rank { get; set; } = "";
}
