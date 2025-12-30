using System;
using System.Windows;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Windows
{
    public partial class OrderLogWindow : Window
    {
        private readonly OrderLogViewModel _viewModel;

        public OrderLogWindow(OrderLogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            // Bind the widget preview's DataContext so settings operate on the same VM
            WidgetPreview.DataContext = _viewModel;
            Loaded += OrderLogWindow_Loaded;
        }

        private async void OrderLogWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            Log.Information("OrderLogWindow Loaded event fired");
            try
            {
                Log.Information("Calling InitializeAsync...");
                await _viewModel.InitializeAsync();
                Log.Information("InitializeAsync completed, Items count: {Count}", _viewModel.Items.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize OrderLog");
            }
        }

        private void OpenWidgetWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var existing = Application.Current.Windows.OfType<OrderLogWidgetWindow>().FirstOrDefault();
                if (existing != null)
                {
                    existing.Show();
                    existing.Activate();
                    return;
                }

                MessageBox.Show("Widget window is not currently running. Use the Launcher (Ctrl+Alt+W) to open the widget.", "Open Widget", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open widget window from settings");
            }
        }

        private async void AddBlankOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var order = SOUP.Features.OrderLog.Models.OrderItem.CreateBlankOrder();
                await _viewModel.AddOrderAsync(order);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add blank order from settings");
            }
        }

        private async void AddBlankNote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var note = SOUP.Features.OrderLog.Models.OrderItem.CreateBlankNote();
                await _viewModel.AddOrderAsync(note);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add blank note from settings");
            }
        }
    }
}
