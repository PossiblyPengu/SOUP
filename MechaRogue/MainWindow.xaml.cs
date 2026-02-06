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

using MechaRogue.Controls;
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
        
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Wire up battlefield events
        if (Battlefield != null)
        {
            Battlefield.MechSelected += OnBattlefieldMechSelected;
        }
        
        // Subscribe to attack events from ViewModel
        if (DataContext is BattleViewModel vm)
        {
            vm.AttackAnimationRequested += OnAttackAnimationRequested;
            vm.BattlefieldRefreshRequested += OnBattlefieldRefreshRequested;
            
            // Initial refresh
            Battlefield?.RefreshMechs();
        }
    }
    
    private void OnBattlefieldRefreshRequested(object? sender, EventArgs e)
    {
        Battlefield?.RefreshMechs();
    }
    
    private void OnBattlefieldMechSelected(object? sender, MechViewModel mech)
    {
        if (DataContext is not BattleViewModel vm) return;
        
        if (mech.IsEnemy)
        {
            vm.SelectTargetCommand.Execute(mech);
        }
        else
        {
            vm.SelectMechCommand.Execute(mech);
        }
    }
    
    private void OnAttackAnimationRequested(object? sender, AttackAnimationEventArgs e)
    {
        Battlefield?.AnimateAttack(e.Attacker, e.Target, e.IsCritical, e.PartDestroyed);
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