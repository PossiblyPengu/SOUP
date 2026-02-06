using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using MechaRogue.ViewModels;

namespace MechaRogue;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is BattleViewModel vm && sender is ComboBox combo)
        {
            vm.AutoBattleSpeed = combo.SelectedIndex switch
            {
                0 => 800,  // Slow
                1 => 500,  // Normal  
                2 => 200,  // Fast
                3 => 50,   // Turbo
                _ => 500
            };
        }
    }
}