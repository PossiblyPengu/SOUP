using Avalonia.Controls;

namespace BusinessToolsSuite.Desktop;

public class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            var view = new BusinessToolsSuite.Features.ExpireWise.Views.ExpireWiseView();
            this.Content = view;
            this.Title = "ExpireWise";
            this.Width = 1200;
            this.Height = 800;
        }
        catch
        {
            this.Title = "ExpireWise (no view)";
        }
    }
}
