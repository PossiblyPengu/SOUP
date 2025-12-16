using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SOUP.Features.OrderLog.Models;
public partial class OrderItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _vendorName = string.Empty;

    [ObservableProperty]
    private string _transferNumbers = string.Empty;

    [ObservableProperty]
    private string _whsShipmentNumbers = string.Empty;

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
        // Set color based on status
        ColorHex = value switch
        {
            OrderStatus.NotReady => "#FF4444",    // Red
            OrderStatus.OnDeck => "#FFD700",      // Yellow/Gold
            OrderStatus.InProgress => "#4CAF50",  // Green
            OrderStatus.Done => ColorHex,          // Keep existing color
            _ => ColorHex
        };

        // Auto-archive when marked as Done
        if (value == OrderStatus.Done)
        {
            IsArchived = true;
        }

        // Manage StartedAt/CompletedAt based on status transitions
        if (value == OrderStatus.InProgress)
        {
            if (StartedAt == null) StartedAt = DateTime.UtcNow;
            CompletedAt = null;
        }
        else if (value == OrderStatus.Done)
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
