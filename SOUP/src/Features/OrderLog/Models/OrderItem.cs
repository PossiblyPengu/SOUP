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
    
    /// <summary>
    /// Stores the status before archiving, so it can be restored when unarchiving.
    /// </summary>
    [ObservableProperty]
    private OrderStatus? _previousStatus;
    
    [ObservableProperty]
    private string _colorHex = Constants.OrderLogColors.DefaultOrder;
    [ObservableProperty]
    private Guid? _linkedGroupId;

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
            
            // Format based on duration
            if (ts.TotalDays >= 7)
            {
                int weeks = (int)(ts.TotalDays / 7);
                int days = (int)(ts.TotalDays % 7);
                return days > 0 ? $"{weeks}w {days}d" : $"{weeks}w";
            }
            else if (ts.TotalDays >= 1)
            {
                int days = (int)ts.TotalDays;
                int hours = ts.Hours;
                return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
            }
            else if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            }
            else
            {
                return $"{ts.Minutes}m {ts.Seconds:D2}s";
            }
        }
    }

    // Cache for optimized refresh - only notify when display actually changes
    private string _lastTimeDisplay = string.Empty;

    /// <summary>
    /// Called by a UI timer to raise PropertyChanged for computed properties.
    /// Only raises notifications if the displayed value actually changed.
    /// </summary>
    public void RefreshTimeInProgress()
    {
        var currentDisplay = TimeInProgressDisplay;
        if (currentDisplay != _lastTimeDisplay)
        {
            _lastTimeDisplay = currentDisplay;
            OnPropertyChanged(nameof(TimeInProgress));
            OnPropertyChanged(nameof(TimeInProgressDisplay));
        }
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
        // Note: Archiving is now handled by the ViewModel (SetStatusAsync) to ensure proper
        // collection management and undo support. This method is kept for potential future use.
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
    /// Whether this item should be rendered as an order card in the UI.
    /// Sticky notes are always renderable; orders with empty vendor name are considered invalid.
    /// </summary>
    [JsonIgnore]
    public bool IsRenderable => NoteType == NoteType.StickyNote || !string.IsNullOrWhiteSpace(VendorName) || IsPlaceholder;

    /// <summary>
    /// Transient flag used to indicate this order is a UI placeholder (should render, but not be treated as a filled order).
    /// This is not persisted.
    /// </summary>
    [JsonIgnore]
    public bool IsPlaceholder { get; set; } = false;

    /// <summary>
    /// Display title - uses VendorName for orders, first line of NoteContent for sticky notes
    /// </summary>
    [JsonIgnore]
    public string DisplayTitle => NoteType == NoteType.StickyNote
        ? (string.IsNullOrWhiteSpace(NoteContent) ? "Quick Note" : NoteContent.Split('\n')[0].Trim())
        : VendorName;

    /// <summary>
    /// Checks if this item is practically empty (no meaningful content).
    /// Used to filter out blank placeholders during persistence and linking operations.
    /// </summary>
    [JsonIgnore]
    public bool IsPracticallyEmpty =>
        string.IsNullOrWhiteSpace(VendorName)
        && string.IsNullOrWhiteSpace(TransferNumbers)
        && string.IsNullOrWhiteSpace(WhsShipmentNumbers)
        && string.IsNullOrWhiteSpace(NoteContent);

    /// <summary>
    /// Factory helper to create a new blank Order (non-sticky note) with sensible defaults.
    /// </summary>
    public static OrderItem CreateBlankOrder(string vendorName = "",
                                            string transferNumbers = "",
                                            string whsShipmentNumbers = "",
                                            string? colorHex = null,
                                            bool isPlaceholder = false)
    {
        return new OrderItem
        {
            NoteType = NoteType.Order,
            VendorName = vendorName ?? string.Empty,
            TransferNumbers = transferNumbers ?? string.Empty,
            WhsShipmentNumbers = whsShipmentNumbers ?? string.Empty,
            ColorHex = colorHex ?? Constants.OrderLogColors.DefaultOrder,
            Status = OrderStatus.NotReady,
            IsPlaceholder = isPlaceholder,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory helper to create a new blank Sticky Note with sensible defaults.
    /// </summary>
    public static OrderItem CreateBlankNote(string content = "", string? colorHex = null)
    {
        return new OrderItem
        {
            NoteType = NoteType.StickyNote,
            NoteTitle = string.Empty,
            NoteContent = content ?? string.Empty,
            ColorHex = colorHex ?? Constants.OrderLogColors.DefaultNote,
            Status = OrderStatus.OnDeck,
            CreatedAt = DateTime.UtcNow
        };
    }
}
