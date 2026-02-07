namespace MechaRogue.Controls;

using MechaRogue.Models;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

/// <summary>
/// Animated battlefield with GBA Medabots-style outdoor terrain.
/// Side-view RPG perspective: player mech on left, enemy on right,
/// bright sky, green grass, pixel-art clouds, hill silhouettes.
/// </summary>
public class BattlefieldCanvas : Canvas
{
    // ── Dependency Properties ─────────────────────────────
    public static readonly DependencyProperty PlayerBotProperty =
        DependencyProperty.Register(nameof(PlayerBot), typeof(Medabot), typeof(BattlefieldCanvas),
            new PropertyMetadata(null, (d, _) => ((BattlefieldCanvas)d).RedrawScene()));

    public static readonly DependencyProperty EnemyBotProperty =
        DependencyProperty.Register(nameof(EnemyBot), typeof(Medabot), typeof(BattlefieldCanvas),
            new PropertyMetadata(null, (d, _) => ((BattlefieldCanvas)d).RedrawScene()));

    public Medabot? PlayerBot
    {
        get => (Medabot?)GetValue(PlayerBotProperty);
        set => SetValue(PlayerBotProperty, value);
    }

    public Medabot? EnemyBot
    {
        get => (Medabot?)GetValue(EnemyBotProperty);
        set => SetValue(EnemyBotProperty, value);
    }

    // ── Charge Gauge Properties (0-100) ───────────────────
    public static readonly DependencyProperty PlayerChargeProperty =
        DependencyProperty.Register(nameof(PlayerChargeValue), typeof(double), typeof(BattlefieldCanvas),
            new PropertyMetadata(0.0, (d, _) => ((BattlefieldCanvas)d).UpdateChargeGauges()));

    public static readonly DependencyProperty EnemyChargeProperty =
        DependencyProperty.Register(nameof(EnemyChargeValue), typeof(double), typeof(BattlefieldCanvas),
            new PropertyMetadata(0.0, (d, _) => ((BattlefieldCanvas)d).UpdateChargeGauges()));

    public double PlayerChargeValue
    {
        get => (double)GetValue(PlayerChargeProperty);
        set => SetValue(PlayerChargeProperty, value);
    }

    public double EnemyChargeValue
    {
        get => (double)GetValue(EnemyChargeProperty);
        set => SetValue(EnemyChargeProperty, value);
    }

    // ── Gauge UI elements (updated every tick) ────────────
    private Rectangle? _playerGaugeBg, _playerGaugeFill;
    private Rectangle? _enemyGaugeBg, _enemyGaugeFill;
    private TextBlock? _playerGaugeLabel, _enemyGaugeLabel;
    private Border? _playerReadyFlash, _enemyReadyFlash;
    private const double GaugeWidth = 80;
    private const double GaugeHeight = 8;

    // ── Layout ────────────────────────────────────────────
    private const double SceneHeight = 320;
    private const double GroundRatio = 0.72;              // ground at 72% height
    private const double SpriteScale = 4.5;
    private const double SpriteW = 16 * SpriteScale;      // 72
    private const double SpriteH = 20 * SpriteScale;      // 90

    // Proportional positions (scale with container width)
    private double GroundY => SceneHeight * GroundRatio;   // ~230
    private double PlayerX => Math.Max(90, ActualWidth * 0.24);
    private double EnemyX => Math.Max(200, ActualWidth * 0.76);

    // ── Animation state ───────────────────────────────────
    private readonly DispatcherTimer _animTimer;
    private readonly List<AnimParticle> _particles = [];
    private readonly List<DamagePopup> _popups = [];
    private readonly Random _rng = new();
    private double _shakeX, _shakeY;
    private int _shakeTicks;
    private int _breatheFrame;

    // ── Cached sprite elements ────────────────────────────
    private readonly Canvas _playerSprite = new();
    private readonly Canvas _enemySprite = new();
    private readonly Canvas _effectsLayer = new();

    public BattlefieldCanvas()
    {
        Height = SceneHeight;
        ClipToBounds = true;
        Background = Brushes.Transparent;

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30fps
        _animTimer.Tick += AnimTick;

        Loaded += (_, _) =>
        {
            Children.Add(_playerSprite);
            Children.Add(_enemySprite);
            Children.Add(_effectsLayer);
            _animTimer.Start();
            RedrawScene();
        };

        Unloaded += (_, _) => _animTimer.Stop();
        SizeChanged += (_, _) => { if (IsLoaded) RedrawScene(); };
    }

    // ════════════════════════════════════════════════════════
    //  SCENE DRAWING — GBA-style outdoor terrain
    // ════════════════════════════════════════════════════════

    public void RedrawScene()
    {
        if (ActualWidth < 20) return;

        Children.Clear();
        _effectsLayer.Children.Clear();

        DrawSky();
        DrawClouds();
        DrawHills();
        DrawGround();
        DrawGroundDetails();

        // Mechs
        DrawMech(_playerSprite, PlayerBot, PlayerX - SpriteW / 2, GroundY - SpriteH, false);
        DrawMech(_enemySprite, EnemyBot, EnemyX - SpriteW / 2, GroundY - SpriteH, true);
        Children.Add(_playerSprite);
        Children.Add(_enemySprite);

        // Name plates above mechs
        DrawNamePlates();

        // Charge gauges above sprites
        DrawChargeGauges();

        // Effects layer on top of everything
        Children.Add(_effectsLayer);
    }

