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
/// Service that manages widget windows on separate threads to prevent modal dialog blocking.
/// The widget runs completely independently and can continue working even when
/// modal dialogs are open in the main application.
/// </summary>
public class WidgetThreadService : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private Thread? _widgetThread;
    private Dispatcher? _widgetDispatcher;
    private OrderLogWidgetWindow? _widgetWindow;
    private readonly object _lock = new();
    private bool _disposed;
    
    /// <summary>
    /// Event raised when the widget is closed
    /// </summary>
    public event Action? WidgetClosed;

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
            _widgetThread = new Thread(WidgetThreadProc);
            _widgetThread.SetApartmentState(ApartmentState.STA);
            _widgetThread.IsBackground = false; // Keep app alive while widget is open
            _widgetThread.Name = "OrderLogWidgetThread";
            _widgetThread.Start();
        }
    }
    
    private void WidgetThreadProc()
    {
        try
        {
            // Get services - these are thread-safe singletons
            var viewModel = _serviceProvider.GetRequiredService<OrderLogViewModel>();
            
            // Create the widget window on this thread
            _widgetWindow = new OrderLogWidgetWindow(viewModel, _serviceProvider);
            _widgetWindow.SetSeparateThreadMode();
            
            // Store the dispatcher for this thread
            _widgetDispatcher = Dispatcher.CurrentDispatcher;
            
            // Handle window closed to clean up
            _widgetWindow.OnWidgetClosed += OnWidgetWindowClosed;
            _widgetWindow.Closed += (s, e) =>
            {
                lock (_lock)
                {
                    _widgetWindow = null;
                }
                // Mark thread as background so it doesn't block process exit
                if (Thread.CurrentThread == _widgetThread)
                {
                    Thread.CurrentThread.IsBackground = true;
                }
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            };
            
            _widgetWindow.Show();
            
            Log.Information("OrderLog widget started on separate thread (ThreadId: {ThreadId})", 
                Environment.CurrentManagedThreadId);
            
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
            Log.Information("Widget thread ended");
        }
    }
    
    private void OnWidgetWindowClosed()
    {
        WidgetClosed?.Invoke();
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

        // Mark thread as background FIRST so it won't block process exit
        if (_widgetThread != null && _widgetThread.IsAlive)
        {
            _widgetThread.IsBackground = true;
        }

        CloseWidget();
        
        // Wait briefly for graceful shutdown
        if (_widgetThread != null && _widgetThread.IsAlive)
        {
            try
            {
                _widgetThread.Join(TimeSpan.FromMilliseconds(300));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error waiting for widget thread to exit");
            }
        }
        
        GC.SuppressFinalize(this);
    }
}
