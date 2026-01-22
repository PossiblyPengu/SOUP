using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Core.Interfaces;
using SOUP.Core.Common;

namespace SOUP.Features.ExpireWise.Services;

public class ExpireWiseImportExportService
{
    private readonly IExpireWiseRepository _repository;
    private readonly ILogger<ExpireWiseImportExportService>? _logger;
    private readonly SOUP.Core.Interfaces.IFileImportExportService _fileService;

    public ExpireWiseImportExportService(IExpireWiseRepository repository, SOUP.Core.Interfaces.IFileImportExportService fileService, ILogger<ExpireWiseImportExportService>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<Result<int>> ImportFromCsvAsync(string filePath)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length < 2) return Result<int>.Failure("CSV file is empty or missing header");

            var items = new List<ExpirationItem>();
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 2) continue;

                if (!DateTime.TryParse(parts[1], out var expirationDate))
                {
                    _logger?.LogWarning("Skipping row {Row}: Invalid date '{Date}'", i + 1, parts[1]);
                    continue;
                }

                items.Add(new ExpirationItem
                {
                    Id = Guid.NewGuid(),
                    ItemNumber = parts[0].Trim(),
                    ExpiryDate = expirationDate,
                    Description = parts.Length > 2 ? parts[2].Trim() : string.Empty,
                    Notes = parts.Length > 3 ? parts[3].Trim() : string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            var ok = await _repository.ReplaceAllAsync(items);
            return ok ? Result<int>.Success(items.Count) : Result<int>.Failure("Import failed (transaction rolled back)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Import failed");
            return Result<int>.Failure($"Import error: {ex.Message}");
        }
    }

    public async Task<Result<int>> ImportItemsAsync(IEnumerable<ExpirationItem> items)
    {
        try
        {
            var list = items.ToList();
            var ok = await _repository.ReplaceAllAsync(list);
            return ok ? Result<int>.Success(list.Count) : Result<int>.Failure("Import failed (transaction rolled back)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Import items failed");
            return Result<int>.Failure($"Import error: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ExportToCsvAsync(string filePath, IEnumerable<ExpirationItem> items)
    {
        try
        {
            var result = await _fileService.ExportToCsvAsync(items.ToList(), filePath);
            if (result.IsSuccess) return Result<bool>.Success(true);
            return Result<bool>.Failure(result.ErrorMessage ?? "Export failed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Export failed");
            return Result<bool>.Failure($"Export error: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ExportToExcelAsync(string filePath, IEnumerable<ExpirationItem> items)
    {
        try
        {
            var result = await _fileService.ExportToExcelAsync(items.ToList(), filePath);
            if (result.IsSuccess) return Result<bool>.Success(true);
            return Result<bool>.Failure(result.ErrorMessage ?? "Export failed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Export to Excel failed");
            return Result<bool>.Failure($"Export error: {ex.Message}");
        }
    }
}
