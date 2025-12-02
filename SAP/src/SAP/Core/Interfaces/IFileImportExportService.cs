using SAP.Core.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

public interface IFileImportExportService
{
    Task<Result<IEnumerable<T>>> ImportFromCsvAsync<T>(string filePath) where T : class, new();
    Task<Result<IEnumerable<T>>> ImportFromExcelAsync<T>(string filePath) where T : class, new();
    Task<Result> ExportToCsvAsync<T>(IEnumerable<T> data, string filePath) where T : class;
    Task<Result> ExportToExcelAsync<T>(IEnumerable<T> data, string filePath) where T : class;
}
