using Avalonia.Controls;

namespace BusinessToolsSuite.Desktop;

public class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            var view = new BusinessToolsSuite.Features.EssentialsBuddy.Views.EssentialsBuddyView();
            this.Content = view;
            this.Title = "Essentials Buddy";
            this.Width = 1200;
            this.Height = 800;
        }
        catch
        {
            this.Title = "Essentials Buddy (no view)";
        }
    }
}
