using System.Windows;
using System.Windows.Controls;
using MechaRogue.Services;
using MechaRogue.ViewModels;

namespace MechaRogue.Behaviors;

/// <summary>
/// Attached behavior that plays animations on mech cards based on ViewModel state.
/// </summary>
public static class MechAnimationBehavior
{
    public static readonly DependencyProperty AnimationProperty =
        DependencyProperty.RegisterAttached(
            "Animation",
            typeof(MechAnimation),
            typeof(MechAnimationBehavior),
            new PropertyMetadata(MechAnimation.None, OnAnimationChanged));
    
    public static readonly DependencyProperty IsEnemyProperty =
        DependencyProperty.RegisterAttached(
            "IsEnemy",
            typeof(bool),
            typeof(MechAnimationBehavior),
            new PropertyMetadata(false));
    
    public static MechAnimation GetAnimation(DependencyObject obj) => (MechAnimation)obj.GetValue(AnimationProperty);
    public static void SetAnimation(DependencyObject obj, MechAnimation value) => obj.SetValue(AnimationProperty, value);
    
    public static bool GetIsEnemy(DependencyObject obj) => (bool)obj.GetValue(IsEnemyProperty);
    public static void SetIsEnemy(DependencyObject obj, bool value) => obj.SetValue(IsEnemyProperty, value);
    
    private static void OnAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        
        var animation = (MechAnimation)e.NewValue;
        if (animation == MechAnimation.None) return;
        
        var isEnemy = GetIsEnemy(element);
        
        switch (animation)
        {
            case MechAnimation.Attack:
                // Lunge toward the enemy (right for player, left for enemy)
                AnimationService.AttackLunge(element, lungeRight: !isEnemy);
                break;
                
            case MechAnimation.Damage:
                AnimationService.ShakeHorizontal(element, intensity: 8);
                AnimationService.FlashDamage(element);
                break;
                
            case MechAnimation.Critical:
                AnimationService.ShakeHorizontal(element, intensity: 15);
                AnimationService.FlashCritical(element);
                break;
                
            case MechAnimation.PartBreak:
                AnimationService.PartBreak(element);
                break;
                
            case MechAnimation.Destroyed:
                AnimationService.Destroy(element);
                break;
                
            case MechAnimation.Selected:
                AnimationService.Bounce(element);
                break;
                
            case MechAnimation.Victory:
                AnimationService.Victory(element);
                break;
        }
    }
}
