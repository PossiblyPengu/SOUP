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
        catch { return ""; }
    }

    private static int ParseQuantity(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return (int)d;
        return 0;
    }
}