    private void DrawChargeGauges()
    {
        double gaugeY = GroundY - SpriteH - 52;

        // ── Player gauge ──
        _playerGaugeLabel = new TextBlock
        {
            Text = "CHARGE",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Opacity = 0.8
            }
        };
        SetLeft(_playerGaugeLabel, PlayerX - GaugeWidth / 2);
        SetTop(_playerGaugeLabel, gaugeY - 12);
        Children.Add(_playerGaugeLabel);

        _playerGaugeBg = new Rectangle
        {
            Width = GaugeWidth, Height = GaugeHeight,
            Fill = new SolidColorBrush(Color.FromArgb(0xAA, 0x10, 0x10, 0x18)),
            Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
            RadiusX = 2, RadiusY = 2
        };
        SetLeft(_playerGaugeBg, PlayerX - GaugeWidth / 2);
        SetTop(_playerGaugeBg, gaugeY);
        Children.Add(_playerGaugeBg);

        _playerGaugeFill = new Rectangle
        {
            Width = 0, Height = GaugeHeight - 2,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x40, 0x90, 0xFF), Color.FromRgb(0x20, 0xDD, 0xFF), 0),
            RadiusX = 1, RadiusY = 1
        };
        SetLeft(_playerGaugeFill, PlayerX - GaugeWidth / 2 + 1);
        SetTop(_playerGaugeFill, gaugeY + 1);
        Children.Add(_playerGaugeFill);

        _playerReadyFlash = new Border
        {
            Width = GaugeWidth + 10, Height = GaugeHeight + 10,
            CornerRadius = new CornerRadius(4),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x00, 0x40, 0xFF, 0x40)),
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent,
            Visibility = Visibility.Collapsed
        };
        SetLeft(_playerReadyFlash, PlayerX - GaugeWidth / 2 - 5);
        SetTop(_playerReadyFlash, gaugeY - 5);
        Children.Add(_playerReadyFlash);

        // ── Enemy gauge ──
        _enemyGaugeLabel = new TextBlock
        {
            Text = "CHARGE",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Opacity = 0.8
            }
        };
        SetLeft(_enemyGaugeLabel, EnemyX - GaugeWidth / 2);
        SetTop(_enemyGaugeLabel, gaugeY - 12);
        Children.Add(_enemyGaugeLabel);

        _enemyGaugeBg = new Rectangle
        {
            Width = GaugeWidth, Height = GaugeHeight,
            Fill = new SolidColorBrush(Color.FromArgb(0xAA, 0x10, 0x10, 0x18)),
            Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
            RadiusX = 2, RadiusY = 2
        };
        SetLeft(_enemyGaugeBg, EnemyX - GaugeWidth / 2);
        SetTop(_enemyGaugeBg, gaugeY);
        Children.Add(_enemyGaugeBg);

        _enemyGaugeFill = new Rectangle
        {
            Width = 0, Height = GaugeHeight - 2,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0x50, 0x40), Color.FromRgb(0xFF, 0xAA, 0x30), 0),
            RadiusX = 1, RadiusY = 1
        };
        SetLeft(_enemyGaugeFill, EnemyX - GaugeWidth / 2 + 1);
        SetTop(_enemyGaugeFill, gaugeY + 1);
        Children.Add(_enemyGaugeFill);

        _enemyReadyFlash = new Border
        {
            Width = GaugeWidth + 10, Height = GaugeHeight + 10,
            CornerRadius = new CornerRadius(4),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x00, 0xFF, 0x50, 0x40)),
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent,
            Visibility = Visibility.Collapsed
        };
        SetLeft(_enemyReadyFlash, EnemyX - GaugeWidth / 2 - 5);
        SetTop(_enemyReadyFlash, gaugeY - 5);
        Children.Add(_enemyReadyFlash);

        UpdateChargeGauges();
    }

    private void UpdateChargeGauges()
    {
        double maxFill = GaugeWidth - 2;

        if (_playerGaugeFill != null)
        {
            double pct = Math.Clamp(PlayerChargeValue / 100.0, 0, 1);
            _playerGaugeFill.Width = maxFill * pct;

            // Glow when full
            bool playerReady = PlayerChargeValue >= 100;
            if (_playerReadyFlash != null)
            {
                _playerReadyFlash.Visibility = playerReady ? Visibility.Visible : Visibility.Collapsed;
                _playerReadyFlash.BorderBrush = new SolidColorBrush(
                    Color.FromArgb((byte)(playerReady ? 0x80 + (int)(Math.Sin(_breatheFrame * 0.15) * 60 + 60) : 0x00),
                        0x40, 0xFF, 0x40));
            }
            if (_playerGaugeLabel != null)
                _playerGaugeLabel.Text = playerReady ? "READY!" : "CHARGE";
        }

        if (_enemyGaugeFill != null)
        {
            double pct = Math.Clamp(EnemyChargeValue / 100.0, 0, 1);
            _enemyGaugeFill.Width = maxFill * pct;

            bool enemyReady = EnemyChargeValue >= 100;
            if (_enemyReadyFlash != null)
            {
                _enemyReadyFlash.Visibility = enemyReady ? Visibility.Visible : Visibility.Collapsed;
                _enemyReadyFlash.BorderBrush = new SolidColorBrush(
                    Color.FromArgb((byte)(enemyReady ? 0x80 + (int)(Math.Sin(_breatheFrame * 0.15) * 60 + 60) : 0x00),
                        0xFF, 0x50, 0x40));
            }
            if (_enemyGaugeLabel != null)
                _enemyGaugeLabel.Text = enemyReady ? "READY!" : "CHARGE";
        }
    }

    private void DrawSky()
    {
        // GBA-style bright blue sky with multi-stop gradient
        var sky = new Rectangle
        {
            Width = ActualWidth,
            Height = SceneHeight,
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x28, 0x58, 0xC0), 0.0),   // deep blue top
                    new GradientStop(Color.FromRgb(0x48, 0x88, 0xE0), 0.35),  // mid blue
                    new GradientStop(Color.FromRgb(0x78, 0xB0, 0xF0), 0.65),  // light blue
                    new GradientStop(Color.FromRgb(0xA8, 0xD0, 0xFF), 0.90),  // pale horizon
                },
                new Point(0.5, 0), new Point(0.5, 1))
        };
        Children.Add(sky);
    }

    private void DrawClouds()
    {
        double w = ActualWidth;
        var cloudWhite = Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF);
        var cloudShadow = Color.FromArgb(0x40, 0x80, 0xAA, 0xDD);

        var cloudData = new (double xRatio, double y, double scale)[]
        {
            (0.10, 20, 1.1),
            (0.50, 10, 1.5),
            (0.82, 28, 0.9),
            (0.32, 52, 0.65),
        };

        foreach (var (xr, cy, scale) in cloudData)
        {
            double cx = w * xr;
            double bw = 44 * scale;
            double bh = 14 * scale;

            // Shadow
            var sh = new Rectangle
            {
                Width = bw, Height = bh,
                RadiusX = bh / 2, RadiusY = bh / 2,
                Fill = new SolidColorBrush(cloudShadow)
            };
            SetLeft(sh, cx - bw / 2 + 2);
            SetTop(sh, cy + 3);
            Children.Add(sh);

            // Main body
            var body = new Rectangle
            {
                Width = bw, Height = bh,
                RadiusX = bh / 2, RadiusY = bh / 2,
                Fill = new SolidColorBrush(cloudWhite)
            };
            SetLeft(body, cx - bw / 2);
            SetTop(body, cy);
            Children.Add(body);

            // Top bump 1
            double tw = bw * 0.50;
            double th = bh * 0.65;
            var bump1 = new Rectangle
            {
                Width = tw, Height = th,
                RadiusX = th / 2, RadiusY = th / 2,
                Fill = new SolidColorBrush(cloudWhite)
            };
            SetLeft(bump1, cx - tw / 2 - bw * 0.12);
            SetTop(bump1, cy - th * 0.40);
            Children.Add(bump1);

            // Top bump 2
            double tw2 = bw * 0.40;
            double th2 = bh * 0.55;
            var bump2 = new Rectangle
            {
                Width = tw2, Height = th2,
                RadiusX = th2 / 2, RadiusY = th2 / 2,
                Fill = new SolidColorBrush(cloudWhite)
            };
            SetLeft(bump2, cx - tw2 / 2 + bw * 0.15);
            SetTop(bump2, cy - th2 * 0.30);
            Children.Add(bump2);
        }
    }

    private void DrawHills()
    {
        double w = ActualWidth;
        double baseY = GroundY;

        // Far hills — simple ellipses in forest green
        var hillColors = new[]
        {
            Color.FromRgb(0x30, 0x70, 0x28),
            Color.FromRgb(0x28, 0x62, 0x20),
            Color.FromRgb(0x35, 0x78, 0x2C),
        };

        var hills = new (double xRatio, double wRatio, double h)[]
        {
            (0.00, 0.40, 50),
            (0.28, 0.48, 62),
            (0.62, 0.45, 45),
        };

        for (int i = 0; i < hills.Length; i++)
        {
            var (xr, wr, h) = hills[i];
            var hill = new Ellipse
            {
                Width = w * wr,
                Height = h * 2,
                Fill = new SolidColorBrush(hillColors[i])
            };
            SetLeft(hill, w * xr);
            SetTop(hill, baseY - h);
            Children.Add(hill);
        }

        // Tree line silhouettes along horizon
        var treeRng = new Random(123); // deterministic
        var treeColor = Color.FromRgb(0x20, 0x52, 0x18);

        for (double tx = 0; tx < w; tx += 10 + treeRng.Next(10))
        {
            double treeH = 6 + treeRng.Next(12);
            var tree = new Rectangle
            {
                Width = 5 + treeRng.Next(4),
                Height = treeH,
                RadiusX = 2.5, RadiusY = 2.5,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(120 + treeRng.Next(80)),
                    treeColor.R, treeColor.G, treeColor.B))
            };
            SetLeft(tree, tx);
            SetTop(tree, baseY - treeH + 3);
            Children.Add(tree);
        }
    }

    private void DrawGround()
    {
        double w = ActualWidth;
        double gndH = SceneHeight - GroundY;

        // Main grass gradient
        var grassBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(0x50, 0xB0, 0x40), 0.0),   // bright green
                new GradientStop(Color.FromRgb(0x40, 0x98, 0x30), 0.3),   // medium green
                new GradientStop(Color.FromRgb(0x34, 0x80, 0x24), 0.65),  // darker green
                new GradientStop(Color.FromRgb(0x28, 0x68, 0x18), 1.0),   // dark green bottom
            },
            new Point(0.5, 0), new Point(0.5, 1));

        var ground = new Rectangle { Width = w, Height = gndH + 2, Fill = grassBrush };
        SetLeft(ground, 0);
        SetTop(ground, GroundY);
        Children.Add(ground);

        // Horizontal stripe bands (GBA terrain banding effect)
        for (double y = GroundY + 6; y < SceneHeight; y += 16)
        {
            var stripe = new Rectangle
            {
                Width = w, Height = 6,
                Fill = new SolidColorBrush(Color.FromArgb(0x22, 0x60, 0xD0, 0x40))
            };
            SetLeft(stripe, 0);
            SetTop(stripe, y);
            Children.Add(stripe);
        }

        // Bright horizon edge line
        var edge = new Line
        {
            X1 = 0, Y1 = GroundY, X2 = w, Y2 = GroundY,
            Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0xC0, 0x50)),
            StrokeThickness = 2
        };
        Children.Add(edge);
    }

    private void DrawGroundDetails()
    {
        double w = ActualWidth;
        var detRng = new Random(42); // deterministic for consistency

        // Small flowers
        var flowerColors = new[]
        {
            Color.FromRgb(0xFF, 0xFF, 0x60),  // yellow
            Color.FromRgb(0xFF, 0x80, 0x80),  // pink
            Color.FromRgb(0xFF, 0xFF, 0xFF),  // white
            Color.FromRgb(0x80, 0xC0, 0xFF),  // light blue
        };

        for (int i = 0; i < 18; i++)
        {
            double fx = detRng.NextDouble() * w;
            double fy = GroundY + 6 + detRng.NextDouble() * (SceneHeight - GroundY - 18);
            double fs = 2 + detRng.NextDouble() * 2.5;
            var fc = flowerColors[i % flowerColors.Length];

            var flower = new Ellipse
            {
                Width = fs, Height = fs,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(140 + detRng.Next(80)), fc.R, fc.G, fc.B))
            };
            SetLeft(flower, fx);
            SetTop(flower, fy);
            Children.Add(flower);
        }

        // Small rocks
        for (int i = 0; i < 6; i++)
        {
            double rx = detRng.NextDouble() * w;
            double ry = GroundY + 4 + detRng.NextDouble() * (SceneHeight - GroundY - 12);
            double rs = 2 + detRng.NextDouble() * 3;

            var rock = new Ellipse
            {
                Width = rs * 1.6, Height = rs,
                Fill = new SolidColorBrush(Color.FromRgb(
                    (byte)(0x6A + detRng.Next(0x28)),
                    (byte)(0x7A + detRng.Next(0x28)),
                    (byte)(0x58 + detRng.Next(0x28))))
            };
            SetLeft(rock, rx);
            SetTop(rock, ry);
            Children.Add(rock);
        }

        // Grass tufts (small vertical lines)
        for (int i = 0; i < 12; i++)
        {
            double gx = detRng.NextDouble() * w;
            double gy = GroundY + 3 + detRng.NextDouble() * (SceneHeight - GroundY - 15);

            for (int j = 0; j < 3; j++)
            {
                var blade = new Line
                {
                    X1 = gx + j * 2, Y1 = gy,
                    X2 = gx + j * 2 + (detRng.NextDouble() - 0.5) * 3, Y2 = gy - 4 - detRng.NextDouble() * 4,
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)(100 + detRng.Next(60)), 0x60, 0xC8, 0x40)),
                    StrokeThickness = 1.2
                };
                Children.Add(blade);
            }
        }
    }

    private void DrawNamePlates()
    {
        // GBA-style name/status plates above each mech
        if (PlayerBot != null)
        {
            var plate = BuildNamePlate(PlayerBot.Name, PlayerBot.ModelId,
                Color.FromArgb(0xCC, 0x18, 0x40, 0x90));
            SetLeft(plate, PlayerX - 52);
            SetTop(plate, GroundY - SpriteH - 28);
            Children.Add(plate);
        }

        if (EnemyBot != null)
        {
            var plate = BuildNamePlate(EnemyBot.Name, EnemyBot.ModelId,
                Color.FromArgb(0xCC, 0x90, 0x20, 0x18));
            SetLeft(plate, EnemyX - 52);
            SetTop(plate, GroundY - SpriteH - 28);
            Children.Add(plate);
        }
    }

    private static Border BuildNamePlate(string name, string modelId, Color bgColor)
    {
        return new Border
        {
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(3),
            Child = new TextBlock
            {
                Text = $"{name} [{modelId}]",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Opacity = 0.7
                }
            }
        };
    }

    // ── Pixel Mech Drawing ────────────────────────────────
    private static readonly int[,] _template = MedabotSprite.BuildTemplate();

    // Brighter destroyed/body colors visible against outdoor background
    private static readonly Color _destroyedColor = Color.FromRgb(0x58, 0x58, 0x58);
    private static readonly Color _bodyColor = Color.FromRgb(0x68, 0x70, 0x78);

    private static readonly Dictionary<string, Color[]> _palettes = new()
    {
        ["KBT"] = [Color.FromRgb(0xD2, 0x99, 0x22), Color.FromRgb(0xB8, 0x80, 0x10), Color.FromRgb(0xE0, 0xA8, 0x30)],
        ["KWG"] = [Color.FromRgb(0x58, 0xA6, 0xFF), Color.FromRgb(0x1F, 0x6F, 0xEB), Color.FromRgb(0x79, 0xC0, 0xFF)],
        ["CAT"] = [Color.FromRgb(0xBC, 0x8C, 0xFF), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xD0, 0xAA, 0xFF)],
        ["TOT"] = [Color.FromRgb(0x3F, 0xB9, 0x50), Color.FromRgb(0x23, 0x8B, 0x2F), Color.FromRgb(0x56, 0xD3, 0x64)],
        ["NAS"] = [Color.FromRgb(0xFF, 0x7B, 0x72), Color.FromRgb(0xE0, 0x55, 0x4D), Color.FromRgb(0xFF, 0xA0, 0x98)],
        ["DOG"] = [Color.FromRgb(0x8B, 0x94, 0x9E), Color.FromRgb(0x6E, 0x76, 0x81), Color.FromRgb(0xA8, 0xB2, 0xBC)],
        ["STG"] = [Color.FromRgb(0xF8, 0x51, 0x49), Color.FromRgb(0xD0, 0x30, 0x28), Color.FromRgb(0xFF, 0x78, 0x70)],
    };

    private void DrawMech(Canvas target, Medabot? bot, double x, double y, bool flipX)
    {
        target.Children.Clear();
        if (bot == null) return;

        var prefix = bot.ModelId.Length >= 3 ? bot.ModelId[..3] : "KBT";
        var palette = _palettes.TryGetValue(prefix, out var p) ? p : [_bodyColor, _bodyColor, _bodyColor];
        double s = SpriteScale;

        SetLeft(target, x);
        SetTop(target, y);
        target.Width = 16 * s;
        target.Height = 20 * s;

        if (flipX)
        {
            target.RenderTransformOrigin = new Point(0.5, 0.5);
            target.RenderTransform = new ScaleTransform(-1, 1);
        }
        else
        {
            target.RenderTransform = Transform.Identity;
        }

        bool headDead = bot.Head.IsDestroyed;
        bool rArmDead = bot.RightArm.IsDestroyed;
        bool lArmDead = bot.LeftArm.IsDestroyed;
        bool legsDead = bot.Legs.IsDestroyed;

        // Black outline border (1px scaled border around each pixel — GBA sprite style)
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                int zone = _template[r, c];
                if (zone == 0) continue;

                // Check if this pixel is on the edge (adjacent to transparent)
                bool isEdge = false;
                if (r == 0 || c == 0 || r == 19 || c == 15)
                    isEdge = true;
                else if (_template[r - 1, c] == 0 || _template[r + 1, c] == 0 ||
                         _template[r, c - 1] == 0 || _template[r, c + 1] == 0)
                    isEdge = true;

                if (isEdge)
                {
                    var outline = new Rectangle
                    {
                        Width = s + 1, Height = s + 1,
                        Fill = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x18))
                    };
                    SetLeft(outline, c * s - 0.5);
                    SetTop(outline, r * s - 0.5);
                    target.Children.Add(outline);
                }
            }
        }

        // Fill pixels
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                int zone = _template[r, c];
                if (zone == 0) continue;

                Color clr = zone switch
                {
                    1 => headDead ? _destroyedColor : palette[0],
                    2 => _bodyColor,
                    3 => rArmDead ? _destroyedColor : palette[1],
                    4 => lArmDead ? _destroyedColor : palette[2],
                    5 => legsDead ? _destroyedColor : palette[0],
                    _ => _bodyColor
                };

                var rect = new Rectangle { Width = s, Height = s, Fill = new SolidColorBrush(clr) };
                SetLeft(rect, c * s);
                SetTop(rect, r * s);
                target.Children.Add(rect);
            }
        }

        // Shadow on grass
        var shadow = new Ellipse
        {
            Width = SpriteW * 0.85,
            Height = 10,
            Fill = new SolidColorBrush(Color.FromArgb(0x50, 0x10, 0x30, 0x10))
        };
        SetLeft(shadow, (SpriteW - SpriteW * 0.85) / 2);
        SetTop(shadow, SpriteH + 2);
        target.Children.Add(shadow);

        // KO X-eyes overlay
        if (bot.IsKnockedOut)
        {
            var overlay = new Rectangle
            {
                Width = 16 * s, Height = 20 * s,
                Fill = new SolidColorBrush(Color.FromArgb(0x88, 0, 0, 0))
            };
            target.Children.Add(overlay);
            foreach (var (x1, y1, x2, y2) in new[] { (5, 1.5, 7, 3.5), (7, 1.5, 5, 3.5), (9, 1.5, 11, 3.5), (11, 1.5, 9, 3.5) })
            {
                var ln = new Line
                {
                    X1 = x1 * s, Y1 = y1 * s, X2 = x2 * s, Y2 = y2 * s,
                    Stroke = Brushes.Red, StrokeThickness = s * 0.5
                };
                target.Children.Add(ln);
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  ANIMATION TICK (~30fps)
    // ════════════════════════════════════════════════════════

    private void AnimTick(object? sender, EventArgs e)
    {
        _breatheFrame++;

        // Idle breathing (subtle bob)
        double breatheOffset = Math.Sin(_breatheFrame * 0.08) * 2.0;
        if (PlayerBot != null)
            SetTop(_playerSprite, GroundY - SpriteH + breatheOffset);
        if (EnemyBot != null)
            SetTop(_enemySprite, GroundY - SpriteH - breatheOffset);

        // Update charge gauge visuals every frame (pulsing glow)
        UpdateChargeGauges();

        // Screen shake
        if (_shakeTicks > 0)
        {
            _shakeTicks--;
            _shakeX = (_rng.NextDouble() - 0.5) * _shakeTicks * 0.6;
            _shakeY = (_rng.NextDouble() - 0.5) * _shakeTicks * 0.4;
            RenderTransform = new TranslateTransform(_shakeX, _shakeY);
        }
        else if (_shakeX != 0 || _shakeY != 0)
        {
            _shakeX = _shakeY = 0;
            RenderTransform = Transform.Identity;
        }

        // Update particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.VX;
            p.Y += p.VY;
            p.VY += p.Gravity;
            p.Life--;
            p.Opacity = Math.Max(0, (double)p.Life / p.MaxLife);

            if (p.Life <= 0)
            {
                _effectsLayer.Children.Remove(p.Element);
                _particles.RemoveAt(i);
            }
            else
            {
                SetLeft(p.Element, p.X);
                SetTop(p.Element, p.Y);
                p.Element.Opacity = p.Opacity;
            }
        }

        // Update damage popups
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var dp = _popups[i];
            dp.Y -= 1.2;
            dp.Life--;
            dp.Element.Opacity = Math.Max(0, (double)dp.Life / dp.MaxLife);

            if (dp.Life <= 0)
            {
                _effectsLayer.Children.Remove(dp.Element);
                _popups.RemoveAt(i);
            }
            else
            {
                SetTop(dp.Element, dp.Y);
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  PUBLIC ANIMATION API
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Play a full attack animation sequence for the given action result.
    /// </summary>
    public void PlayActionResult(ActionResult result)
    {
        if (result == null) return;

        bool isPlayerAttacking = result.Attacker.IsPlayerOwned;
        double fromX = isPlayerAttacking ? PlayerX : EnemyX;
        double fromY = GroundY - SpriteH / 2;
        double toX = isPlayerAttacking ? EnemyX : PlayerX;
        double toY = GroundY - SpriteH / 2;

        if (result.IsMedaforce)
        {
            PlayMedaforceAnimation(fromX, fromY, toX, toY, result);
            return;
        }

        if (result.HealAmount > 0)
        {
            PlayHealAnimation(isPlayerAttacking ? PlayerX : EnemyX, GroundY - SpriteH / 2);
            SpawnDamagePopup(isPlayerAttacking ? PlayerX : EnemyX, GroundY - SpriteH - 10,
                $"+{result.HealAmount}", Color.FromRgb(0x3F, 0xB9, 0x50));
            return;
        }

        var partAction = result.UsingPart?.Action ?? ActionType.Shooting;

        if (!result.Hit)
        {
            // Miss: projectile that sails past
            if (partAction == ActionType.Shooting)
                SpawnProjectile(fromX, fromY, toX + (isPlayerAttacking ? 80 : -80), toY - 20,
                    Color.FromRgb(0xFF, 0xDD, 0x44), speed: 12, size: 5);
            SpawnDamagePopup(toX, toY - 30, "MISS", Color.FromRgb(0xDD, 0xDD, 0xDD));
            return;
        }

        // ── Hit animations ──
        switch (partAction)
        {
            case ActionType.Shooting:
                SpawnProjectile(fromX, fromY, toX, toY, Color.FromRgb(0xFF, 0xDD, 0x44), speed: 14, size: 5);
                DelayAction(200, () =>
                {
                    SpawnImpact(toX, toY, Color.FromRgb(0xFF, 0xD7, 0x00), 10);
                    TriggerShake(result.Critical ? 12 : 6);
                    SpawnDamagePopup(toX, toY - 30, result.Damage.ToString(),
                        result.Critical ? Color.FromRgb(0xFF, 0xD7, 0x00) : Color.FromRgb(0xFF, 0x44, 0x44),
                        result.Critical);
                    if (result.PartDestroyed)
                        SpawnExplosion(toX, toY, 15);
                    RefreshMech(isPlayerAttacking);
                });
                break;

            case ActionType.Melee:
                PlayMeleeDash(isPlayerAttacking, () =>
                {
                    SpawnSlashEffect(toX, toY);
                    SpawnImpact(toX, toY, Color.FromRgb(0xFF, 0x88, 0x44), 8);
                    TriggerShake(result.Critical ? 14 : 8);
                    SpawnDamagePopup(toX, toY - 30, result.Damage.ToString(),
                        result.Critical ? Color.FromRgb(0xFF, 0xD7, 0x00) : Color.FromRgb(0xFF, 0x44, 0x44),
                        result.Critical);
                    if (result.PartDestroyed)
                        SpawnExplosion(toX, toY, 15);
                    RefreshMech(isPlayerAttacking);
                });
                break;

            default: // Support / other
                SpawnImpact(toX, toY, Color.FromRgb(0x88, 0xCC, 0xFF), 6);
                TriggerShake(4);
                SpawnDamagePopup(toX, toY - 30, result.Damage.ToString(), Color.FromRgb(0xFF, 0x44, 0x44));
                RefreshMech(isPlayerAttacking);
                break;
        }

        // KO explosion
        if (result.TargetKnockedOut)
        {
            DelayAction(400, () =>
            {
                SpawnExplosion(toX, toY, 25);
                TriggerShake(18);
                SpawnDamagePopup(toX, toY - 50, "KNOCKOUT!", Color.FromRgb(0xFF, 0x44, 0x44), isBig: true);
            });
        }
    }

    /// <summary>Refresh a mech sprite after damage/state change.</summary>
    public void RefreshMech(bool attackerIsPlayer)
    {
        if (attackerIsPlayer)
            DrawMech(_enemySprite, EnemyBot, EnemyX - SpriteW / 2, GroundY - SpriteH, true);
        else
            DrawMech(_playerSprite, PlayerBot, PlayerX - SpriteW / 2, GroundY - SpriteH, false);
    }

    /// <summary>Refresh both mech sprites.</summary>
    public void RefreshAll()
    {
        DrawMech(_playerSprite, PlayerBot, PlayerX - SpriteW / 2, GroundY - SpriteH, false);
        DrawMech(_enemySprite, EnemyBot, EnemyX - SpriteW / 2, GroundY - SpriteH, true);
    }

    // ════════════════════════════════════════════════════════
    //  ANIMATION PRIMITIVES
    // ════════════════════════════════════════════════════════

    private void TriggerShake(int intensity) => _shakeTicks = intensity;

    private void SpawnProjectile(double fromX, double fromY, double toX, double toY, Color color, double speed, double size)
    {
        double dx = toX - fromX;
        double dy = toY - fromY;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double vx = dx / dist * speed;
        double vy = dy / dist * speed;
        int life = (int)(dist / speed) + 2;

        // Glow
        var glow = new Ellipse
        {
            Width = size * 3, Height = size * 3,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0x80, color.R, color.G, color.B),
                Colors.Transparent)
        };
        SetLeft(glow, fromX - size * 1.5);
        SetTop(glow, fromY - size * 1.5);
        _effectsLayer.Children.Add(glow);
        _particles.Add(new AnimParticle
        {
            X = fromX - size * 1.5, Y = fromY - size * 1.5,
            VX = vx, VY = vy, Gravity = 0,
            Life = life, MaxLife = life, Element = glow
        });

        // Core
        var core = new Ellipse
        {
            Width = size, Height = size,
            Fill = new SolidColorBrush(color)
        };
        SetLeft(core, fromX - size / 2);
        SetTop(core, fromY - size / 2);
        _effectsLayer.Children.Add(core);
        _particles.Add(new AnimParticle
        {
            X = fromX - size / 2, Y = fromY - size / 2,
            VX = vx, VY = vy, Gravity = 0,
            Life = life, MaxLife = life, Element = core
        });

        // Trail particles
        for (int i = 0; i < 3; i++)
        {
            var trail = new Ellipse
            {
                Width = size * 0.6, Height = size * 0.6,
                Fill = new SolidColorBrush(Color.FromArgb(0x60, color.R, color.G, color.B))
            };
            SetLeft(trail, fromX);
            SetTop(trail, fromY);
            _effectsLayer.Children.Add(trail);
            _particles.Add(new AnimParticle
            {
                X = fromX, Y = fromY,
                VX = vx * (0.6 + _rng.NextDouble() * 0.3),
                VY = vy * (0.6 + _rng.NextDouble() * 0.3),
                Gravity = 0,
                Life = life - i * 2, MaxLife = life, Element = trail
            });
        }
    }

    private void SpawnImpact(double x, double y, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 1.5 + _rng.NextDouble() * 4;
            double sz = 2 + _rng.NextDouble() * 4;

            var spark = new Ellipse
            {
                Width = sz, Height = sz,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(180 + _rng.Next(75)), color.R, color.G, color.B))
            };
            SetLeft(spark, x);
            SetTop(spark, y);
            _effectsLayer.Children.Add(spark);

            int life = 10 + _rng.Next(15);
            _particles.Add(new AnimParticle
            {
                X = x, Y = y,
                VX = Math.Cos(angle) * speed, VY = Math.Sin(angle) * speed,
                Gravity = 0.15,
                Life = life, MaxLife = life, Element = spark
            });
        }

        // Flash circle
        var flash = new Ellipse
        {
            Width = 44, Height = 44,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF), Colors.Transparent)
        };
        SetLeft(flash, x - 22);
        SetTop(flash, y - 22);
        _effectsLayer.Children.Add(flash);
        _particles.Add(new AnimParticle
        {
            X = x - 22, Y = y - 22,
            VX = 0, VY = 0, Gravity = 0,
            Life = 5, MaxLife = 5, Element = flash
        });
    }

    private void SpawnExplosion(double x, double y, int count)
    {
        var explosionColors = new[]
        {
            Color.FromRgb(0xFF, 0xD7, 0x00),
            Color.FromRgb(0xFF, 0x8C, 0x00),
            Color.FromRgb(0xF8, 0x51, 0x49),
            Color.FromRgb(0xFF, 0xFF, 0xFF)
        };

        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 2 + _rng.NextDouble() * 6;
            double sz = 3 + _rng.NextDouble() * 6;
            var color = explosionColors[_rng.Next(explosionColors.Length)];

            var ember = new Ellipse { Width = sz, Height = sz, Fill = new SolidColorBrush(color) };
            SetLeft(ember, x);
            SetTop(ember, y);
            _effectsLayer.Children.Add(ember);

            int life = 15 + _rng.Next(20);
            _particles.Add(new AnimParticle
            {
                X = x, Y = y,
                VX = Math.Cos(angle) * speed, VY = Math.Sin(angle) * speed - 1,
                Gravity = 0.12,
                Life = life, MaxLife = life, Element = ember
            });
        }

        // Big flash
        var bigFlash = new Ellipse
        {
            Width = 80, Height = 80,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0xBB, 0xFF, 0xDD, 0x44), Colors.Transparent)
        };
        SetLeft(bigFlash, x - 40);
        SetTop(bigFlash, y - 40);
        _effectsLayer.Children.Add(bigFlash);
        _particles.Add(new AnimParticle
        {
            X = x - 40, Y = y - 40,
            VX = 0, VY = 0, Gravity = 0,
            Life = 8, MaxLife = 8, Element = bigFlash
        });
    }

    private void SpawnSlashEffect(double x, double y)
    {
        var slashColors = new[] { Colors.White, Color.FromRgb(0xFF, 0xD7, 0x00) };
        for (int i = 0; i < 3; i++)
        {
            double offset = (i - 1) * 10;
            var line = new Line
            {
                X1 = x - 24 + offset, Y1 = y - 30,
                X2 = x + 24 + offset, Y2 = y + 30,
                Stroke = new SolidColorBrush(slashColors[i % slashColors.Length]),
                StrokeThickness = 3.5 - i * 0.6,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round
            };
            _effectsLayer.Children.Add(line);
            _particles.Add(new AnimParticle
            {
                X = 0, Y = 0, VX = 0, VY = 0, Gravity = 0,
                Life = 8 + i * 2, MaxLife = 10 + i * 2, Element = line
            });
        }
    }

    private void PlayMeleeDash(bool isPlayerAttacking, Action onImpact)
    {
        var sprite = isPlayerAttacking ? _playerSprite : _enemySprite;
        double startX = isPlayerAttacking ? PlayerX - SpriteW / 2 : EnemyX - SpriteW / 2;
        double targetX = isPlayerAttacking ? EnemyX - SpriteW / 2 - 40 : PlayerX - SpriteW / 2 + 40;

        var dashAnim = new DoubleAnimation
        {
            From = startX,
            To = targetX,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        dashAnim.Completed += (_, _) =>
        {
            onImpact();

            var returnAnim = new DoubleAnimation
            {
                From = targetX,
                To = startX,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            sprite.BeginAnimation(LeftProperty, returnAnim);
        };

        sprite.BeginAnimation(LeftProperty, dashAnim);
    }

    private void PlayMedaforceAnimation(double fromX, double fromY, double toX, double toY, ActionResult result)
    {
        bool isPlayerAttacking = result.Attacker.IsPlayerOwned;
        var medaforceColor = Color.FromRgb(0xBC, 0x8C, 0xFF);

        // Charge-up particles converging on attacker
        for (int i = 0; i < 20; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double radius = 30 + _rng.NextDouble() * 20;
            double startPX = fromX + Math.Cos(angle) * radius;
            double startPY = fromY + Math.Sin(angle) * radius;
            double sz = 2 + _rng.NextDouble() * 3;

            var orb = new Ellipse
            {
                Width = sz, Height = sz,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(150 + _rng.Next(105)),
                    medaforceColor.R, medaforceColor.G, medaforceColor.B))
            };
            SetLeft(orb, startPX);
            SetTop(orb, startPY);
            _effectsLayer.Children.Add(orb);

            _particles.Add(new AnimParticle
            {
                X = startPX, Y = startPY,
                VX = (fromX - startPX) * 0.08,
                VY = (fromY - startPY) * 0.08,
                Gravity = 0,
                Life = 12 + i / 2, MaxLife = 20, Element = orb
            });
        }

        // Delayed beam barrage
        DelayAction(350, () =>
        {
            for (int i = 0; i < 5; i++)
            {
                SpawnProjectile(fromX, fromY + (i - 2) * 5, toX, toY + (i - 2) * 5,
                    medaforceColor, speed: 18, size: 6);
            }

            DelayAction(200, () =>
            {
                SpawnExplosion(toX, toY, 30);
                TriggerShake(20);
                SpawnDamagePopup(toX, toY - 40,
                    result.Damage > 0 ? result.Damage.ToString() : "MEDAFORCE!",
                    medaforceColor, isBig: true);
                if (result.PartDestroyed)
                    SpawnExplosion(toX, toY, 20);
                RefreshMech(isPlayerAttacking);

                if (result.TargetKnockedOut)
                {
                    DelayAction(300, () =>
                    {
                        SpawnExplosion(toX, toY, 35);
                        TriggerShake(25);
                        SpawnDamagePopup(toX, toY - 60, "KNOCKOUT!",
                            Color.FromRgb(0xFF, 0x44, 0x44), isBig: true);
                    });
                }
            });
        });
    }

    private void PlayHealAnimation(double x, double y)
    {
        var healColor = Color.FromRgb(0x3F, 0xB9, 0x50);
        for (int i = 0; i < 10; i++)
        {
            double sz = 2 + _rng.NextDouble() * 3;
            var orb = new Ellipse
            {
                Width = sz, Height = sz,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(150 + _rng.Next(105)),
                    healColor.R, healColor.G, healColor.B))
            };
            double px = x - 15 + _rng.NextDouble() * 30;
            double py = y + 10;
            SetLeft(orb, px);
            SetTop(orb, py);
            _effectsLayer.Children.Add(orb);

            int life = 20 + _rng.Next(15);
            _particles.Add(new AnimParticle
            {
                X = px, Y = py,
                VX = (_rng.NextDouble() - 0.5) * 1.5,
                VY = -1.5 - _rng.NextDouble() * 2,
                Gravity = 0,
                Life = life, MaxLife = life, Element = orb
            });
        }
    }

    private void SpawnDamagePopup(double x, double y, string text, Color color, bool isBig = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = isBig ? 20 : 15,
            FontWeight = isBig ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color),
            FontFamily = new FontFamily("Consolas"),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1.5, Opacity = 0.9
            }
        };

        SetLeft(tb, x - (text.Length * (isBig ? 6 : 4.5)));
        SetTop(tb, y);
        _effectsLayer.Children.Add(tb);

        int life = isBig ? 50 : 35;
        _popups.Add(new DamagePopup
        {
            X = x, Y = y,
            Life = life, MaxLife = life, Element = tb
        });
    }

    private void DelayAction(int milliseconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    // ── Data classes for animation ────────────────────────
    private class AnimParticle
    {
        public double X, Y, VX, VY, Gravity, Opacity;
        public int Life, MaxLife;
        public UIElement Element = null!;
    }

    private class DamagePopup
    {
        public double X, Y;
        public int Life, MaxLife;
        public UIElement Element = null!;
    }
}
