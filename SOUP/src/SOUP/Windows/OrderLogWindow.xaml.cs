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
            Closed += OrderLogWindow_Closed;
        }

        private async void OrderLogWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize OrderLog");
            }
        }

        private void OrderLogWindow_Closed(object? sender, EventArgs e)
        {
            // Unsubscribe events to prevent memory leaks
            Loaded -= OrderLogWindow_Loaded;
            Closed -= OrderLogWindow_Closed;
            
            // Dispose ViewModel if it implements IDisposable
            (_viewModel as IDisposable)?.Dispose();
        }
    }
}
