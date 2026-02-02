using System.Collections.ObjectModel;
using System.IO;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.AllocationBuddy;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Service responsible for exporting allocation data to various formats.
/// Handles Excel and CSV export functionality.
/// </summary>
public class AllocationExportService
{
    private readonly ILogger<AllocationExportService>? _logger;

    public AllocationExportService(ILogger<AllocationExportService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Exports allocation data to an Excel file.
    /// </summary>
    /// <param name="locations">The collection of location allocations to export.</param>
    /// <param name="filePath">The file path to save the Excel file.</param>
    /// <returns>The number of items exported.</returns>
    /// <exception cref="ArgumentException">Thrown when locations is empty.</exception>
    public int ExportToExcel(ObservableCollection<LocationAllocation> locations, string filePath)
    {
        if (locations.Count == 0)
        {
            throw new ArgumentException("No data to export", nameof(locations));
        }

        // Create Excel file using ClosedXML
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Allocations");

        // Headers
        worksheet.Cell(1, 1).Value = "Location";
        worksheet.Cell(1, 2).Value = "Location Name";
        worksheet.Cell(1, 3).Value = "Item Number";
        worksheet.Cell(1, 4).Value = "Description";
        worksheet.Cell(1, 5).Value = "Quantity";
        worksheet.Cell(1, 6).Value = "SKU";

        // Style headers
        var headerRange = worksheet.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data
        int row = 2;
        int itemCount = 0;
        foreach (var location in locations)
        {
            foreach (var item in location.Items)
            {
                worksheet.Cell(row, 1).Value = location.Location;
                worksheet.Cell(row, 2).Value = location.LocationName ?? "";
                worksheet.Cell(row, 3).Value = item.ItemNumber;
                worksheet.Cell(row, 4).Value = item.Description;
                worksheet.Cell(row, 5).Value = item.Quantity;
                worksheet.Cell(row, 6).Value = item.SKU ?? "";
                row++;
                itemCount++;
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(filePath);

        _logger?.LogInformation("Exported {Count} items to Excel: {FilePath}", itemCount, filePath);
        return itemCount;
    }

    /// <summary>
    /// Exports allocation data to a CSV file.
    /// </summary>
    /// <param name="locations">The collection of location allocations to export.</param>
    /// <param name="filePath">The file path to save the CSV file.</param>
    /// <returns>The number of items exported.</returns>
    /// <exception cref="ArgumentException">Thrown when locations is empty.</exception>
    public async Task<int> ExportToCsvAsync(ObservableCollection<LocationAllocation> locations, string filePath)
    {
        if (locations.Count == 0)
        {
            throw new ArgumentException("No data to export", nameof(locations));
        }

        using var writer = new StreamWriter(filePath);

        // Headers
        await writer.WriteLineAsync("Location,Location Name,Item Number,Description,Quantity,SKU");

        // Data
        int itemCount = 0;
        foreach (var location in locations)
        {
            foreach (var item in location.Items)
            {
                var locationName = EscapeCsvField(location.LocationName ?? "");
                var description = EscapeCsvField(item.Description);
                var sku = EscapeCsvField(item.SKU ?? "");
                await writer.WriteLineAsync($"{location.Location},{locationName},{item.ItemNumber},{description},{item.Quantity},{sku}");
                itemCount++;
            }
        }

        _logger?.LogInformation("Exported {Count} items to CSV: {FilePath}", itemCount, filePath);
        return itemCount;
    }

    /// <summary>
    /// Escapes a CSV field by wrapping it in quotes if it contains special characters.
    /// </summary>
    /// <param name="field">The field to escape.</param>
    /// <returns>The escaped field.</returns>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
