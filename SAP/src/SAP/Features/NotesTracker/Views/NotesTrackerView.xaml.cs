using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SAP.Features.NotesTracker.Models;
using SAP.Features.NotesTracker.ViewModels;

namespace SAP.Features.NotesTracker.Views;

public partial class NotesTrackerView : UserControl
{
    private Point _dragStartPoint;
    private NoteItem? _draggedItem;
    private Dictionary<NoteItem, (string vendor, string transfers, string whs)> _editBackups = new();

    public NotesTrackerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesTrackerViewModel vm)
        {
            vm.GroupStatesReset += OnGroupStatesReset;
            RefreshExpanders(vm);
        }

        SetPlaceholder(AddVendorBox);
        SetPlaceholder(AddTransfersBox);
        SetPlaceholder(AddWhsBox);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesTrackerViewModel vm)
        {
            vm.GroupStatesReset -= OnGroupStatesReset;
        }
    }

    private void OnGroupStatesReset()
    {
        Dispatcher.Invoke(() =>
        {
            if (DataContext is NotesTrackerViewModel vm)
            {
                RefreshExpanders(vm);
            }
        });
    }

    #region Inline Add Note

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is string placeholder && tb.Text == placeholder)
        {
            tb.Text = string.Empty;
            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f4f4f5"));
        }
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            SetPlaceholder(tb);
        }
    }

    private void SetPlaceholder(TextBox textBox)
    {
        if (string.IsNullOrEmpty(textBox.Text) && textBox.Tag is string placeholder)
        {
            textBox.Text = placeholder;
            textBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717a"));
        }
        else if (!string.IsNullOrEmpty(textBox.Text))
        {
            textBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f4f4f5"));
        }
    }

    private async void AddNoteInline_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NotesTrackerViewModel vm) return;

        if (AddVendorBox.Text == AddVendorBox.Tag as string)
            vm.NewNoteVendorName = string.Empty;
        if (AddTransfersBox.Text == AddTransfersBox.Tag as string)
            vm.NewNoteTransferNumbers = string.Empty;
        if (AddWhsBox.Text == AddWhsBox.Tag as string)
            vm.NewNoteWhsShipmentNumbers = string.Empty;

        var success = await vm.AddNoteInlineAsync();
        if (success)
        {
            SetPlaceholder(AddVendorBox);
            SetPlaceholder(AddTransfersBox);
            SetPlaceholder(AddWhsBox);
            AddVendorBox.Focus();
        }
    }

    private void ClearAddNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NotesTrackerViewModel vm) return;

        vm.NewNoteVendorName = string.Empty;
        vm.NewNoteTransferNumbers = string.Empty;
        vm.NewNoteWhsShipmentNumbers = string.Empty;

        SetPlaceholder(AddVendorBox);
        SetPlaceholder(AddTransfersBox);
        SetPlaceholder(AddWhsBox);
        AddVendorBox.Focus();
    }

    private async void AddNoteColorBar_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NotesTrackerViewModel vm) return;

        var picker = new NotesColorPickerWindow(vm.GetNewNoteColor()) { Owner = Window.GetWindow(this) };

        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedHex))
        {
            vm.SetNewNoteColor(picker.SelectedHex);
            AddNoteColorBar.Background = new BrushConverter().ConvertFromString(picker.SelectedHex) as Brush;
        }
    }

    #endregion

    #region Inline Editing

    private void EditNote_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var note = button?.DataContext as NoteItem;
        if (note == null) return;

        var container = FindAncestor<ContentPresenter>(button);
        if (container == null) return;

        _editBackups[note] = (note.VendorName, note.TransferNumbers, note.WhsShipmentNumbers);

        var viewMode = FindVisualChild<StackPanel>(container, "ViewMode");
        var editMode = FindVisualChild<StackPanel>(container, "EditMode");
        var viewActions = FindVisualChild<Grid>(container, "ViewActions");
        var editActions = FindVisualChild<Grid>(container, "EditActions");

        if (viewMode != null) viewMode.Visibility = Visibility.Collapsed;
        if (editMode != null)
        {
            editMode.Visibility = Visibility.Visible;
            var firstTextBox = FindVisualChild<TextBox>(editMode);
            firstTextBox?.Focus();
            firstTextBox?.SelectAll();
        }
        if (viewActions != null) viewActions.Visibility = Visibility.Collapsed;
        if (editActions != null) editActions.Visibility = Visibility.Visible;
    }

    private async void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var note = button?.DataContext as NoteItem;
        if (note == null || DataContext is not NotesTrackerViewModel vm) return;

        var container = FindAncestor<ContentPresenter>(button);
        if (container == null) return;

        _editBackups.Remove(note);
        await vm.SaveAsync();

        ToggleEditMode(container, false);
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var note = button?.DataContext as NoteItem;
        if (note == null) return;

        if (_editBackups.TryGetValue(note, out var backup))
        {
            note.VendorName = backup.vendor;
            note.TransferNumbers = backup.transfers;
            note.WhsShipmentNumbers = backup.whs;
            _editBackups.Remove(note);
        }

        var container = FindAncestor<ContentPresenter>(button);
        if (container != null)
        {
            ToggleEditMode(container, false);
        }
    }

    private void ToggleEditMode(ContentPresenter container, bool isEditMode)
    {
        var viewMode = FindVisualChild<StackPanel>(container, "ViewMode");
        var editMode = FindVisualChild<StackPanel>(container, "EditMode");
        var viewActions = FindVisualChild<Grid>(container, "ViewActions");
        var editActions = FindVisualChild<Grid>(container, "EditActions");

        if (viewMode != null) viewMode.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
        if (editMode != null) editMode.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        if (viewActions != null) viewActions.Visibility = isEditMode ? Visibility.Collapsed : Visibility.Visible;
        if (editActions != null) editActions.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NoteText_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var textBlock = sender as TextBlock;
            var note = textBlock?.DataContext as NoteItem;
            if (note == null) return;

            var container = FindAncestor<ContentPresenter>(textBlock);
            if (container == null) return;

            _editBackups[note] = (note.VendorName, note.TransferNumbers, note.WhsShipmentNumbers);
            ToggleEditMode(container, true);

            var editMode = FindVisualChild<StackPanel>(container, "EditMode");
            var firstTextBox = FindVisualChild<TextBox>(editMode);
            firstTextBox?.Focus();
            firstTextBox?.SelectAll();
        }
    }

    private async void NoteColorBar_Click(object sender, MouseButtonEventArgs e)
    {
        var border = sender as Border;
        var note = border?.DataContext as NoteItem;
        if (note == null || DataContext is not NotesTrackerViewModel vm) return;

        var picker = new NotesColorPickerWindow(note.ColorHex) { Owner = Window.GetWindow(this) };

        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedHex))
        {
            await vm.SetColorAsync(note, picker.SelectedHex);
        }
    }

    #endregion

    #region Selection & Legacy Support

    private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not NotesTrackerViewModel vm) return;

        foreach (var item in e.AddedItems.OfType<NoteItem>())
        {
            if (!vm.SelectedItems.Contains(item))
                vm.SelectedItems.Add(item);
        }

        foreach (var item in e.RemovedItems.OfType<NoteItem>())
        {
            vm.SelectedItems.Remove(item);
        }
    }

    private async void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NoteItem note }) return;
        if (DataContext is not NotesTrackerViewModel vm) return;

        var picker = new NotesColorPickerWindow(note.ColorHex) { Owner = Window.GetWindow(this) };

        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedHex))
        {
            await vm.SetColorAsync(note, picker.SelectedHex);
        }
    }

    #endregion

    #region Drag and Drop

    private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        _draggedItem = listBoxItem?.DataContext as NoteItem;
    }

    private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;

        var pos = e.GetPosition(null);
        var diff = pos - _dragStartPoint;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var data = new DataObject("NoteItem", _draggedItem);
            DragDrop.DoDragDrop(NotesListBox, data, DragDropEffects.Move);
        }
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("NoteItem") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ListBox_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("NoteItem")) return;
        if (e.Data.GetData("NoteItem") is not NoteItem dropped) return;
        if (DataContext is not NotesTrackerViewModel vm) return;

        var pos = e.GetPosition(NotesListBox);
        var element = NotesListBox.InputHitTest(pos) as DependencyObject;
        var targetContainer = FindAncestor<ListBoxItem>(element);
        var target = targetContainer?.DataContext as NoteItem;

        await vm.MoveNoteAsync(dropped, target);
        _draggedItem = null;
    }

    #endregion

    #region Group Expander State

    private void GroupExpander_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Expander { DataContext: CollectionViewGroup group } expander &&
            DataContext is NotesTrackerViewModel vm)
        {
            var name = group.Name?.ToString() ?? string.Empty;
            expander.IsExpanded = vm.GetGroupState(name, true);
        }
    }

    private void GroupExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is Expander { DataContext: CollectionViewGroup group } &&
            DataContext is NotesTrackerViewModel vm)
        {
            var name = group.Name?.ToString() ?? string.Empty;
            vm.SetGroupState(name, true);
        }
    }

    private void GroupExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        if (sender is Expander { DataContext: CollectionViewGroup group } &&
            DataContext is NotesTrackerViewModel vm)
        {
            var name = group.Name?.ToString() ?? string.Empty;
            vm.SetGroupState(name, false);
        }
    }

    private void RefreshExpanders(NotesTrackerViewModel vm)
    {
        foreach (var expander in FindVisualChildren<Expander>(this))
        {
            if (expander.DataContext is CollectionViewGroup group)
            {
                var name = group.Name?.ToString() ?? string.Empty;
                expander.IsExpanded = vm.GetGroupState(name, true);
            }
        }
    }

    #endregion

    #region Visual Tree Helpers

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent, string? name = null) where T : DependencyObject
    {
        if (parent == null) return null;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && (name == null || (child is FrameworkElement fe && fe.Name == name)))
                return typedChild;

            var result = FindVisualChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) yield return typedChild;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    #endregion
}
