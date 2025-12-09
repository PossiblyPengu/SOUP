using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAP.Features.NotesTracker.Models;
using SAP.Features.NotesTracker.Services;
using Microsoft.Extensions.Logging;
using System.Windows.Threading;

namespace SAP.Features.NotesTracker.ViewModels
{
    public partial class NotesTrackerViewModel : ObservableObject
    {
        private readonly INotesService _notesService;
        private readonly ILogger<NotesTrackerViewModel>? _logger;
        private readonly SAP.Features.NotesTracker.Services.GroupStateStore _groupStateStore;

        public ObservableCollection<NoteItem> Items { get; } = new ObservableCollection<NoteItem>();

        public ObservableCollection<NoteItem> SelectedItems { get; } = new ObservableCollection<NoteItem>();

        [ObservableProperty]
        private NoteItem? _selectedItem;

        public NotesTrackerViewModel(INotesService notesService, SAP.Features.NotesTracker.Services.GroupStateStore groupStateStore, ILogger<NotesTrackerViewModel>? logger = null)
        {
            _notesService = notesService;
            _logger = logger;
            _groupStateStore = groupStateStore;

            // Start a dispatcher timer to refresh the TimeInProgress display every second
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                foreach (var it in Items)
                {
                    it.RefreshTimeInProgress();
                }
            };
            _timer.Start();
        }

        [RelayCommand]
        private void ResetGroups()
        {
            _groupStateStore.ResetAll();
            // no immediate UI change here; view expanders will load state next time or user can refresh
            GroupStatesReset?.Invoke();
        }

        /// <summary>
        /// Raised when group states are reset so views can refresh expanders immediately.
        /// </summary>
        public event Action? GroupStatesReset;

        private readonly DispatcherTimer _timer;

        public bool GetGroupState(string name, bool defaultValue = true) => _groupStateStore.Get(name, defaultValue);

        public void SetGroupState(string name, bool value) => _groupStateStore.Set(name, value);

        public async Task LoadAsync()
        {
            try
            {
                var items = await _notesService.LoadAsync();
                Items.Clear();
                // Items returned already ordered by Order; still group by VendorName in view
                foreach (var it in items) Items.Add(it);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load notes");
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                // Ensure Order is set based on current list order before saving
                for (int i = 0; i < Items.Count; i++) Items[i].Order = i;
                await _notesService.SaveAsync(Items.ToList());
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to save notes");
            }
        }

        [RelayCommand]
        private void AddSampleNote()
        {
            var note = new NoteItem
            {
                VendorName = "VENDOR",
                TransferNumbers = "T12345..T12350",
                WhsShipmentNumbers = "WSH123..WSH130",
                ColorHex = "#B56576",
                StartedAt = DateTime.UtcNow
            };
            Items.Insert(0, note);
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (SelectedItems == null || SelectedItems.Count == 0) return;
            var toRemove = SelectedItems.ToList();
            foreach (var it in toRemove) Items.Remove(it);
            SelectedItems.Clear();
            await SaveAsync();
        }

        [RelayCommand]
        private async Task ToggleCompleteAsync(NoteItem item)
        {
            if (item == null) return;
            item.IsComplete = !item.IsComplete;
            if (item.IsComplete)
            {
                item.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                item.CompletedAt = null;
            }
            await SaveAsync();
        }

        [RelayCommand]
        private async Task BulkToggleCompleteAsync()
        {
            if (SelectedItems == null || SelectedItems.Count == 0) return;
            foreach (var it in SelectedItems)
            {
                it.IsComplete = !it.IsComplete;
                it.CompletedAt = it.IsComplete ? DateTime.UtcNow : null;
            }
            await SaveAsync();
        }

        [RelayCommand]
        private async Task BulkSetColorAsync(string colorHex)
        {
            if (SelectedItems == null || SelectedItems.Count == 0) return;
            foreach (var it in SelectedItems)
            {
                it.ColorHex = colorHex;
            }
            await SaveAsync();
        }

        [RelayCommand]
        private async Task BulkStartAsync()
        {
            if (SelectedItems == null || SelectedItems.Count == 0) return;
            foreach (var it in SelectedItems)
            {
                if (it.StartedAt == null) it.StartedAt = DateTime.UtcNow;
                // clear completed if restarting
                it.CompletedAt = null;
                it.IsComplete = false;
            }
            await SaveAsync();
        }

        [RelayCommand]
        private async Task BulkStopAsync()
        {
            if (SelectedItems == null || SelectedItems.Count == 0) return;
            foreach (var it in SelectedItems)
            {
                if (it.StartedAt != null && it.CompletedAt == null)
                {
                    it.CompletedAt = DateTime.UtcNow;
                }
            }
            await SaveAsync();
        }

        [RelayCommand]
        private async Task StartAsync(NoteItem item)
        {
            if (item == null) return;
            if (item.StartedAt == null) item.StartedAt = DateTime.UtcNow;
            item.CompletedAt = null;
            item.IsComplete = false;
            await SaveAsync();
        }

        [RelayCommand]
        private async Task StopAsync(NoteItem item)
        {
            if (item == null) return;
            if (item.StartedAt != null && item.CompletedAt == null)
            {
                item.CompletedAt = DateTime.UtcNow;
            }
            await SaveAsync();
        }

        public async Task SetColorAsync(NoteItem item, string colorHex)
        {
            if (item == null) return;
            item.ColorHex = colorHex;
            await SaveAsync();
        }
    }
}
