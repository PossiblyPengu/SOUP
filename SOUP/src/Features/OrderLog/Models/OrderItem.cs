using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SOUP.Features.OrderLog.Models;

public enum NoteType
{
    Order = 0,
    StickyNote = 1
}

public partial class OrderItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private NoteType _noteType = NoteType.Order;

    [ObservableProperty]
    private string _vendorName = string.Empty;

    /// <summary>
    /// Title for sticky notes (e.g., "Quick Note", "TODO", etc.)
    /// </summary>
    [ObservableProperty]
    private string _noteTitle = string.Empty;

    [ObservableProperty]
    private string _transferNumbers = string.Empty;

    [ObservableProperty]
    private string _whsShipmentNumbers = string.Empty;

    /// <summary>
    /// Content for sticky notes
    /// </summary>
    [ObservableProperty]
    private string _noteContent = string.Empty;

    public enum OrderStatus
    {
        NotReady = 0,
        OnDeck = 1,
        InProgress = 2,
        Done = 3
    }

    [ObservableProperty]
    private OrderStatus _status = OrderStatus.NotReady;
    [ObservableProperty]
    private string _colorHex = "#B56576";
    
    [ObservableProperty]
    private bool _isArchived = false;

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
            if (Status == OrderStatus.Done && CompletedAt != null) return CompletedAt.Value - StartedAt.Value;
            if (Status == OrderStatus.InProgress) return DateTime.UtcNow - StartedAt.Value;
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

    partial void OnStatusChanged(OrderStatus value)
    {
        UpdateStatusColor(value);
        UpdateArchiveState(value);
        UpdateTimestamps(value);
        RefreshTimeInProgress();
    }

    private void UpdateStatusColor(OrderStatus status)
    {
        // Only update color for non-Done statuses (keep custom colors when marking done)
        ColorHex = status switch
        {
            OrderStatus.NotReady => Constants.OrderLogColors.StatusNotReady,
            OrderStatus.OnDeck => Constants.OrderLogColors.StatusOnDeck,
            OrderStatus.InProgress => Constants.OrderLogColors.StatusInProgress,
            OrderStatus.Done => ColorHex,         // Keep existing color
            _ => ColorHex
        };
    }

    private void UpdateArchiveState(OrderStatus status)
    {
        // Auto-archive when marked as Done (only for orders, not sticky notes)
        if (status == OrderStatus.Done && NoteType == NoteType.Order)
        {
            IsArchived = true;
        }
    }

    private void UpdateTimestamps(OrderStatus status)
    {
        switch (status)
        {
            case OrderStatus.InProgress:
                StartedAt ??= DateTime.UtcNow;
                CompletedAt = null;
                break;

            case OrderStatus.Done:
                StartedAt ??= DateTime.UtcNow;
                CompletedAt = DateTime.UtcNow;
                break;

            default: // NotReady or OnDeck
                StartedAt = null;
                CompletedAt = null;
                break;
        }
    }

    /// <summary>
    /// Helper property to check if this is a sticky note
    /// </summary>
    [JsonIgnore]
    public bool IsStickyNote => NoteType == NoteType.StickyNote;

    /// <summary>
    /// Display title - uses VendorName for orders, first line of NoteContent for sticky notes
    /// </summary>
    [JsonIgnore]
    public string DisplayTitle => NoteType == NoteType.StickyNote
        ? (string.IsNullOrWhiteSpace(NoteContent) ? "Quick Note" : NoteContent.Split('\n')[0].Trim())
        : VendorName;
}
