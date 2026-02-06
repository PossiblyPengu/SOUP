using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MechaRogue.Controls;
using MechaRogue.ViewModels;

namespace MechaRogue.Controls;

/// <summary>
/// A visual battlefield control that displays mechs in positions and animates combat.
/// </summary>
public class BattlefieldControl : Canvas
{
    private readonly Dictionary<MechViewModel, BattlefieldMech> _mechVisuals = [];
    
    // Battlefield layout constants
    private const double FieldWidth = 600;
    private const double FieldHeight = 300;
    private const double MechSize = 64;
    private const double PlayerStartX = 80;
    private const double EnemyStartX = 520;
    private const double RowSpacing = 90;
    private const double TopMargin = 30;
    
    public static readonly DependencyProperty PlayerSquadProperty = DependencyProperty.Register(
        nameof(PlayerSquad), typeof(IEnumerable<MechViewModel>), typeof(BattlefieldControl),
        new PropertyMetadata(null, OnSquadChanged));
    
    public static readonly DependencyProperty EnemySquadProperty = DependencyProperty.Register(
        nameof(EnemySquad), typeof(IEnumerable<MechViewModel>), typeof(BattlefieldControl),
        new PropertyMetadata(null, OnSquadChanged));
    
    public static readonly DependencyProperty SelectedPlayerMechProperty = DependencyProperty.Register(
        nameof(SelectedPlayerMech), typeof(MechViewModel), typeof(BattlefieldControl),
        new PropertyMetadata(null, OnSelectionChanged));
    
    public static readonly DependencyProperty SelectedTargetMechProperty = DependencyProperty.Register(
        nameof(SelectedTargetMech), typeof(MechViewModel), typeof(BattlefieldControl),
        new PropertyMetadata(null, OnSelectionChanged));
    
    public IEnumerable<MechViewModel>? PlayerSquad
    {
        get => (IEnumerable<MechViewModel>?)GetValue(PlayerSquadProperty);
        set => SetValue(PlayerSquadProperty, value);
    }
    
    public IEnumerable<MechViewModel>? EnemySquad
    {
        get => (IEnumerable<MechViewModel>?)GetValue(EnemySquadProperty);
        set => SetValue(EnemySquadProperty, value);
    }
    
    public MechViewModel? SelectedPlayerMech
    {
        get => (MechViewModel?)GetValue(SelectedPlayerMechProperty);
        set => SetValue(SelectedPlayerMechProperty, value);
    }
    
    public MechViewModel? SelectedTargetMech
    {
        get => (MechViewModel?)GetValue(SelectedTargetMechProperty);
        set => SetValue(SelectedTargetMechProperty, value);
    }
    
    public BattlefieldControl()
    {
        Width = FieldWidth;
        Height = FieldHeight;
        ClipToBounds = true;
        
        // Draw battlefield background
        DrawBattlefield();
    }
    
    private void DrawBattlefield()
    {
        // Ground
        var ground = new Rectangle
        {
            Width = FieldWidth,
            Height = FieldHeight,
            Fill = new LinearGradientBrush(
                Color.FromRgb(20, 30, 50),
                Color.FromRgb(30, 45, 70),
                90)
        };
        Children.Add(ground);
        
        // Grid lines for arena feel
        var gridBrush = new SolidColorBrush(Color.FromArgb(30, 100, 150, 200));
        for (int i = 0; i <= 6; i++)
        {
            var vLine = new Line
            {
                X1 = i * 100, Y1 = 0,
                X2 = i * 100, Y2 = FieldHeight,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            Children.Add(vLine);
        }
        for (int i = 0; i <= 3; i++)
        {
            var hLine = new Line
            {
                X1 = 0, Y1 = i * 100,
                X2 = FieldWidth, Y2 = i * 100,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            Children.Add(hLine);
        }
        
        // Center line
        var centerLine = new Line
        {
            X1 = FieldWidth / 2, Y1 = 0,
            X2 = FieldWidth / 2, Y2 = FieldHeight,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 200, 100)),
            StrokeThickness = 2,
            StrokeDashArray = [5, 5]
        };
        Children.Add(centerLine);
        
        // Player zone indicator
        var playerZone = new Rectangle
        {
            Width = 150,
            Height = FieldHeight - 20,
            Fill = new SolidColorBrush(Color.FromArgb(20, 100, 200, 100)),
            RadiusX = 10,
            RadiusY = 10
        };
        SetLeft(playerZone, 10);
        SetTop(playerZone, 10);
        Children.Add(playerZone);
        
        // Enemy zone indicator  
        var enemyZone = new Rectangle
        {
            Width = 150,
            Height = FieldHeight - 20,
            Fill = new SolidColorBrush(Color.FromArgb(20, 200, 100, 100)),
            RadiusX = 10,
            RadiusY = 10
        };
        SetLeft(enemyZone, FieldWidth - 160);
        SetTop(enemyZone, 10);
        Children.Add(enemyZone);
    }
    
