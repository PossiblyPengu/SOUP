using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SAP.Features.NotesTracker.Models;
public partial class NoteItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _vendorName = string.Empty;

    [ObservableProperty]
    private string _transferNumbers = string.Empty;

    [ObservableProperty]
    private string _whsShipmentNumbers = string.Empty;

    public enum NoteStatus
    {
        NotReady = 0,
        OnDeck = 1,
        InProgress = 2,
        Done = 3
    }

    [ObservableProperty]
    private NoteStatus _status = NoteStatus.NotReady;
    [ObservableProperty]
    private string _colorHex = "#B56576";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Order for manual sorting/reordering (lower = earlier in list)
    /// </summary>
    public int Order { get; set; }

    [JsonIgnore]
    public TimeSpan TimeInProgress
    {
        get
        {
            if (StartedAt == null) return TimeSpan.Zero;
            if (Status == NoteStatus.Done && CompletedAt != null) return CompletedAt.Value - StartedAt.Value;
            if (Status == NoteStatus.InProgress) return DateTime.UtcNow - StartedAt.Value;
            return TimeSpan.Zero;
        }
    }

    [JsonIgnore]
    public string TimeInProgressDisplay
    {
        get
        {
            var ts = TimeInProgress;
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }

    /// <summary>
    /// Called by a UI timer to raise PropertyChanged for computed properties
    /// </summary>
    public void RefreshTimeInProgress()
    {
        OnPropertyChanged(nameof(TimeInProgress));
        OnPropertyChanged(nameof(TimeInProgressDisplay));
    }

    partial void OnStatusChanged(NoteStatus value)
    {
        // Set color based on status
        ColorHex = value switch
        {
            NoteStatus.NotReady => "#FF4444",    // Red
            NoteStatus.OnDeck => "#FFD700",      // Yellow/Gold
            NoteStatus.InProgress => "#4CAF50",  // Green
            NoteStatus.Done => ColorHex,          // Keep existing color
            _ => ColorHex
        };

        // Manage StartedAt/CompletedAt based on status transitions
        if (value == NoteStatus.InProgress)
        {
            if (StartedAt == null) StartedAt = DateTime.UtcNow;
            CompletedAt = null;
        }
        else if (value == NoteStatus.Done)
        {
            if (StartedAt == null) StartedAt = DateTime.UtcNow;
            CompletedAt = DateTime.UtcNow;
        }
        else // NotReady or OnDeck
        {
            StartedAt = null;
            CompletedAt = null;
        }

        RefreshTimeInProgress();
    }
}
