using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Constants;

namespace SOUP.Features.OrderLog.Views;

public partial class OrderLogView : UserControl
{
    private bool _showingArchivedTab = false;
    private System.Windows.Point _dragStartPoint;

    public OrderLogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialization complete
    }

    private void ActiveTab_Checked(object sender, RoutedEventArgs e)
    {
        _showingArchivedTab = false;
        UpdateTabState();
    }

    private void ArchivedTab_Checked(object sender, RoutedEventArgs e)
    {
        _showingArchivedTab = true;
        UpdateTabState();
    }

    private void UpdateTabState()
    {
        // Guard against null elements during initialization
        if (ActiveItemsPanel == null || ArchivedItemsPanel == null || 
            AddOrderButton == null || AddNoteButton == null)
            return;
            
        // Update panel visibility based on selected tab
        if (_showingArchivedTab)
        {
            ActiveItemsPanel.Visibility = Visibility.Collapsed;
            ArchivedItemsPanel.Visibility = Visibility.Visible;
            AddOrderButton.Visibility = Visibility.Collapsed;
            AddNoteButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ActiveItemsPanel.Visibility = Visibility.Visible;
            ArchivedItemsPanel.Visibility = Visibility.Collapsed;
            AddOrderButton.Visibility = Visibility.Visible;
            AddNoteButton.Visibility = Visibility.Visible;
        }
    }

    private void AddBlankOrder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            _ = AddBlankOrderAsync(vm);
        }
    }

    private void AddBlankNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            _ = AddBlankNoteAsync(vm);
        }
    }

    private async Task AddBlankOrderAsync(OrderLogViewModel vm)
    {
        var order = Models.OrderItem.CreateBlankOrder();
        await vm.AddOrderAsync(order);
    }

    private async Task AddBlankNoteAsync(OrderLogViewModel vm)
    {
        var note = Models.OrderItem.CreateBlankNote();
        await vm.AddOrderAsync(note);
    }

    private void ColorBar_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not Models.OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Only allow color picking for sticky notes - orders use status colors
        if (order.NoteType != Models.NoteType.StickyNote) return;

        var picker = new OrderColorPickerWindow(order.ColorHex)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            order.ColorHex = picker.SelectedColor;
            _ = vm.SaveAsync();
        }
    }

    private void ChangeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not Models.OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Only allow color picking for sticky notes - orders use status colors
        if (order.NoteType != Models.NoteType.StickyNote) return;

        var picker = new OrderColorPickerWindow(order.ColorHex)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            order.ColorHex = picker.SelectedColor;
            _ = vm.SaveAsync();
        }
    }

    private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.DataContext is not Models.OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        if (comboBox.SelectedItem is not ComboBoxItem selectedItem) return;

        if (selectedItem.Tag is Models.OrderItem.OrderStatus newStatus && order.Status != newStatus)
        {
            _ = vm.SetStatusAsync(order, newStatus);
        }
    }

    private void UnifiedStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (comboBox.DataContext is not ViewModels.OrderItemGroup group) return;
        if (DataContext is not OrderLogViewModel vm) return;
        if (comboBox.SelectedItem is not ComboBoxItem selectedItem) return;
        if (selectedItem.Tag is not Models.OrderItem.OrderStatus newStatus) return;

        // Apply status to ALL members in the group
        foreach (var member in group.Members)
        {
            if (member.Status != newStatus)
            {
                _ = vm.SetStatusAsync(member, newStatus);
            }
        }
    }

    private void SetStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Models.OrderItem order, Tag: Models.OrderItem.OrderStatus status } &&
            DataContext is OrderLogViewModel vm)
        {
            _ = vm.SetStatusAsync(order, status);
        }
    }

    private async void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Models.OrderItem? order = null;
            
            if (sender is Button btn)
                order = btn.DataContext as Models.OrderItem;
            else if (sender is MenuItem menuItem)
                order = menuItem.DataContext as Models.OrderItem;
            
            if (order != null && DataContext is OrderLogViewModel vm)
            {
                vm.Items.Remove(order);
                vm.ArchivedItems.Remove(order);
                await vm.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete note");
        }
    }

    private async void UnarchiveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    await vm.UnarchiveOrderCommand.ExecuteAsync(order);
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to unarchive note");
        }
    }

    private async void DeleteArchivedNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    vm.ArchivedItems.Remove(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete archived order");
        }
    }

    // Inline editing for order card fields
    private void EditableField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            // Show placeholder hint if empty
            if (string.IsNullOrEmpty(tb.Text) && tb.Tag is string placeholder)
            {
                tb.Foreground = Application.Current?.Resources["TextDisabledBrush"] as Brush ?? Brushes.Gray;
            }
            else
            {
                tb.Foreground = Application.Current?.Resources["TextPrimaryBrush"] as Brush ?? Brushes.White;
            }
            tb.Background = Application.Current?.Resources["SurfaceHoverBrush"] as Brush ?? Brushes.Transparent;
            tb.SelectAll();
        }
    }

    private async void EditableField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Background = Brushes.Transparent;
            tb.Foreground = Application.Current?.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray;
            
            // Save changes
            if (DataContext is OrderLogViewModel vm)
            {
                await vm.SaveAsync();
            }
        }
    }

    #region Text Formatting Tools

    private void FormatBold_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.FormatBold(sender, this);

    private void FormatItalic_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.FormatItalic(sender, this);

    private void FormatUnderline_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.FormatUnderline(sender, this);

    private void InsertBullet_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertBullet(sender, this);

    private void InsertCheckbox_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertCheckbox(sender, this);

    private void InsertTimestamp_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertTimestamp(sender, this);

    private void InsertDivider_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertDivider(sender, this);

    private void NoteContent_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is RichTextBox rtb)
            Helpers.TextFormattingHelper.HandleListAutoContinuation(rtb, e);
    }

    private void NoteContent_Loaded(object sender, RoutedEventArgs e)
    {
        Helpers.TextFormattingHelper.LoadNoteContent(sender);
    }

    private void NoteContent_LostFocus(object sender, RoutedEventArgs e)
    {
        // Save rich content
        Helpers.TextFormattingHelper.UpdateNoteContent(sender, this);
    }

    private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            _dragStartPoint = e.GetPosition(null);
        }
    }

    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is FrameworkElement fe && fe.DataContext is Models.OrderItem order)
        {
            // If multiple selected, pack all selected ids into payload
            if (DataContext is OrderLogViewModel vm && vm.SelectedItems.Count > 1 && vm.SelectedItems.Contains(order))
            {
                var ids = vm.SelectedItems.Select(i => i.Id).ToArray();
                var data = new DataObject();
                data.SetData("OrderItemIds", ids);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            }
            else
            {
                var data = new DataObject("OrderItemId", order.Id);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            }
        }
    }

    private void Item_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("OrderItemId"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        // Visual highlight - different colors for link vs move
        if (sender is Border b)
        {
            if (b.Tag == null) b.Tag = b.BorderBrush;

            // Check if Ctrl is held for link mode
            bool isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (isLinkMode)
            {
                // Purple/violet for linking
                b.BorderBrush = new SolidColorBrush(Color.FromRgb(138, 43, 226)); // BlueViolet
                b.BorderThickness = new Thickness(3);
            }
            else
            {
                // Green for move
                b.BorderBrush = Application.Current?.Resources["SuccessBrush"] as Brush ?? Brushes.LightGreen;
                b.BorderThickness = new Thickness(2);
            }
        }

        e.Handled = true;
    }

    private void Item_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b && b.Tag is Brush orig)
        {
            b.BorderBrush = orig;
            b.Tag = null;
        }
    }

    private async void Item_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent("OrderItemId") && !e.Data.GetDataPresent("OrderItemIds")) return;

            var droppedIds = new System.Collections.Generic.List<Guid>();
            if (e.Data.GetDataPresent("OrderItemIds") && e.Data.GetData("OrderItemIds") is System.Guid[] arr)
            {
                droppedIds.AddRange(arr);
            }
            else if (e.Data.GetDataPresent("OrderItemId"))
            {
                droppedIds.Add((Guid)e.Data.GetData("OrderItemId"));
            }

            Models.OrderItem? dropped = null;

            if (DataContext is OrderLogViewModel vm)
            {
                var droppedItems = vm.Items.Concat(vm.ArchivedItems).Where(i => droppedIds.Contains(i.Id)).ToList();

                // If single and linked group present, expand inside MoveOrdersAsync (it will handle)
                Models.OrderItem? target = null;
                if (sender is FrameworkElement fe && fe.DataContext is Models.OrderItem ti) target = ti;

                if (droppedItems.Count > 0)
                {
                    // If Ctrl is held, create/merge linked group instead of moving
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                    {
                        await vm.LinkItemsAsync(droppedItems, target);
                        vm.StatusMessage = "Linked items";
                    }
                    else
                    {
                        await vm.MoveOrdersAsync(droppedItems, target);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Drop failed");
        }
    }

    private async void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    await vm.MoveUpCommand.ExecuteAsync(order);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed MoveUp");
        }
    }

    private async void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    await vm.MoveDownCommand.ExecuteAsync(order);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed MoveDown");
        }
    }

    private async void LinkWith_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    var dlg = new LinkOrdersWindow(order, vm) { Owner = Window.GetWindow(this) };
                    if (dlg.ShowDialog() == true)
                    {
                        await vm.SaveAsync();
                        vm.StatusMessage = "Orders linked";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to link orders");
        }
    }

    private async void Unlink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    if (order.LinkedGroupId == null)
                    {
                        vm.StatusMessage = "Order was not linked";
                        return;
                    }

                    var gid = order.LinkedGroupId;

                    // Clear linked id for all items in same group
                    foreach (var item in vm.Items)
                    {
                        if (item.LinkedGroupId == gid) item.LinkedGroupId = null;
                    }
                    foreach (var item in vm.ArchivedItems)
                    {
                        if (item.LinkedGroupId == gid) item.LinkedGroupId = null;
                    }

                    await vm.SaveAsync();
                    vm.StatusMessage = "Unlinked group";
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to unlink orders");
        }
    }

    #endregion

    #region Merged Card Drag and Drop

    private System.Windows.Point _mergedCardDragStartPoint;

    private void MergedCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            _mergedCardDragStartPoint = e.GetPosition(null);
        }
    }

    private void MergedCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not ViewModels.OrderItemGroup group) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _mergedCardDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _mergedCardDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Drag all member IDs
        var ids = group.Members.Select(m => m.Id).ToArray();
        var data = new DataObject();
        data.SetData("OrderItemIds", ids);
        data.SetData("IsMergedCard", true); // Flag to indicate it's a merged card drag

        DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
    }

    private void MergedCard_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("OrderItemId") || e.Data.GetDataPresent("OrderItemIds"))
        {
            e.Effects = DragDropEffects.Move;

            // Visual feedback - different colors for link vs move
            if (sender is Border b)
            {
                if (b.Tag == null) b.Tag = b.BorderBrush;

                bool isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (isLinkMode)
                {
                    // Purple/violet for linking
                    b.BorderBrush = new SolidColorBrush(Color.FromRgb(138, 43, 226));
                    b.BorderThickness = new Thickness(4);
                }
                else
                {
                    // Blue/accent for move
                    b.BorderBrush = (System.Windows.Media.Brush)Application.Current?.Resources["AccentBrush"] ?? System.Windows.Media.Brushes.LightBlue;
                    b.BorderThickness = new Thickness(3);
                }
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void MergedCard_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b && b.Tag is System.Windows.Media.Brush orig)
        {
            b.BorderBrush = orig;
            b.BorderThickness = new Thickness(1);
            b.Tag = null;
        }
    }

    private async void MergedCard_Drop(object sender, DragEventArgs e)
    {
        try
        {
            // Reset visual feedback
            if (sender is Border b && b.Tag is System.Windows.Media.Brush orig)
            {
                b.BorderBrush = orig;
                b.BorderThickness = new Thickness(1);
                b.Tag = null;
            }

            if (!e.Data.GetDataPresent("OrderItemId") && !e.Data.GetDataPresent("OrderItemIds")) return;

            var droppedIds = new System.Collections.Generic.List<Guid>();
            if (e.Data.GetDataPresent("OrderItemIds") && e.Data.GetData("OrderItemIds") is Guid[] arr)
            {
                droppedIds.AddRange(arr);
            }
            else if (e.Data.GetDataPresent("OrderItemId"))
            {
                droppedIds.Add((Guid)e.Data.GetData("OrderItemId"));
            }

            if (DataContext is not OrderLogViewModel vm) return;
            if (sender is not FrameworkElement fe || fe.DataContext is not ViewModels.OrderItemGroup targetGroup) return;

            var droppedItems = vm.Items.Concat(vm.ArchivedItems).Where(i => droppedIds.Contains(i.Id)).ToList();
            var target = targetGroup.First; // Drop before the first item of target group

            if (droppedItems.Count > 0)
            {
                await vm.MoveOrdersAsync(droppedItems, target);
                vm.StatusMessage = $"Moved {droppedItems.Count} item(s)";
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Merged card drop failed");
        }
    }

    #endregion

    #region Section Drag Handles (Split-Drag)

    private System.Windows.Point _sectionDragStartPoint;

    private void SectionDragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            _sectionDragStartPoint = e.GetPosition(null);
            e.Handled = true; // Prevent merged card drag from starting
        }
    }

    private void SectionDragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not Border border) return;

        // Find the OrderItem from the Border's DataContext
        var current = border.DataContext;
        if (current is not Models.OrderItem orderItem) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _sectionDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _sectionDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Drag this single order (will auto-unlink when dropped elsewhere)
        var data = new DataObject();
        data.SetData("OrderItemId", orderItem.Id);
        data.SetData("SplitFromGroup", true); // Flag to indicate split-drag

        DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
        e.Handled = true;
    }

    #endregion
}

