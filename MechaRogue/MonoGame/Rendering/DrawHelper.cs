namespace MechaRogue.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Helper for drawing rectangles, lines, gradients, and UI primitives
/// using a single 1×1 white pixel texture.
/// </summary>
public class DrawHelper
{
    private readonly Texture2D _pixel;

    public DrawHelper(GraphicsDevice gd)
    {
        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void FillRect(SpriteBatch sb, Rectangle rect, Color color) =>
        sb.Draw(_pixel, rect, color);

    public void DrawRect(SpriteBatch sb, Rectangle rect, Color color, int thickness = 1)
    {
        FillRect(sb, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color); // top
        FillRect(sb, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color); // bottom
        FillRect(sb, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color); // left
        FillRect(sb, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color); // right
    }

    /// <summary>Vertical gradient fill.</summary>
    public void FillGradientV(SpriteBatch sb, Rectangle rect, Color top, Color bottom)
    {
        for (int y = 0; y < rect.Height; y++)
        {
            float t = (float)y / Math.Max(1, rect.Height - 1);
            var color = Color.Lerp(top, bottom, t);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y + y, rect.Width, 1), color);
        }
    }

    /// <summary>Horizontal gradient fill.</summary>
    public void FillGradientH(SpriteBatch sb, Rectangle rect, Color left, Color right)
    {
        for (int x = 0; x < rect.Width; x++)
        {
            float t = (float)x / Math.Max(1, rect.Width - 1);
            var color = Color.Lerp(left, right, t);
            sb.Draw(_pixel, new Rectangle(rect.X + x, rect.Y, 1, rect.Height), color);
        }
    }

    /// <summary>Draw a horizontal bar (HP bar, charge gauge, etc).</summary>
    public void DrawBar(SpriteBatch sb, Rectangle rect, float fillPercent, Color fillColor, Color bgColor, Color borderColor)
    {
        FillRect(sb, rect, bgColor);
        int fillWidth = (int)((rect.Width - 2) * Math.Clamp(fillPercent, 0, 1));
        if (fillWidth > 0)
            FillRect(sb, new Rectangle(rect.X + 1, rect.Y + 1, fillWidth, rect.Height - 2), fillColor);
        DrawRect(sb, rect, borderColor);
    }
}
