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
    private readonly Lock _lock = new();
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
        Dispatcher? dispatcher;
        OrderLogWidgetWindow? window;

        lock (_lock)
        {
            dispatcher = _widgetDispatcher;
            window = _widgetWindow;
        }

        if (window != null && dispatcher != null)
        {
            try
            {
                // Use BeginInvoke for async close, then wait with timeout
                var operation = dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                {
                    try
                    {
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error closing widget window");
                    }
                }));

                // Wait for close with timeout
                operation.Wait(TimeSpan.FromMilliseconds(500));

                // If the window didn't close gracefully, force shutdown the dispatcher
                if (!operation.Status.HasFlag(DispatcherOperationStatus.Completed))
                {
                    Log.Warning("Widget close timed out, forcing dispatcher shutdown");
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during widget close, forcing dispatcher shutdown");
                try
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                }
                catch { /* Ignore - dispatcher might already be shut down */ }
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
                if (!_widgetThread.Join(TimeSpan.FromMilliseconds(500)))
                {
                    Log.Warning("Widget thread did not exit gracefully, forcing dispatcher shutdown");
                    // Force dispatcher shutdown if thread is still alive
                    try
                    {
                        _widgetDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                    catch { /* Ignore */ }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error waiting for widget thread to exit");
            }
        }

        GC.SuppressFinalize(this);
    }
}
