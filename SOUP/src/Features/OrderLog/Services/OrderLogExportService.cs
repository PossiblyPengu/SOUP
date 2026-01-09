using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service for exporting OrderLog data to various formats.
/// </summary>
public interface IOrderLogExportService
{
    Task ExportToCsvAsync(IEnumerable<OrderItem> items, string filePath);
}

public class OrderLogExportService : IOrderLogExportService
{
    private readonly ILogger<OrderLogExportService>? _logger;

    public OrderLogExportService(ILogger<OrderLogExportService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Exports order items to CSV format.
    /// </summary>
    /// <param name="items">Items to export</param>
    /// <param name="filePath">Full path to save the file</param>
    public async Task ExportToCsvAsync(IEnumerable<OrderItem> items, string filePath)
    {
        try
        {
            var itemsList = items.ToList();
            var csvContent = BuildCsvContent(itemsList);

            await File.WriteAllTextAsync(filePath, csvContent);

            _logger?.LogInformation("Exported {Count} orders to CSV: {FilePath}", itemsList.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export to CSV");
            throw;
        }
    }

    private string BuildCsvContent(List<OrderItem> items)
    {
        var csv = new StringBuilder();

        // Header
        csv.AppendLine("Type,Vendor/Title,Status,Created,Completed,Transfer Numbers,WHS Shipment Numbers,Note Content");

        // Rows
        foreach (var item in items)
        {
            var type = item.NoteType == NoteType.StickyNote ? "Note" : "Order";
            var vendorOrTitle = item.NoteType == NoteType.StickyNote
                ? EscapeCsvField(item.NoteTitle ?? "")
                : EscapeCsvField(item.VendorName ?? "");
            var status = item.Status.ToString();
            var created = item.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            var completed = item.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            var transfers = EscapeCsvField(item.TransferNumbers ?? "");
            var shipments = EscapeCsvField(item.WhsShipmentNumbers ?? "");
            var noteContent = item.NoteType == NoteType.StickyNote
                ? EscapeCsvField(item.NoteContent ?? "")
                : "";

            csv.AppendLine($"{type},{vendorOrTitle},{status},{created},{completed},{transfers},{shipments},{noteContent}");
        }

        return csv.ToString();
    }

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
