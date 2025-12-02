using System.Windows;
using SAP.ViewModels;
using SAP.Helpers;

namespace SAP.Windows;

public partial class ExpireWiseWindow : Window
{
    public ExpireWiseWindow(ExpireWiseViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Enable smooth window opening animation
        Loaded += (s, e) => WindowAnimationHelper.AnimateWindowOpen(this);
    }
}
