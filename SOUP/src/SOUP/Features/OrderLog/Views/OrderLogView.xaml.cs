using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Views;

public partial class OrderLogView : UserControl
{
    public OrderLogView()
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
        if (DataContext is OrderLogViewModel vm)
        {
            try
            {
                var hex = vm.GetNewNoteColor();
                if (!string.IsNullOrEmpty(hex))
                {
                    ColorPreview.Background = new BrushConverter().ConvertFromString(hex) as Brush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set color preview: {ex.Message}");
            }
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

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is OrderLogViewModel vm)
        {
            _ = AddOrderAsync(vm);
            e.Handled = true;
        }
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            _ = AddOrderAsync(vm);
        }
    }

    private async Task AddOrderAsync(OrderLogViewModel vm)
    {
        // Clear placeholders
        var vendor = VendorBox.Text == "Vendor" ? "" : VendorBox.Text;
        var transfers = TransfersBox.Text == "Transfers" ? "" : TransfersBox.Text;
        var whs = WhsBox.Text == "WHs" ? "" : WhsBox.Text;

        if (string.IsNullOrWhiteSpace(vendor)) return;

        var order = new Models.OrderItem
        {
            VendorName = vendor,
            TransferNumbers = transfers,
            WhsShipmentNumbers = whs,
            ColorHex = vm.GetNewNoteColor(),
            Status = Models.OrderItem.OrderStatus.NotReady
        };

        await vm.AddOrderAsync(order);

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
        if (DataContext is not OrderLogViewModel vm) return;

        var picker = new OrderColorPickerWindow(vm.GetNewNoteColor())
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
        if (sender is not Border border || border.DataContext is not Models.OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;

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
        if (sender is not Border border || border.DataContext is not Models.OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Cycle through statuses: NotReady -> OnDeck -> InProgress -> Done -> NotReady
        var newStatus = order.Status switch
        {
            Models.OrderItem.OrderStatus.NotReady => Models.OrderItem.OrderStatus.OnDeck,
            Models.OrderItem.OrderStatus.OnDeck => Models.OrderItem.OrderStatus.InProgress,
            Models.OrderItem.OrderStatus.InProgress => Models.OrderItem.OrderStatus.Done,
            Models.OrderItem.OrderStatus.Done => Models.OrderItem.OrderStatus.NotReady,
            _ => Models.OrderItem.OrderStatus.NotReady
        };
        
        _ = vm.SetStatusAsync(order, newStatus);
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
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    vm.Items.Remove(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete note: {ex.Message}");
        }
    }

    private async void UnarchiveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    order.IsArchived = false;
                    vm.ArchivedItems.Remove(order);
                    vm.Items.Insert(0, order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to unarchive note: {ex.Message}");
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

