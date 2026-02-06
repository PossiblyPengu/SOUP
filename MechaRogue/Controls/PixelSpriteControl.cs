using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MechaRogue.Models;

namespace MechaRogue.Controls;

/// <summary>
/// A control that renders pixel sprites at a scaled size.
/// </summary>
public class PixelSpriteControl : Canvas
{
    public static readonly DependencyProperty SpriteProperty = DependencyProperty.Register(
        nameof(Sprite), typeof(PixelSprite), typeof(PixelSpriteControl),
        new PropertyMetadata(null, OnSpriteChanged));
    
    public static readonly DependencyProperty PixelScaleProperty = DependencyProperty.Register(
        nameof(PixelScale), typeof(double), typeof(PixelSpriteControl),
        new PropertyMetadata(3.0, OnSpriteChanged));
    
    public static readonly DependencyProperty IsFlippedProperty = DependencyProperty.Register(
        nameof(IsFlipped), typeof(bool), typeof(PixelSpriteControl),
        new PropertyMetadata(false, OnSpriteChanged));
    
    public static readonly DependencyProperty IsDimmedProperty = DependencyProperty.Register(
        nameof(IsDimmed), typeof(bool), typeof(PixelSpriteControl),
        new PropertyMetadata(false, OnSpriteChanged));
    
    public PixelSprite? Sprite
    {
        get => (PixelSprite?)GetValue(SpriteProperty);
        set => SetValue(SpriteProperty, value);
    }
    
    public double PixelScale
    {
        get => (double)GetValue(PixelScaleProperty);
        set => SetValue(PixelScaleProperty, value);
    }
    
    public bool IsFlipped
    {
        get => (bool)GetValue(IsFlippedProperty);
        set => SetValue(IsFlippedProperty, value);
    }
    
    public bool IsDimmed
    {
        get => (bool)GetValue(IsDimmedProperty);
        set => SetValue(IsDimmedProperty, value);
    }
    
    private static void OnSpriteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PixelSpriteControl control)
        {
            control.RenderSprite();
        }
    }
    
    private void RenderSprite()
    {
        Children.Clear();
        
        var sprite = Sprite;
        if (sprite == null || sprite.Rows.Length == 0)
            return;
        
        var scale = PixelScale;
        var dimOpacity = IsDimmed ? 0.4 : 1.0;
        
        Width = sprite.Width * scale;
        Height = sprite.Height * scale;
        
        for (int y = 0; y < sprite.Rows.Length; y++)
        {
            var row = sprite.Rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                var colorIndex = row[x];
                
                if (colorIndex == '0' || !sprite.Palette.TryGetValue(colorIndex, out var colorHex))
                    continue;
                
                if (colorHex == "transparent")
                    continue;
                
                var color = ParseColor(colorHex);
                
                var rect = new Rectangle
                {
                    Width = scale,
                    Height = scale,
                    Fill = new SolidColorBrush(color),
                    Opacity = dimOpacity
                };
                
                // Apply flip if needed
                var drawX = IsFlipped ? (sprite.Width - 1 - x) * scale : x * scale;
                
                SetLeft(rect, drawX);
                SetTop(rect, y * scale);
                Children.Add(rect);
            }
        }
    }
    
    private static Color ParseColor(string hex)
    {
        if (hex.StartsWith('#') && hex.Length == 7)
        {
            var r = Convert.ToByte(hex.Substring(1, 2), 16);
            var g = Convert.ToByte(hex.Substring(3, 2), 16);
            var b = Convert.ToByte(hex.Substring(5, 2), 16);
            return Color.FromRgb(r, g, b);
        }
        return Colors.Magenta; // Fallback for debugging
    }
}
