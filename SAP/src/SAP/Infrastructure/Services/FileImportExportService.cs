using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using SAP.Core.Common;
using SAP.Core.Interfaces;

namespace SAP.Infrastructure.Services;

/// <summary>
/// Service for importing and exporting files (Excel, CSV)
/// </summary>
public class FileImportExportService : IFileImportExportService
{
    private readonly ILogger<FileImportExportService>? _logger;

    public FileImportExportService(ILogger<FileImportExportService>? logger = null)
    {
        _logger = logger;
    }

    // Maximum allowed file path length
    private const int MaxPathLength = 260;

    // Maximum clipboard text length to prevent DoS
    private const int MaxClipboardTextLength = 10_000_000; // 10MB

    public async Task<Result<IEnumerable<T>>> ImportFromExcelAsync<T>(string filePath) where T : class, new()
    {
        try
        {
            // Validate file path
            var pathValidation = ValidateFilePath(filePath);
            if (!pathValidation.IsSuccess)
                return Result<IEnumerable<T>>.Failure(pathValidation.ErrorMessage ?? "Invalid file path");

            if (!File.Exists(filePath))
                return Result<IEnumerable<T>>.Failure($"File not found: {filePath}");

            // Validate file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
                return Result<IEnumerable<T>>.Failure($"Invalid file type: expected Excel file (.xlsx, .xls), got {extension}");

            await using var stream = File.OpenRead(filePath);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var data = new List<T>();
            var properties = typeof(T).GetProperties().Where(p => p.CanWrite).ToArray();
            var headers = worksheet.Row(1).CellsUsed()
                .Select((c, idx) => new { Index = idx, Name = c.Value.ToString() ?? "" })
                .ToList();

            var columnPropertyMap = BuildColumnPropertyMap(headers.Select(h => h.Name).ToList(), properties);

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
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

            return Result<IEnumerable<T>>.Success(data);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing from Excel: {FilePath}", filePath);
            return Result<IEnumerable<T>>.Failure($"Import failed: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<T>>> ImportFromCsvAsync<T>(string filePath) where T : class, new()
    {
        try
        {
            // Validate file path
            var pathValidation = ValidateFilePath(filePath);
            if (!pathValidation.IsSuccess)
                return Result<IEnumerable<T>>.Failure(pathValidation.ErrorMessage ?? "Invalid file path");

            if (!File.Exists(filePath))
                return Result<IEnumerable<T>>.Failure($"File not found: {filePath}");

            // Validate file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".csv")
                return Result<IEnumerable<T>>.Failure($"Invalid file type: expected CSV file (.csv), got {extension}");

            var data = new List<T>();
            var properties = typeof(T).GetProperties().Where(p => p.CanWrite).ToArray();

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            csv.Read();
            csv.ReadHeader();

            var csvHeaders = csv.HeaderRecord?.ToList() ?? new List<string>();
            var columnPropertyMap = BuildColumnPropertyMap(csvHeaders, properties);

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

            return await Task.FromResult(Result<IEnumerable<T>>.Success(data));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing from CSV: {FilePath}", filePath);
            return Result<IEnumerable<T>>.Failure($"Import failed: {ex.Message}");
        }
    }

    public async Task<Result> ExportToExcelAsync<T>(IEnumerable<T> data, string filePath) where T : class
    {
        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Data");

            var properties = typeof(T).GetProperties();
            var dataList = data.ToList();

            for (int i = 0; i < properties.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = properties[i].Name;
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

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

            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exporting to Excel: {FilePath}", filePath);
            return Result.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<Result> ExportToCsvAsync<T>(IEnumerable<T> data, string filePath) where T : class
    {
        try
        {
            await using var writer = new StreamWriter(filePath);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            var dataList = data.ToList();
            await csv.WriteRecordsAsync(dataList);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exporting to CSV: {FilePath}", filePath);
            return Result.Failure($"Export failed: {ex.Message}");
        }
    }

    private Dictionary<string, PropertyInfo> BuildColumnPropertyMap(List<string> headers, PropertyInfo[] properties)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header))
                continue;

            var prop = properties.FirstOrDefault(p =>
                p.Name.Equals(header, StringComparison.OrdinalIgnoreCase));

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

    private static string NormalizeColumnName(string name)
    {
        return name.Replace(" ", "").Replace(".", "").Replace("_", "").ToLowerInvariant();
    }

    private static object? ConvertCellValue(IXLCell cell, Type targetType)
    {
        if (cell.IsEmpty())
            return null;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(DateTime))
        {
            if (cell.Value.IsDateTime)
                return cell.Value.GetDateTime();
            if (cell.Value.IsNumber)
            {
                var excelDate = cell.Value.GetNumber();
                var epoch = new DateTime(1899, 12, 30);
                return epoch.AddDays(excelDate);
            }
        }

        var text = cell.Value.ToString();
        return ConvertValue(text, targetType);
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

    /// <summary>
    /// Validates a file path for security and correctness
    /// </summary>
    private static Result ValidateFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result.Failure("File path is required");

        if (filePath.Length > MaxPathLength)
            return Result.Failure($"File path exceeds maximum length of {MaxPathLength} characters");

        // Check for path traversal attempts
        var fullPath = Path.GetFullPath(filePath);
        if (!fullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase) &&
            (filePath.Contains("..") || filePath.Contains("~")))
        {
            // Path was normalized, check if it contains suspicious patterns
            if (filePath.Contains("..\\") || filePath.Contains("../"))
                return Result.Failure("Path traversal is not allowed");
        }

        // Check for invalid characters
        var invalidChars = Path.GetInvalidPathChars();
        if (filePath.Any(c => invalidChars.Contains(c)))
            return Result.Failure("File path contains invalid characters");

        return Result.Success();
    }

    /// <summary>
    /// Sanitizes a string for use in file names
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Limit length
        if (sanitized.Length > 200)
            sanitized = sanitized.Substring(0, 200);

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }
}
