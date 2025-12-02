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
using SAP.Core.Entities.ExpireWise;

namespace SAP.Infrastructure.Services.Parsers;

/// <summary>
/// Parser for ExpireWise expiration tracking files
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

            var items = new List<ExpirationItem>();

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                var itemNumber = GetCellString(row, headers, "Item Number", "Item No.", "Item No", "Item");
                var description = GetCellString(row, headers, "Item Description", "Description", "Desc");
                var location = GetCellString(row, headers, "Location", "Loc", "Bin Code");
                var units = GetCellInt(row, headers, "Units Expiring", "Units", "Qty", "Quantity");
                var expiryDate = GetCellDateTime(row, headers, "Expiry Date", "ExpiryDate", "Expiry", "Exp Date");

                if (string.IsNullOrWhiteSpace(description) ||
                    string.IsNullOrWhiteSpace(location) ||
                    expiryDate == null)
                    continue;

                var item = new ExpirationItem
                {
                    Upc = itemNumber.Trim(),
                    Description = description.Trim(),
                    Location = location.Trim(),
                    Quantity = units > 0 ? units : 1,
                    ExpiryDate = expiryDate.Value
                };

                items.Add(item);
            }

            return Result<IReadOnlyList<ExpirationItem>>.Success(
                items.OrderBy(i => i.ExpiryDate).ThenBy(i => i.Upc).ToList());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing Excel: {FilePath}", filePath);
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

            var headers = csv.HeaderRecord?.ToList() ?? new List<string>();
            var items = new List<ExpirationItem>();

            while (csv.Read())
            {
                var itemNumber = GetFieldByNames(csv, headers, "Item Number", "Item No.", "Item No", "Item");
                var description = GetFieldByNames(csv, headers, "Item Description", "Description", "Desc");
                var location = GetFieldByNames(csv, headers, "Location", "Loc", "Bin Code");
                var unitsStr = GetFieldByNames(csv, headers, "Units Expiring", "Units", "Qty", "Quantity");
                var expiryStr = GetFieldByNames(csv, headers, "Expiry Date", "ExpiryDate", "Expiry", "Exp Date");

                var units = ParseInt(unitsStr);
                var expiryDate = ParseDateTime(expiryStr);

                if (string.IsNullOrWhiteSpace(description) ||
                    string.IsNullOrWhiteSpace(location) ||
                    expiryDate == null)
                    continue;

                var item = new ExpirationItem
                {
                    Upc = itemNumber.Trim(),
                    Description = description.Trim(),
                    Location = location.Trim(),
                    Quantity = units > 0 ? units : 1,
                    ExpiryDate = expiryDate.Value
                };

                items.Add(item);
            }

            return await Task.FromResult(Result<IReadOnlyList<ExpirationItem>>.Success(
                items.OrderBy(i => i.ExpiryDate).ThenBy(i => i.Upc).ToList()));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing CSV: {FilePath}", filePath);
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
                if (cell.Value.IsDateTime)
                    return cell.Value.GetDateTime();
                if (cell.Value.IsNumber)
                {
                    var excelDate = cell.Value.GetNumber();
                    var epoch = new DateTime(1899, 12, 30);
                    return epoch.AddDays(excelDate);
                }
                var text = cell.Value.ToString();
                var result = ParseDateTime(text);
                if (result != null)
                    return result;
            }
        }
        return null;
    }

    private static DateTime? ParseDateTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (DateTime.TryParse(text, out var dt))
            return dt;

        var formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM", "MMM yyyy" };
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
