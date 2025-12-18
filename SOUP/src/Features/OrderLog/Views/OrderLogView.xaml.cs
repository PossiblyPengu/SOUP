using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Views;

public partial class OrderLogView : UserControl
{
    private bool _showingArchivedTab = false;

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
                tb.Foreground = Application.Current?.Resources["TextPrimaryBrush"] as Brush ?? Brushes.White;
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
        var order = new Models.OrderItem
        {
            NoteType = Models.NoteType.Order,
            VendorName = "New Order",
            TransferNumbers = string.Empty,
            WhsShipmentNumbers = string.Empty,
            ColorHex = "#B56576",
            Status = Models.OrderItem.OrderStatus.NotReady
        };

        await vm.AddOrderAsync(order);
    }

    private async Task AddBlankNoteAsync(OrderLogViewModel vm)
    {
        var note = new Models.OrderItem
        {
            NoteType = Models.NoteType.StickyNote,
            NoteContent = "",
            ColorHex = "#FFD700",
            Status = Models.OrderItem.OrderStatus.OnDeck
        };

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

    private void SetStatus_NotReady(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, Models.OrderItem.OrderStatus.NotReady);
    }

    private void SetStatus_OnDeck(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, Models.OrderItem.OrderStatus.OnDeck);
    }

    private void SetStatus_InProgress(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, Models.OrderItem.OrderStatus.InProgress);
    }

    private void SetStatus_Done(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
            if (DataContext is OrderLogViewModel vm)
                _ = vm.SetStatusAsync(order, Models.OrderItem.OrderStatus.Done);
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
}

