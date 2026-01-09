using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using Microsoft.Data.Sqlite;
using JsonSerializer = System.Text.Json.JsonSerializer;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("  LiteDB to SQLite Migration Tool for SOUP");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SOUP");

// Database paths
var mainDbPath = Path.Combine(appData, "Data", "SOUP.db");
var dictionaryDbPath = Path.Combine(appData, "Shared", "dictionaries.db");
var orderLogDbPath = Path.Combine(appData, "OrderLog", "orders.db");

// Backup suffix
var backupSuffix = $".litedb-backup-{DateTime.Now:yyyyMMdd-HHmmss}";

var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

Console.WriteLine($"App Data Path: {appData}");
Console.WriteLine();

// Check which databases exist and are LiteDB (not already SQLite)
var hasMainDb = File.Exists(mainDbPath) && !IsSqlite(mainDbPath);
var hasDictDb = File.Exists(dictionaryDbPath) && !IsSqlite(dictionaryDbPath);
var hasOrderDb = File.Exists(orderLogDbPath) && !IsSqlite(orderLogDbPath);

if (!hasMainDb && !hasDictDb && !hasOrderDb)
{
    Console.WriteLine("No LiteDB databases found to migrate.");
    if (File.Exists(mainDbPath) || File.Exists(dictionaryDbPath) || File.Exists(orderLogDbPath))
        Console.WriteLine("(All existing databases are already SQLite format)");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

Console.WriteLine("Found LiteDB databases to migrate:");
if (hasMainDb) Console.WriteLine($"  • Main DB: {mainDbPath}");
if (hasDictDb) Console.WriteLine($"  • Dictionary DB: {dictionaryDbPath}");
if (hasOrderDb) Console.WriteLine($"  • OrderLog DB: {orderLogDbPath}");

// Show already-migrated
if (File.Exists(mainDbPath) && !hasMainDb) Console.WriteLine($"  ✓ Main DB: already SQLite");
if (File.Exists(dictionaryDbPath) && !hasDictDb) Console.WriteLine($"  ✓ Dictionary DB: already SQLite");
if (File.Exists(orderLogDbPath) && !hasOrderDb) Console.WriteLine($"  ✓ OrderLog DB: already SQLite");
Console.WriteLine();

Console.WriteLine("This tool will:");
Console.WriteLine("  1. Backup existing .db files (rename to .litedb-backup-*)");
Console.WriteLine("  2. Read data from LiteDB backups");
Console.WriteLine("  3. Create new SQLite databases with the migrated data");
Console.WriteLine();
Console.Write("Continue? (y/n): ");

var key = Console.ReadKey();
Console.WriteLine();

if (key.KeyChar != 'y' && key.KeyChar != 'Y')
{
    Console.WriteLine("Migration cancelled.");
    return;
}

Console.WriteLine();

try
{
    // Migrate Dictionary DB
    if (hasDictDb)
    {
        Console.WriteLine("═══ Migrating Dictionary Database ═══");
        MigrateDictionaryDb(dictionaryDbPath, backupSuffix, jsonOptions);
        Console.WriteLine();
    }

    // Migrate OrderLog DB
    if (hasOrderDb)
    {
        Console.WriteLine("═══ Migrating OrderLog Database ═══");
        MigrateOrderLogDb(orderLogDbPath, backupSuffix, jsonOptions);
        Console.WriteLine();
    }

    // Migrate Main DB (AllocationBuddy, EssentialsBuddy, ExpireWise)
    if (hasMainDb)
    {
        Console.WriteLine("═══ Migrating Main Database ═══");
        MigrateMainDb(mainDbPath, backupSuffix, jsonOptions);
        Console.WriteLine();
    }

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  Migration Complete!");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine("Backup files created with suffix: " + backupSuffix);
    Console.WriteLine("You can delete the backup files after verifying the migration.");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// ═══════════════════════════════════════════════════════════════
// Migration Methods
// ═══════════════════════════════════════════════════════════════

static void MigrateDictionaryDb(string dbPath, string backupSuffix, JsonSerializerOptions jsonOptions)
{
    var backupPath = dbPath + backupSuffix;
    
    // Backup original
    Console.WriteLine($"  Backing up to: {Path.GetFileName(backupPath)}");
    File.Move(dbPath, backupPath);

    // Read from LiteDB
    Console.Write("  Reading items from LiteDB... ");
    List<BsonDocument> items;
    List<BsonDocument> stores;
    
    using (var liteDb = new LiteDatabase($"Filename={backupPath};Connection=Direct;ReadOnly=true"))
    {
        var itemsCol = liteDb.GetCollection("items");
        var storesCol = liteDb.GetCollection("stores");
        items = itemsCol.FindAll().ToList();
        stores = storesCol.FindAll().ToList();
    }
    Console.WriteLine($"{items.Count} items, {stores.Count} stores");

    // Create SQLite database
    Console.Write("  Creating SQLite database... ");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    
    using var sqlite = new SqliteConnection($"Data Source={dbPath}");
    sqlite.Open();

    // Enable WAL mode
    using (var cmd = sqlite.CreateCommand())
    {
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
    }

    // Create tables
    using (var cmd = sqlite.CreateCommand())
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Items (
                Number TEXT PRIMARY KEY,
                Description TEXT NOT NULL DEFAULT '',
                Skus TEXT NOT NULL DEFAULT '[]',
                IsEssential INTEGER NOT NULL DEFAULT 0,
                IsPrivateLabel INTEGER NOT NULL DEFAULT 0,
                Tags TEXT NOT NULL DEFAULT '[]'
            );
            CREATE INDEX IF NOT EXISTS IX_Items_Description ON Items(Description);

            CREATE TABLE IF NOT EXISTS Stores (
                Code TEXT PRIMARY KEY,
                Name TEXT NOT NULL DEFAULT '',
                Rank TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS IX_Stores_Name ON Stores(Name);
            CREATE INDEX IF NOT EXISTS IX_Stores_Rank ON Stores(Rank);
        ";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("OK");

    // Insert items
    Console.Write("  Migrating items... ");
    using (var transaction = sqlite.BeginTransaction())
    {
        foreach (var item in items)
        {
            using var cmd = sqlite.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Items (Number, Description, Skus, IsEssential, IsPrivateLabel, Tags)
                VALUES (@Number, @Description, @Skus, @IsEssential, @IsPrivateLabel, @Tags)
            ";
            cmd.Parameters.AddWithValue("@Number", item["_id"].AsString);
            cmd.Parameters.AddWithValue("@Description", item.TryGetValue("Description", out var desc) ? desc.AsString : "");
            cmd.Parameters.AddWithValue("@Skus", item.TryGetValue("Skus", out var skus) ? JsonSerializer.Serialize(skus.AsArray.Select(s => s.AsString).ToList(), jsonOptions) : "[]");
            cmd.Parameters.AddWithValue("@IsEssential", item.TryGetValue("IsEssential", out var ess) && ess.AsBoolean ? 1 : 0);
            cmd.Parameters.AddWithValue("@IsPrivateLabel", item.TryGetValue("IsPrivateLabel", out var pl) && pl.AsBoolean ? 1 : 0);
            cmd.Parameters.AddWithValue("@Tags", item.TryGetValue("Tags", out var tags) ? JsonSerializer.Serialize(tags.AsArray.Select(t => t.AsString).ToList(), jsonOptions) : "[]");
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    Console.WriteLine($"{items.Count} items migrated");

    // Insert stores
    Console.Write("  Migrating stores... ");
    using (var transaction = sqlite.BeginTransaction())
    {
        foreach (var store in stores)
        {
            using var cmd = sqlite.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Stores (Code, Name, Rank)
                VALUES (@Code, @Name, @Rank)
            ";
            cmd.Parameters.AddWithValue("@Code", store["_id"].AsString);
            cmd.Parameters.AddWithValue("@Name", store.TryGetValue("Name", out var name) ? name.AsString : "");
            cmd.Parameters.AddWithValue("@Rank", store.TryGetValue("Rank", out var rank) ? rank.AsString : "");
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    Console.WriteLine($"{stores.Count} stores migrated");
}

static void MigrateOrderLogDb(string dbPath, string backupSuffix, JsonSerializerOptions jsonOptions)
{
    var backupPath = dbPath + backupSuffix;
    
    // Backup original
    Console.WriteLine($"  Backing up to: {Path.GetFileName(backupPath)}");
    File.Move(dbPath, backupPath);

    // Read from LiteDB
    Console.Write("  Reading orders from LiteDB... ");
    List<BsonDocument> orders;
    
    using (var liteDb = new LiteDatabase($"Filename={backupPath};Connection=Direct;ReadOnly=true"))
    {
        var ordersCol = liteDb.GetCollection("orders");
        orders = ordersCol.FindAll().ToList();
    }
    Console.WriteLine($"{orders.Count} orders");

    // Create SQLite database
    Console.Write("  Creating SQLite database... ");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    
    using var sqlite = new SqliteConnection($"Data Source={dbPath}");
    sqlite.Open();

    // Enable WAL mode
    using (var cmd = sqlite.CreateCommand())
    {
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
    }

    // Create table
    using (var cmd = sqlite.CreateCommand())
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                Data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Orders_SortOrder ON Orders(SortOrder);
        ";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("OK");

    // Insert orders
    Console.Write("  Migrating orders... ");
    using (var transaction = sqlite.BeginTransaction())
    {
        foreach (var order in orders)
        {
            // Convert BsonDocument to a dictionary for JSON serialization
            var orderDict = BsonToDict(order);
            
            using var cmd = sqlite.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Orders (Id, SortOrder, Data)
                VALUES (@Id, @SortOrder, @Data)
            ";
            
            var id = order["_id"].AsGuid;
            var sortOrder = order.TryGetValue("Order", out var ord) ? ord.AsInt32 : 0;
            
            // Remove _id from the dict (we store Id separately)
            orderDict.Remove("_id");
            orderDict["Id"] = id.ToString();
            
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            cmd.Parameters.AddWithValue("@SortOrder", sortOrder);
            cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(orderDict, jsonOptions));
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    Console.WriteLine($"{orders.Count} orders migrated");
}

static void MigrateMainDb(string dbPath, string backupSuffix, JsonSerializerOptions jsonOptions)
{
    var backupPath = dbPath + backupSuffix;
    
    // Backup original
    Console.WriteLine($"  Backing up to: {Path.GetFileName(backupPath)}");
    File.Move(dbPath, backupPath);

    // Read collections from LiteDB
    Console.Write("  Reading collections from LiteDB... ");
    var collections = new Dictionary<string, List<BsonDocument>>();
    
    using (var liteDb = new LiteDatabase($"Filename={backupPath};Connection=Direct;ReadOnly=true"))
    {
        foreach (var colName in liteDb.GetCollectionNames())
        {
            var col = liteDb.GetCollection(colName);
            collections[colName] = col.FindAll().ToList();
        }
    }
    
    var totalDocs = collections.Values.Sum(c => c.Count);
    Console.WriteLine($"{collections.Count} collections, {totalDocs} documents");

    // Create SQLite database
    Console.Write("  Creating SQLite database... ");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    
    using var sqlite = new SqliteConnection($"Data Source={dbPath}");
    sqlite.Open();

    // Enable WAL mode
    using (var cmd = sqlite.CreateCommand())
    {
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("OK");

    // Migrate each collection
    foreach (var (colName, docs) in collections)
    {
        Console.Write($"  Migrating {colName}... ");
        
        // Create table for this collection
        using (var cmd = sqlite.CreateCommand())
        {
            cmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS [{colName}] (
                    Id TEXT PRIMARY KEY,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    Data TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_{colName}_IsDeleted ON [{colName}](IsDeleted);
            ";
            cmd.ExecuteNonQuery();
        }

        // Insert documents
        using var transaction = sqlite.BeginTransaction();
        foreach (var doc in docs)
        {
            var docDict = BsonToDict(doc);
            
            using var cmd = sqlite.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $@"
                INSERT INTO [{colName}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
                VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
            ";
            
            var id = doc["_id"].AsGuid;
            var createdAt = doc.TryGetValue("CreatedAt", out var ca) ? ca.AsDateTime : DateTime.UtcNow;
            var updatedAt = doc.TryGetValue("UpdatedAt", out var ua) && !ua.IsNull ? ua.AsDateTime : (DateTime?)null;
            var isDeleted = doc.TryGetValue("IsDeleted", out var del) && del.AsBoolean;
            
            docDict.Remove("_id");
            docDict["Id"] = id.ToString();
            
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            cmd.Parameters.AddWithValue("@CreatedAt", createdAt.ToString("O"));
            cmd.Parameters.AddWithValue("@UpdatedAt", updatedAt?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDeleted", isDeleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(docDict, jsonOptions));
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
        
        Console.WriteLine($"{docs.Count} documents");
    }
}

static Dictionary<string, object?> BsonToDict(BsonDocument doc)
{
    var result = new Dictionary<string, object?>();
    
    foreach (var key in doc.Keys)
    {
        result[key] = BsonToObject(doc[key]);
    }
    
    return result;
}

static object? BsonToObject(BsonValue value)
{
    return value.Type switch
    {
        BsonType.Null => null,
        BsonType.Int32 => value.AsInt32,
        BsonType.Int64 => value.AsInt64,
        BsonType.Double => value.AsDouble,
        BsonType.Decimal => value.AsDecimal,
        BsonType.String => value.AsString,
        BsonType.Boolean => value.AsBoolean,
        BsonType.DateTime => value.AsDateTime.ToString("O"),
        BsonType.Guid => value.AsGuid.ToString(),
        BsonType.Array => value.AsArray.Select(BsonToObject).ToList(),
        BsonType.Document => BsonToDict(value.AsDocument),
        _ => value.ToString()
    };
}

static bool IsSqlite(string path)
{
    try
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[16];
        fs.Read(buffer, 0, 16);
        var header = System.Text.Encoding.ASCII.GetString(buffer);
        return header.StartsWith("SQLite format 3");
    }
    catch
    {
        return false;
    }
}
