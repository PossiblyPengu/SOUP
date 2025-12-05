using System.Windows;
using System.Windows.Media.Animation;

namespace SAP.Helpers;

/// <summary>
/// Helper class for applying smooth animations to WPF windows.
/// </summary>
/// <remarks>
/// Provides fade-in/fade-out animations for window open and close transitions,
/// creating a more polished user experience.
/// </remarks>
public static class WindowAnimationHelper
{
    /// <summary>
    /// Animates a window opening with a fade-in effect.
    /// </summary>
    /// <param name="window">The window to animate.</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    public static void AnimateWindowOpen(Window window, double durationMs = 300)
    {
        // Windows don't support RenderTransform, so we only animate opacity
        window.Opacity = 0;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        window.BeginAnimation(Window.OpacityProperty, fadeIn);
    }

    /// <summary>
    /// Animates a window closing with a fade-out effect.
    /// </summary>
    /// <param name="window">The window to animate.</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    /// <returns>A task that completes when the animation finishes.</returns>
    public static async Task AnimateWindowCloseAsync(Window window, double durationMs = 200)
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        var scaleX = new DoubleAnimation
        {
            From = 1,
            To = 0.95,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        var scaleY = new DoubleAnimation
        {
            From = 1,
            To = 0.95,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        window.BeginAnimation(Window.OpacityProperty, fadeOut);

        if (window.RenderTransform is System.Windows.Media.ScaleTransform scaleTransform)
        {
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
        }

        await Task.Delay((int)durationMs);
    }

    /// <summary>
    /// Enables automatic open/close animations for a window.
    /// </summary>
    /// <param name="window">The window to enable animations on.</param>
    /// <remarks>
    /// Call this method once during window initialization to attach
    /// animation handlers to the Loaded and Closing events.
    /// </remarks>
    public static void EnableWindowAnimations(Window window)
    {
        window.Loaded += (s, e) => AnimateWindowOpen(window);

        window.Closing += async (s, e) =>
        {
            if (!e.Cancel)
            {
                e.Cancel = true;
                await AnimateWindowCloseAsync(window);
                window.Closing -= null; // Prevent recursion
                window.Close();
            }
        };
    }
}
