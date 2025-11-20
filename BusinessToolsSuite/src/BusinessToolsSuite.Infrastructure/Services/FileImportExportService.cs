using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace BusinessToolsSuite.Infrastructure.Services;

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

    public async Task<Result<IReadOnlyList<T>>> ImportFromExcelAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class
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
            var properties = typeof(T).GetProperties();
            var headers = worksheet.Row(1).CellsUsed()
                .Select(c => c.Value.ToString())
                .ToList();

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                var item = Activator.CreateInstance<T>();
                for (int i = 0; i < headers.Count; i++)
                {
                    var prop = properties.FirstOrDefault(p =>
                        p.Name.Equals(headers[i], StringComparison.OrdinalIgnoreCase));

                    if (prop != null && prop.CanWrite)
                    {
                        var cellValue = row.Cell(i + 1).Value;
                        var cellText = cellValue.ToString();
                        if (!string.IsNullOrWhiteSpace(cellText))
                        {
                            try
                            {
                                var value = Convert.ChangeType(cellText, prop.PropertyType);
                                prop.SetValue(item, value);
                            }
                            catch
                            {
                                // Skip conversion errors
                            }
                        }
                    }
                }
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

    public async Task<Result<IReadOnlyList<T>>> ImportFromCsvAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            if (!File.Exists(filePath))
                return Result<IReadOnlyList<T>>.Failure($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            var records = csv.GetRecords<T>().ToList();

            _logger?.LogInformation("Successfully imported {Count} records from CSV: {FilePath}",
                records.Count, filePath);

            return await Task.FromResult(Result<IReadOnlyList<T>>.Success(records));
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
