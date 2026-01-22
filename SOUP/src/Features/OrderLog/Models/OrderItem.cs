using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SOUP.Features.OrderLog.Models;

public enum NoteType
{
    Order = 0,
    StickyNote = 1
}

public enum NoteCategory
{
    General = 0,
    Todo = 1,
    Reminder = 2,
    Log = 3,
    Idea = 4
}

public partial class OrderItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private NoteType _noteType = NoteType.Order;

    /// <summary>
    /// Category for sticky notes (General, Todo, Reminder, Log, Idea).
    /// Only applies when NoteType is StickyNote.
    /// </summary>
    [ObservableProperty]
    private NoteCategory _noteCategory = NoteCategory.General;

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

    /// <summary>
    /// Number of other items linked to this item (excluding itself).
    /// Updated by the ViewModel when linked groups change.
    /// </summary>
    [ObservableProperty]
    private int _linkedItemCount = 0;

    [ObservableProperty]
    private bool _isArchived = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Formatted display of the creation timestamp.
    /// Shows time only if created today, otherwise shows date and time.
    /// </summary>
    [JsonIgnore]
    public string CreatedAtDisplay
    {
        get
        {
            if (CreatedAt.Date == DateTime.Today)
                return CreatedAt.ToString("h:mm tt").ToLower();
            else if (CreatedAt.Year == DateTime.Now.Year)
                return CreatedAt.ToString("MMM d, h:mm tt").ToLower();
            else
                return CreatedAt.ToString("MMM d yyyy, h:mm tt").ToLower();
        }
    }

    /// <summary>
    /// Accumulated time from previous "In Progress" sessions (laps).
    /// Stored as ticks for JSON serialization.
    /// </summary>
    public long AccumulatedTimeTicks { get; set; } = 0;

    [JsonIgnore]
    public TimeSpan AccumulatedTime
    {
        get => TimeSpan.FromTicks(AccumulatedTimeTicks);
        set => AccumulatedTimeTicks = value.Ticks;
    }

    /// <summary>
    /// Order for manual sorting/reordering (lower = earlier in list)
    /// </summary>
    public int Order { get; set; }

    [JsonIgnore]
    public TimeSpan TimeInProgress
    {
        get
        {
            // Current lap time only
            if (StartedAt == null) return TimeSpan.Zero;
            if (Status == OrderStatus.InProgress) return DateTime.Now - StartedAt.Value;
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Total time including all previous laps plus current session.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TotalTimeInProgress => AccumulatedTime + TimeInProgress;

    /// <summary>
    /// Whether there are previous laps to show.
    /// </summary>
    [JsonIgnore]
    public bool HasPreviousLaps => AccumulatedTimeTicks > 0;

    [JsonIgnore]
    public string TimeInProgressDisplay
    {
        get
        {
            var current = TimeInProgress;
            var total = TotalTimeInProgress;

            // If there are previous laps, show "current (total)"
            if (HasPreviousLaps && Status == OrderStatus.InProgress)
            {
                return $"{FormatTimeSpan(current)} ({FormatTimeSpan(total)})";
            }

            // For Done status with laps, just show total
            if (HasPreviousLaps && Status == OrderStatus.Done)
            {
                return FormatTimeSpan(total);
            }

            // No laps, just show current/total
            return FormatTimeSpan(total);
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
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

    private void UpdateTimestamps(OrderStatus status)
    {
        // Initialize _previousStatusForLap if this is the first status change after loading
        // Use StartedAt presence to detect if we were InProgress (StartedAt is only set when InProgress)
        _previousStatusForLap ??= StartedAt != null ? OrderStatus.InProgress : Status;

        // If leaving InProgress, accumulate the current session time
        if (_previousStatusForLap == OrderStatus.InProgress && status != OrderStatus.InProgress && StartedAt != null)
        {
            AccumulatedTime += DateTime.Now - StartedAt.Value;
        }

        switch (status)
        {
            case OrderStatus.InProgress:
                // Only set StartedAt if not already set (preserve on deserialization/reload)
                if (StartedAt == null)
                {
                    StartedAt = DateTime.Now;
                }
                CompletedAt = null;
                break;

            case OrderStatus.Done:
                // Time was already accumulated above when leaving InProgress
                StartedAt = null; // Clear so TimeInProgress returns Zero
                CompletedAt ??= DateTime.Now; // Only set if not already set
                break;

            default: // NotReady or OnDeck
                StartedAt = null;
                CompletedAt = null;
                // Keep AccumulatedTime so we can resume later
                break;
        }

        _previousStatusForLap = status;
    }

    // Track previous status for lap accumulation (initialized to current status)
    [JsonIgnore]
    private OrderStatus? _previousStatusForLap;

    /// <summary>
    /// Resets the accumulated lap time to zero.
    /// </summary>
    public void ResetAccumulatedTime()
    {
        AccumulatedTimeTicks = 0;
        OnPropertyChanged(nameof(AccumulatedTime));
        OnPropertyChanged(nameof(TotalTimeInProgress));
        OnPropertyChanged(nameof(HasPreviousLaps));
        RefreshTimeInProgress();
    }

    /// <summary>
    /// Helper property to check if this is a sticky note
    /// </summary>
    [JsonIgnore]
    public bool IsStickyNote => NoteType == NoteType.StickyNote;

    /// <summary>
    /// Whether this item should be rendered as an order card in the UI.
    /// Items with any content are renderable. Only truly empty items are hidden.
    /// </summary>
    [JsonIgnore]
    public bool IsRenderable => !IsPracticallyEmpty || IsPlaceholder;

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
