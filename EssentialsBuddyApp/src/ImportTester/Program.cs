using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using BusinessToolsSuite.Infrastructure.Services.Parsers;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.Json;

// Helper types for parsing the JS dictionary file are declared at the end of this file

Console.WriteLine("AllocationBuddy import tester");

// Inspect test_files for xlsx and csv variants
var testDir = Path.Combine("d:", "CODE", "Cshp", "test_files");
if (!Directory.Exists(testDir))
{
    Console.WriteLine("Test files directory not found: " + testDir);
}
else
{
    var xlsxFiles = Directory.EnumerateFiles(testDir, "*.xlsx").ToList();
    var csvFiles = Directory.EnumerateFiles(testDir, "*.csv").ToList();

    Console.WriteLine($"Found {xlsxFiles.Count} xlsx and {csvFiles.Count} csv files in {testDir}");

    foreach (var xf in xlsxFiles)
    {
        Console.WriteLine($"\n--- XLSX: {Path.GetFileName(xf)} ---");
        try
        {
            using var wb = new XLWorkbook(xf);
            foreach (var ws in wb.Worksheets)
            {
                Console.WriteLine($"Sheet: {ws.Name}");
                var headerRow = ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
                Console.WriteLine("Headers: " + string.Join(" | ", headerRow));

                var rows = ws.RowsUsed().Skip(1).Take(5);
                foreach (var r in rows)
                {
                    var values = r.Cells(1, Math.Max(1, headerRow.Count)).Select(c => c.GetString());
                    Console.WriteLine(string.Join(" | ", values));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading xlsx: " + ex.Message);
        }
    }

    foreach (var cf in csvFiles)
    {
        Console.WriteLine($"\n--- CSV: {Path.GetFileName(cf)} ---");
        try
        {
            using var reader = new StreamReader(cf);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            if (csv.Read())
            {
                csv.ReadHeader();
                var headers = csv.HeaderRecord?.ToList() ?? new List<string>();
                Console.WriteLine("Headers: " + string.Join(" | ", headers));

                int count = 0;
                while (csv.Read() && count < 5)
                {
                    var values = headers.Select(h => csv.GetField(h) ?? "");
                    Console.WriteLine(string.Join(" | ", values));
                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading csv: " + ex.Message);
        }
    }
}

// Also demonstrate parsing the repo sample CSV if present
var candidates = new[] {
    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..","..","..","..","..","UnifiedApp","src","renderer","modules","allocation-buddy","test-data","sample-data.csv")),
    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..","..","..","..","UnifiedApp","src","renderer","modules","allocation-buddy","test-data","sample-data.csv")),
    Path.GetFullPath(Path.Combine("d:", "CODE", "Cshp", "UnifiedApp", "src", "renderer", "modules", "allocation-buddy", "test-data", "sample-data.csv"))
};

string? full = null;
foreach (var c in candidates)
{
    if (File.Exists(c))
    {
        full = c; break;
    }
}
if (full == null)
{
    Console.WriteLine("\nRepo sample file not found. Tried:");
    foreach (var c in candidates) Console.WriteLine("  " + c);
}
else
{
    var parser = new AllocationBuddyParser();

    // Try load the JS dictionary (items + stores) from the original repo
    var dictPath = Path.GetFullPath(Path.Combine("..","..","..","..","UnifiedApp","src","renderer","modules","allocation-buddy","src","js","dictionaries.js"));
    if (!File.Exists(dictPath))
    {
        dictPath = Path.GetFullPath(Path.Combine("d:", "CODE", "Cshp", "UnifiedApp", "src", "renderer", "modules", "allocation-buddy", "src", "js", "dictionaries.js"));
    }
    if (File.Exists(dictPath))
    {
        try
        {
            var txt = File.ReadAllText(dictPath);
            var idx = txt.IndexOf('{');
            var last = txt.LastIndexOf("};");
            if (idx >= 0 && last > idx)
            {
                var json = txt.Substring(idx, last - idx + 1);
                var jsdict = JsonSerializer.Deserialize<JsDict>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (jsdict != null)
                {
                    var items = jsdict.items?.Select(i => new BusinessToolsSuite.Infrastructure.Services.Parsers.DictionaryItem { Number = i.number ?? "", Description = i.desc ?? "", Skus = i.sku ?? new List<string>() }).ToList() ?? new List<BusinessToolsSuite.Infrastructure.Services.Parsers.DictionaryItem>();
                    parser.SetDictionaryItems(items);

                    var stores = jsdict.stores?.Select(s => new BusinessToolsSuite.Infrastructure.Services.Parsers.StoreEntry { Code = s.id.ToString(), Name = s.name ?? "", Rank = s.rank ?? "" }).ToList() ?? new List<BusinessToolsSuite.Infrastructure.Services.Parsers.StoreEntry>();
                    parser.SetStoreDictionary(stores);

                    Console.WriteLine($"Loaded dictionary: {items.Count} items, {stores.Count} stores from {dictPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to load JS dictionary: " + ex.Message);
        }
    }
    else
    {
        Console.WriteLine("Dictionary file not found at expected paths; continuing without it.");
    }

    var result = await parser.ParseCsvAsync(full);
    if (!result.IsSuccess)
    {
        Console.WriteLine("Parse failed: " + result.ErrorMessage);
    }
    else
    {
        Console.WriteLine($"\nParsed {result.Value.Count} entries from repo sample: {full}");
    }
}
// Additional: parse every file in the test_files folder using the same parser + dictionaries
var allTestDir = Path.Combine("d:", "CODE", "Cshp", "test_files");
if (Directory.Exists(allTestDir))
{
    var testXlsx = Directory.EnumerateFiles(allTestDir, "*.xlsx").ToList();
    var testCsv = Directory.EnumerateFiles(allTestDir, "*.csv").ToList();

    Console.WriteLine($"\nParsing {testXlsx.Count} xlsx and {testCsv.Count} csv files in {allTestDir} with loaded dictionaries...");

    // The parser instance exists only when the repo sample was found; if it wasn't, skip
    if (File.Exists(full ?? ""))
    {
        var parserForTests = new AllocationBuddyParser();
        // Attempt to reuse the JS dictionary if it was loaded earlier by reading it again (best-effort)
        var dictPath2 = Path.GetFullPath(Path.Combine("d:", "CODE", "Cshp", "UnifiedApp", "src", "renderer", "modules", "allocation-buddy", "src", "js", "dictionaries.js"));
        if (File.Exists(dictPath2))
        {
            try
            {
                var txt2 = File.ReadAllText(dictPath2);
                var idx2 = txt2.IndexOf('{');
                var last2 = txt2.LastIndexOf("};");
                if (idx2 >= 0 && last2 > idx2)
                {
                    var json2 = txt2.Substring(idx2, last2 - idx2 + 1);
                    var jsdict2 = JsonSerializer.Deserialize<JsDict>(json2, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (jsdict2 != null)
                    {
                        var items2 = jsdict2.items?.Select(i => new BusinessToolsSuite.Infrastructure.Services.Parsers.DictionaryItem { Number = i.number ?? "", Description = i.desc ?? "", Skus = i.sku ?? new List<string>() }).ToList() ?? new List<BusinessToolsSuite.Infrastructure.Services.Parsers.DictionaryItem>();
                        parserForTests.SetDictionaryItems(items2);
                        var stores2 = jsdict2.stores?.Select(s => new BusinessToolsSuite.Infrastructure.Services.Parsers.StoreEntry { Code = s.id.ToString(), Name = s.name ?? "", Rank = s.rank ?? "" }).ToList() ?? new List<BusinessToolsSuite.Infrastructure.Services.Parsers.StoreEntry>();
                        parserForTests.SetStoreDictionary(stores2);
                    }
                }
            }
            catch { }
        }

        foreach (var xf in testXlsx)
        {
            Console.WriteLine($"\n--- Parsing XLSX: {Path.GetFileName(xf)} ---");
            try
            {
                var r = await parserForTests.ParseExcelAsync(xf);
                if (!r.IsSuccess)
                {
                    Console.WriteLine("Parse failed: " + r.ErrorMessage);
                    continue;
                }
                var entries = r.Value;
                Console.WriteLine($"Parsed {entries.Count} entries from {Path.GetFileName(xf)}");
                foreach (var e in entries.Take(5))
                {
                    Console.WriteLine($"{e.StoreId} | {e.ItemNumber} | {e.Quantity} | {e.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing xlsx: " + ex.Message);
            }
        }

        foreach (var cf in testCsv)
        {
            Console.WriteLine($"\n--- Parsing CSV: {Path.GetFileName(cf)} ---");
            try
            {
                var r = await parserForTests.ParseCsvAsync(cf);
                if (!r.IsSuccess)
                {
                    Console.WriteLine("Parse failed: " + r.ErrorMessage);
                    continue;
                }
                var entries = r.Value;
                Console.WriteLine($"Parsed {entries.Count} entries from {Path.GetFileName(cf)}");
                foreach (var e in entries.Take(5))
                {
                    Console.WriteLine($"{e.StoreId} | {e.ItemNumber} | {e.Quantity} | {e.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing csv: " + ex.Message);
            }
        }
    }
    else
    {
        Console.WriteLine("Skipping test_files parsing because repo sample file wasn't found earlier (parser unavailable in this run). To enable, ensure sample-data.csv exists in repo paths).");
    }
}
else
{
    Console.WriteLine($"Test files directory not found: {allTestDir}");
}

record JsItem
{
    public string? number { get; init; }
    public string? desc { get; init; }
    public List<string>? sku { get; init; }
}

record JsStore
{
    public int id { get; init; }
    public string? name { get; init; }
    public string? rank { get; init; }
}

record JsDict
{
    public List<JsItem>? items { get; init; }
    public List<JsStore>? stores { get; init; }
}
