namespace SOUP.Features.OrderLog.Models;

public class OrderLogWidgetSettings
{
    public double CardFontSize { get; set; } = 13.0;
    public bool ShowNowPlaying { get; set; } = true;
    public bool ShowArchived { get; set; } = true;
    public int UndoTimeoutSeconds { get; set; } = 5;
    public string DefaultOrderColor { get; set; } = "#B56576";
    public string DefaultNoteColor { get; set; } = "#FFD700";
    public bool NotesOnlyMode { get; set; } = false;
    public bool SortByStatus { get; set; } = false;
    public bool SortStatusDescending { get; set; } = false;
    // Archived view preferences
    public string ArchivedSearchQuery { get; set; } = string.Empty;
    public bool ArchivedSortByDate { get; set; } = true;
    public bool ArchivedSortDescending { get; set; } = true;
}
