using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MechaRogue.Services;

/// <summary>
/// Provides visual animations for battle effects.
/// </summary>
public static class AnimationService
{
    /// <summary>Shakes an element horizontally (for damage taken).</summary>
    public static void ShakeHorizontal(FrameworkElement element, double intensity = 10, double durationMs = 300)
    {
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        
        // Rapid back-and-forth shake
        var steps = 6;
        var stepDuration = durationMs / steps;
        for (int i = 0; i < steps; i++)
        {
            var offset = (i % 2 == 0) ? intensity : -intensity;
            offset *= (1.0 - (double)i / steps); // Decay over time
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(offset, TimeSpan.FromMilliseconds(stepDuration * i)));
        }
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(durationMs)));
        
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
    
    /// <summary>Flashes an element red (for taking damage).</summary>
    public static void FlashDamage(FrameworkElement element, double durationMs = 200)
    {
        if (element is not Panel panel) return;
        
        var originalBrush = panel.Background;
        var flashBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
        
        var animation = new ColorAnimation
        {
            From = Color.FromRgb(255, 80, 80),
            To = Colors.Transparent,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        animation.Completed += (s, e) => panel.Background = originalBrush;
        
        panel.Background = flashBrush;
        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }
    
    /// <summary>Flashes an element gold (for critical hits).</summary>
    public static void FlashCritical(FrameworkElement element, double durationMs = 400)
    {
        if (element is not Panel panel) return;
        
        var originalBrush = panel.Background;
        var flashBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
        
        var animation = new ColorAnimation
        {
            From = Color.FromRgb(255, 215, 0),
            To = Colors.Transparent,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        animation.Completed += (s, e) => panel.Background = originalBrush;
        
        panel.Background = flashBrush;
        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }
    
    /// <summary>Plays an attack lunge animation toward target.</summary>
    public static void AttackLunge(FrameworkElement attacker, bool lungeRight = true, double durationMs = 200)
    {
        var transform = new TranslateTransform();
        attacker.RenderTransform = transform;
        
        var direction = lungeRight ? 1 : -1;
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(direction * 20, TimeSpan.FromMilliseconds(durationMs * 0.3),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(durationMs),
            new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
    
    /// <summary>Plays a bounce animation (for selection or victory).</summary>
    public static void Bounce(FrameworkElement element, double durationMs = 400)
    {
        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;
        
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-15, TimeSpan.FromMilliseconds(durationMs * 0.25),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(durationMs * 0.5),
            new BounceEase { Bounces = 2, Bounciness = 3 }));
        
        transform.BeginAnimation(TranslateTransform.YProperty, animation);
    }
    
    /// <summary>Fades and shrinks element (for destruction).</summary>
    public static void Destroy(FrameworkElement element, double durationMs = 500)
    {
        var scaleTransform = new ScaleTransform(1, 1);
        var translateTransform = new TranslateTransform();
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(translateTransform);
        element.RenderTransform = transformGroup;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        
        var scaleAnim = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        
        var rotateAnim = new DoubleAnimation
        {
            From = 0,
            To = -20,
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        
        var fadeAnim = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
    }
    
    /// <summary>Pulses element scale (for charging/power up).</summary>
    public static void Pulse(FrameworkElement element, double durationMs = 600, int pulseCount = 2)
    {
        var transform = new ScaleTransform(1, 1);
        element.RenderTransform = transform;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 1.15,
            Duration = TimeSpan.FromMilliseconds(durationMs / (pulseCount * 2)),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(pulseCount)
        };
        
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
    
    /// <summary>Flashes element with part break effect (purple flash + shake).</summary>
    public static void PartBreak(FrameworkElement element)
    {
        ShakeHorizontal(element, 15, 400);
        
        if (element is Panel panel)
        {
            var originalBrush = panel.Background;
            var flashBrush = new SolidColorBrush(Color.FromRgb(180, 80, 220));
            
            var animation = new ColorAnimation
            {
                From = Color.FromRgb(180, 80, 220),
                To = Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            animation.Completed += (s, e) => panel.Background = originalBrush;
            
            panel.Background = flashBrush;
            flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }
    
    /// <summary>Victory celebration animation.</summary>
    public static void Victory(FrameworkElement element)
    {
        Pulse(element, 800, 3);
    }
    
    /// <summary>Slide in from side animation.</summary>
    public static void SlideIn(FrameworkElement element, bool fromLeft = true, double durationMs = 300)
    {
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        
        var startX = fromLeft ? -100 : 100;
        var animation = new DoubleAnimation
        {
            From = startX,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        var fadeAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
        element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
    }
}
