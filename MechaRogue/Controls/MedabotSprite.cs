namespace MechaRogue.Controls;

using MechaRogue.Models;
using System.Windows.Shapes;

/// <summary>
/// Renders a simple pixel-art Medabot from its parts.
/// Each part slot gets a colored region; destroyed parts turn gray.
/// </summary>
public class MedabotSprite : Canvas
{
    public static readonly DependencyProperty MedabotProperty =
        DependencyProperty.Register(nameof(Bot), typeof(Medabot), typeof(MedabotSprite),
            new PropertyMetadata(null, (d, _) => ((MedabotSprite)d).Redraw()));

    public static readonly DependencyProperty SpriteScaleProperty =
        DependencyProperty.Register(nameof(SpriteScale), typeof(double), typeof(MedabotSprite),
            new PropertyMetadata(3.0, (d, _) => ((MedabotSprite)d).Redraw()));

    public Medabot? Bot
    {
        get => (Medabot?)GetValue(MedabotProperty);
        set => SetValue(MedabotProperty, value);
    }

    public double SpriteScale
    {
        get => (double)GetValue(SpriteScaleProperty);
        set => SetValue(SpriteScaleProperty, value);
    }

    // 16×20 pixel template:
    //   Row 0-3:  Head (columns 4-11)
    //   Row 4-5:  Neck/body core
    //   Row 6-11: Arms (0-5 left arm, 10-15 right arm), body center
    //   Row 12-19: Legs (4-11)

    private static readonly int[,] _template = BuildTemplate();

    public static int[,] BuildTemplate()
    {
        // 0=bg, 1=head, 2=body, 3=rightArm, 4=leftArm, 5=legs
        var t = new int[20, 16];

        // Head (rows 0-4, cols 4-11)
        for (int r = 0; r < 5; r++)
            for (int c = 4; c < 12; c++)
                t[r, c] = 1;
        // Eyes
        t[2, 5] = 0; t[2, 6] = 0; t[2, 9] = 0; t[2, 10] = 0;

        // Neck/body core (rows 5-6, cols 5-10)
        for (int r = 5; r < 7; r++)
            for (int c = 5; c < 11; c++)
                t[r, c] = 2;

        // Body torso (rows 7-11, cols 5-10)
        for (int r = 7; r < 12; r++)
            for (int c = 5; c < 11; c++)
                t[r, c] = 2;

        // Right arm (rows 7-12, cols 11-14)
        for (int r = 7; r < 13; r++)
            for (int c = 11; c < 15; c++)
                t[r, c] = 3;

        // Left arm (rows 7-12, cols 1-4)
        for (int r = 7; r < 13; r++)
            for (int c = 1; c < 5; c++)
                t[r, c] = 4;

        // Legs (rows 12-19, cols 4-7 left leg, 8-11 right leg)
        for (int r = 12; r < 20; r++)
        {
            for (int c = 4; c < 7; c++)
                t[r, c] = 5;
            for (int c = 9; c < 12; c++)
                t[r, c] = 5;
        }

        return t;
    }

    private static readonly Color _destroyedColor = Color.FromRgb(0x30, 0x36, 0x3D);
    private static readonly Color _bodyColor = Color.FromRgb(0x48, 0x52, 0x5C);

    private static readonly Dictionary<string, Color[]> _modelPalettes = new()
    {
        ["KBT"] = [Color.FromRgb(0xD2, 0x99, 0x22), Color.FromRgb(0xB8, 0x80, 0x10), Color.FromRgb(0xE0, 0xA8, 0x30)],
        ["KWG"] = [Color.FromRgb(0x58, 0xA6, 0xFF), Color.FromRgb(0x1F, 0x6F, 0xEB), Color.FromRgb(0x79, 0xC0, 0xFF)],
        ["CAT"] = [Color.FromRgb(0xBC, 0x8C, 0xFF), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xD0, 0xAA, 0xFF)],
        ["TOT"] = [Color.FromRgb(0x3F, 0xB9, 0x50), Color.FromRgb(0x23, 0x8B, 0x2F), Color.FromRgb(0x56, 0xD3, 0x64)],
        ["NAS"] = [Color.FromRgb(0xFF, 0x7B, 0x72), Color.FromRgb(0xE0, 0x55, 0x4D), Color.FromRgb(0xFF, 0xA0, 0x98)],
        ["DOG"] = [Color.FromRgb(0x8B, 0x94, 0x9E), Color.FromRgb(0x6E, 0x76, 0x81), Color.FromRgb(0xA8, 0xB2, 0xBC)],
        ["STG"] = [Color.FromRgb(0xF8, 0x51, 0x49), Color.FromRgb(0xD0, 0x30, 0x28), Color.FromRgb(0xFF, 0x78, 0x70)],
    };

    private Color[] GetPalette()
    {
        if (Bot == null) return [_bodyColor, _bodyColor, _bodyColor];
        var prefix = Bot.ModelId.Length >= 3 ? Bot.ModelId[..3] : "KBT";
        return _modelPalettes.TryGetValue(prefix, out var p) ? p : [_bodyColor, _bodyColor, _bodyColor];
    }

    public void Redraw()
    {
        Children.Clear();
        if (Bot == null) return;

        double s = SpriteScale;
        Width = 16 * s;
        Height = 20 * s;

        var palette = GetPalette();
        bool headDead = Bot.Head.IsDestroyed;
        bool rArmDead = Bot.RightArm.IsDestroyed;
        bool lArmDead = Bot.LeftArm.IsDestroyed;
        bool legsDead = Bot.Legs.IsDestroyed;

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

                var rect = new Rectangle
                {
                    Width = s,
                    Height = s,
                    Fill = new SolidColorBrush(clr)
                };
                SetLeft(rect, c * s);
                SetTop(rect, r * s);
                Children.Add(rect);
            }
        }

        // KO overlay
        if (Bot.IsKnockedOut)
        {
            var overlay = new Rectangle
            {
                Width = 16 * s,
                Height = 20 * s,
                Fill = new SolidColorBrush(Color.FromArgb(0x88, 0x00, 0x00, 0x00))
            };
            Children.Add(overlay);

            // X eyes
            var x1 = new Line { X1 = 5 * s, Y1 = 1.5 * s, X2 = 7 * s, Y2 = 3.5 * s, Stroke = Brushes.Red, StrokeThickness = s * 0.5 };
            var x2 = new Line { X1 = 7 * s, Y1 = 1.5 * s, X2 = 5 * s, Y2 = 3.5 * s, Stroke = Brushes.Red, StrokeThickness = s * 0.5 };
            var x3 = new Line { X1 = 9 * s, Y1 = 1.5 * s, X2 = 11 * s, Y2 = 3.5 * s, Stroke = Brushes.Red, StrokeThickness = s * 0.5 };
            var x4 = new Line { X1 = 11 * s, Y1 = 1.5 * s, X2 = 9 * s, Y2 = 3.5 * s, Stroke = Brushes.Red, StrokeThickness = s * 0.5 };
            Children.Add(x1);
            Children.Add(x2);
            Children.Add(x3);
            Children.Add(x4);
        }
    }
}
