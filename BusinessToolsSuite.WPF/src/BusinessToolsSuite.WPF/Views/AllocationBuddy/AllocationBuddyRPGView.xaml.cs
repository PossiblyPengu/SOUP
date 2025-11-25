using System.Windows.Controls;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views.AllocationBuddy;

public partial class AllocationBuddyRPGView : UserControl
{
    public AllocationBuddyRPGView()
    {
        InitializeComponent();
    }

    public AllocationBuddyRPGView(AllocationBuddyRPGViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
