using System.Windows;
using System.Windows.Controls;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Models;
using SOUP.Windows;

namespace SOUP.Features.OrderLog.Views;

public partial class OrderLogSettingsView : UserControl
{
    public OrderLogSettingsView()
    {
        InitializeComponent();
    }

    private async void AddBlankOrder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            var order = OrderItem.CreateBlankOrder();
            await vm.AddOrderAsync(order);
        }
    }

    private async void AddBlankNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            var note = OrderItem.CreateBlankNote();
            await vm.AddOrderAsync(note);
        }
    }

    private void OpenWidgetWindow_Click(object sender, RoutedEventArgs e)
    {
        // Use the application's DI container to obtain the widget window (it is registered as a singleton)
        var widget = App.GetService<Windows.OrderLogWidgetWindow>();
        widget.ShowWidget();
    }
}
