using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SOUP.Behaviors;

/// <summary>
/// Attached behavior that provides animated content transitions for ContentControl elements.
/// </summary>
/// <remarks>
/// <para>
/// When enabled, this behavior animates the content with a fade-in and slide-up effect
/// when the control is loaded. The animation uses easing for a smooth, natural feel.
/// </para>
/// <para>
/// Usage in XAML:
/// <code>&lt;ContentControl behaviors:ContentTransitionBehavior.EnableTransition="True" /&gt;</code>
/// </para>
/// </remarks>
public static class ContentTransitionBehavior
{
    /// <summary>
    /// Identifies the EnableTransition attached property.
    /// </summary>
    public static readonly DependencyProperty EnableTransitionProperty =
        DependencyProperty.RegisterAttached(
            "EnableTransition",
            typeof(bool),
            typeof(ContentTransitionBehavior),
            new PropertyMetadata(false, OnEnableTransitionChanged));

    /// <summary>
    /// Gets the value of the EnableTransition attached property.
    /// </summary>
    /// <param name="obj">The dependency object to get the value from.</param>
    /// <returns><c>true</c> if transitions are enabled; otherwise, <c>false</c>.</returns>
    public static bool GetEnableTransition(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableTransitionProperty);
    }

    /// <summary>
    /// Sets the value of the EnableTransition attached property.
    /// </summary>
    /// <param name="obj">The dependency object to set the value on.</param>
    /// <param name="value"><c>true</c> to enable transitions; otherwise, <c>false</c>.</param>
    public static void SetEnableTransition(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableTransitionProperty, value);
    }

    /// <summary>
    /// Called when the EnableTransition property changes.
    /// </summary>
    private static void OnEnableTransitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ContentControl contentControl && (bool)e.NewValue)
        {
            contentControl.Loaded += OnContentControlLoaded;
        }
    }

    /// <summary>
    /// Handles the Loaded event to trigger the transition animation.
    /// </summary>
    private static void OnContentControlLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentControl contentControl)
        {
            contentControl.Loaded -= OnContentControlLoaded;

            // Animate fade in and slide up
            contentControl.Opacity = 0;
            contentControl.RenderTransform = new System.Windows.Media.TranslateTransform(0, 20);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var slideUp = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            contentControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((System.Windows.Media.TranslateTransform)contentControl.RenderTransform)
                .BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);
        }
    }
}
