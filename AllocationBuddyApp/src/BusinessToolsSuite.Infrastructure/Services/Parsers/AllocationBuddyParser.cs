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

public class StoreEntry
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rank { get; set; } = "";
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

    // Store dictionary for exact store matching
    public List<StoreEntry> StoreDictionary { get; set; } = new();

    public void SetStoreDictionary(List<StoreEntry> stores)
    {
        StoreDictionary = stores ?? new List<StoreEntry>();
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

            // Detect columns using content-based detection (dictionary-driven when available)
            var detected = DetectColumns(worksheet, headers);
            var storeCol = detected.StoreCol;
            var itemCol = detected.ItemCol;
            var qtyCol = detected.QtyCol;
            var descCol = detected.DescCol;

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

            // Map stores to StoreDictionary if available (exact match by code or name)
            if (StoreDictionary != null && StoreDictionary.Count > 0)
            {
                var storesByCode = StoreDictionary.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
                var storesByName = StoreDictionary.ToDictionary(s => s.Name.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    var key = entry.StoreId?.Trim() ?? "";
                    if (storesByCode.TryGetValue(key, out var s))
                    {
                        entry.StoreId = s.Code;
                        entry.StoreName = s.Name;
                    }
                    else if (storesByName.TryGetValue(key.ToUpperInvariant(), out s))
                    {
                        entry.StoreId = s.Code;
                        entry.StoreName = s.Name;
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

            // Read all CSV rows into memory (needed for content-based detection)
            var rows = new List<string[]>();
            while (csv.Read())
            {
                var rec = new string[headers.Count];
                for (int i = 0; i < headers.Count; i++)
                {
                    try { rec[i] = csv.GetField(headers[i]) ?? ""; }
                    catch { rec[i] = ""; }
                }
                rows.Add(rec);
            }

            var entries = new List<AllocationEntry>();

            // Detect columns - prefer content-based detection when dictionaries are available
            string storeCol = null, itemCol = null, qtyCol = null, descCol = null;
            if ((DictionaryItems != null && DictionaryItems.Count > 0) || (StoreDictionary != null && StoreDictionary.Count > 0))
            {
                // Item detection (dictionary exact or SKU match)
                if (DictionaryItems != null && DictionaryItems.Count > 0)
                {
                    var dictNumbers = new HashSet<string>(DictionaryItems.Select(d => NormalizeItemNo(d.Number)), StringComparer.OrdinalIgnoreCase);
                    var dictSkus = new HashSet<string>(DictionaryItems.SelectMany(d => d.Skus ?? new List<string>()).Select(s => NormalizeItemNo(s)), StringComparer.OrdinalIgnoreCase);

                    int bestMatchCount = 0; int bestIdx = -1;
                    for (int col = 0; col < headers.Count; col++)
                    {
                        int matchCount = 0;
                        foreach (var r in rows.Take(500))
                        {
                            var n = NormalizeItemNo(r[col]);
                            if (dictNumbers.Contains(n) || dictSkus.Contains(n)) matchCount++;
                        }
                        if (matchCount > bestMatchCount) { bestMatchCount = matchCount; bestIdx = col; }
                    }
                    if (bestIdx >= 0 && bestMatchCount > 0) itemCol = headers[bestIdx];
                }

                // Qty detection by numeric prevalence
                int bestQtyMatches = 0; int bestQtyIdx = -1;
                for (int col = 0; col < headers.Count; col++)
                {
                    int qtyMatches = 0;
                    foreach (var r in rows.Take(500))
                    {
                        if (int.TryParse((r[col] ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out _)) qtyMatches++;
                    }
                    if (qtyMatches > bestQtyMatches) { bestQtyMatches = qtyMatches; bestQtyIdx = col; }
                }
                if (bestQtyIdx >= 0 && bestQtyMatches > 0) qtyCol = headers[bestQtyIdx];

                // Store detection using exact store dictionary if present, otherwise heuristics
                if (StoreDictionary != null && StoreDictionary.Count > 0)
                {
                    int bestStoreMatches = 0; int bestStoreIdx = -1;
                    var storeCodes = new HashSet<string>(StoreDictionary.Select(s => s.Code), StringComparer.OrdinalIgnoreCase);
                    var storeNames = new HashSet<string>(StoreDictionary.Select(s => s.Name.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
                    for (int col = 0; col < headers.Count; col++)
                    {
                        int matches = 0;
                        foreach (var r in rows.Take(500))
                        {
                            var v = (r[col] ?? "").Trim();
                            if (string.IsNullOrEmpty(v)) continue;
                            if (storeCodes.Contains(v) || storeNames.Contains(v.ToUpperInvariant())) matches++;
                        }
                        if (matches > bestStoreMatches) { bestStoreMatches = matches; bestStoreIdx = col; }
                    }
                    if (bestStoreIdx >= 0 && bestStoreMatches > 0) storeCol = headers[bestStoreIdx];
                }
                else
                {
                    int bestStoreScore = 0; int bestStoreIdx = -1;
                    for (int col = 0; col < headers.Count; col++)
                    {
                        var vals = rows.Select(r => (r[col] ?? "").Trim()).Where(s => !string.IsNullOrEmpty(s)).Take(200).ToList();
                        if (vals.Count == 0) continue;
                        int codeMatches = vals.Count(v => System.Text.RegularExpressions.Regex.IsMatch(v, "^\\d{1,4}$"));
                        int nameMatches = vals.Count(v => System.Text.RegularExpressions.Regex.IsMatch(v, "[A-Za-z]"));
                        int distinct = vals.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                        int score = codeMatches * 3 + nameMatches * 1 + Math.Min(distinct, 10);
                        if (score > bestStoreScore) { bestStoreScore = score; bestStoreIdx = col; }
                    }
                    if (bestStoreIdx >= 0 && bestStoreScore > 0) storeCol = headers[bestStoreIdx];
                }

                // Desc column heuristic
                descCol = FindColumnName(headers, "Description", "Desc", "Item Description", "ItemDescription");
            }
            else
            {
                var detected = DetectColumnsCsv(headers);
                storeCol = detected.StoreCol;
                itemCol = detected.ItemCol;
                qtyCol = detected.QtyCol;
            }

            _logger?.LogInformation("AllocationBuddy: CSV columns - Store: {Store}, Item: {Item}, Qty: {Qty}", storeCol, itemCol, qtyCol);

            int skippedCount = 0;

            // Map column names to indexes for faster access
            int storeIdx = headers.FindIndex(h => h.Equals(storeCol, StringComparison.OrdinalIgnoreCase));
            int itemIdx = headers.FindIndex(h => h.Equals(itemCol, StringComparison.OrdinalIgnoreCase));
            int qtyIdx = headers.FindIndex(h => h.Equals(qtyCol, StringComparison.OrdinalIgnoreCase));
            int descIdx = descCol != null ? headers.FindIndex(h => h.Equals(descCol, StringComparison.OrdinalIgnoreCase)) : -1;

            foreach (var rec in rows)
            {
                var storeValue = storeIdx >= 0 && storeIdx < rec.Length ? rec[storeIdx] : "";
                var itemValue = itemIdx >= 0 && itemIdx < rec.Length ? rec[itemIdx] : "";
                var qtyValue = qtyIdx >= 0 && qtyIdx < rec.Length ? rec[qtyIdx] : "";
                var descValue = descIdx >= 0 && descIdx < rec.Length ? rec[descIdx] : GetFieldByNamesRec(rec, headers, "Description", "Desc", "Item Description");

                var quantity = ParseQuantity(qtyValue);
                if (quantity <= 0) { skippedCount++; continue; }

                var entry = new AllocationEntry
                {
                    StoreId = storeValue.Trim(),
                    StoreName = storeValue.Trim(),
                    ItemNumber = NormalizeItemNo(itemValue),
                    Description = descValue,
                    Quantity = quantity,
                    Rank = ParseRankFromRec(rec, headers)
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

            // Map stores using StoreDictionary if available
            if (StoreDictionary != null && StoreDictionary.Count > 0)
            {
                var storesByCode = StoreDictionary.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
                var storesByName = StoreDictionary.ToDictionary(s => s.Name.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    var key = entry.StoreId?.Trim() ?? "";
                    if (storesByCode.TryGetValue(key, out var s))
                    {
                        entry.StoreId = s.Code;
                        entry.StoreName = s.Name;
                    }
                    else if (storesByName.TryGetValue(key.ToUpperInvariant(), out s))
                    {
                        entry.StoreId = s.Code;
                        entry.StoreName = s.Name;
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
    /// Detect columns using smart detection: prefer dictionary-based scanning across rows,
    /// fallback to header-name heuristics and position-based guesses.
    /// Returns column names for store, item, qty and description.
    /// </summary>
    private (string StoreCol, string ItemCol, string QtyCol, string DescCol) DetectColumns(IXLWorksheet worksheet, List<string> headers)
    {
        // Initial header-name heuristics
        var storeCol = FindColumnName(headers,
            "Store Name", "Shop Name", "Loc Name", "Location Code", "Store Code", "Store", "Shop", "Location", "Loc");

        var itemCol = FindColumnName(headers,
            "Item", "Item No", "Item No.", "Item Number", "Product", "SKU");

        var qtyCol = FindColumnName(headers,
            "Qty", "Quantity", "Amount", "Allocation", "Units", "Maximum Inventory", "Max Inv", "Reorder Point");

        var descCol = FindColumnName(headers, "Description", "Desc", "Item Description", "ItemDescription");

        // If dictionary items are available, scan columns to find the best item column
        if (DictionaryItems != null && DictionaryItems.Count > 0)
        {
            var rows = worksheet.RowsUsed().Skip(1).Take(500).ToList();
            var dictNumbers = new HashSet<string>(DictionaryItems.Select(d => NormalizeItemNo(d.Number)), StringComparer.OrdinalIgnoreCase);
            var dictSkus = new HashSet<string>(DictionaryItems.SelectMany(d => d.Skus ?? new List<string>()).Select(s => NormalizeItemNo(s)), StringComparer.OrdinalIgnoreCase);

            int bestMatchCount = 0;
            int bestIdx = -1;
            for (int col = 1; col <= headers.Count; col++)
            {
                int matchCount = 0;
                foreach (var r in rows)
                {
                    var cell = r.Cell(col).GetString();
                    var n = NormalizeItemNo(cell);
                    if (dictNumbers.Contains(n) || dictSkus.Contains(n))
                        matchCount++;
                }
                if (matchCount > bestMatchCount)
                {
                    bestMatchCount = matchCount;
                    bestIdx = col - 1;
                }
            }

            if (bestIdx >= 0 && bestMatchCount > 0)
                itemCol = headers[bestIdx];

            // Detect qty column by numeric prevalence
            int bestQtyMatches = 0; int bestQtyIdx = -1;
            for (int col = 1; col <= headers.Count; col++)
            {
                int qtyMatches = 0;
                foreach (var r in rows)
                {
                    var cell = r.Cell(col).GetString();
                    if (int.TryParse(cell?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        qtyMatches++;
                }
                if (qtyMatches > bestQtyMatches)
                {
                    bestQtyMatches = qtyMatches;
                    bestQtyIdx = col - 1;
                }
            }
            if (bestQtyIdx >= 0 && bestQtyMatches > 0)
                qtyCol = headers[bestQtyIdx];

            // Detect store column: prefer exact matches against StoreDictionary if available
            if (StoreDictionary != null && StoreDictionary.Count > 0)
            {
                int bestStoreMatches = 0; int bestStoreIdx = -1;
                var storeCodes = new HashSet<string>(StoreDictionary.Select(s => s.Code), StringComparer.OrdinalIgnoreCase);
                var storeNames = new HashSet<string>(StoreDictionary.Select(s => s.Name.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
                for (int col = 1; col <= headers.Count; col++)
                {
                    int matches = 0;
                    foreach (var r in rows)
                    {
                        var v = r.Cell(col).GetString().Trim();
                        if (string.IsNullOrEmpty(v)) continue;
                        if (storeCodes.Contains(v) || storeNames.Contains(v.ToUpperInvariant())) matches++;
                    }
                    if (matches > bestStoreMatches) { bestStoreMatches = matches; bestStoreIdx = col - 1; }
                }
                if (bestStoreIdx >= 0 && bestStoreMatches > 0) storeCol = headers[bestStoreIdx];
            }
            else
            {
                int bestStoreScore = 0; int bestStoreIdx = -1;
                for (int col = 1; col <= headers.Count; col++)
                {
                    var values = rows.Select(r => r.Cell(col).GetString().Trim()).Where(s => !string.IsNullOrEmpty(s)).Take(200).ToList();
                    if (values.Count == 0) continue;
                    int codeMatches = values.Count(v => System.Text.RegularExpressions.Regex.IsMatch(v, "^\\d{1,4}$"));
                    int nameMatches = values.Count(v => System.Text.RegularExpressions.Regex.IsMatch(v, "[A-Za-z]"));
                    int distinct = values.Select(v => v).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    int score = codeMatches * 3 + nameMatches * 1 + Math.Min(distinct, 10);
                    if (score > bestStoreScore)
                    {
                        bestStoreScore = score;
                        bestStoreIdx = col - 1;
                    }
                }
                if (bestStoreIdx >= 0 && bestStoreScore > 0)
                    storeCol = headers[bestStoreIdx];
            }
        }

        // Fallback to position-based if needed
        if (string.IsNullOrEmpty(storeCol) && headers.Count > 0)
            storeCol = headers[0];
        if (string.IsNullOrEmpty(itemCol) && headers.Count > 1)
            itemCol = headers[1];
        if (string.IsNullOrEmpty(qtyCol) && headers.Count > 2)
            qtyCol = headers[2];

        return (storeCol, itemCol, qtyCol, descCol);
    }

    private (string StoreCol, string ItemCol, string QtyCol) DetectColumnsCsv(List<string> headers)
    {
        // Simple header-based heuristics for CSV. We'll prefer content-based detection in ParseCsvAsync
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

    private static string GetFieldByNamesRec(string[] rec, List<string> headers, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var idx = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx < rec.Length)
            {
                return rec[idx] ?? "";
            }
        }
        return "";
    }

    private static StoreRank ParseRankFromRec(string[] rec, List<string> headers)
    {
        var val = GetFieldByNamesRec(rec, headers, "Rank", "Store Rank", "StoreRank", "Priority").Trim().ToUpperInvariant();
        if (Enum.TryParse<StoreRank>(val, out var rank))
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
