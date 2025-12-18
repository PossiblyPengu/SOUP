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
            OrderLogView.DataContext = _viewModel;
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
    }
}
