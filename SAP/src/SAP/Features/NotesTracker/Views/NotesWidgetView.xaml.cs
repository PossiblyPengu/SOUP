using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SAP.Features.NotesTracker.ViewModels;

namespace SAP.Features.NotesTracker.Views;

public partial class NotesWidgetView : UserControl
{
    public NotesWidgetView()
    {
        InitializeComponent();
    }

    private void StatusToggle_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not Models.NoteItem note) return;
        if (DataContext is not NotesTrackerViewModel vm) return;
        
        var newStatus = note.Status switch
        {
            Models.NoteItem.NoteStatus.NotReady => Models.NoteItem.NoteStatus.OnDeck,
            Models.NoteItem.NoteStatus.OnDeck => Models.NoteItem.NoteStatus.InProgress,
            Models.NoteItem.NoteStatus.InProgress => Models.NoteItem.NoteStatus.Done,
            Models.NoteItem.NoteStatus.Done => Models.NoteItem.NoteStatus.NotReady,
            _ => Models.NoteItem.NoteStatus.NotReady
        };
        
        _ = vm.SetStatusAsync(note, newStatus);
    }

    private void SetStatus_NotReady(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.NoteItem note)
            if (DataContext is NotesTrackerViewModel vm)
                _ = vm.SetStatusAsync(note, Models.NoteItem.NoteStatus.NotReady);
    }

    private void SetStatus_OnDeck(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.NoteItem note)
            if (DataContext is NotesTrackerViewModel vm)
                _ = vm.SetStatusAsync(note, Models.NoteItem.NoteStatus.OnDeck);
    }

    private void SetStatus_InProgress(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.NoteItem note)
            if (DataContext is NotesTrackerViewModel vm)
                _ = vm.SetStatusAsync(note, Models.NoteItem.NoteStatus.InProgress);
    }

    private void SetStatus_Done(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.NoteItem note)
            if (DataContext is NotesTrackerViewModel vm)
                _ = vm.SetStatusAsync(note, Models.NoteItem.NoteStatus.Done);
    }

    private async void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.NoteItem note)
            if (DataContext is NotesTrackerViewModel vm)
            {
                vm.Items.Remove(note);
                await vm.SaveAsync();
            }
    }
}
