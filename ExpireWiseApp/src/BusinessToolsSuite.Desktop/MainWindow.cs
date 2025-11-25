using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using BusinessToolsSuite.Features.ExpireWise.ViewModels;
using BusinessToolsSuite.Features.ExpireWise.Views;

namespace BusinessToolsSuite.Desktop;

public class MainWindow : Window
{
    public MainWindow()
    {
        // Get ViewModel from DI container
        var viewModel = Program.AppHost?.Services.GetRequiredService<ExpireWiseViewModel>();

        var view = new ExpireWiseView
        {
            DataContext = viewModel
        };

        this.Content = view;
        this.Title = "ExpireWise";
        this.Width = 1200;
        this.Height = 800;
    }
}
