using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using SAP.Core.Common;
using SAP.Core.Entities.AllocationBuddy;

namespace SAP.Infrastructure.Services.Parsers;

/// <summary>
/// Dictionary item for item number/description mapping
/// </summary>
public class DictionaryItem
{
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Skus { get; set; } = new();
}

/// <summary>
/// Store entry for store code/name mapping
/// </summary>
public class StoreEntry
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rank { get; set; } = "";
}

/// <summary>
/// Column mapping result from smart detection
/// </summary>
public class ColumnMap
{
    public int StoreColumnIndex { get; set; } = -1;
    public int ItemColumnIndex { get; set; } = -1;
    public int QuantityColumnIndex { get; set; } = -1;
}

/// <summary>
/// Parser for AllocationBuddy files
/// </summary>
public class AllocationBuddyParser
{
    private readonly ILogger<AllocationBuddyParser>? _logger;

    public List<DictionaryItem> DictionaryItems { get; set; } = new();
    public List<StoreEntry> StoreDictionary { get; set; } = new();

    public AllocationBuddyParser(ILogger<AllocationBuddyParser>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Smart column detection by analyzing actual data against LiteDB dictionaries
    /// </summary>
    private ColumnMap DetectColumnsByData(List<IXLRow> dataRows, List<string> headers)
    {
        var result = new ColumnMap();

        if (dataRows.Count == 0) return result;

        // Sample first 10 rows (or all if less than 10)
        var sampleSize = Math.Min(10, dataRows.Count);
        var sampleRows = dataRows.Take(sampleSize).ToList();

        // Get number of columns
        var columnCount = sampleRows[0].CellsUsed().Count();

        // For each column, count how many values match stores and items
        var storeMatches = new int[columnCount];
        var itemMatches = new int[columnCount];
        var numericValues = new int[columnCount];
        var storeCodePattern = new int[columnCount]; // 3-digit numbers (likely store codes)

        foreach (var row in sampleRows)
        {
            for (int colIdx = 0; colIdx < columnCount; colIdx++)
            {
                // Get cell value by index (1-based in ClosedXML)
                var cell = row.Cell(colIdx + 1);
                var value = cell.Value.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(value)) continue;

                // Check if value exists in store dictionary (by code or name)
                bool isStoreMatch = StoreDictionary.Any(s =>
                    s.Code.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Equals(value, StringComparison.OrdinalIgnoreCase));

                if (isStoreMatch)
                {
                    storeMatches[colIdx]++;
                }

                // Check if 3-digit number (common store code pattern)
                if (int.TryParse(value, out var numValue) && numValue >= 100 && numValue <= 999)
                {
                    storeCodePattern[colIdx]++;
                }

                // Check if value exists in item dictionary (by number or SKU)
                if (DictionaryItems.Any(i =>
                    i.Number.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    i.Skus.Any(sku => sku.Equals(value, StringComparison.OrdinalIgnoreCase))))
                {
                    itemMatches[colIdx]++;
                }

                // Check if numeric (for quantity column)
                if (double.TryParse(value, out _))
                {
                    numericValues[colIdx]++;
                }
            }
        }

        // Boost store matches with store code pattern matches
        for (int i = 0; i < columnCount; i++)
        {
            // If column has 3-digit patterns and dictionary matches, give it priority
            if (storeCodePattern[i] > 0)
            {
                storeMatches[i] += storeCodePattern[i];
                _logger?.LogInformation("Column {Index} has {Count} store code patterns (3-digit numbers)",
                    i, storeCodePattern[i]);
            }
        }

        // Find column with most store matches
        int maxStoreMatches = storeMatches.Max();
        if (maxStoreMatches > 0)
        {
            result.StoreColumnIndex = Array.IndexOf(storeMatches, maxStoreMatches);
            _logger?.LogInformation("Store column detected at index {Index} with {Matches}/{Total} matches",
                result.StoreColumnIndex, maxStoreMatches, sampleSize);
        }

        // Find column with most item matches (excluding store column)
        for (int i = 0; i < itemMatches.Length; i++)
        {
            if (i == result.StoreColumnIndex) continue; // Skip store column
            if (result.ItemColumnIndex < 0 || itemMatches[i] > itemMatches[result.ItemColumnIndex])
            {
                result.ItemColumnIndex = i;
            }
        }

        if (result.ItemColumnIndex >= 0)
        {
            _logger?.LogInformation("Item column detected at index {Index} with {Matches}/{Total} matches",
                result.ItemColumnIndex, itemMatches[result.ItemColumnIndex], sampleSize);
        }

        // Find column with most numeric values (excluding store and item columns)
        for (int i = 0; i < numericValues.Length; i++)
        {
            if (i == result.StoreColumnIndex || i == result.ItemColumnIndex) continue;
            if (result.QuantityColumnIndex < 0 || numericValues[i] > numericValues[result.QuantityColumnIndex])
            {
                result.QuantityColumnIndex = i;
            }
        }

        if (result.QuantityColumnIndex >= 0)
        {
            _logger?.LogInformation("Quantity column detected at index {Index} with {Matches}/{Total} numeric values",
                result.QuantityColumnIndex, numericValues[result.QuantityColumnIndex], sampleSize);
        }

        // If data-based detection failed, fall back to header-based detection
        if (result.StoreColumnIndex < 0 || result.ItemColumnIndex < 0)
        {
            _logger?.LogInformation("Data-based detection incomplete (Store: {Store}, Item: {Item}), falling back to header detection",
                result.StoreColumnIndex, result.ItemColumnIndex);
            result = DetectColumnsByHeaders(headers, result);
        }

        return result;
    }

