using System;
using System.IO;
using System.Linq;
using LiteDB;

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var dbDir = Path.Combine(appData, "SOUP", "OrderLog");
var dbPath = Path.Combine(dbDir, "orders.db");

if (!File.Exists(dbPath))
{
    Console.WriteLine($"DB not found: {dbPath}");
    return 1;
}

using var db = new LiteDatabase(dbPath);
var col = db.GetCollection<BsonDocument>("orders");
var all = col.FindAll().ToList();
Console.WriteLine($"Total orders in DB: {all.Count}");

bool IsBlank(BsonDocument doc)
{
    string Get(BsonValue? v) => v == null ? string.Empty : (v.IsString ? v.AsString : v.ToString());
    var v = Get(doc["VendorName"]);
    var t = Get(doc["TransferNumbers"]);
    var w = Get(doc["WhsShipmentNumbers"]);
    var n = Get(doc["NoteContent"]);
    return string.IsNullOrWhiteSpace(v) && string.IsNullOrWhiteSpace(t) && string.IsNullOrWhiteSpace(w) && string.IsNullOrWhiteSpace(n);
}

var blanks = all.Where(IsBlank).ToList();
Console.WriteLine($"Blank (practically-empty) orders: {blanks.Count}");

if (blanks.Count > 0)
{
    Console.WriteLine("--- Blank items ---");
    foreach (var b in blanks)
    {
        var id = b.ContainsKey("_id") ? b["_id"].ToString() : (b.ContainsKey("Id") ? b["Id"].ToString() : "<no-id>");
        var created = b.ContainsKey("CreatedAt") ? b["CreatedAt"].ToString() : "";
        Console.WriteLine($"Id: {id} CreatedAt: {created}");
    }
}

var emptyVendor = all.Where(d => { var v = d.ContainsKey("VendorName") ? d["VendorName"] : null; return v == null || (v.IsString && string.IsNullOrWhiteSpace(v.AsString)); }).ToList();
Console.WriteLine($"Records with empty VendorName: {emptyVendor.Count}");
// If invoked with 'delete', remove the practically-empty records from the DB.
if (args.Length > 0 && args[0].Equals("delete", StringComparison.OrdinalIgnoreCase))
{
    if (blanks.Count == 0)
    {
        Console.WriteLine("No blank records to delete.");
        return 0;
    }

    Console.WriteLine("Deleting blank records...");
    foreach (var b in blanks)
    {
        // Attempt to determine _id
        var id = b.ContainsKey("_id") ? b["_id"] : (b.ContainsKey("Id") ? b["Id"] : null);
        if (id != null)
        {
            try
            {
                // _collection.Delete accepts BsonValue for id
                col.Delete(id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete id {id}: {ex.Message}");
            }
        }
    }

    Console.WriteLine("Delete operation complete.");
    // Re-run quick counts
    var allAfter = col.FindAll().ToList();
    Console.WriteLine($"Total orders after delete: {allAfter.Count}");
    var blanksAfter = allAfter.Where(IsBlank).ToList();
    Console.WriteLine($"Blank orders remaining: {blanksAfter.Count}");
    return 0;
}

return 0;