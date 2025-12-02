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
using SAP.Core.Entities.EssentialsBuddy;

namespace SAP.Infrastructure.Services.Parsers;

/// <summary>
/// Parser for EssentialsBuddy inventory files
/// </summary>
public class EssentialsBuddyParser
{
    private readonly ILogger<EssentialsBuddyParser>? _logger;

    public EssentialsBuddyParser(ILogger<EssentialsBuddyParser>? logger = null)
    {
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<InventoryItem>>> ParseExcelAsync(
        string filePath,
        int defaultThreshold = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<InventoryItem>>.Failure($"File not found: {filePath}");

            await using var stream = File.OpenRead(filePath);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var headers = worksheet.Row(1).CellsUsed()
                .Select(c => c.Value.ToString() ?? "")
                .ToList();

            return ProcessRows(rows, headers, defaultThreshold, (row, colIndex) =>
                row.Cell(colIndex + 1).Value.ToString() ?? "");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing Excel: {FilePath}", filePath);
            return Result<IReadOnlyList<InventoryItem>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<InventoryItem>>> ParseCsvAsync(
        string filePath,
        int defaultThreshold = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<InventoryItem>>.Failure($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            csv.Read();
            csv.ReadHeader();

            var headers = csv.HeaderRecord?.ToList() ?? new List<string>();
            var rows = new List<Dictionary<string, string>>();

            while (csv.Read())
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    row[header] = csv.GetField(header) ?? "";
                }
                rows.Add(row);
            }

            return await Task.FromResult(ProcessRowsDictionary(rows, headers, defaultThreshold));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing CSV: {FilePath}", filePath);
            return Result<IReadOnlyList<InventoryItem>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    private Result<IReadOnlyList<InventoryItem>> ProcessRows<TRow>(
        List<TRow> rows,
        List<string> headers,
        int defaultThreshold,
        Func<TRow, int, string> getCellValue)
    {
        var itemNoCol = FindColumnIndex(headers, "Item No.", "Item No", "ItemNo", "Item Number");
        var binCodeCol = FindColumnIndex(headers, "Bin Code", "BinCode", "Bin");
        var qtyCol = FindColumnIndex(headers, "Available Qty. to Take", "Quantity", "Qty");
        var descCol = FindColumnIndex(headers, "Description", "Desc");

        if (itemNoCol < 0 || qtyCol < 0)
        {
            return Result<IReadOnlyList<InventoryItem>>.Failure(
                "Required columns not found. Need: Item No and Quantity columns");
        }

        var itemMap = new Dictionary<string, (string Description, int Quantity, int Threshold)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var itemNo = getCellValue(row, itemNoCol).Trim().ToUpperInvariant();
            var binCode = binCodeCol >= 0 ? getCellValue(row, binCodeCol).Trim() : "N/A";
            var qtyText = getCellValue(row, qtyCol);
            var description = descCol >= 0 ? getCellValue(row, descCol).Trim() : "";

            if (string.IsNullOrWhiteSpace(itemNo))
                continue;

            // Filter: Only include bins starting with 9-90
            if (!binCode.ToUpperInvariant().StartsWith("9-90"))
                continue;

            var qty = ParseQuantity(qtyText);

            if (itemMap.TryGetValue(itemNo, out var existing))
            {
                itemMap[itemNo] = (existing.Description, existing.Quantity + qty, existing.Threshold);
            }
            else
            {
                itemMap[itemNo] = (description, qty, defaultThreshold);
            }
        }

        var items = itemMap.Select(kvp => new InventoryItem
        {
            Upc = kvp.Key,
            Description = kvp.Value.Description,
            QuantityOnHand = kvp.Value.Quantity,
            MinThreshold = kvp.Value.Threshold,
            BinCode = "9-90*"
        }).OrderBy(i => i.Status).ThenBy(i => i.Upc).ToList();

        return Result<IReadOnlyList<InventoryItem>>.Success(items);
    }

    private Result<IReadOnlyList<InventoryItem>> ProcessRowsDictionary(
        List<Dictionary<string, string>> rows,
        List<string> headers,
        int defaultThreshold)
    {
        return ProcessRows(rows, headers, defaultThreshold, (row, colIndex) =>
        {
            var header = headers[colIndex];
            return row.TryGetValue(header, out var value) ? value : "";
        });
    }

    private static int FindColumnIndex(List<string> headers, params string[] possibleNames)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (possibleNames.Any(name => name.Equals(headers[i].Trim(), StringComparison.OrdinalIgnoreCase)))
                return i;
        }
        return -1;
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
