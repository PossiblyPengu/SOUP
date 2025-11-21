using System.Globalization;
using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace BusinessToolsSuite.Infrastructure.Services;

/// <summary>
/// Service for importing and exporting files (Excel, CSV)
/// Supports flexible column name mapping from various Excel formats
/// </summary>
public class FileImportExportService : IFileImportExportService
{
    private readonly ILogger<FileImportExportService>? _logger;

    // Column name mappings for flexible import (property name -> possible Excel column names)
    private static readonly Dictionary<string, string[]> ColumnMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // AllocationBuddy mappings
        ["ItemNumber"] = ["Item No.", "Item No", "ItemNo", "Item Number", "Item", "Product", "SKU", "Code", "No."],
        ["Description"] = ["Description", "Desc", "Item Description", "ItemDescription", "Name", "Product Name"],
        ["StoreId"] = ["Store ID", "StoreID", "Store Code", "StoreCode", "Location Code", "LocationCode", "Loc", "Store"],
        ["StoreName"] = ["Store Name", "StoreName", "Shop Name", "ShopName", "Location Name", "Loc Name"],
        ["Quantity"] = ["Qty", "Quantity", "Amount", "Allocation", "Units", "Maximum Inventory", "Max Inv", "Reorder Point", "Available Qty. to Take", "Quantity Available to Take", "Qty Available to Take", "Available to Take"],
        ["Rank"] = ["Rank", "Store Rank", "StoreRank", "Priority"],
        ["Category"] = ["Category", "Cat", "Item Category", "Product Category"],
        ["UnitPrice"] = ["Unit Price", "UnitPrice", "Price", "Cost"],
        ["UnitCost"] = ["Unit Cost", "UnitCost", "Cost"],
        ["AllocationDate"] = ["Allocation Date", "AllocationDate", "Date"],
        ["Notes"] = ["Notes", "Note", "Comments", "Remarks"],

        // EssentialsBuddy mappings
        ["BinCode"] = ["Bin Code", "BinCode", "Bin", "Location"],
        ["QuantityOnHand"] = ["Quantity", "Qty", "Quantity On Hand", "QuantityOnHand", "On Hand", "Available Qty. to Take", "Quantity Available to Take"],
        ["MinimumThreshold"] = ["Minimum Threshold", "MinimumThreshold", "Min Threshold", "Threshold", "Reorder Point"],
        ["MaximumThreshold"] = ["Maximum Threshold", "MaximumThreshold", "Max Threshold", "Maximum Inventory"],

