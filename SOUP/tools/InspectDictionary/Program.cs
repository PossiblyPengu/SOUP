using System;
using System.IO;
using System.Linq;
using LiteDB;

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var dbPath = Path.Combine(appData, "SOUP", "Shared", "dictionaries.db");

Console.WriteLine($"Looking for: {dbPath}");
if (!File.Exists(dbPath))
{
    Console.WriteLine("DB not found!");
    return 1;
}

Console.WriteLine($"DB size: {new FileInfo(dbPath).Length / 1024.0:F2} KB");

using var db = new LiteDatabase($"Filename={dbPath};Connection=Shared;ReadOnly=true");

// List collections
Console.WriteLine("\n--- Collections ---");
foreach (var col in db.GetCollectionNames())
{
    Console.WriteLine($"  {col}");
}

// Check items collection
var items = db.GetCollection<BsonDocument>("items");
var count = items.Count();
Console.WriteLine($"\n--- Items collection ---");
Console.WriteLine($"Total items: {count}");

if (count > 0)
{
    Console.WriteLine("\nFirst 5 items:");
    foreach (var item in items.FindAll().Take(5))
    {
        var number = item.ContainsKey("_id") ? item["_id"].ToString() : "(no id)";
        var desc = item.ContainsKey("Description") ? item["Description"].AsString : "(no desc)";
        var skus = item.ContainsKey("Skus") ? item["Skus"] : null;
        var skuCount = skus?.AsArray?.Count ?? 0;
        var skuList = skus?.AsArray?.Select(s => s.AsString).Take(3).ToList() ?? new();
        Console.WriteLine($"  Number: {number}");
        Console.WriteLine($"    Desc: {desc}");
        Console.WriteLine($"    SKUs ({skuCount}): [{string.Join(", ", skuList)}]");
    }
    
    // Check how many have SKUs
    var withSkus = items.FindAll().Count(i => i.ContainsKey("Skus") && i["Skus"].AsArray.Count > 0);
    Console.WriteLine($"\nItems with SKUs: {withSkus}");
}

// Check stores
var stores = db.GetCollection<BsonDocument>("stores");
var storeCount = stores.Count();
Console.WriteLine($"\n--- Stores collection ---");
Console.WriteLine($"Total stores: {storeCount}");

// Test SKU lookup
Console.WriteLine("\n--- Testing SKU lookup ---");
var testSku = args.Length > 0 ? args[0] : "107351";
Console.WriteLine($"Looking for SKU: {testSku}");
var found = items.FindOne(x => x["Skus"].AsArray.Contains(testSku));
if (found != null)
{
    Console.WriteLine($"  Found! Number: {found["_id"]}, Desc: {found["Description"]}");
}
else
{
    Console.WriteLine("  NOT FOUND with Contains query");
    
    // Try a different approach
    var allItems = items.FindAll().ToList();
    var match = allItems.FirstOrDefault(i => 
        i.ContainsKey("Skus") && 
        i["Skus"].AsArray.Any(s => s.AsString == testSku));
    if (match != null)
    {
        Console.WriteLine($"  Found with LINQ! Number: {match["_id"]}, Desc: {match["Description"]}");
    }
    else
    {
        Console.WriteLine("  Also not found with LINQ");
    }
}

// Test by item number (primary key)
Console.WriteLine("\n--- Testing Number lookup ---");
var testNumber = "00079";
Console.WriteLine($"Looking for Number: {testNumber}");
var foundByNum = items.FindById(testNumber);
if (foundByNum != null)
{
    Console.WriteLine($"  Found! Desc: {foundByNum["Description"]}");
}
else
{
    Console.WriteLine("  NOT FOUND");
}

return 0;
