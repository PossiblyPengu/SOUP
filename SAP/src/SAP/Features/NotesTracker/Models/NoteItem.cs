using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace SAP.Features.NotesTracker.Models
{
    public class NoteItem : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string? _vendorName;
        public string? VendorName { get => _vendorName; set => SetProperty(ref _vendorName, value); }

        private string? _transferNumbers;
        public string? TransferNumbers { get => _transferNumbers; set => SetProperty(ref _transferNumbers, value); }

        private string? _whsShipmentNumbers;
        public string? WhsShipmentNumbers { get => _whsShipmentNumbers; set => SetProperty(ref _whsShipmentNumbers, value); }

        private bool _isComplete;
        public bool IsComplete { get => _isComplete; set => SetProperty(ref _isComplete, value); }

        private string? _colorHex;
        public string? ColorHex { get => _colorHex; set => SetProperty(ref _colorHex, value); }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Order for manual sorting/reordering (lower = earlier in list)
        public int Order { get; set; }

        [JsonIgnore]
        public TimeSpan TimeInProgress
        {
            get
            {
                if (StartedAt == null) return TimeSpan.Zero;
                if (IsComplete && CompletedAt != null) return CompletedAt.Value - StartedAt.Value;
                return DateTime.UtcNow - StartedAt.Value;
            }
        }

        // Called by a UI timer to raise PropertyChanged for the computed TimeInProgress property
        public void RefreshTimeInProgress()
        {
            OnPropertyChanged(nameof(TimeInProgress));
            OnPropertyChanged(nameof(TimeInProgressDisplay));
        }

        [JsonIgnore]
        public string TimeInProgressDisplay
        {
            get
            {
                var ts = TimeInProgress;
                if (ts.TotalHours >= 1) return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
                return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
            }
        }
    }
}
