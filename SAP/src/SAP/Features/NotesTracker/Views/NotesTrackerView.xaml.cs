using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SAP.Features.NotesTracker.ViewModels;

namespace SAP.Features.NotesTracker.Views;

public partial class NotesTrackerView : UserControl
{
    public NotesTrackerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetPlaceholder(VendorBox);
        SetPlaceholder(TransfersBox);
        SetPlaceholder(WhsBox);

        // Set initial color
        if (DataContext is NotesTrackerViewModel vm)
        {
            try
            {
                var hex = vm.GetNewNoteColor();
                if (!string.IsNullOrEmpty(hex))
                {
                    ColorPreview.Background = new BrushConverter().ConvertFromString(hex) as Brush;
                }
            }
            catch { }
        }
    }

    private void SetPlaceholder(TextBox tb)
    {
        if (tb.Tag is string placeholder && string.IsNullOrEmpty(tb.Text))
        {
            tb.Text = placeholder;
            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717a"));
        }
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is string placeholder)
        {
            if (tb.Text == placeholder)
            {
                tb.Text = "";
                tb.Foreground = Application.Current.Resources["TextPrimaryBrush"] as Brush;
            }
        }
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            SetPlaceholder(tb);
        }
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is NotesTrackerViewModel vm)
        {
            _ = AddNoteAsync(vm);
            e.Handled = true;
        }
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesTrackerViewModel vm)
        {
            _ = AddNoteAsync(vm);
        }
    }

    private async Task AddNoteAsync(NotesTrackerViewModel vm)
    {
        // Clear placeholders
        var vendor = VendorBox.Text == "Vendor" ? "" : VendorBox.Text;
        var transfers = TransfersBox.Text == "Transfers" ? "" : TransfersBox.Text;
        var whs = WhsBox.Text == "WHs" ? "" : WhsBox.Text;

        if (string.IsNullOrWhiteSpace(vendor)) return;

        var note = new Models.NoteItem
        {
            VendorName = vendor,
            TransferNumbers = transfers,
            WhsShipmentNumbers = whs,
            ColorHex = vm.GetNewNoteColor(),
            Status = Models.NoteItem.NoteStatus.NotReady
        };

        await vm.AddNoteAsync(note);

        // Clear fields
        VendorBox.Text = "";
        TransfersBox.Text = "";
        WhsBox.Text = "";
        SetPlaceholder(VendorBox);
        SetPlaceholder(TransfersBox);
        SetPlaceholder(WhsBox);
        VendorBox.Focus();
    }

    private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NotesTrackerViewModel vm) return;

        var picker = new NotesColorPickerWindow(vm.GetNewNoteColor())
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            vm.SetNewNoteColor(picker.SelectedColor);
            ColorPreview.Background = new BrushConverter().ConvertFromString(picker.SelectedColor) as Brush;
        }
    }

    private void ColorBar_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not Models.NoteItem note) return;
        if (DataContext is not NotesTrackerViewModel vm) return;

        var picker = new NotesColorPickerWindow(note.ColorHex)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            note.ColorHex = picker.SelectedColor;
            _ = vm.SaveAsync();
        }
    }

    private void ChangeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not Models.NoteItem note) return;
        if (DataContext is not NotesTrackerViewModel vm) return;

        var picker = new NotesColorPickerWindow(note.ColorHex)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            note.ColorHex = picker.SelectedColor;
            _ = vm.SaveAsync();
        }
    }

    private void StatusToggle_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not Models.NoteItem note) return;
        if (DataContext is not NotesTrackerViewModel vm) return;
        
        // Cycle through statuses: NotReady -> OnDeck -> InProgress -> Done -> NotReady
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

    private async void UnarchiveNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.NoteItem note)
            if (DataContext is NotesTrackerViewModel vm)
            {
                note.IsArchived = false;
                vm.ArchivedItems.Remove(note);
                vm.Items.Insert(0, note);
                await vm.SaveAsync();
            }
    }
}

