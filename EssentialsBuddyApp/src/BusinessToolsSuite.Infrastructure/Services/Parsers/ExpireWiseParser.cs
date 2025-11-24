using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using BusinessToolsSuite.Core.Entities.ExpireWise;
using BusinessToolsSuite.Core.Common;
using Microsoft.Extensions.Logging;

namespace BusinessToolsSuite.Infrastructure.Services.Parsers;

/// <summary>
/// Specialized parser for ExpireWise that matches the exact JavaScript logic:
/// - Expected columns: Item Number, SKU, Item Description, Location, Units Expiring, Expiry Date
/// - Handles Excel date numbers
/// - Filters items that have description, location, and expiry date
/// </summary>
public class ExpireWiseParser
{
    private readonly ILogger<ExpireWiseParser>? _logger;

    public ExpireWiseParser(ILogger<ExpireWiseParser>? logger = null)
    {
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ExpirationItem>>> ParseExcelAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<ExpirationItem>>.Failure($"File not found: {filePath}");

            await using var stream = File.OpenRead(filePath);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var headers = worksheet.Row(1).CellsUsed()
                .Select(c => c.Value.ToString() ?? "")
                .ToList();

            _logger?.LogInformation("ExpireWise: Excel columns found: {Columns}", string.Join(", ", headers));

            var items = new List<ExpirationItem>();
            int skippedCount = 0;

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                var itemNumber = GetCellString(row, headers, "Item Number", "ItemNumber", "Item No.", "Item No", "Item");
                var sku = GetCellString(row, headers, "SKU", "Sku");
                var description = GetCellString(row, headers, "Item Description", "Description", "Desc");
                var location = GetCellString(row, headers, "Location", "Loc", "Bin Code", "BinCode");
                var units = GetCellInt(row, headers, "Units Expiring", "Units", "Qty", "Quantity");
                var expiryDate = GetCellDateTime(row, headers, "Expiry Date", "ExpiryDate", "Expiry", "Exp Date", "Exp");

                // Filter: must have description, location, and expiry (exact JS logic)
                if (string.IsNullOrWhiteSpace(description) ||
                    string.IsNullOrWhiteSpace(location) ||
                    expiryDate == null)
                {
                    skippedCount++;
                    continue;
                }

                var item = new ExpirationItem
                {
                    ItemNumber = itemNumber.Trim(),
                    Description = description.Trim(),
                    Location = location.Trim(),
                    Units = units > 0 ? units : 1, // Default to 1 if not specified (JS logic)
                    ExpiryDate = expiryDate.Value,
                    Notes = sku // Store SKU in notes if present
                };

                items.Add(item);
            }

            _logger?.LogInformation("ExpireWise: Parsed {Count} items, Skipped {Skipped}",
                items.Count, skippedCount);

            // Sort by expiry date (earliest first)
            items = items.OrderBy(i => i.ExpiryDate).ThenBy(i => i.ItemNumber).ToList();

            return Result<IReadOnlyList<ExpirationItem>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing Excel for ExpireWise: {FilePath}", filePath);
            return Result<IReadOnlyList<ExpirationItem>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<ExpirationItem>>> ParseCsvAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<ExpirationItem>>.Failure($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            csv.Read();
            csv.ReadHeader();

            var headers = csv.HeaderRecord?.ToList() ?? [];
            var items = new List<ExpirationItem>();
            int skippedCount = 0;

            _logger?.LogInformation("ExpireWise: CSV columns found: {Columns}", string.Join(", ", headers));

            while (csv.Read())
            {
                var itemNumber = GetFieldByNames(csv, headers, "Item Number", "ItemNumber", "Item No.", "Item No", "Item");
                var sku = GetFieldByNames(csv, headers, "SKU", "Sku");
                var description = GetFieldByNames(csv, headers, "Item Description", "Description", "Desc");
                var location = GetFieldByNames(csv, headers, "Location", "Loc", "Bin Code", "BinCode");
                var unitsStr = GetFieldByNames(csv, headers, "Units Expiring", "Units", "Qty", "Quantity");
                var expiryStr = GetFieldByNames(csv, headers, "Expiry Date", "ExpiryDate", "Expiry", "Exp Date", "Exp");

                var units = ParseInt(unitsStr);
                var expiryDate = ParseDateTime(expiryStr);

                // Filter: must have description, location, and expiry
                if (string.IsNullOrWhiteSpace(description) ||
                    string.IsNullOrWhiteSpace(location) ||
                    expiryDate == null)
                {
                    skippedCount++;
                    continue;
                }

                var item = new ExpirationItem
                {
                    ItemNumber = itemNumber.Trim(),
                    Description = description.Trim(),
                    Location = location.Trim(),
                    Units = units > 0 ? units : 1,
                    ExpiryDate = expiryDate.Value,
                    Notes = sku
                };

                items.Add(item);
            }

            _logger?.LogInformation("ExpireWise: Parsed {Count} items from CSV, Skipped {Skipped}",
                items.Count, skippedCount);

            return await Task.FromResult(Result<IReadOnlyList<ExpirationItem>>.Success(
                items.OrderBy(i => i.ExpiryDate).ThenBy(i => i.ItemNumber).ToList()));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing CSV for ExpireWise: {FilePath}", filePath);
            return Result<IReadOnlyList<ExpirationItem>>.Failure($"Parse failed: {ex.Message}");
        }
    }

    private static string GetCellString(IXLRow row, List<string> headers, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var index = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var value = row.Cell(index + 1).Value.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        return "";
    }

    private static int GetCellInt(IXLRow row, List<string> headers, params string[] possibleNames)
    {
        var text = GetCellString(row, headers, possibleNames);
        return ParseInt(text);
    }

    private static DateTime? GetCellDateTime(IXLRow row, List<string> headers, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var index = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var cell = row.Cell(index + 1);
                var result = ConvertToDateTime(cell);
                if (result != null)
                    return result;
            }
        }
        return null;
    }

    /// <summary>
    /// Convert Excel cell to DateTime, handling Excel date numbers (exact JS logic)
    /// </summary>
    private static DateTime? ConvertToDateTime(IXLCell cell)
    {
        try
        {
            if (cell.IsEmpty())
                return null;

            // Try to get as DateTime directly (ClosedXML handles Excel dates)
            if (cell.Value.IsDateTime)
            {
                return cell.Value.GetDateTime();
            }

            // Handle Excel date numbers (JS: excelDate * 86400000)
            if (cell.Value.IsNumber)
            {
                var excelDate = cell.Value.GetNumber();
                // Excel epoch is 1899-12-30 (accounting for Excel 1900 leap year bug)
                var epoch = new DateTime(1899, 12, 30);
                var date = epoch.AddDays(excelDate);

                // Handle Excel 1900 leap year bug (dates before 1900-03-01)
                if (excelDate < 60)
                    date = date.AddDays(-1);

                return date;
            }

            // Try parsing as string
            var text = cell.Value.ToString();
            return ParseDateTime(text);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ParseDateTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (DateTime.TryParse(text, out var dt))
            return dt;

        // Try various date formats
        var formats = new[]
        {
            "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy",
            "yyyy-MM", "MMM yyyy", "MMMM yyyy",
            "yyyy/MM/dd", "dd-MM-yyyy"
        };

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        return null;
    }

    private static string GetFieldByNames(CsvReader csv, List<string> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.Any(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var value = csv.GetField(name);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch { }
            }
        }
        return "";
    }

    private static int ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return (int)d;
        return 0;
    }
}
