using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace SOUP.Windows;

/// <summary>
/// Themed splash screen that displays during startup and after updates.
/// Respects dark/light mode and basic theme settings.
/// </summary>
public partial class SplashWindow : Window
{
    private Storyboard? _fadeInAnimation;
    private Storyboard? _fadeOutAnimation;
    private Storyboard? _logoAnimation;
    private Storyboard? _progressAnimation;
    private DateTime _shownAt;
    private const int MinDisplayMilliseconds = 5000; // ensure splash is visible at least this long

    public SplashWindow()
    {
        InitializeComponent();
        
        // Set version text
        VersionText.Text = $"v{Core.AppVersion.Version}";
        
        Loaded += (s, e) =>
        {
            _shownAt = DateTime.UtcNow;
            StartAnimations();
        };
    }

    private void StartAnimations()
    {
        // Get animations from resources
        _fadeInAnimation = Resources["FadeInAnimation"] as Storyboard;
        _logoAnimation = Resources["LogoScaleAnimation"] as Storyboard;
        _progressAnimation = Resources["ProgressAnimation"] as Storyboard;

        // Start fade in
        _fadeInAnimation?.Begin(this, HandoffBehavior.SnapshotAndReplace);
        
        // Start logo scale animation
        _logoAnimation?.Begin(this, HandoffBehavior.SnapshotAndReplace);
        
        // Start progress animation
        _progressAnimation?.Begin(this, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Updates the status message displayed on the splash screen.
    /// </summary>
    public void SetStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
        });
    }

    /// <summary>
    /// Closes the splash screen with fade-out animation.
    /// </summary>
    public async Task CloseAsync()
    {
        try
        {
            _progressAnimation?.Stop(this);
            _logoAnimation?.Stop(this);
            
            _fadeOutAnimation = Resources["FadeOutAnimation"] as Storyboard;
            // Ensure minimum display time so the splash is visible even on fast startups
            var elapsed = (DateTime.UtcNow - _shownAt).TotalMilliseconds;
            if (elapsed < MinDisplayMilliseconds)
            {
                await Task.Delay(MinDisplayMilliseconds - (int)elapsed);
            }

            if (_fadeOutAnimation != null)
            {
                _fadeOutAnimation.Completed += (s, e) =>
                {
                    Dispatcher.Invoke(() => Close());
                };
                
                _fadeOutAnimation.Begin(this, HandoffBehavior.SnapshotAndReplace);
                
                // Wait for animation to complete
                await Task.Delay(700);
            }
            else
            {
                Close();
            }
        }
        catch
        {
            Close();
        }
    }
}
