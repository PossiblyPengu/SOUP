using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SAP.Features.NotesTracker.Models;
using SAP.Features.NotesTracker.Services;

namespace SAP.Features.NotesTracker.ViewModels;

public partial class NotesTrackerViewModel : ObservableObject, IDisposable
{
    private readonly INotesService _notesService;
    private readonly GroupStateStore _groupStateStore;
    private readonly ILogger<NotesTrackerViewModel>? _logger;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public ObservableCollection<NoteItem> Items { get; } = new();
    public ObservableCollection<NoteItem> SelectedItems { get; } = new();

    [ObservableProperty]
    private NoteItem? _selectedItem;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _newNoteVendorName = string.Empty;

    [ObservableProperty]
    private string _newNoteTransferNumbers = string.Empty;

    [ObservableProperty]
    private string _newNoteWhsShipmentNumbers = string.Empty;

    private string _newNoteColorHex = "#B56576";

    public event Action? GroupStatesReset;

    public NotesTrackerViewModel(
        INotesService notesService,
        GroupStateStore groupStateStore,
        ILogger<NotesTrackerViewModel>? logger = null)
    {
        _notesService = notesService;
        _groupStateStore = groupStateStore;
        _logger = logger;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _logger?.LogInformation("NotesTrackerViewModel initialized");
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshTimeInProgress();
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading notes...";

        try
        {
            var items = await _notesService.LoadAsync();
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
            StatusMessage = $"Loaded {items.Count} notes";
            _logger?.LogInformation("Loaded {Count} notes into view", items.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load notes: {ex.Message}";
            _logger?.LogError(ex, "Failed to load notes");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Order = i;
            }
            await _notesService.SaveAsync(Items.ToList());
            StatusMessage = $"Saved {Items.Count} notes";
            _logger?.LogInformation("Saved {Count} notes", Items.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            _logger?.LogError(ex, "Failed to save notes");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        var count = SelectedItems.Count;
        var toRemove = SelectedItems.ToList();
        foreach (var item in toRemove)
        {
            Items.Remove(item);
        }
        SelectedItems.Clear();

        await SaveAsync();
        StatusMessage = $"Deleted {count} note(s)";
    }

    [RelayCommand]
    private async Task ToggleCompleteAsync(NoteItem? item)
    {
        if (item == null) return;
        item.Status = item.Status == NoteItem.NoteStatus.Done ? NoteItem.NoteStatus.NotReady : NoteItem.NoteStatus.Done;
        await SaveAsync();
    }

    [RelayCommand]
    private async Task BulkToggleCompleteAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
        {
            item.Status = item.Status == NoteItem.NoteStatus.Done ? NoteItem.NoteStatus.NotReady : NoteItem.NoteStatus.Done;
        }
        await SaveAsync();
    }

    [RelayCommand]
    private async Task BulkSetColorAsync(string? colorHex)
    {
        if (SelectedItems.Count == 0 || string.IsNullOrEmpty(colorHex)) return;

        foreach (var item in SelectedItems)
        {
            item.ColorHex = colorHex;
        }
        await SaveAsync();
    }

    [RelayCommand]
    private async Task BulkStartAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
        {
            item.Status = NoteItem.NoteStatus.InProgress;
        }
        await SaveAsync();
        StatusMessage = $"Started {SelectedItems.Count} note(s)";
    }

    [RelayCommand]
    private async Task BulkStopAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
        {
            item.Status = NoteItem.NoteStatus.Done;
        }
        await SaveAsync();
        StatusMessage = $"Stopped {SelectedItems.Count} note(s)";
    }

    [RelayCommand]
    private async Task StartAsync(NoteItem? item)
    {
        if (item == null) return;
        item.Status = NoteItem.NoteStatus.InProgress;
        await SaveAsync();
    }

    [RelayCommand]
    private async Task StopAsync(NoteItem? item)
    {
        if (item == null) return;
        item.Status = NoteItem.NoteStatus.Done;
        await SaveAsync();
    }

    [RelayCommand]
    private void ResetGroups()
    {
        _groupStateStore.ResetAll();
        GroupStatesReset?.Invoke();
        StatusMessage = "Group states reset";
    }

    public async Task SetColorAsync(NoteItem item, string colorHex)
    {
        item.ColorHex = colorHex;
        await SaveAsync();
    }

    public async Task SetStatusAsync(NoteItem item, NoteStatus status)
    {
        if (item == null) return;
        item.Status = status;
        await SaveAsync();
    }

    public async Task AddNoteAsync(NoteItem note)
    {
        Items.Insert(0, note);
        await SaveAsync();
        StatusMessage = "Note added";
    }

    public async Task<bool> AddNoteInlineAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteVendorName))
        {
            StatusMessage = "Vendor name is required";
            return false;
        }

        var note = new NoteItem
        {
            VendorName = NewNoteVendorName.Trim(),
            TransferNumbers = NewNoteTransferNumbers?.Trim() ?? string.Empty,
            WhsShipmentNumbers = NewNoteWhsShipmentNumbers?.Trim() ?? string.Empty,
            ColorHex = _newNoteColorHex,
            CreatedAt = DateTime.UtcNow
        };

        Items.Insert(0, note);
        await SaveAsync();

        // Clear the form
        NewNoteVendorName = string.Empty;
        NewNoteTransferNumbers = string.Empty;
        NewNoteWhsShipmentNumbers = string.Empty;
        _newNoteColorHex = "#B56576";

        StatusMessage = "Note added";
        return true;
    }

    public void SetNewNoteColor(string colorHex)
    {
        _newNoteColorHex = colorHex;
    }

    public string GetNewNoteColor() => _newNoteColorHex;

    public async Task MoveNoteAsync(NoteItem dropped, NoteItem? target)
    {
        if (dropped == target) return;

        if (target == null)
        {
            if (Items.Contains(dropped))
            {
                Items.Remove(dropped);
                Items.Add(dropped);
            }
        }
        else
        {
            int oldIndex = Items.IndexOf(dropped);
            int newIndex = Items.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0) return;

            Items.RemoveAt(oldIndex);
            if (oldIndex < newIndex) newIndex--;
            Items.Insert(newIndex, dropped);
        }

        await SaveAsync();
    }

    public bool GetGroupState(string? name, bool defaultValue = true)
        => _groupStateStore.Get(name, defaultValue);

    public void SetGroupState(string? name, bool value)
        => _groupStateStore.Set(name, value);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _logger?.LogInformation("NotesTrackerViewModel disposed");
        }

        _disposed = true;
    }
}