    /// <summary>
    /// Fallback header-based column detection when data detection fails
    /// </summary>
    private ColumnMap DetectColumnsByHeaders(List<string> headers, ColumnMap existingMap)
    {
        var result = existingMap;
        
        // Common store/location header patterns
        var storePatterns = new[] { "store", "location", "loc", "site", "branch", "warehouse", "whse", "ship-to", "shipto", "customer", "dest", "destination" };
        
        // Common item/product header patterns  
        var itemPatterns = new[] { "item", "product", "sku", "part", "article", "material", "upc", "barcode", "code", "number", "no.", "no" };
        
        // Common quantity header patterns
        var qtyPatterns = new[] { "qty", "quantity", "amount", "count", "units", "pcs", "pieces", "ordered", "order qty", "pick qty", "pick" };

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i].ToLowerInvariant();
            
            // Detect store column
            if (result.StoreColumnIndex < 0 && storePatterns.Any(p => header.Contains(p)))
            {
                result.StoreColumnIndex = i;
                _logger?.LogInformation("Store column detected by header '{Header}' at index {Index}", headers[i], i);
            }
            
            // Detect item column
            if (result.ItemColumnIndex < 0 && i != result.StoreColumnIndex && itemPatterns.Any(p => header.Contains(p)))
            {
                result.ItemColumnIndex = i;
                _logger?.LogInformation("Item column detected by header '{Header}' at index {Index}", headers[i], i);
            }
            
