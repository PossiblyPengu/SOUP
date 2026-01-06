using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Windows;

namespace SOUP.Services;

/// <summary>
/// Service that manages widget windows on separate threads to prevent modal dialog blocking
/// </summary>
public class WidgetThreadService : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private Thread? _widgetThread;
    private Dispatcher? _widgetDispatcher;
    private OrderLogWidgetWindow? _widgetWindow;
    private readonly object _lock = new();
    private bool _disposed;

    public WidgetThreadService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Shows the OrderLog widget on its own thread (independent from modal dialogs)
    /// </summary>
    public void ShowOrderLogWidget()
    {
        lock (_lock)
        {
            // If widget already exists, just show/activate it
            if (_widgetWindow != null && _widgetDispatcher != null)
            {
                _widgetDispatcher.Invoke(() =>
                {
                    _widgetWindow.Show();
                    _widgetWindow.Activate();
                    if (_widgetWindow.WindowState == WindowState.Minimized)
                    {
                        _widgetWindow.WindowState = WindowState.Normal;
                    }
                });
                return;
            }

            // Create new thread for widget
            _widgetThread = new Thread(() =>
            {
                try
                {
                    // Create the widget window on this thread
                    var viewModel = _serviceProvider.GetRequiredService<OrderLogViewModel>();
                    _widgetWindow = new OrderLogWidgetWindow(viewModel, _serviceProvider);
                    
                    // Store the dispatcher for this thread
                    _widgetDispatcher = Dispatcher.CurrentDispatcher;
                    
                    // Handle window closed to clean up
                    _widgetWindow.Closed += (s, e) =>
                    {
                        lock (_lock)
                        {
                            _widgetWindow = null;
                        }
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    };
                    
                    _widgetWindow.Show();
                    
                    Log.Information("OrderLog widget started on separate thread");
                    
                    // Run the dispatcher for this thread
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error running widget on separate thread");
                }
                finally
                {
                    lock (_lock)
                    {
                        _widgetDispatcher = null;
                        _widgetThread = null;
                    }
                }
            });

            _widgetThread.SetApartmentState(ApartmentState.STA);
            _widgetThread.IsBackground = true;
            _widgetThread.Name = "OrderLogWidgetThread";
            _widgetThread.Start();
        }
    }

    /// <summary>
    /// Closes the widget window if open
    /// </summary>
    public void CloseWidget()
    {
        lock (_lock)
        {
            if (_widgetWindow != null && _widgetDispatcher != null)
            {
                _widgetDispatcher.Invoke(() =>
                {
                    _widgetWindow?.Close();
                });
            }
        }
    }

    /// <summary>
    /// Gets whether the widget is currently open
    /// </summary>
    public bool IsWidgetOpen
    {
        get
        {
            lock (_lock)
            {
                return _widgetWindow != null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CloseWidget();
        
        // Give the thread time to shut down
        _widgetThread?.Join(TimeSpan.FromSeconds(2));
        
        GC.SuppressFinalize(this);
    }
}
