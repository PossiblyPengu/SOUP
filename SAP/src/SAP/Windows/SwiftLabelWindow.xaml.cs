using System.Windows;
using SAP.ViewModels;
using SAP.Helpers;

namespace SAP.Windows;

public partial class SwiftLabelWindow : Window
{
    public SwiftLabelWindow(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Enable smooth window opening animation
        Loaded += (s, e) => WindowAnimationHelper.AnimateWindowOpen(this);
    }
}
