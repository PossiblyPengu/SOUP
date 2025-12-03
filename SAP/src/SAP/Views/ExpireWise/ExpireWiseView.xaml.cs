using System.Windows;
using System.Windows.Controls;
using SAP.ViewModels;

namespace SAP.Views.ExpireWise;

public partial class ExpireWiseView : UserControl
{
    public ExpireWiseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpireWiseViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
