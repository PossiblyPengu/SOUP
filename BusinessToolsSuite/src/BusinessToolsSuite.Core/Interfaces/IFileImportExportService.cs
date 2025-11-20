using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Interfaces;

/// <summary>
/// Service interface for importing and exporting files
/// </summary>
public interface IFileImportExportService
{
    Task<Result<IReadOnlyList<T>>> ImportFromExcelAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class;

    Task<Result<IReadOnlyList<T>>> ImportFromCsvAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class;

    Task<Result> ExportToExcelAsync<T>(
        IEnumerable<T> data,
        string filePath,
        string? sheetName = null,
        CancellationToken cancellationToken = default) where T : class;

    Task<Result> ExportToCsvAsync<T>(
        IEnumerable<T> data,
        string filePath,
        CancellationToken cancellationToken = default) where T : class;
}
