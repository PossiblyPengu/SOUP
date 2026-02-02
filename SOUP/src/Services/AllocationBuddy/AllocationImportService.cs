using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.Logging;
using SOUP.Core.Common;
using SOUP.Core.Entities.AllocationBuddy;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Service responsible for importing allocation data from various sources.
/// Handles importing from Excel, CSV, and clipboard text.
/// </summary>
public class AllocationImportService
{
    private readonly AllocationBuddyParser _parser;
    private readonly AllocationCalculationService _calculationService;
    private readonly AllocationBuddyConfiguration _configuration;
    private readonly ILogger<AllocationImportService>? _logger;

    public AllocationImportService(
        AllocationBuddyParser parser,
        AllocationCalculationService calculationService,
        AllocationBuddyConfiguration configuration,
        ILogger<AllocationImportService>? logger = null)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <summary>
    /// Validates clipboard text length against configured maximum.
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>True if valid, false if exceeds maximum length.</returns>
    public bool ValidateClipboardTextLength(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        var maxLength = _configuration.MaxClipboardTextLengthBytes;
        if (text.Length > maxLength)
        {
            _logger?.LogWarning("Clipboard text rejected: {Length} bytes exceeds maximum of {Max}",
                text.Length, maxLength);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Imports allocation data from an Excel file.
    /// </summary>
    /// <param name="filePath">The path to the Excel file.</param>
    /// <param name="locations">The collection to populate with locations.</param>
    /// <param name="itemPool">The collection to populate with pool items.</param>
    /// <returns>Result containing the number of entries imported or error message.</returns>
    public async Task<Result<int>> ImportFromExcelAsync(
        string filePath,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        try
        {
            _logger?.LogInformation("Importing from Excel: {FilePath}", filePath);

            var parseResult = await _parser.ParseExcelAsync(filePath);

            if (!parseResult.IsSuccess || parseResult.Value == null)
            {
                _logger?.LogError("Excel import failed: {Error}", parseResult.ErrorMessage);
                return Result<int>.Failure(parseResult.ErrorMessage ?? "Parse failed");
            }

            if (parseResult.Value.Count == 0)
            {
                _logger?.LogWarning("No entries found in Excel file: {FilePath}", filePath);
                return Result<int>.Failure("No valid entries found. Check file has Store, Item, and Quantity columns.");
            }

            _calculationService.PopulateFromEntries(parseResult.Value, locations, itemPool);

            _logger?.LogInformation("Successfully imported {Count} entries from Excel", parseResult.Value.Count);
            return Result<int>.Success(parseResult.Value.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception importing from Excel: {FilePath}", filePath);
            return Result<int>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Imports allocation data from a CSV file.
    /// </summary>
    /// <param name="filePath">The path to the CSV file.</param>
    /// <param name="locations">The collection to populate with locations.</param>
    /// <param name="itemPool">The collection to populate with pool items.</param>
    /// <returns>Result containing the number of entries imported or error message.</returns>
    public async Task<Result<int>> ImportFromCsvAsync(
        string filePath,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        try
        {
            _logger?.LogInformation("Importing from CSV: {FilePath}", filePath);

            var parseResult = await _parser.ParseCsvAsync(filePath);

            if (!parseResult.IsSuccess || parseResult.Value == null)
            {
                _logger?.LogError("CSV import failed: {Error}", parseResult.ErrorMessage);
                return Result<int>.Failure(parseResult.ErrorMessage ?? "Parse failed");
            }

            if (parseResult.Value.Count == 0)
            {
                _logger?.LogWarning("No entries found in CSV file: {FilePath}", filePath);
                return Result<int>.Failure("No valid entries found. Check file has Store, Item, and Quantity columns.");
            }

            _calculationService.PopulateFromEntries(parseResult.Value, locations, itemPool);

            _logger?.LogInformation("Successfully imported {Count} entries from CSV", parseResult.Value.Count);
            return Result<int>.Success(parseResult.Value.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception importing from CSV: {FilePath}", filePath);
            return Result<int>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Imports allocation data from a file (Excel or CSV based on extension).
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="locations">The collection to populate with locations.</param>
    /// <param name="itemPool">The collection to populate with pool items.</param>
    /// <returns>Result containing the number of entries imported or error message.</returns>
    public async Task<Result<int>> ImportFromFileAsync(
        string filePath,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return await ImportFromCsvAsync(filePath, locations, itemPool);
        }
        else if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                 filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
        {
            return await ImportFromExcelAsync(filePath, locations, itemPool);
        }
        else
        {
            return Result<int>.Failure("Unsupported file format. Only .csv, .xlsx, and .xls files are supported.");
        }
    }

    /// <summary>
    /// Imports allocation data from clipboard text.
    /// </summary>
    /// <param name="clipboardText">The clipboard text to parse.</param>
    /// <param name="locations">The collection to populate with locations.</param>
    /// <param name="itemPool">The collection to populate with pool items.</param>
    /// <returns>Result containing the number of entries imported or error message.</returns>
    public Result<int> ImportFromClipboardText(
        string clipboardText,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                return Result<int>.Failure("No text provided");
            }

            // Validate length
            if (!ValidateClipboardTextLength(clipboardText))
            {
                var maxMB = _configuration.MaxClipboardTextLengthBytes / 1_000_000;
                return Result<int>.Failure($"Text too large (max {maxMB}MB)");
            }

            _logger?.LogInformation("Importing from clipboard text ({Length} characters)", clipboardText.Length);

            var parseResult = _parser.ParseFromClipboardText(clipboardText);

            if (!parseResult.IsSuccess || parseResult.Value == null)
            {
                _logger?.LogError("Clipboard import failed: {Error}", parseResult.ErrorMessage);
                return Result<int>.Failure(parseResult.ErrorMessage ?? "Parse failed");
            }

            if (parseResult.Value.Count == 0)
            {
                _logger?.LogWarning("No entries found in clipboard text");
                return Result<int>.Failure("No valid entries found in pasted text.");
            }

            _calculationService.PopulateFromEntries(parseResult.Value, locations, itemPool);

            _logger?.LogInformation("Successfully imported {Count} entries from clipboard", parseResult.Value.Count);
            return Result<int>.Success(parseResult.Value.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception importing from clipboard text");
            return Result<int>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Imports multiple files in batch.
    /// </summary>
    /// <param name="filePaths">The file paths to import.</param>
    /// <returns>List of import results for each file.</returns>
    public async Task<List<FileImportResult>> ImportMultipleFilesAsync(string[] filePaths)
    {
        var results = new List<FileImportResult>();

        foreach (var filePath in filePaths)
        {
            try
            {
                Result<IReadOnlyList<AllocationEntry>> parseResult;

                if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    parseResult = await _parser.ParseCsvAsync(filePath);
                }
                else
                {
                    parseResult = await _parser.ParseExcelAsync(filePath);
                }

                if (!parseResult.IsSuccess)
                {
                    results.Add(new FileImportResult
                    {
                        FileName = Path.GetFileName(filePath),
                        Success = false,
                        Message = parseResult.ErrorMessage ?? "Parse failed",
                        Count = 0
                    });
                    continue;
                }

                if (parseResult.Value != null)
                {
                    results.Add(new FileImportResult
                    {
                        FileName = Path.GetFileName(filePath),
                        Success = true,
                        Message = "Imported",
                        Count = parseResult.Value.Count
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing file: {FilePath}", filePath);
                results.Add(new FileImportResult
                {
                    FileName = Path.GetFileName(filePath),
                    Success = false,
                    Message = ex.Message,
                    Count = 0
                });
            }
        }

        return results;
    }
}