    private static void OnSquadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BattlefieldControl ctrl)
        {
            ctrl.RefreshMechs();
        }
    }
    
    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BattlefieldControl ctrl)
        {
            ctrl.UpdateSelections();
        }
    }
    
    public void RefreshMechs()
    {
        // Remove old mech visuals
        foreach (var visual in _mechVisuals.Values)
        {
            Children.Remove(visual);
        }
        _mechVisuals.Clear();
        
        // Add player mechs
        int playerIndex = 0;
        if (PlayerSquad != null)
        {
            foreach (var mech in PlayerSquad)
            {
                var visual = CreateMechVisual(mech, false, playerIndex);
                _mechVisuals[mech] = visual;
                Children.Add(visual);
                
                var pos = GetPlayerPosition(playerIndex);
                SetLeft(visual, pos.X);
                SetTop(visual, pos.Y);
                visual.HomeX = pos.X;
                visual.HomeY = pos.Y;
                
                playerIndex++;
            }
        }
        
        // Add enemy mechs
        int enemyIndex = 0;
        if (EnemySquad != null)
        {
            foreach (var mech in EnemySquad)
            {
                var visual = CreateMechVisual(mech, true, enemyIndex);
                _mechVisuals[mech] = visual;
                Children.Add(visual);
                
                var pos = GetEnemyPosition(enemyIndex);
                SetLeft(visual, pos.X);
                SetTop(visual, pos.Y);
                visual.HomeX = pos.X;
                visual.HomeY = pos.Y;
                
                enemyIndex++;
            }
        }
        
        UpdateSelections();
    }
    
    private Point GetPlayerPosition(int index) => new(PlayerStartX, TopMargin + index * RowSpacing);
    private Point GetEnemyPosition(int index) => new(EnemyStartX, TopMargin + index * RowSpacing);
    
    private BattlefieldMech CreateMechVisual(MechViewModel mech, bool isEnemy, int index)
    {
        var visual = new BattlefieldMech(mech, isEnemy);
        visual.MechClicked += OnMechClicked;
        return visual;
    }
    
    private void OnMechClicked(object? sender, MechViewModel mech)
    {
        // Raise command through routed event or callback
        MechSelected?.Invoke(this, mech);
    }
    
    public event EventHandler<MechViewModel>? MechSelected;
    
    private void UpdateSelections()
    {
        foreach (var (mech, visual) in _mechVisuals)
        {
            visual.IsSelected = mech == SelectedPlayerMech || mech == SelectedTargetMech;
            visual.IsTargeted = mech == SelectedTargetMech;
        }
    }
    
    /// <summary>Animates an attack from attacker to target - mechs run to center to fight.</summary>
    public void AnimateAttack(MechViewModel attacker, MechViewModel target, bool isCritical, bool partDestroyed)
    {
        if (!_mechVisuals.TryGetValue(attacker, out var attackerVisual)) return;
        if (!_mechVisuals.TryGetValue(target, out var targetVisual)) return;
        
        // Calculate center battlefield position
        var centerX = FieldWidth / 2;
        var centerY = FieldHeight / 2;
        
        // Offset so they meet in center but not overlap
        var attackerCenterX = attacker.IsEnemy ? centerX + 30 : centerX - 30;
        var targetCenterX = target.IsEnemy ? centerX + 30 : centerX - 30;
        
        var runDuration = TimeSpan.FromMilliseconds(300);
        var attackDelay = TimeSpan.FromMilliseconds(100);
        var returnDuration = TimeSpan.FromMilliseconds(250);
        
        // Add running bobbing effect
        attackerVisual.StartRunning();
        targetVisual.StartRunning();
        
        // PHASE 1: Both mechs run to center
        var attackerMoveX = new DoubleAnimation(attackerVisual.HomeX, attackerCenterX, runDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var attackerMoveY = new DoubleAnimation(attackerVisual.HomeY, centerY, runDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        var targetMoveX = new DoubleAnimation(targetVisual.HomeX, targetCenterX, runDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var targetMoveY = new DoubleAnimation(targetVisual.HomeY, centerY, runDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        // PHASE 2: Attack happens at center
        attackerMoveX.Completed += async (s, e) =>
        {
            attackerVisual.StopRunning();
            targetVisual.StopRunning();
            
            await Task.Delay(attackDelay);
            
            // Quick lunge toward each other
            var lungeDistance = 20;
            var lungeX = attacker.IsEnemy ? -lungeDistance : lungeDistance;
            
            var attackLunge = new DoubleAnimation(attackerCenterX, attackerCenterX + lungeX, TimeSpan.FromMilliseconds(80))
            {
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            attackLunge.Completed += async (s2, e2) =>
            {
                // Hit effect on target
                targetVisual.PlayHitEffect(isCritical, partDestroyed);
                
                await Task.Delay(150);
                
                // Start running back
                attackerVisual.StartRunning();
                targetVisual.StartRunning();
                
                // PHASE 3: Both return to home positions
                var attackerReturnX = new DoubleAnimation(attackerCenterX + lungeX, attackerVisual.HomeX, returnDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                var attackerReturnY = new DoubleAnimation(centerY, attackerVisual.HomeY, returnDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                
                var targetReturnX = new DoubleAnimation(targetCenterX, targetVisual.HomeX, returnDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                var targetReturnY = new DoubleAnimation(centerY, targetVisual.HomeY, returnDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                
                attackerReturnX.Completed += (s3, e3) =>
                {
                    attackerVisual.StopRunning();
                    targetVisual.StopRunning();
                };
                
                attackerVisual.BeginAnimation(LeftProperty, attackerReturnX);
                attackerVisual.BeginAnimation(TopProperty, attackerReturnY);
                targetVisual.BeginAnimation(LeftProperty, targetReturnX);
                targetVisual.BeginAnimation(TopProperty, targetReturnY);
            };
            
            attackerVisual.BeginAnimation(LeftProperty, attackLunge);
        };
        
        // Start the animation
        attackerVisual.BeginAnimation(LeftProperty, attackerMoveX);
        attackerVisual.BeginAnimation(TopProperty, attackerMoveY);
        targetVisual.BeginAnimation(LeftProperty, targetMoveX);
        targetVisual.BeginAnimation(TopProperty, targetMoveY);
    }
    
    /// <summary>Plays idle bounce animation on all operational mechs.</summary>
    public void PlayIdleAnimations()
    {
        foreach (var (mech, visual) in _mechVisuals)
        {
            if (mech.IsOperational)
            {
                visual.PlayIdleBounce();
            }
        }
    }
}

/// <summary>
/// Visual representation of a mech on the battlefield.
/// </summary>
public class BattlefieldMech : Border
{
    private readonly MechViewModel _mech;
    private readonly bool _isEnemy;
    private readonly PixelSpriteControl _sprite;
    private readonly Border _selectionRing;
    private readonly TextBlock _nameLabel;
    private readonly Rectangle _hpBar;
    private readonly Rectangle _hpBarBg;
    
    public double HomeX { get; set; }
    public double HomeY { get; set; }
    
    public event EventHandler<MechViewModel>? MechClicked;
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            _selectionRing.BorderBrush = value 
                ? new SolidColorBrush(_isEnemy ? Color.FromRgb(255, 100, 100) : Color.FromRgb(100, 255, 100))
                : Brushes.Transparent;
            _selectionRing.BorderThickness = value ? new Thickness(3) : new Thickness(0);
        }
    }
    
    private bool _isTargeted;
    public bool IsTargeted
    {
        get => _isTargeted;
        set
        {
            _isTargeted = value;
            if (value && _isEnemy)
            {
                _selectionRing.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 200, 50));
            }
        }
    }
    
    public BattlefieldMech(MechViewModel mech, bool isEnemy)
    {
        _mech = mech;
        _isEnemy = isEnemy;
        
        Width = 80;
        Height = 90;
        Background = Brushes.Transparent;
        Cursor = System.Windows.Input.Cursors.Hand;
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        // Selection ring
        _selectionRing = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(-4)
        };
        
        // Sprite container
        var spriteContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        _sprite = new PixelSpriteControl
        {
            Sprite = mech.Sprite,
            PixelScale = 3.5,
            IsFlipped = isEnemy,
            IsDimmed = !mech.IsOperational
        };
        spriteContainer.Child = _sprite;
        
        var spriteWithRing = new Grid();
        spriteWithRing.Children.Add(_selectionRing);
        spriteWithRing.Children.Add(spriteContainer);
        Grid.SetRow(spriteWithRing, 0);
        grid.Children.Add(spriteWithRing);
        
        // Name label
        _nameLabel = new TextBlock
        {
            Text = mech.Name,
            Foreground = Brushes.White,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 75
        };
        Grid.SetRow(_nameLabel, 1);
        grid.Children.Add(_nameLabel);
        
        // HP bar
        var hpGrid = new Grid { Height = 6, Width = 60, Margin = new Thickness(0, 2, 0, 0) };
        _hpBarBg = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            RadiusX = 2,
            RadiusY = 2
        };
        _hpBar = new Rectangle
        {
            Fill = new SolidColorBrush(isEnemy ? Color.FromRgb(220, 80, 80) : Color.FromRgb(80, 220, 80)),
            RadiusX = 2,
            RadiusY = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = mech.IsOperational ? 60 : 0
        };
        hpGrid.Children.Add(_hpBarBg);
        hpGrid.Children.Add(_hpBar);
        Grid.SetRow(hpGrid, 2);
        grid.Children.Add(hpGrid);
        
        Child = grid;
        
        MouseLeftButtonDown += (s, e) =>
        {
            if (_mech.IsOperational)
            {
                MechClicked?.Invoke(this, _mech);
            }
        };
        
        // Subscribe to mech changes
        mech.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MechViewModel.IsOperational))
            {
                Dispatcher.Invoke(() =>
                {
                    _sprite.IsDimmed = !mech.IsOperational;
                    _hpBar.Width = mech.IsOperational ? 60 : 0;
                    Opacity = mech.IsOperational ? 1.0 : 0.5;
                });
            }
        };
    }
    
    public void PlayHitEffect(bool isCritical, bool partDestroyed)
    {
        var intensity = isCritical ? 15 : (partDestroyed ? 12 : 8);
        var flashColor = isCritical ? Color.FromRgb(255, 215, 0) 
            : (partDestroyed ? Color.FromRgb(180, 80, 220) : Color.FromRgb(255, 80, 80));
        
        // Shake
        var transform = new TranslateTransform();
        RenderTransform = transform;
        
        var shakeAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(300)
        };
        for (int i = 0; i < 6; i++)
        {
            var offset = (i % 2 == 0 ? intensity : -intensity) * (1.0 - i / 6.0);
            shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(offset, TimeSpan.FromMilliseconds(50 * i)));
        }
        shakeAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(300)));
        transform.BeginAnimation(TranslateTransform.XProperty, shakeAnim);
        
        // Flash
        var flashBorder = new Border
        {
            Background = new SolidColorBrush(flashColor),
            Opacity = 0.7,
            CornerRadius = new CornerRadius(6)
        };
        
        if (Child is Grid grid)
        {
            grid.Children.Add(flashBorder);
            
            var fadeAnim = new DoubleAnimation(0.7, 0, TimeSpan.FromMilliseconds(300));
            fadeAnim.Completed += (s, e) => grid.Children.Remove(flashBorder);
            flashBorder.BeginAnimation(OpacityProperty, fadeAnim);
        }
        
        // Show damage number
        ShowDamageNumber(isCritical ? "CRIT!" : (partDestroyed ? "BREAK!" : "HIT!"), flashColor);
    }
    
    private void ShowDamageNumber(string text, Color color)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        var transform = new TranslateTransform(0, 0);
        label.RenderTransform = transform;
        
        if (Child is Grid grid)
        {
            grid.Children.Add(label);
            
            var moveUp = new DoubleAnimation(0, -30, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(600));
            fadeOut.Completed += (s, e) => grid.Children.Remove(label);
            
            transform.BeginAnimation(TranslateTransform.YProperty, moveUp);
            label.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
    
    public void PlayIdleBounce()
    {
        var transform = RenderTransform as TranslateTransform ?? new TranslateTransform();
        RenderTransform = transform;
        
        var bounce = new DoubleAnimation
        {
            From = 0,
            To = -3,
            Duration = TimeSpan.FromMilliseconds(500),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        
        // Stagger the animation start
        bounce.BeginTime = TimeSpan.FromMilliseconds(new Random().Next(0, 500));
        transform.BeginAnimation(TranslateTransform.YProperty, bounce);
    }
    
    private ScaleTransform? _scaleTransform;
    
    public void StartRunning()
    {
        // Stop idle animation
        var transform = RenderTransform as TranslateTransform ?? new TranslateTransform();
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        
        // Add scale transform for squash/stretch running effect
        _scaleTransform = new ScaleTransform(1, 1);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_scaleTransform);
        transformGroup.Children.Add(transform);
        RenderTransform = transformGroup;
        RenderTransformOrigin = new Point(0.5, 1.0); // Bottom center
        
        // Fast bobbing for running
        var runBob = new DoubleAnimation
        {
            From = 1.0,
            To = 0.85,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        
        var runSquash = new DoubleAnimation
        {
            From = 1.0,
            To = 1.1,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, runBob);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, runSquash);
    }
    
    public void StopRunning()
    {
        if (_scaleTransform != null)
        {
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
        }
        
        // Reset to simple transform
        var transform = new TranslateTransform();
        RenderTransform = transform;
    }
}
