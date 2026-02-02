using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for coordinating undo/redo UI state and timers.
/// Manages undo timeout, countdown display, and UI message state.
/// </summary>
public class OrderUndoCoordinator : IDisposable
{
    private readonly ILogger<OrderUndoCoordinator>? _logger;
    private DispatcherTimer? _undoTimer;
    private DispatcherTimer? _undoCountdownTimer;
    private bool _disposed;

    public event Action? UndoTimerExpired;
    public event Action<int>? SecondsRemainingChanged;

    public OrderUndoCoordinator(ILogger<OrderUndoCoordinator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts the undo timer with countdown UI updates.
    /// </summary>
    /// <param name="timeoutSeconds">Timeout in seconds before undo expires</param>
    /// <param name="message">Status message to display</param>
    /// <param name="setStatus">Action to update status message</param>
    public void StartUndoTimer(
        int timeoutSeconds,
        string message,
        Action<string> setStatus)
    {
        setStatus(message + " - tap Undo to revert");

        // Initialize or reuse main timeout timer
        if (_undoTimer == null)
        {
            _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(timeoutSeconds) };
            _undoTimer.Tick += OnUndoTimerTick;
        }
        else
        {
            _undoTimer.Stop();
            _undoTimer.Interval = TimeSpan.FromSeconds(timeoutSeconds);
        }
        _undoTimer.Start();

        // Start countdown timer for UI updates
        StartCountdownTimer(timeoutSeconds);
    }

    /// <summary>
    /// Starts the countdown timer that updates remaining seconds.
    /// </summary>
    private void StartCountdownTimer(int initialSeconds)
    {
        var secondsRemaining = initialSeconds;
        SecondsRemainingChanged?.Invoke(secondsRemaining);

        if (_undoCountdownTimer == null)
        {
            _undoCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _undoCountdownTimer.Tick += (s, e) =>
            {
                try
                {
                    if (secondsRemaining > 0)
                    {
                        secondsRemaining--;
                        SecondsRemainingChanged?.Invoke(secondsRemaining);
                    }

                    if (secondsRemaining <= 0)
                    {
                        _undoCountdownTimer?.Stop();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error in undo countdown timer tick");
                }
            };
        }
        else
        {
            _undoCountdownTimer.Stop();
        }

        _undoCountdownTimer.Start();
    }

    /// <summary>
    /// Stops all undo timers immediately.
    /// </summary>
    public void StopTimers()
    {
        _undoTimer?.Stop();
        _undoCountdownTimer?.Stop();
        SecondsRemainingChanged?.Invoke(0);
    }

    /// <summary>
    /// Cancels the undo operation by stopping timers.
    /// </summary>
    public void CancelUndo()
    {
        StopTimers();
    }

    /// <summary>
    /// Updates the undo timer interval if needed.
    /// </summary>
    public void UpdateTimerInterval(int newTimeoutSeconds)
    {
        if (_undoTimer != null && !_undoTimer.IsEnabled)
        {
            _undoTimer.Interval = TimeSpan.FromSeconds(newTimeoutSeconds);
        }
    }

    private void OnUndoTimerTick(object? sender, EventArgs e)
    {
        // Timer expired - notify listeners
        StopTimers();
        UndoTimerExpired?.Invoke();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            if (_undoTimer != null)
            {
                _undoTimer.Stop();
                _undoTimer.Tick -= OnUndoTimerTick;
                _undoTimer = null;
            }

            if (_undoCountdownTimer != null)
            {
                _undoCountdownTimer.Stop();
                _undoCountdownTimer = null;
            }

            _logger?.LogDebug("OrderUndoCoordinator disposed");
        }

        _disposed = true;
    }
}
