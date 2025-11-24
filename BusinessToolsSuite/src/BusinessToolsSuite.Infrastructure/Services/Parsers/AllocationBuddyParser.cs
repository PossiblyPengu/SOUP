using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using BusinessToolsSuite.Core.Entities.AllocationBuddy;
using BusinessToolsSuite.Core.Common;
using Microsoft.Extensions.Logging;

namespace BusinessToolsSuite.Infrastructure.Services.Parsers;

// Simple dictionary item model (fallback if separate model not present)
public class DictionaryItem
{
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Skus { get; set; } = new();
}

/// <summary>
/// Specialized parser for AllocationBuddy that matches the exact JavaScript logic:
/// - Smart column detection
/// - Skips zero quantity rows
/// - Filters out repeated header rows
/// </summary>
public class AllocationBuddyParser
{
    // Dictionary data for matching
    public List<DictionaryItem> DictionaryItems { get; set; } = new();

    public void SetDictionaryItems(List<DictionaryItem> items)
    {
        DictionaryItems = items ?? new List<DictionaryItem>();
    }

    // Add: Dictionary-based column mapping support
    public Dictionary<string, string> HeaderMappings { get; set; } = new(); // e.g. { "SKU": "ItemNumber", "Qty": "Quantity" }

    public void SetHeaderMappings(Dictionary<string, string> mappings)
    {
        HeaderMappings = mappings ?? new Dictionary<string, string>();
    }

    private readonly ILogger<AllocationBuddyParser>? _logger;

    public AllocationBuddyParser(ILogger<AllocationBuddyParser>? logger = null)
    {
        _logger = logger;
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

            var headers = worksheet.Row(1).CellsUsed()
                .Select(c => c.Value.ToString() ?? "")
                .ToList();

            _logger?.LogInformation("AllocationBuddy: Excel columns found: {Columns}", string.Join(", ", headers));

            // Detect columns using the JS logic
            // Use dictionary-based mapping if available
            string storeCol = null, itemCol = null, qtyCol = null, descCol = null;
            if (HeaderMappings.Count > 0)
            {
                storeCol = HeaderMappings.FirstOrDefault(x => x.Value == "StoreId").Key;
                itemCol = HeaderMappings.FirstOrDefault(x => x.Value == "ItemNumber").Key;
                qtyCol = HeaderMappings.FirstOrDefault(x => x.Value == "Quantity").Key;
                descCol = HeaderMappings.FirstOrDefault(x => x.Value == "Description").Key;
            }
            else
            {
                var detected = DetectColumns(worksheet, headers);
                storeCol = detected.StoreCol;
                itemCol = detected.ItemCol;
                qtyCol = detected.QtyCol;
            }

            _logger?.LogInformation("AllocationBuddy: Detected columns - Store: {Store}, Item: {Item}, Qty: {Qty}",
                storeCol, itemCol, qtyCol);

            var entries = new List<AllocationEntry>();
            int skippedCount = 0;

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                // Skip header-like rows
                if (IsHeaderRow(row, headers))
                {
                    skippedCount++;
                    continue;
                }

                var storeValue = GetCellValue(row, headers, storeCol);
                var itemValue = GetCellValue(row, headers, itemCol);
                var qtyValue = GetCellValue(row, headers, qtyCol);
                var descValue = descCol != null ? GetCellValue(row, headers, descCol) : GetDescription(row, headers);

                var quantity = ParseQuantity(qtyValue);

                // Skip rows with zero or no quantity (exact JS logic)
                if (quantity <= 0)
                {
                    skippedCount++;
                    continue;
                }

                var entry = new AllocationEntry
                {
                    StoreId = storeValue.Trim(),
                    StoreName = storeValue.Trim(),
                    ItemNumber = NormalizeItemNo(itemValue),
                    Description = descValue,
                    Quantity = quantity,
                    Rank = ParseRank(row, headers)
                };

                entries.Add(entry);
            }

            _logger?.LogInformation("AllocationBuddy: Parsed {Count} entries, Skipped {Skipped}", entries.Count, skippedCount);

