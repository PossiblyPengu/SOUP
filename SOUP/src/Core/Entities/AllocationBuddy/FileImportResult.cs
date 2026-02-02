namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Result of a file import operation.
/// </summary>
public class FileImportResult
{
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
}