        // ExpireWise mappings
        ["ExpiryDate"] = ["Expiry Date", "ExpiryDate", "Expiry", "Expiration Date", "Exp Date", "Exp"],
        ["Location"] = ["Location", "Loc", "Bin Code", "BinCode", "Bin"],
        ["Units"] = ["Units", "Units Expiring", "Qty", "Quantity", "Amount"],
    };

    public FileImportExportService(ILogger<FileImportExportService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<T>>> ImportFromExcelAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            if (!File.Exists(filePath))
                return Result<IReadOnlyList<T>>.Failure($"File not found: {filePath}");

            await using var stream = File.OpenRead(filePath);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var data = new List<T>();
            var properties = typeof(T).GetProperties().Where(p => p.CanWrite).ToArray();
            var headers = worksheet.Row(1).CellsUsed()
                .Select((c, idx) => new { Index = idx, Name = c.Value.ToString() ?? "" })
                .ToList();

            _logger?.LogInformation("Excel columns found: {Columns}", string.Join(", ", headers.Select(h => h.Name)));

            // Build column-to-property mapping
            var columnPropertyMap = BuildColumnPropertyMap(headers.Select(h => h.Name).ToList(), properties);

            _logger?.LogInformation("Column mappings: {Mappings}",
                string.Join(", ", columnPropertyMap.Select(kvp => $"{kvp.Key} -> {kvp.Value.Name}")));

            var headerNames = headers.Select(h => h.Name).ToList();

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                // Skip rows that look like repeated headers
                if (IsHeaderRow(row, headerNames))
                    continue;

                var item = new T();
                bool hasAnyValue = false;

                for (int i = 0; i < headers.Count; i++)
                {
                    var headerName = headers[i].Name;
                    if (!columnPropertyMap.TryGetValue(headerName, out var prop))
                        continue;

                    var cell = row.Cell(i + 1);
                    var value = ConvertCellValue(cell, prop.PropertyType);

                    if (value != null)
                    {
                        prop.SetValue(item, value);
                        hasAnyValue = true;
                    }
                }

                if (hasAnyValue)
                    data.Add(item);
            }

            _logger?.LogInformation("Successfully imported {Count} records from Excel: {FilePath}",
                data.Count, filePath);

            return Result<IReadOnlyList<T>>.Success(data);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing from Excel: {FilePath}", filePath);
            return Result<IReadOnlyList<T>>.Failure($"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Build mapping from Excel column names to entity properties using flexible matching
    /// </summary>
    private Dictionary<string, PropertyInfo> BuildColumnPropertyMap(List<string> excelHeaders, PropertyInfo[] properties)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in excelHeaders)
        {
            if (string.IsNullOrWhiteSpace(header))
                continue;

            // Try exact match first
            var prop = properties.FirstOrDefault(p =>
                p.Name.Equals(header, StringComparison.OrdinalIgnoreCase));

            // Try column mappings
            if (prop == null)
            {
                foreach (var propInfo in properties)
                {
                    if (ColumnMappings.TryGetValue(propInfo.Name, out var aliases))
                    {
                        if (aliases.Any(alias => alias.Equals(header, StringComparison.OrdinalIgnoreCase)))
                        {
                            prop = propInfo;
                            break;
                        }
                    }
                }
            }

            // Try fuzzy match (header contains property name or vice versa)
            if (prop == null)
            {
                var normalizedHeader = NormalizeColumnName(header);
                prop = properties.FirstOrDefault(p =>
                    NormalizeColumnName(p.Name).Contains(normalizedHeader) ||
                    normalizedHeader.Contains(NormalizeColumnName(p.Name)));
            }

            if (prop != null && !map.ContainsKey(header))
            {
                map[header] = prop;
            }
        }

        return map;
    }

    /// <summary>
    /// Normalize column name for fuzzy matching (remove spaces, dots, lowercase)
    /// </summary>
    private static string NormalizeColumnName(string name)
    {
        return name.Replace(" ", "").Replace(".", "").Replace("_", "").ToLowerInvariant();
    }

    /// <summary>
    /// Check if a row appears to be a repeated header row
    /// </summary>
    private static bool IsHeaderRow(IXLRow row, List<string> headerNames)
    {
        int headerMatchCount = 0;
        for (int i = 0; i < headerNames.Count; i++)
        {
            var cellValue = row.Cell(i + 1).Value.ToString()?.Trim() ?? "";
            var headerValue = headerNames[i]?.Trim() ?? "";

            if (!string.IsNullOrEmpty(cellValue) &&
                cellValue.Equals(headerValue, StringComparison.OrdinalIgnoreCase))
            {
                headerMatchCount++;
            }
        }

        // If more than half the cells match header names, it's likely a header row
        return headerMatchCount > headerNames.Count / 2;
    }

    /// <summary>
    /// Convert cell value to target type, handling Excel-specific formats
    /// </summary>
    private static object? ConvertCellValue(IXLCell cell, Type targetType)
    {
        if (cell.IsEmpty())
            return null;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle DateTime specially - Excel stores dates as numbers
        if (underlyingType == typeof(DateTime))
        {
            return ConvertToDateTime(cell);
        }

        // For other types, convert from the cell's text value
        var text = cell.Value.ToString();
        return ConvertValue(text, targetType);
    }

    /// <summary>
    /// Convert Excel cell to DateTime, handling Excel date numbers
    /// </summary>
    private static DateTime? ConvertToDateTime(IXLCell cell)
    {
        try
        {
            // Try to get as DateTime directly (ClosedXML handles Excel dates)
            if (cell.Value.IsDateTime)
            {
                return cell.Value.GetDateTime();
            }

            // Handle Excel date numbers
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
            if (DateTime.TryParse(text, out var dt))
                return dt;

            // Try various date formats
            var formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM", "MMM yyyy" };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertValue(string? text, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string)) return text.Trim();
        if (underlyingType == typeof(int)) return int.TryParse(text, out var i) ? i : null;
        if (underlyingType == typeof(decimal)) return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (underlyingType == typeof(double)) return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var db) ? db : null;
        if (underlyingType == typeof(DateTime)) return DateTime.TryParse(text, out var dt) ? dt : null;
        if (underlyingType == typeof(bool)) return bool.TryParse(text, out var b) ? b : null;
        if (underlyingType == typeof(Guid)) return Guid.TryParse(text, out var g) ? g : null;
        if (underlyingType.IsEnum) return Enum.TryParse(underlyingType, text, true, out var e) ? e : null;

        try { return Convert.ChangeType(text, underlyingType, CultureInfo.InvariantCulture); }
        catch { return null; }
    }

    public async Task<Result<IReadOnlyList<T>>> ImportFromCsvAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            if (!File.Exists(filePath))
                return Result<IReadOnlyList<T>>.Failure($"File not found: {filePath}");

            var data = new List<T>();
            var properties = typeof(T).GetProperties().Where(p => p.CanWrite).ToArray();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim()
            });

            // Read header manually to apply column mapping
            csv.Read();
            csv.ReadHeader();

            var csvHeaders = csv.HeaderRecord?.ToList() ?? [];

            _logger?.LogInformation("CSV columns found: {Columns}", string.Join(", ", csvHeaders));

            // Build column-to-property mapping
            var columnPropertyMap = BuildColumnPropertyMap(csvHeaders, properties);

            _logger?.LogInformation("Column mappings: {Mappings}",
                string.Join(", ", columnPropertyMap.Select(kvp => $"{kvp.Key} -> {kvp.Value.Name}")));

            while (csv.Read())
            {
                var item = new T();
                bool hasAnyValue = false;

                foreach (var header in csvHeaders)
                {
                    if (!columnPropertyMap.TryGetValue(header, out var prop))
                        continue;

                    var fieldValue = csv.GetField(header);
                    if (string.IsNullOrWhiteSpace(fieldValue))
                        continue;

                    var value = ConvertValue(fieldValue, prop.PropertyType);
                    if (value != null)
                    {
                        prop.SetValue(item, value);
                        hasAnyValue = true;
                    }
                }

                if (hasAnyValue)
                    data.Add(item);
            }

            _logger?.LogInformation("Successfully imported {Count} records from CSV: {FilePath}",
                data.Count, filePath);

            return await Task.FromResult(Result<IReadOnlyList<T>>.Success(data));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing from CSV: {FilePath}", filePath);
            return Result<IReadOnlyList<T>>.Failure($"Import failed: {ex.Message}");
        }
    }

    public async Task<Result> ExportToExcelAsync<T>(
        IEnumerable<T> data,
        string filePath,
        string? sheetName = null,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName ?? "Data");

            var properties = typeof(T).GetProperties();
            var dataList = data.ToList();

            // Write headers
            for (int i = 0; i < properties.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = properties[i].Name;
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Write data
            for (int row = 0; row < dataList.Count; row++)
            {
                for (int col = 0; col < properties.Length; col++)
                {
                    var value = properties[col].GetValue(dataList[row]);
                    worksheet.Cell(row + 2, col + 1).Value = value?.ToString() ?? string.Empty;
                }
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);

            _logger?.LogInformation("Successfully exported {Count} records to Excel: {FilePath}",
                dataList.Count, filePath);

            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exporting to Excel: {FilePath}", filePath);
            return Result.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<Result> ExportToCsvAsync<T>(
        IEnumerable<T> data,
        string filePath,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            await using var writer = new StreamWriter(filePath);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            var dataList = data.ToList();
            await csv.WriteRecordsAsync(dataList, cancellationToken);

            _logger?.LogInformation("Successfully exported {Count} records to CSV: {FilePath}",
                dataList.Count, filePath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exporting to CSV: {FilePath}", filePath);
            return Result.Failure($"Export failed: {ex.Message}");
        }
    }
}