            // Use dictionary to enrich entries (match by number or SKU)
            if (DictionaryItems != null && DictionaryItems.Count > 0)
            {
                foreach (var entry in entries)
                {
                    var dictMatch = DictionaryItems.FirstOrDefault(d =>
                        string.Equals(d.Number, entry.ItemNumber, StringComparison.OrdinalIgnoreCase) ||
                        (d.Skus != null && d.Skus.Contains(entry.ItemNumber, StringComparer.OrdinalIgnoreCase)) ||
                        (d.Skus != null && d.Skus.Contains(entry.SKU ?? "", StringComparer.OrdinalIgnoreCase)));
                    if (dictMatch != null)
                    {
                        entry.Description = string.IsNullOrWhiteSpace(entry.Description) ? dictMatch.Description : entry.Description;
                        entry.ItemNumber = dictMatch.Number;
                        entry.SKU = dictMatch.Skus?.FirstOrDefault() ?? entry.SKU;
                    }
                }
            }

            // Sort by store, then by item (exact JS logic)
            entries = entries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber).ToList();

            return Result<IReadOnlyList<AllocationEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing Excel for AllocationBuddy: {FilePath}", filePath);
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
            var entries = new List<AllocationEntry>();

            // Detect columns
            // Use dictionary-based mapping if available
            string storeCol = null, itemCol = null, qtyCol = null, descCol = null;
            if (HeaderMappings.Count > 0)
            {
                storeCol = HeaderMappings.FirstOrDefault(x => x.Value == "StoreId").Key;
                itemCol = HeaderMappings.FirstOrDefault(x => x.Value == "ItemNumber").Key;
                qtyCol = HeaderMappings.FirstOrDefault(x => x.Value == "Quantity").Key;
                descCol = HeaderMappings.FirstOrDefault(x => x.Value == "Description").Key;
            }
            else
            {
                var detected = DetectColumnsCsv(headers);
                storeCol = detected.StoreCol;
                itemCol = detected.ItemCol;
                qtyCol = detected.QtyCol;
            }

            _logger?.LogInformation("AllocationBuddy: CSV columns - Store: {Store}, Item: {Item}, Qty: {Qty}",
                storeCol, itemCol, qtyCol);

            int skippedCount = 0;

            while (csv.Read())
            {
                var storeValue = GetField(csv, storeCol);
                var itemValue = GetField(csv, itemCol);
                var qtyValue = GetField(csv, qtyCol);
                var descValue = descCol != null ? GetField(csv, descCol) : GetFieldByNames(csv, headers, "Description", "Desc", "Item Description");

                var quantity = ParseQuantity(qtyValue);

                // Skip rows with zero quantity
                if (quantity <= 0)
                {
                    skippedCount++;
                    continue;
                }

                var entry = new AllocationEntry
                {
                    StoreId = storeValue.Trim(),
                    StoreName = storeValue.Trim(),
                    ItemNumber = NormalizeItemNo(itemValue),
                    Description = descValue,
                    Quantity = quantity,
                    Rank = ParseRankCsv(csv, headers)
                };

                entries.Add(entry);
            }

            _logger?.LogInformation("AllocationBuddy: Parsed {Count} entries from CSV", entries.Count);

            // Enrich with dictionary matches
            if (DictionaryItems != null && DictionaryItems.Count > 0)
            {
                foreach (var entry in entries)
                {
                    var dictMatch = DictionaryItems.FirstOrDefault(d =>
                        string.Equals(d.Number, entry.ItemNumber, StringComparison.OrdinalIgnoreCase) ||
                        (d.Skus != null && d.Skus.Contains(entry.ItemNumber, StringComparer.OrdinalIgnoreCase)) ||
                        (d.Skus != null && d.Skus.Contains(entry.SKU ?? "", StringComparer.OrdinalIgnoreCase)));
                    if (dictMatch != null)
                    {
                        entry.Description = string.IsNullOrWhiteSpace(entry.Description) ? dictMatch.Description : entry.Description;
                        entry.ItemNumber = dictMatch.Number;
                        entry.SKU = dictMatch.Skus?.FirstOrDefault() ?? entry.SKU;
                    }
                }
            }

            return await Task.FromResult(Result<IReadOnlyList<AllocationEntry>>.Success(entries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber).ToList()));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing CSV for AllocationBuddy: {FilePath}", filePath);
            return Result<IReadOnlyList<AllocationEntry>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detect columns using smart detection (matching JS detectColumnsUsingDictionary logic)
    /// </summary>
    private (string StoreCol, string ItemCol, string QtyCol) DetectColumns(IXLWorksheet worksheet, List<string> headers)
    {
        // Try header-based detection first (fallback in JS)
        var storeCol = FindColumnName(headers,
            "Store Name", "Shop Name", "Loc Name", "Location Code", "Store Code", "Store", "Shop", "Location", "Loc");

        var itemCol = FindColumnName(headers,
            "Item", "Item No", "Item No.", "Item Number", "Product", "SKU");

        var qtyCol = FindColumnName(headers,
            "Qty", "Quantity", "Amount", "Allocation", "Units", "Maximum Inventory", "Max Inv", "Reorder Point");

        // Fallback to position-based if needed
        if (string.IsNullOrEmpty(storeCol) && headers.Count > 0)
            storeCol = headers[0];
        if (string.IsNullOrEmpty(itemCol) && headers.Count > 1)
            itemCol = headers[1];
        if (string.IsNullOrEmpty(qtyCol) && headers.Count > 2)
            qtyCol = headers[2];

        return (storeCol, itemCol, qtyCol);
    }

    private (string StoreCol, string ItemCol, string QtyCol) DetectColumnsCsv(List<string> headers)
    {
        var storeCol = FindColumnName(headers,
            "Store Name", "Shop Name", "Loc Name", "Location Code", "Store Code", "Store", "Shop", "Location", "Loc");

        var itemCol = FindColumnName(headers,
            "Item", "Item No", "Item No.", "Item Number", "Product", "SKU");

        var qtyCol = FindColumnName(headers,
            "Qty", "Quantity", "Amount", "Allocation", "Units", "Maximum Inventory", "Max Inv", "Reorder Point");

        if (string.IsNullOrEmpty(storeCol) && headers.Count > 0)
            storeCol = headers[0];
        if (string.IsNullOrEmpty(itemCol) && headers.Count > 1)
            itemCol = headers[1];
        if (string.IsNullOrEmpty(qtyCol) && headers.Count > 2)
            qtyCol = headers[2];

        return (storeCol, itemCol, qtyCol);
    }

    private static string FindColumnName(List<string> headers, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var match = headers.FirstOrDefault(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        return "";
    }

    private static string GetCellValue(IXLRow row, List<string> headers, string columnName)
    {
        var index = headers.FindIndex(h => h.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return "";
        return row.Cell(index + 1).Value.ToString() ?? "";
    }

    private static string GetDescription(IXLRow row, List<string> headers)
    {
        var names = new[] { "Description", "Desc", "Item Description", "ItemDescription" };
        foreach (var name in names)
        {
            var value = GetCellValue(row, headers, name);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static StoreRank ParseRank(IXLRow row, List<string> headers)
    {
        var names = new[] { "Rank", "Store Rank", "StoreRank", "Priority" };
        foreach (var name in names)
        {
            var value = GetCellValue(row, headers, name).Trim().ToUpperInvariant();
            if (Enum.TryParse<StoreRank>(value, out var rank))
                return rank;
        }
        return StoreRank.C; // Default
    }

    private static StoreRank ParseRankCsv(CsvReader csv, List<string> headers)
    {
        var value = GetFieldByNames(csv, headers, "Rank", "Store Rank", "StoreRank", "Priority")
            .Trim().ToUpperInvariant();
        if (Enum.TryParse<StoreRank>(value, out var rank))
            return rank;
        return StoreRank.C;
    }

    private static string GetField(CsvReader csv, string columnName)
    {
        try { return csv.GetField(columnName) ?? ""; }
        catch { return ""; }
    }

    private static string GetFieldByNames(CsvReader csv, List<string> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.Any(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                try { return csv.GetField(name) ?? ""; }
                catch { }
            }
        }
        return "";
    }

    /// <summary>
    /// Check if row is a repeated header row (exact JS filterHeaderRows logic)
    /// </summary>
    private static bool IsHeaderRow(IXLRow row, List<string> headers)
    {
        int matchCount = 0;
        for (int i = 0; i < headers.Count; i++)
        {
            var cellValue = row.Cell(i + 1).Value.ToString()?.Trim() ?? "";
            var headerValue = headers[i].Trim();

            if (!string.IsNullOrEmpty(cellValue) &&
                (cellValue.Equals(headerValue, StringComparison.OrdinalIgnoreCase) ||
                 cellValue.Contains(headerValue, StringComparison.OrdinalIgnoreCase) ||
                 headerValue.Contains(cellValue, StringComparison.OrdinalIgnoreCase)))
            {
                matchCount++;
            }
        }

        // If more than half match, it's a header row
        return matchCount > headers.Count / 2;
    }

    private static string NormalizeItemNo(string itemNo)
    {
        if (string.IsNullOrWhiteSpace(itemNo))
            return "";
        return itemNo.Trim().ToUpperInvariant();
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
