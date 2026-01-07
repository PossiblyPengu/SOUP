using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;
using SOUP.Core;
using SOUP.Services;

namespace SOUP.Windows;

/// <summary>
/// About dialog showing application version, build info, installed modules, and changelog.
/// </summary>
public partial class AboutWindow : Window
{
    private int _iconClickCount = 0;
    private DateTime _lastIconClick = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutWindow"/> class.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        LoadVersionInfo();
        LoadModuleInfo();
        LoadChangelog();
    }

    /// <summary>
    /// Loads version and build information.
    /// </summary>
    private void LoadVersionInfo()
    {
        try
        {
            // .NET Runtime version
            RuntimeText.Text = $".NET {Environment.Version.Major}.{Environment.Version.Minor}";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load version info");
        }
    }

    /// <summary>
    /// Loads installed module information.
    /// </summary>
    private void LoadModuleInfo()
    {
        var config = ModuleConfiguration.Instance;
        
        AddModuleEntry("AllocationBuddy", "📊", config.AllocationBuddyEnabled, "#6366f1");
        AddModuleEntry("EssentialsBuddy", "✅", config.EssentialsBuddyEnabled, "#f59e0b");
        AddModuleEntry("ExpireWise", "📅", config.ExpireWiseEnabled, "#10b981");
        AddModuleEntry("OrderLog", "📋", config.OrderLogEnabled, "#3b82f6");
        AddModuleEntry("SwiftLabel", "🏷️", config.SwiftLabelEnabled, "#ec4899");
    }

    /// <summary>
    /// Loads the latest changelog entry.
    /// </summary>
    private void LoadChangelog()
    {
        try
        {
            var latest = AppVersion.LatestChanges;
            ChangelogVersionText.Text = $"v{latest.Version}";
            ChangelogTitleText.Text = latest.Title;
            ChangelogItems.ItemsSource = latest.Changes;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load changelog");
        }
    }

    /// <summary>
    /// Adds a module entry to the modules panel.
    /// </summary>
    private void AddModuleEntry(string name, string icon, bool isEnabled, string accentColor)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceHoverBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 2, 0, 2),
            Opacity = isEnabled ? 1.0 : 0.5
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(iconText, 0);

        var nameText = new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameText, 1);

        var statusBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Background = isEnabled 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor)!) { Opacity = 0.2 }
                : (Brush)FindResource("SurfaceActiveBrush")
        };

        var statusText = new TextBlock
        {
            Text = isEnabled ? "Enabled" : "Disabled",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = isEnabled 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor)!)
                : (Brush)FindResource("TextTertiaryBrush")
        };

        statusBorder.Child = statusText;
        Grid.SetColumn(statusBorder, 2);

        grid.Children.Add(iconText);
        grid.Children.Add(nameText);
        grid.Children.Add(statusBorder);

        border.Child = grid;
        ModulesPanel.Children.Add(border);
    }

    /// <summary>
    /// Handles title bar dragging.
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <summary>
    /// Handles clicks on the app icon for doom easter egg activation.
    /// </summary>
    private void AppIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Check if Fun Stuff is enabled
        if (!SOUP.Services.ModuleConfiguration.Instance.FunStuffEnabled)
            return;
            
        var now = DateTime.Now;

        if ((now - _lastIconClick).TotalSeconds > 2)
            _iconClickCount = 0;

        _lastIconClick = now;
        _iconClickCount++;

        if (_iconClickCount >= 5)
        {
            _iconClickCount = 0;
            var dungeon = new DungeonCrawler { Owner = this };
            dungeon.ShowDialog();
        }
    }

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
