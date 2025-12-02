using System.Text.Json;
using System.Text.RegularExpressions;
using LiteDB;
using ClosedXML.Excel;

Console.WriteLine("LiteDB Dictionary Importer");
Console.WriteLine("==========================");

var sharedPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "SAP",
    "Shared"
);

Directory.CreateDirectory(sharedPath);

var dbPath = Path.Combine(sharedPath, "dictionaries.db");
var jsPath = args.Length > 0 ? args[0] : @"D:\CODE\Cshp\SAP\src\SAP\Assets\dictionaries.js";

// Check for essentials file argument
string? essentialsPath = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--essentials" && i + 1 < args.Length)
    {
        essentialsPath = args[i + 1];
    }
}

// Check for update-essentials-only mode (doesn't delete existing DB)
bool updateEssentialsOnly = args.Contains("--update-essentials");

// Check for Excel import mode
string? excelImportPath = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--import-excel" && i + 1 < args.Length)
    {
        excelImportPath = args[i + 1];
    }
}

// Excel import mode - import items directly from Excel spreadsheet
if (!string.IsNullOrEmpty(excelImportPath))
{
    if (!File.Exists(excelImportPath))
    {
        Console.WriteLine($"ERROR: Excel file not found: {excelImportPath}");
        return 1;
    }

    Console.WriteLine($"\nImporting items from Excel: {excelImportPath}");
    
    using var workbook = new XLWorkbook(excelImportPath);
    var worksheet = workbook.Worksheets.First();
    var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
    var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;
    
    // Find column indices by header names
    int itemNumberCol = -1, descriptionCol = -1, skuCol = -1, styleListCol = -1;
    
    // Print all headers for debugging
    Console.WriteLine("  Headers found:");
    for (int col = 1; col <= lastCol; col++)
    {
        var header = worksheet.Cell(1, col).GetString()?.Trim() ?? "";
        if (!string.IsNullOrEmpty(header))
        {
            Console.WriteLine($"    Col {col}: {header}");
        }
        
        var headerUpper = header.ToUpperInvariant();
        
        // Exact matches first (higher priority)
        if (header.Equals("No.", StringComparison.OrdinalIgnoreCase))
            itemNumberCol = col;
        else if (header.Equals("Barcode No.", StringComparison.OrdinalIgnoreCase))
            skuCol = col;
        else if (header.Equals("Description", StringComparison.OrdinalIgnoreCase))
            descriptionCol = col;
        else if (header.Equals("Style List", StringComparison.OrdinalIgnoreCase))
            styleListCol = col;
        // Fuzzy matches (lower priority, only if not already set)
        else if (itemNumberCol == -1 && headerUpper.Contains("ITEM") && (headerUpper.Contains("NUMBER") || headerUpper.Contains("NO")))
            itemNumberCol = col;
        else if (descriptionCol == -1 && headerUpper.Contains("ITEM") && headerUpper.Contains("DESC"))
            descriptionCol = col;
        else if (skuCol == -1 && (headerUpper.Contains("SKU") || headerUpper == "UPC"))
            skuCol = col;
        else if (styleListCol == -1 && headerUpper.Contains("STYLE") && headerUpper.Contains("LIST"))
            styleListCol = col;
    }
    
    // Fallback: check for exact common headers
    for (int col = 1; col <= lastCol; col++)
    {
        var header = worksheet.Cell(1, col).GetString()?.Trim() ?? "";
        if (itemNumberCol == -1 && (header.Equals("Item No.", StringComparison.OrdinalIgnoreCase) || 
            header.Equals("Item Number", StringComparison.OrdinalIgnoreCase) ||
            header.Equals("No.", StringComparison.OrdinalIgnoreCase)))
            itemNumberCol = col;
        if (descriptionCol == -1 && (header.Equals("Item Description", StringComparison.OrdinalIgnoreCase) ||
            header.Equals("Description", StringComparison.OrdinalIgnoreCase)))
            descriptionCol = col;
        if (skuCol == -1 && header.Equals("SKU", StringComparison.OrdinalIgnoreCase))
            skuCol = col;
        if (styleListCol == -1 && header.Equals("Style List", StringComparison.OrdinalIgnoreCase))
            styleListCol = col;
    }
    
    Console.WriteLine($"  Found columns: ItemNumber={itemNumberCol}, Description={descriptionCol}, SKU={skuCol}, StyleList={styleListCol}");
    
    if (itemNumberCol == -1)
    {
        Console.WriteLine("ERROR: Could not find Item Number column");
        return 1;
    }
    
    // Open database (will create if doesn't exist, update if exists)
    Console.WriteLine($"Using database: {dbPath}");
    
    using var db = new LiteDatabase(dbPath);
    var collection = db.GetCollection<DictionaryItemEntity>("items");
    collection.EnsureIndex(x => x.Description);
    collection.EnsureIndex(x => x.Skus);
    collection.EnsureIndex(x => x.IsEssential);
    
    var items = new Dictionary<string, DictionaryItemEntity>();
    var essentialCount = 0;
    
    Console.WriteLine($"  Processing {lastRow - 1} data rows...");
    
    for (int row = 2; row <= lastRow; row++) // Skip header row
    {
        var itemNumber = GetCellValue(worksheet, row, itemNumberCol);
        if (string.IsNullOrEmpty(itemNumber)) continue;
        
        var description = descriptionCol > 0 ? GetCellValue(worksheet, row, descriptionCol) : "";
        var sku = skuCol > 0 ? GetCellValue(worksheet, row, skuCol) : "";
        var styleList = styleListCol > 0 ? GetCellValue(worksheet, row, styleListCol) : "";
        
        var isEssential = styleList.Contains("Essential", StringComparison.OrdinalIgnoreCase);
        
        if (!items.TryGetValue(itemNumber, out var existing))
        {
            existing = new DictionaryItemEntity
            {
                Number = itemNumber,
                Description = description ?? "",
                Skus = new List<string>(),
                IsEssential = isEssential
            };
            items[itemNumber] = existing;
        }
        
        // Add SKU if not already present
        if (!string.IsNullOrEmpty(sku) && !existing.Skus.Contains(sku))
        {
            existing.Skus.Add(sku);
        }
        
        // Update essential flag if any row has it
        if (isEssential && !existing.IsEssential)
        {
            existing.IsEssential = true;
        }
        
        // Update description if empty
        if (string.IsNullOrEmpty(existing.Description) && !string.IsNullOrEmpty(description))
        {
            existing.Description = description;
        }
        
        if (isEssential) essentialCount++;
    }
    
    var count = collection.InsertBulk(items.Values);
    Console.WriteLine($"\nInserted {count} items into LiteDB");
    Console.WriteLine($"  Essential items: {items.Values.Count(i => i.IsEssential)}");
    Console.WriteLine($"  Items with SKUs: {items.Values.Count(i => i.Skus.Count > 0)}");
    Console.WriteLine($"  Items with descriptions: {items.Values.Count(i => !string.IsNullOrEmpty(i.Description))}");
    
    Console.WriteLine($"\nDatabase created at: {dbPath}");
    Console.WriteLine($"Size: {new FileInfo(dbPath).Length / 1024.0 / 1024.0:F2} MB");
    Console.WriteLine("\nDone!");
    return 0;
}