            // Detect quantity column
            if (result.QuantityColumnIndex < 0 && i != result.StoreColumnIndex && i != result.ItemColumnIndex && qtyPatterns.Any(p => header.Contains(p)))
            {
                result.QuantityColumnIndex = i;
                _logger?.LogInformation("Quantity column detected by header '{Header}' at index {Index}", headers[i], i);
            }
        }

        // Last resort: if we still don't have columns, use positional defaults
        if (result.StoreColumnIndex < 0 && result.ItemColumnIndex < 0 && headers.Count >= 3)
        {
            _logger?.LogWarning("Using positional fallback: Column 0 = Store, Column 1 = Item, Column 2 = Quantity");
            result.StoreColumnIndex = 0;
            result.ItemColumnIndex = 1;
            result.QuantityColumnIndex = 2;
        }
        else if (result.StoreColumnIndex < 0 && result.ItemColumnIndex < 0 && headers.Count >= 2)
        {
            _logger?.LogWarning("Using minimal positional fallback: Column 0 = Store, Column 1 = Item");
            result.StoreColumnIndex = 0;
            result.ItemColumnIndex = 1;
        }

        return result;
    }

    public void SetDictionaryItems(List<DictionaryItem> items)
    {
        DictionaryItems = items ?? new List<DictionaryItem>();
    }

    public void SetStoreDictionary(List<StoreEntry> stores)
    {
        StoreDictionary = stores ?? new List<StoreEntry>();
    }

    public async Task<Result<IReadOnlyList<AllocationEntry>>> ParseExcelAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<AllocationEntry>>.Failure($"File not found: {filePath}");

            await using var stream = File.OpenRead(filePath);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Read all data first
            var allRows = worksheet.RowsUsed().ToList();
            if (allRows.Count < 2) // Need at least header + 1 data row
                return Result<IReadOnlyList<AllocationEntry>>.Failure("File must have at least a header row and one data row");

            var headerRow = allRows[0];
            var dataRows = allRows.Skip(1).ToList();

            var headers = headerRow.CellsUsed()
                .Select(c => c.Value.ToString()?.Trim() ?? "")
                .ToList();

            _logger?.LogInformation("Excel headers: {Headers}", string.Join(", ", headers));

            // Smart column detection by analyzing actual data against dictionaries
            var columnMap = DetectColumnsByData(dataRows, headers);

            _logger?.LogInformation("Smart detection - Store col: {StoreCol}, Item col: {ItemCol}, Qty col: {QtyCol}",
                columnMap.StoreColumnIndex, columnMap.ItemColumnIndex, columnMap.QuantityColumnIndex);

            var entries = new List<AllocationEntry>();

            foreach (var row in dataRows)
            {
                // Use direct cell access by column index (1-based in ClosedXML)
                var storeValue = columnMap.StoreColumnIndex >= 0
                    ? row.Cell(columnMap.StoreColumnIndex + 1).Value.ToString()?.Trim() ?? ""
                    : "";

                var itemValue = columnMap.ItemColumnIndex >= 0
                    ? row.Cell(columnMap.ItemColumnIndex + 1).Value.ToString()?.Trim() ?? ""
                    : "";

                var qtyValue = columnMap.QuantityColumnIndex >= 0
                    ? row.Cell(columnMap.QuantityColumnIndex + 1).Value.ToString()?.Trim() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(storeValue) || string.IsNullOrWhiteSpace(itemValue))
                    continue;

                var quantity = ParseQuantity(qtyValue);
                if (quantity <= 0)
                    continue;

                var entry = new AllocationEntry
                {
                    StoreId = storeValue,
                    StoreName = storeValue,
                    ItemNumber = itemValue,
                    Description = "",  // Will be filled by enrichment
                    Quantity = quantity
                };

                entries.Add(entry);
            }

            _logger?.LogInformation("Parsed {Count} entries before enrichment", entries.Count);

            // Enrich with store dictionary
            EnrichWithStoreDictionary(entries);

            // Enrich with item dictionary
            EnrichWithItemDictionary(entries);

            _logger?.LogInformation("Completed enrichment, returning {Count} entries", entries.Count);

            return Result<IReadOnlyList<AllocationEntry>>.Success(
                entries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber).ToList());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing Excel: {FilePath}", filePath);
            return Result<IReadOnlyList<AllocationEntry>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<AllocationEntry>>> ParseCsvAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<AllocationEntry>>.Failure($"File not found: {filePath}");

            // Read all lines first to detect format
            var allLines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            
            // Try to detect pivot table format (stores as columns)
            var pivotResult = TryParsePivotCsv(allLines);
            if (pivotResult.IsSuccess && pivotResult.Value?.Count > 0)
            {
                _logger?.LogInformation("Parsed as pivot CSV with {Count} entries", pivotResult.Value.Count);
                return pivotResult;
            }

            // Fall back to standard row-based parsing
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord?.ToList() ?? new List<string>();

            var storeCol = FindColumnName(headers, "Store Name", "Store Nam", "Store Cod", "Store Code", "Store", "Location");
            var itemCol = FindColumnName(headers, "Item", "Item No", "Item Number", "SKU");
            var qtyCol = FindColumnName(headers, "Qty", "Quantity", "Amount");
            var descCol = FindColumnName(headers, "Description", "Desc");

            var entries = new List<AllocationEntry>();

            while (csv.Read())
            {
                var storeValue = GetField(csv, storeCol);
                var itemValue = GetField(csv, itemCol);
                var qtyValue = GetField(csv, qtyCol);
                var descValue = !string.IsNullOrEmpty(descCol) ? GetField(csv, descCol) : "";

                if (string.IsNullOrWhiteSpace(storeValue) || string.IsNullOrWhiteSpace(itemValue))
                    continue;

                var quantity = ParseQuantity(qtyValue);
                if (quantity <= 0)
                    continue;

                var entry = new AllocationEntry
                {
                    StoreId = storeValue.Trim(),
                    StoreName = storeValue.Trim(),
                    ItemNumber = itemValue.Trim(),
                    Description = descValue,
                    Quantity = quantity
                };

                entries.Add(entry);
            }

            EnrichWithStoreDictionary(entries);

            // Enrich with item dictionary
            EnrichWithItemDictionary(entries);

            return await Task.FromResult(Result<IReadOnlyList<AllocationEntry>>.Success(
                entries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber).ToList()));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing CSV: {FilePath}", filePath);
            return Result<IReadOnlyList<AllocationEntry>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    private void EnrichWithStoreDictionary(List<AllocationEntry> entries)
    {
        if (StoreDictionary == null || StoreDictionary.Count == 0)
            return;

        var storesByCode = StoreDictionary.ToDictionary(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase);
        var storesByName = StoreDictionary.ToDictionary(s => s.Name.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var key = entry.StoreId?.Trim() ?? "";
            if (string.IsNullOrEmpty(key)) continue;

            if (storesByCode.TryGetValue(key, out var store))
            {
                entry.StoreId = store.Code;
                entry.StoreName = store.Name;
            }
            else if (storesByName.TryGetValue(key.ToUpperInvariant(), out store))
            {
                entry.StoreId = store.Code;
                entry.StoreName = store.Name;
            }
        }
    }

    private void EnrichWithItemDictionary(List<AllocationEntry> entries)
    {
        if (DictionaryItems == null || DictionaryItems.Count == 0)
            return;

        // Create lookup dictionaries for fast access
        var itemsByNumber = DictionaryItems.ToDictionary(i => i.Number.Trim(), StringComparer.OrdinalIgnoreCase);
        var itemsBySku = new Dictionary<string, DictionaryItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in DictionaryItems)
        {
            foreach (var sku in item.Skus)
            {
                if (!itemsBySku.ContainsKey(sku.Trim()))
                    itemsBySku[sku.Trim()] = item;
            }
        }

        foreach (var entry in entries)
        {
            // Skip if already has a description
            if (!string.IsNullOrWhiteSpace(entry.Description))
                continue;

            var itemNumber = entry.ItemNumber?.Trim() ?? "";
            if (string.IsNullOrEmpty(itemNumber))
                continue;

            // Try exact item number match first
            if (itemsByNumber.TryGetValue(itemNumber, out var item))
            {
                entry.Description = item.Description;
            }
            // Try SKU match
            else if (itemsBySku.TryGetValue(itemNumber, out item))
            {
                entry.Description = item.Description;
            }
        }
    }

    private static string FindColumnName(List<string> headers, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var match = headers.FirstOrDefault(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        return headers.Count > 0 ? headers[0] : "";
    }

    private static string GetCellValue(IXLRow row, List<string> headers, string columnName)
    {
        var index = headers.FindIndex(h => h.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return "";
        return row.Cell(index + 1).Value.ToString() ?? "";
    }

    private static string GetField(CsvReader csv, string columnName)
    {
        try { return csv.GetField(columnName) ?? ""; }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Failed to get CSV field '{ColumnName}'", columnName);
            return "";
        }
    }

    private static int ParseQuantity(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return (int)d;
        return 0;
    }

    /// <summary>
    /// Try to parse CSV as a pivot table where items are rows and stores are columns.
    /// Format: Item, [metadata cols...], Store1, Store2, Store3, ...
    /// </summary>
    private Result<IReadOnlyList<AllocationEntry>> TryParsePivotCsv(string[] lines)
    {
        try
        {
            if (lines.Length < 2)
                return Result<IReadOnlyList<AllocationEntry>>.Failure("Not enough lines");

            // Find the header row (contains "Item" and store codes like 101, 102, etc.)
            int headerRowIndex = -1;
            string[]? headers = null;
            
            for (int i = 0; i < Math.Min(10, lines.Length); i++)
            {
                var fields = lines[i].Split(',');
                
                // Look for a row with "Item" in first column and numeric store codes
                var firstField = fields[0].Trim().ToLowerInvariant();
                if (firstField == "item" || firstField == "item no" || firstField == "item number" || firstField == "sku")
                {
                    // Check if there are numeric columns (store codes) - at least 3
                    int numericCount = 0;
                    for (int j = 1; j < fields.Length; j++)
                    {
                        var val = fields[j].Trim();
                        if (int.TryParse(val, out var num) && num >= 100 && num <= 999)
                            numericCount++;
                    }
                    
                    if (numericCount >= 3)
                    {
                        headerRowIndex = i;
                        headers = fields;
                        break;
                    }
                }
            }

            if (headerRowIndex < 0 || headers == null)
                return Result<IReadOnlyList<AllocationEntry>>.Failure("No pivot header found");

            _logger?.LogInformation("Found pivot header at row {Row} with {Cols} columns", headerRowIndex, headers.Length);

            // Identify store columns (3-digit numbers that match store dictionary or look like store codes)
            var storeColumns = new List<(int Index, string StoreCode)>();
            for (int col = 1; col < headers.Length; col++)
            {
                var header = headers[col].Trim();
                if (int.TryParse(header, out var storeNum) && storeNum >= 100 && storeNum <= 999)
                {
                    storeColumns.Add((col, header));
                }
            }

            if (storeColumns.Count == 0)
                return Result<IReadOnlyList<AllocationEntry>>.Failure("No store columns found");

            _logger?.LogInformation("Found {Count} store columns", storeColumns.Count);

            var entries = new List<AllocationEntry>();

            // Parse data rows
            for (int rowIdx = headerRowIndex + 1; rowIdx < lines.Length; rowIdx++)
            {
                var line = lines[rowIdx];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = line.Split(',');
                if (fields.Length < 2) continue;

                var itemNumber = fields[0].Trim();
                if (string.IsNullOrWhiteSpace(itemNumber)) continue;

                // Skip rows that look like headers or totals
                if (itemNumber.ToLowerInvariant() == "item" || 
                    itemNumber.ToLowerInvariant() == "total" ||
                    itemNumber.ToLowerInvariant().Contains("suggested"))
                    continue;

                // For each store column, check if there's a quantity
                foreach (var (colIndex, storeCode) in storeColumns)
                {
                    if (colIndex >= fields.Length) continue;
                    
                    var qtyStr = fields[colIndex].Trim();
                    var qty = ParseQuantity(qtyStr);
                    
                    if (qty > 0)
                    {
                        entries.Add(new AllocationEntry
                        {
                            StoreId = storeCode,
                            StoreName = storeCode,
                            ItemNumber = itemNumber,
                            Description = "",
                            Quantity = qty
                        });
                    }
                }
            }

            if (entries.Count == 0)
                return Result<IReadOnlyList<AllocationEntry>>.Failure("No entries parsed from pivot");

            // Enrich with dictionaries
            EnrichWithStoreDictionary(entries);
            EnrichWithItemDictionary(entries);

            _logger?.LogInformation("Parsed {Count} entries from pivot CSV", entries.Count);

            return Result<IReadOnlyList<AllocationEntry>>.Success(
                entries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber).ToList());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse as pivot CSV");
            return Result<IReadOnlyList<AllocationEntry>>.Failure($"Pivot parse failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse allocation data from clipboard text (tab or comma separated).
    /// Expects columns: Store, Item, Quantity (and optionally Description).
    /// </summary>
    public Result<IReadOnlyList<AllocationEntry>> ParseFromClipboardText(string clipboardText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
                return Result<IReadOnlyList<AllocationEntry>>.Failure("Clipboard is empty");

            var lines = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return Result<IReadOnlyList<AllocationEntry>>.Failure("No data found in clipboard");

            // Detect delimiter (tab preferred, then comma)
            char delimiter = lines[0].Contains('\t') ? '\t' : ',';

            // Parse first line as potential header
            var firstLine = lines[0].Split(delimiter);
            bool hasHeader = IsHeaderRow(firstLine);
            int startRow = hasHeader ? 1 : 0;

            // Detect column indices
            int storeCol = -1, itemCol = -1, qtyCol = -1, descCol = -1;

            if (hasHeader)
            {
                for (int i = 0; i < firstLine.Length; i++)
                {
                    var h = firstLine[i].Trim().ToLowerInvariant();
                    if (storeCol < 0 && (h.Contains("store") || h.Contains("location") || h.Contains("loc")))
                        storeCol = i;
                    else if (itemCol < 0 && (h.Contains("item") || h.Contains("sku") || h.Contains("product")))
                        itemCol = i;
                    else if (qtyCol < 0 && (h.Contains("qty") || h.Contains("quantity") || h.Contains("amount")))
                        qtyCol = i;
                    else if (descCol < 0 && (h.Contains("desc") || h.Contains("name")))
                        descCol = i;
                }
            }

            // If columns not detected from header, try to infer from data
            if (storeCol < 0 || itemCol < 0 || qtyCol < 0)
            {
                var detected = DetectColumnsFromClipboardData(lines, startRow, delimiter);
                if (storeCol < 0) storeCol = detected.StoreColumnIndex;
                if (itemCol < 0) itemCol = detected.ItemColumnIndex;
                if (qtyCol < 0) qtyCol = detected.QuantityColumnIndex;
            }

            // Fallback to positional: Store=0, Item=1, Qty=2
            if (storeCol < 0) storeCol = 0;
            if (itemCol < 0) itemCol = firstLine.Length > 1 ? 1 : 0;
            if (qtyCol < 0) qtyCol = firstLine.Length > 2 ? 2 : -1;

            var entries = new List<AllocationEntry>();

            for (int i = startRow; i < lines.Length; i++)
            {
                var fields = lines[i].Split(delimiter);
                if (fields.Length < 2) continue;

                var storeValue = storeCol >= 0 && storeCol < fields.Length ? fields[storeCol].Trim() : "";
                var itemValue = itemCol >= 0 && itemCol < fields.Length ? fields[itemCol].Trim() : "";
                var qtyValue = qtyCol >= 0 && qtyCol < fields.Length ? fields[qtyCol].Trim() : "1";
                var descValue = descCol >= 0 && descCol < fields.Length ? fields[descCol].Trim() : "";

                if (string.IsNullOrWhiteSpace(storeValue) || string.IsNullOrWhiteSpace(itemValue))
                    continue;

                var quantity = ParseQuantity(qtyValue);
                if (quantity <= 0) quantity = 1; // Default to 1 if no quantity

                entries.Add(new AllocationEntry
                {
                    StoreId = storeValue,
                    StoreName = storeValue,
                    ItemNumber = itemValue,
                    Description = descValue,
                    Quantity = quantity
                });
            }

            if (entries.Count == 0)
                return Result<IReadOnlyList<AllocationEntry>>.Failure("No valid entries found in clipboard data");

            // Enrich with dictionaries
            EnrichWithStoreDictionary(entries);
            EnrichWithItemDictionary(entries);

            _logger?.LogInformation("Parsed {Count} entries from clipboard", entries.Count);
            return Result<IReadOnlyList<AllocationEntry>>.Success(
                entries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber).ToList());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing clipboard data");
            return Result<IReadOnlyList<AllocationEntry>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    private bool IsHeaderRow(string[] fields)
    {
        // Check if fields look like headers (non-numeric, common header names)
        var headerKeywords = new[] { "store", "item", "qty", "quantity", "location", "sku", "desc", "name", "product", "amount" };
        int matches = 0;
        foreach (var f in fields)
        {
            var lower = f.Trim().ToLowerInvariant();
            if (headerKeywords.Any(k => lower.Contains(k)))
                matches++;
        }
        return matches >= 2; // At least 2 header-like columns
    }

    private ColumnMap DetectColumnsFromClipboardData(string[] lines, int startRow, char delimiter)
    {
        var result = new ColumnMap();
        if (lines.Length <= startRow) return result;

        var sampleSize = Math.Min(10, lines.Length - startRow);
        int columnCount = lines[startRow].Split(delimiter).Length;

        var storeMatches = new int[columnCount];
        var itemMatches = new int[columnCount];
        var numericValues = new int[columnCount];

        for (int i = startRow; i < startRow + sampleSize && i < lines.Length; i++)
        {
            var fields = lines[i].Split(delimiter);
            for (int col = 0; col < Math.Min(fields.Length, columnCount); col++)
            {
                var value = fields[col].Trim();
                if (string.IsNullOrEmpty(value)) continue;

                // Check store dictionary
                if (StoreDictionary.Any(s =>
                    s.Code.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Equals(value, StringComparison.OrdinalIgnoreCase)))
                {
                    storeMatches[col]++;
                }

                // Check 3-digit store code pattern
                if (int.TryParse(value, out var num) && num >= 100 && num <= 999)
                    storeMatches[col]++;

                // Check item dictionary
                if (DictionaryItems.Any(item =>
                    item.Number.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    item.Skus.Any(sku => sku.Equals(value, StringComparison.OrdinalIgnoreCase))))
                {
                    itemMatches[col]++;
                }

                // Check numeric (quantity)
                if (double.TryParse(value, out _))
                    numericValues[col]++;
            }
        }

        // Assign columns based on matches
        int maxStore = storeMatches.Max();
        if (maxStore > 0)
            result.StoreColumnIndex = Array.IndexOf(storeMatches, maxStore);

        int maxItem = 0;
        for (int i = 0; i < itemMatches.Length; i++)
        {
            if (i == result.StoreColumnIndex) continue;
            if (itemMatches[i] > maxItem)
            {
                maxItem = itemMatches[i];
                result.ItemColumnIndex = i;
            }
        }

        // Quantity is the most numeric column that isn't store or item
        int maxNumeric = 0;
        for (int i = 0; i < numericValues.Length; i++)
        {
            if (i == result.StoreColumnIndex || i == result.ItemColumnIndex) continue;
            if (numericValues[i] > maxNumeric)
            {
                maxNumeric = numericValues[i];
                result.QuantityColumnIndex = i;
            }
        }

        return result;
    }
}
