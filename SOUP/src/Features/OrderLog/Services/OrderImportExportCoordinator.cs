using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Models;
using SOUP.Services;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for coordinating import/export operations with dialogs and user feedback.
/// Wraps OrderLogExportService with dialog management and status updates.
/// </summary>
public class OrderImportExportCoordinator
{
    private readonly DialogService _dialogService;
    private readonly OrderLogExportService _exportService;
    private readonly ILogger<OrderImportExportCoordinator>? _logger;

    public OrderImportExportCoordinator(
        DialogService dialogService,
        ILogger<OrderImportExportCoordinator>? logger = null)
    {
        _dialogService = dialogService;
        _exportService = new OrderLogExportService();
        _logger = logger;
    }

    /// <summary>
    /// Result of an import/export operation.
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; set; }
        public int ItemCount { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
        public List<OrderItem> ImportedItems { get; set; } = new();
    }

    /// <summary>
    /// Exports items to CSV with dialog coordination.
    /// </summary>
    /// <param name="items">Items to export</param>
    /// <param name="setLoading">Action to set loading state</param>
    /// <param name="setStatus">Action to set status message</param>
    /// <returns>Operation result</returns>
    public async Task<OperationResult> ExportToCsvAsync(
        IEnumerable<OrderItem> items,
        Action<bool> setLoading,
        Action<string> setStatus)
    {
        var itemsList = items.ToList();
        
        if (itemsList.Count == 0)
        {
            setStatus("No items to export");
            return new OperationResult { Success = false, ErrorMessage = "No items to export" };
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"OrderLog_Export_{timestamp}.csv";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to CSV",
                defaultFileName,
                "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                setStatus("Export cancelled");
                return new OperationResult { Success = false, ErrorMessage = "Cancelled by user" };
            }

            setLoading(true);
            setStatus("Exporting to CSV...");

            await _exportService.ExportToCsvAsync(itemsList, filePath);

            var fileName = System.IO.Path.GetFileName(filePath);
            setStatus($"Exported {itemsList.Count} item(s)");
            _dialogService.ShowExportSuccessDialog(fileName, filePath, itemsList.Count);

            return new OperationResult
            {
                Success = true,
                ItemCount = itemsList.Count,
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            var errorMsg = $"Export error: {ex.Message}";
            setStatus(errorMsg);
            _logger?.LogError(ex, "Failed to export to CSV");
            _dialogService.ShowExportErrorDialog(ex.Message);

            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            setLoading(false);
        }
    }

    /// <summary>
    /// Exports items to JSON with dialog coordination.
    /// </summary>
    public async Task<OperationResult> ExportToJsonAsync(
        IEnumerable<OrderItem> items,
        Action<bool> setLoading,
        Action<string> setStatus)
    {
        var itemsList = items.ToList();

        if (itemsList.Count == 0)
        {
            setStatus("No items to export");
            return new OperationResult { Success = false, ErrorMessage = "No items to export" };
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"OrderLog_Export_{timestamp}.json";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to JSON",
                defaultFileName,
                "JSON Files (*.json)|*.json|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                setStatus("Export cancelled");
                return new OperationResult { Success = false, ErrorMessage = "Cancelled by user" };
            }

            setLoading(true);
            setStatus("Exporting to JSON...");

            await _exportService.ExportToJsonAsync(itemsList, filePath);

            var fileName = System.IO.Path.GetFileName(filePath);
            setStatus($"Exported {itemsList.Count} item(s) to JSON");
            _dialogService.ShowExportSuccessDialog(fileName, filePath, itemsList.Count);

            return new OperationResult
            {
                Success = true,
                ItemCount = itemsList.Count,
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            var errorMsg = $"Export error: {ex.Message}";
            setStatus(errorMsg);
            _logger?.LogError(ex, "Failed to export to JSON");
            _dialogService.ShowExportErrorDialog(ex.Message);

            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            setLoading(false);
        }
    }

    /// <summary>
    /// Imports items from CSV with dialog coordination.
    /// </summary>
    public async Task<OperationResult> ImportFromCsvAsync(
        Action<bool> setLoading,
        Action<string> setStatus)
    {
        try
        {
            var filePaths = await _dialogService.ShowOpenFileDialogAsync(
                "Import from CSV",
                "CSV Files",
                "csv");

            if (filePaths == null || filePaths.Length == 0)
            {
                setStatus("Import cancelled");
                return new OperationResult { Success = false, ErrorMessage = "Cancelled by user" };
            }

            var filePath = filePaths[0];

            setLoading(true);
            setStatus("Importing from CSV...");

            var (success, items, errorMessage) = await _exportService.ImportFromCsvAsync(filePath);

            if (!success)
            {
                var errorMsg = $"Import failed: {errorMessage}";
                setStatus(errorMsg);
                _dialogService.ShowImportErrorDialog(errorMessage);

                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            var fileName = System.IO.Path.GetFileName(filePath);
            setStatus($"Imported {items.Count} item(s) from CSV");
            _dialogService.ShowImportSuccessDialog(fileName, items.Count);

            return new OperationResult
            {
                Success = true,
                ItemCount = items.Count,
                FilePath = filePath,
                ImportedItems = items
            };
        }
        catch (Exception ex)
        {
            var errorMsg = $"Import error: {ex.Message}";
            setStatus(errorMsg);
            _logger?.LogError(ex, "Failed to import from CSV");
            _dialogService.ShowImportErrorDialog(ex.Message);

            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            setLoading(false);
        }
    }
}
