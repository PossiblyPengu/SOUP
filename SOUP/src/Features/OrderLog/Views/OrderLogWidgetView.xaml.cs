using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Views;

/// <summary>
/// Full-featured widget view for Order Log - designed for AppBar docking
/// </summary>
public partial class OrderLogWidgetView : UserControl
{
    public event EventHandler? OpenFullViewRequested;

    public OrderLogWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialization complete
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
        var order = new OrderItem
        {
            NoteType = NoteType.Order,
            VendorName = "New Order",
            TransferNumbers = string.Empty,
            WhsShipmentNumbers = string.Empty,
            ColorHex = "#B56576",
            Status = OrderItem.OrderStatus.NotReady
        };

        await vm.AddOrderAsync(order);
    }

    private async Task AddBlankNoteAsync(OrderLogViewModel vm)
    {
        var note = new OrderItem
        {
            NoteType = NoteType.StickyNote,
            NoteContent = "",
            ColorHex = "#FFD700",
            Status = OrderItem.OrderStatus.OnDeck
        };

        await vm.AddOrderAsync(note);
    }

    private void ColorBar_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Only allow color picking for sticky notes - orders use status colors
        if (order.NoteType != NoteType.StickyNote) return;

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
        if (sender is not MenuItem menuItem || menuItem.DataContext is not OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Only allow color picking for sticky notes - orders use status colors
        if (order.NoteType != NoteType.StickyNote) return;

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

    private void StatusToggle_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;

        // Cycle through statuses: NotReady -> OnDeck -> InProgress -> Done -> NotReady
        var nextStatus = order.Status switch
        {
            OrderItem.OrderStatus.NotReady => OrderItem.OrderStatus.OnDeck,
            OrderItem.OrderStatus.OnDeck => OrderItem.OrderStatus.InProgress,
            OrderItem.OrderStatus.InProgress => OrderItem.OrderStatus.Done,
            OrderItem.OrderStatus.Done => OrderItem.OrderStatus.NotReady,
            _ => OrderItem.OrderStatus.NotReady
        };

        _ = vm.SetStatusAsync(order, nextStatus);
    }

    private void SetStatus_NotReady(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, OrderItem.OrderStatus.NotReady);
    }

    private void SetStatus_OnDeck(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, OrderItem.OrderStatus.OnDeck);
    }

    private void SetStatus_InProgress(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, OrderItem.OrderStatus.InProgress);
    }

    private void SetStatus_Done(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, OrderItem.OrderStatus.Done);
    }

    private async void ArchiveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    order.IsArchived = true;
                    vm.Items.Remove(order);
                    vm.ArchivedItems.Add(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to archive order");
        }
    }

    private async void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    vm.Items.Remove(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete order");
        }
    }

    private async void DeleteArchivedNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
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

    private async void UnarchiveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    // Check if already in Items to prevent duplication
                    if (vm.Items.Contains(order)) return;
                    
                    order.IsArchived = false;
                    vm.ArchivedItems.Remove(order);
                    vm.Items.Insert(0, order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to unarchive order");
        }
    }

    private void OpenFullView_Click(object sender, RoutedEventArgs e)
    {
        OpenFullViewRequested?.Invoke(this, EventArgs.Empty);
    }

    // Inline editing for order card fields
    private void EditableField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Foreground = Application.Current?.Resources["TextPrimaryBrush"] as Brush ?? Brushes.White;
            tb.Background = Application.Current?.Resources["SurfaceHoverBrush"] as Brush ?? Brushes.Transparent;
            tb.SelectAll();
        }
    }

    private async void EditableField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Background = Brushes.Transparent;
            
            // Use disabled color if empty, secondary otherwise
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Foreground = Application.Current?.Resources["TextDisabledBrush"] as Brush ?? Brushes.Gray;
            }
            else
            {
                tb.Foreground = Application.Current?.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray;
            }
            
            // Save changes
            if (DataContext is OrderLogViewModel vm)
            {
                await vm.SaveAsync();
            }
        }
    }
}
