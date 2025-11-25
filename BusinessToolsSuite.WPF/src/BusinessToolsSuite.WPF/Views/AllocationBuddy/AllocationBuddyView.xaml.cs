using System.Windows.Controls;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views.AllocationBuddy;

public partial class AllocationBuddyView : UserControl
{
    public AllocationBuddyView()
    {
        InitializeComponent();
    }

    public AllocationBuddyView(AllocationBuddyViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
