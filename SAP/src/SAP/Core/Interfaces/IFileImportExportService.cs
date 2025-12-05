using SAP.Core.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

/// <summary>
/// Service interface for importing and exporting data to/from files.
/// </summary>
/// <remarks>
/// Supports CSV and Excel (XLSX) file formats for data interchange.
/// </remarks>
public interface IFileImportExportService
{
    /// <summary>
    /// Imports data from a CSV file.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <returns>A result containing the imported entities or an error.</returns>
    Task<Result<IEnumerable<T>>> ImportFromCsvAsync<T>(string filePath) where T : class, new();
    
    /// <summary>
    /// Imports data from an Excel file.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <returns>A result containing the imported entities or an error.</returns>
    Task<Result<IEnumerable<T>>> ImportFromExcelAsync<T>(string filePath) where T : class, new();
    
    /// <summary>
    /// Exports data to a CSV file.
    /// </summary>
    /// <typeparam name="T">The type of entities to export.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="filePath">Path for the output file.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ExportToCsvAsync<T>(IEnumerable<T> data, string filePath) where T : class;
    
    /// <summary>
    /// Exports data to an Excel file.
    /// </summary>
    /// <typeparam name="T">The type of entities to export.</typeparam>
    /// <param name="data">The data to export.</param>
    /// <param name="filePath">Path for the output file.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ExportToExcelAsync<T>(IEnumerable<T> data, string filePath) where T : class;
}