static string GetCellValue(IXLWorksheet worksheet, int row, int col)
{
    var cell = worksheet.Cell(row, col);
    if (cell.TryGetValue<double>(out var numericValue))
    {
        return ((long)numericValue).ToString();
    }
    return cell.GetString()?.Trim() ?? "";
}

if (!updateEssentialsOnly)
{
    // Open database (will create if doesn't exist, update if exists)
    Console.WriteLine($"Using database: {dbPath}");

    if (!File.Exists(jsPath))
    {
        Console.WriteLine($"ERROR: Dictionary JS file not found: {jsPath}");
        return 1;
    }

    Console.WriteLine($"Reading dictionaries from: {jsPath}");
    var jsContent = File.ReadAllText(jsPath);

    using var db = new LiteDatabase(dbPath);

    // Import items
    Console.WriteLine("\nImporting items...");
    var itemsMatch = Regex.Match(jsContent, @"""items"":\s*\[(.*?)\]\s*,\s*""stores""", RegexOptions.Singleline);
    if (itemsMatch.Success)
    {
        var itemsBlock = "[" + itemsMatch.Groups[1].Value + "]";
        var items = System.Text.Json.JsonSerializer.Deserialize<List<DictionaryItemEntity>>(itemsBlock, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        if (items != null && items.Count > 0)
        {
            // Deduplicate by number (keep first occurrence) and remove invisible characters
            var seen = new HashSet<string>();
            var uniqueItems = new List<DictionaryItemEntity>();
            foreach (var item in items)
            {
                // Remove zero-width spaces and other invisible characters
                var cleanNumber = Regex.Replace(item.Number.Trim(), @"[\u200B-\u200D\uFEFF\u00A0]", "");
                if (!string.IsNullOrEmpty(cleanNumber) && seen.Add(cleanNumber))
                {
                    item.Number = cleanNumber;
                    uniqueItems.Add(item);
                }
            }

            var collection = db.GetCollection<DictionaryItemEntity>("items");
            collection.EnsureIndex(x => x.Description);
            collection.EnsureIndex(x => x.Skus);
            collection.EnsureIndex(x => x.IsEssential);
            var count = collection.InsertBulk(uniqueItems);
            Console.WriteLine($"Inserted {count} items into LiteDB");
        }
    }
    else
    {
        Console.WriteLine("WARNING: Could not find items section in JS file");
    }

    // Import stores
    Console.WriteLine("\nImporting stores...");
    var storesMatch = Regex.Match(jsContent, @"""stores"":\s*\[(.*?)\]", RegexOptions.Singleline);
    if (storesMatch.Success)
    {
        var storesBlock = storesMatch.Groups[1].Value;
        var storePattern = new Regex(@"\{\s*""id"":\s*(\d+),\s*""name"":\s*""([^""]+)"",\s*""rank"":\s*""([^""]+)""\s*\}");
        var storeMatches = storePattern.Matches(storesBlock);
        
        var storesCollection = db.GetCollection<StoreEntity>("stores");
        storesCollection.EnsureIndex(x => x.Name);
        storesCollection.EnsureIndex(x => x.Rank);
        
        var stores = new List<StoreEntity>();
        foreach (Match m in storeMatches)
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
    else
    {
        Console.WriteLine("WARNING: Could not find stores section in JS file");
    }

    Console.WriteLine($"\nDatabase created at: {dbPath}");
    Console.WriteLine($"Size: {new FileInfo(dbPath).Length / 1024.0 / 1024.0:F2} MB");
}
else
{
    // Update essentials only mode - open existing DB
    Console.WriteLine("Update essentials only mode - using existing database");
}

using var dbForEssentials = new LiteDatabase(dbPath);

// Import essentials from file if provided
if (!string.IsNullOrEmpty(essentialsPath))
{
    if (!File.Exists(essentialsPath))
    {
        Console.WriteLine($"ERROR: Essentials file not found: {essentialsPath}");
        return 1;
    }

    Console.WriteLine($"\nImporting essentials from: {essentialsPath}");
    var collection = dbForEssentials.GetCollection<DictionaryItemEntity>("items");
    var itemNumbers = new List<string>();
    
    // Support Excel files (.xlsx, .xls) or text files
    if (essentialsPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || 
        essentialsPath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
    {
        // Read from Excel - column A contains item numbers
        using var workbook = new XLWorkbook(essentialsPath);
        var worksheet = workbook.Worksheets.First();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        
        Console.WriteLine($"  Reading {lastRow} rows from Excel column A...");
        
        for (int row = 1; row <= lastRow; row++)
        {
            var cell = worksheet.Cell(row, 1); // Column A
            string? cellValue = cell.GetString()?.Trim();
            
            // Also try getting as number in case it's formatted
            if (string.IsNullOrEmpty(cellValue) && cell.TryGetValue<double>(out var numericValue))
            {
                cellValue = ((long)numericValue).ToString();
            }
            
            if (!string.IsNullOrEmpty(cellValue))
            {
                itemNumbers.Add(cellValue);
            }
        }
        
        // Deduplicate
        itemNumbers = itemNumbers.Distinct().ToList();
    }
    else
    {
        // Text file - one item per line, comma-separated, or JSON array
        var essentialsContent = File.ReadAllText(essentialsPath);
        
        if (essentialsContent.TrimStart().StartsWith("["))
        {
            // JSON array format
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(essentialsContent);
            if (parsed != null) itemNumbers.AddRange(parsed);
        }
        else
        {
            // Line-separated or comma-separated
            var lines = essentialsContent.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            itemNumbers.AddRange(lines.Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
        }
    }
    
    Console.WriteLine($"  Found {itemNumbers.Count} item numbers to mark as essential");

    var markedCount = 0;
    var notFoundCount = 0;
    var notFoundItems = new List<string>();
    foreach (var itemNumber in itemNumbers)
    {
        var item = collection.FindById(itemNumber);
        if (item != null)
        {
            item.IsEssential = true;
            collection.Update(item);
            markedCount++;
        }
        else
        {
            notFoundCount++;
            notFoundItems.Add(itemNumber);
        }
    }
    
    Console.WriteLine($"Marked {markedCount} items as essential");
    if (notFoundCount > 0)
    {
        Console.WriteLine($"WARNING: {notFoundCount} items not found in dictionary");
        if (notFoundCount <= 20)
        {
            foreach (var item in notFoundItems)
            {
                Console.WriteLine($"  - {item}");
            }
        }
        else
        {
            Console.WriteLine($"  (showing first 20)");
            foreach (var item in notFoundItems.Take(20))
            {
                Console.WriteLine($"  - {item}");
            }
        }
    }
}

Console.WriteLine("\nDone!");
Console.WriteLine("\nUsage:");
Console.WriteLine("  ImportDictionary <dictionaries.js>                     - Import from JS dictionary file");
Console.WriteLine("  ImportDictionary --import-excel <file.xlsx>            - Import from Excel (Item No, Description, SKU, Style List)");
Console.WriteLine("  ImportDictionary <dictionaries.js> --essentials <file> - Import dictionary + essentials");
Console.WriteLine("  ImportDictionary --update-essentials --essentials <file> - Update essentials only");

return 0;

public class DictionaryItemEntity
{
    [BsonId]
    public string Number { get; set; } = "";
    
    [System.Text.Json.Serialization.JsonPropertyName("desc")]
    public string Description { get; set; } = "";
    
    [System.Text.Json.Serialization.JsonPropertyName("sku")]
    public List<string> Skus { get; set; } = new();
    
    public bool IsEssential { get; set; } = false;
    
    public List<string> Tags { get; set; } = new();
}

public class StoreEntity
{
    [BsonId]
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rank { get; set; } = "";
}
