using Microsoft.UI.Xaml;

namespace BusinessToolsSuite.WinUI3;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);
    }
}
