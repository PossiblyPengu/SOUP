using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var dbPath = Path.Combine(appData, "SOUP", "OrderLog", "orders.db");

if (!File.Exists(dbPath))
{
    Console.WriteLine($"Database not found: {dbPath}");
    return 1;
}

Console.WriteLine($"Database: {dbPath}");
Console.WriteLine(new string('-', 60));

var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadOnly
}.ToString();

using var connection = new SqliteConnection(connectionString);
connection.Open();

// Count total items
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM Orders";
    var count = Convert.ToInt32(cmd.ExecuteScalar());
    Console.WriteLine($"Total items in database: {count}");
}

// Count by IsArchived status
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT Data FROM Orders";
    using var reader = cmd.ExecuteReader();
    
    int archivedCount = 0;
    int activeCount = 0;
    int doneCount = 0;
    int hasPreviousStatus = 0;
    
    while (reader.Read())
    {
        var json = reader.GetString(0);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        bool isArchived = root.TryGetProperty("IsArchived", out var archivedProp) && archivedProp.GetBoolean();
        
        // Status can be int or string
        int status = 0;
        if (root.TryGetProperty("Status", out var statusProp))
        {
            if (statusProp.ValueKind == JsonValueKind.Number)
                status = statusProp.GetInt32();
            else if (statusProp.ValueKind == JsonValueKind.String)
            {
                var statusStr = statusProp.GetString() ?? "";
                status = statusStr switch
                {
                    "Done" => 3,
                    "InProgress" => 2,
                    "OnDeck" => 1,
                    _ => 0
                };
            }
        }
        
        bool hasPrevStatus = root.TryGetProperty("PreviousStatus", out var prevProp) && prevProp.ValueKind != JsonValueKind.Null;
        
        if (isArchived) archivedCount++;
        else activeCount++;
        
        if (status == 3) doneCount++; // Done = 3
        if (hasPrevStatus) hasPreviousStatus++;
    }
    
    Console.WriteLine($"Items with IsArchived=true: {archivedCount}");
    Console.WriteLine($"Items with IsArchived=false: {activeCount}");
    Console.WriteLine($"Items with Status=Done: {doneCount}");
    Console.WriteLine($"Items with PreviousStatus set: {hasPreviousStatus}");
}

Console.WriteLine(new string('-', 60));

// Check for inconsistent items (should be archived but aren't)
Console.WriteLine("\nChecking for inconsistent items...");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT Data FROM Orders";
    using var reader = cmd.ExecuteReader();
    
    int inconsistentCount = 0;
    
    while (reader.Read())
    {
        var json = reader.GetString(0);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        bool isArchived = root.TryGetProperty("IsArchived", out var archivedProp) && archivedProp.GetBoolean();
        
        // Status can be int or string
        int status = 0;
        if (root.TryGetProperty("Status", out var statusProp))
        {
            if (statusProp.ValueKind == JsonValueKind.Number)
                status = statusProp.GetInt32();
            else if (statusProp.ValueKind == JsonValueKind.String)
            {
                var statusStr = statusProp.GetString() ?? "";
                status = statusStr switch
                {
                    "Done" => 3,
                    "InProgress" => 2,
                    "OnDeck" => 1,
                    _ => 0
                };
            }
        }
        
        bool hasPrevStatus = root.TryGetProperty("PreviousStatus", out var prevProp) && prevProp.ValueKind != JsonValueKind.Null;
        
        // Items that should be archived but aren't
        if (!isArchived && (status == 3 || hasPrevStatus))
        {
            inconsistentCount++;
            var vendor = root.TryGetProperty("VendorName", out var v) ? v.GetString() : "(no vendor)";
            var id = root.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "(no id)";
            Console.WriteLine($"  Inconsistent: {vendor} (Status={status}, HasPrevStatus={hasPrevStatus})");
        }
    }
    
    if (inconsistentCount == 0)
        Console.WriteLine("No inconsistent items found.");
    else
        Console.WriteLine($"\nTotal inconsistent items: {inconsistentCount}");
}

Console.WriteLine(new string('-', 60));
Console.WriteLine("Done.");
return 0;