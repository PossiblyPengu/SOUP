using System.Windows;
using BusinessToolsSuite.WPF.ViewModels;
using BusinessToolsSuite.WPF.Helpers;

namespace BusinessToolsSuite.WPF.Windows;

public partial class EssentialsBuddyWindow : Window
{
    public EssentialsBuddyWindow(EssentialsBuddyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Enable smooth window opening animation
        Loaded += (s, e) => WindowAnimationHelper.AnimateWindowOpen(this);
    }
}
