using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace BusinessToolsSuite.WPF.Behaviors;

/// <summary>
/// Attached behavior to animate content transitions
/// </summary>
public static class ContentTransitionBehavior
{
    public static readonly DependencyProperty EnableTransitionProperty =
        DependencyProperty.RegisterAttached(
            "EnableTransition",
            typeof(bool),
            typeof(ContentTransitionBehavior),
            new PropertyMetadata(false, OnEnableTransitionChanged));

    public static bool GetEnableTransition(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableTransitionProperty);
    }

    public static void SetEnableTransition(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableTransitionProperty, value);
    }

    private static void OnEnableTransitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ContentControl contentControl && (bool)e.NewValue)
        {
            contentControl.Loaded += OnContentControlLoaded;
        }
    }

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
