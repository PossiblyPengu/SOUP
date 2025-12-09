using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using SAP.Features.NotesTracker.ViewModels;
using System.Windows.Input;
using SAP.Features.NotesTracker.Models;

namespace SAP.Features.NotesTracker.Views
{
    public partial class NotesTrackerView : UserControl
    {
        private Point _dragStartPoint;
        private NoteItem? _draggedItem;

        public NotesTrackerView()
        {
            InitializeComponent();
            Loaded += NotesTrackerView_Loaded;
        }
        private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
            {
                // sync selection
                foreach (var added in e.AddedItems.Cast<Models.NoteItem>())
                {
                    if (!vm.SelectedItems.Contains(added)) vm.SelectedItems.Add(added);
                }
                foreach (var removed in e.RemovedItems.Cast<Models.NoteItem>())
                {
                    if (vm.SelectedItems.Contains(removed)) vm.SelectedItems.Remove(removed);
                }
            }
        }

        private void ApplyColor_Click(object sender, RoutedEventArgs e)
        {
            // toolbar color apply removed; keep method stub if referenced elsewhere
        }

        private async void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddNoteWindow();
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                if (DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
                {
                    vm.Items.Insert(0, dlg.Result);
                    await vm.SaveAsync();
                }
            }
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.DataContext is Models.NoteItem note && DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
            {
                var picker = new NotesColorPickerWindow(note.ColorHex);
                picker.Owner = Window.GetWindow(this);
                if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedHex))
                {
                    _ = vm.SetColorAsync(note, picker.SelectedHex!);
                }
            }
        }

        private void NotesTrackerView_Loaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
            {
                vm.GroupStatesReset -= Vm_GroupStatesReset;
                vm.GroupStatesReset += Vm_GroupStatesReset;
                // initial refresh to set expander states according to saved settings
                RefreshExpanders(vm);
            }
        }

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            // find the ListBoxItem under mouse
            var lbItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            _draggedItem = lbItem?.DataContext as NoteItem;
        }

        private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            var diff = new Vector(pos.X - _dragStartPoint.X, pos.Y - _dragStartPoint.Y);
            if (_draggedItem != null && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var data = new DataObject("NoteItem", _draggedItem);
                DragDrop.DoDragDrop(NotesListBox, data, DragDropEffects.Move);
            }
        }

        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("NoteItem"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("NoteItem")) return;
            var dropped = e.Data.GetData("NoteItem") as NoteItem;
            if (dropped == null) return;

            // Figure out the target item under the drop point
            var pos = e.GetPosition(NotesListBox);
            var element = NotesListBox.InputHitTest(pos) as DependencyObject;
            var targetItemContainer = FindAncestor<ListBoxItem>(element);
            NoteItem? target = targetItemContainer?.DataContext as NoteItem;

            if (DataContext is NotesTrackerViewModel vm)
            {
                // If no target, move to end
                if (target == null)
                {
                    if (vm.Items.Contains(dropped))
                    {
                        vm.Items.Remove(dropped);
                        vm.Items.Add(dropped);
                    }
                }
                else
                {
                    if (dropped == target) return;
                    int oldIndex = vm.Items.IndexOf(dropped);
                    int newIndex = vm.Items.IndexOf(target);
                    if (oldIndex < 0 || newIndex < 0) return;

                    // remove first (adjust index if needed)
                    vm.Items.RemoveAt(oldIndex);
                    if (oldIndex < newIndex) newIndex--; // list shifted left
                    vm.Items.Insert(newIndex, dropped);
                }

                await vm.SaveAsync();
            }
            _draggedItem = null;
        }

        private void Vm_GroupStatesReset()
        {
            // Called from VM; marshal to UI thread
            Dispatcher.Invoke(() =>
            {
                if (DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
                {
                    RefreshExpanders(vm);
                }
            });
        }

        private void RefreshExpanders(SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
        {
            foreach (var exp in FindVisualChildren<Expander>(this))
            {
                if (exp.DataContext is System.Windows.Data.CollectionViewGroup g)
                {
                    var name = g.Name?.ToString() ?? string.Empty;
                    exp.IsExpanded = vm.GetGroupState(name, true);
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
        }

        private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void GroupExpander_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander exp && exp.DataContext is System.Windows.Data.CollectionViewGroup g && DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
            {
                var name = g.Name?.ToString() ?? string.Empty;
                // default to expanded
                exp.IsExpanded = vm.GetGroupState(name, true);
            }
        }

        private void GroupExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander exp && exp.DataContext is System.Windows.Data.CollectionViewGroup g && DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
            {
                var name = g.Name?.ToString() ?? string.Empty;
                vm.SetGroupState(name, true);
            }
        }

        private void GroupExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            if (sender is Expander exp && exp.DataContext is System.Windows.Data.CollectionViewGroup g && DataContext is SAP.Features.NotesTracker.ViewModels.NotesTrackerViewModel vm)
            {
                var name = g.Name?.ToString() ?? string.Empty;
                vm.SetGroupState(name, false);
            }
        }
    }
}
