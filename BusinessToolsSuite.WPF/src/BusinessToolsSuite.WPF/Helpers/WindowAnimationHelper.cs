using System.Windows;
using System.Windows.Media.Animation;

namespace BusinessToolsSuite.WPF.Helpers;

public static class WindowAnimationHelper
{
    /// <summary>
    /// Animates a window opening with fade-in and scale effect
    /// </summary>
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
    /// Animates a window closing with fade-out and scale effect
    /// </summary>
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
    /// Attaches automatic window animations to a window
    /// </summary>
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
