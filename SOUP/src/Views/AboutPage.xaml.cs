using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SOUP.Core;
using SOUP.Services;
using SOUP.Windows;
using Serilog;

namespace SOUP.Views;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        LoadVersionInfo();
        LoadModuleInfo();
        LoadChangelog();
    }

    private void LoadVersionInfo()
    {
        try
        {
            RuntimeText.Text = $".NET {Environment.Version.Major}.{Environment.Version.Minor}";
            BuildDateText.Text = AppVersion.BuildDate ?? AppVersion.DisplayVersion;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load version info");
        }
    }

    private void LoadModuleInfo()
    {
        try
        {
            var config = ModuleConfiguration.Instance;
            AddModuleEntry("AllocationBuddy", "📊", config.AllocationBuddyEnabled, "#6366f1");
            AddModuleEntry("EssentialsBuddy", "✅", config.EssentialsBuddyEnabled, "#f59e0b");
            AddModuleEntry("ExpireWise", "📅", config.ExpireWiseEnabled, "#10b981");
            AddModuleEntry("OrderLog", "📋", config.OrderLogEnabled, "#3b82f6");
            AddModuleEntry("SwiftLabel", "🏷️", config.SwiftLabelEnabled, "#ec4899");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load module info");
        }
    }

    private void LoadChangelog()
    {
        try
        {
            var latest = AppVersion.LatestChanges;
            if (latest != null && latest.Changes != null)
            {
                ChangelogVersionText.Text = $"v{latest.Version}";
                ChangelogTitleText.Text = latest.Title;
                ChangelogItems.ItemsSource = latest.Changes;
            }
            else
            {
                ChangelogVersionText.Text = AppVersion.DisplayVersion;
                ChangelogTitleText.Text = "Latest Release";
                ChangelogItems.ItemsSource = new[] { "No recent changes available." };
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load changelog");
        }
    }

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

        var iconText = new TextBlock { Text = icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        Grid.SetColumn(iconText, 0);

        var nameText = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = (Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nameText, 1);

        var statusBorder = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3) };
        statusBorder.Background = isEnabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor)) { Opacity = 0.2 } : (Brush)FindResource("SurfaceActiveBrush");

        var statusText = new TextBlock { Text = isEnabled ? "Enabled" : "Disabled", FontSize = 10, FontWeight = FontWeights.SemiBold };
        statusText.Foreground = isEnabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor)) : (Brush)FindResource("TextTertiaryBrush");

        statusBorder.Child = statusText;
        Grid.SetColumn(statusBorder, 2);

        grid.Children.Add(iconText);
        grid.Children.Add(nameText);
        grid.Children.Add(statusBorder);

        border.Child = grid;
        ModulesPanel.Children.Add(border);
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "⏳ Checking...";

        try
        {
            using var updateService = new UpdateService();
            var updateInfo = await updateService.CheckForUpdatesAsync();

            if (updateInfo != null)
            {
                var shouldUpdate = MessageDialog.Show(
                    Window.GetWindow(this),
                    $"A new version is available!\n\nCurrent: v{updateService.CurrentVersion}\nLatest: v{updateInfo.Version}\n\n{updateInfo.ReleaseNotes}\n\nWould you like to download and install it now?",
                    "Update Available",
                    DialogType.Information,
                    DialogButtons.YesNo);

                if (shouldUpdate)
                {
                    // Initiate update flow similar to AboutWindow
                    CheckUpdateButton.Content = "Downloading...";
                }
            }
            else
            {
                MessageDialog.ShowInfo(Window.GetWindow(this), $"You're running the latest version (v{updateService.CurrentVersion}).", "Update Check");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates");
            MessageDialog.ShowWarning(Window.GetWindow(this), "Failed to check for updates. Please try again later.", "Error");
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
            CheckUpdateButton.Content = "🔄 Check for Updates";
        }
    }

}
