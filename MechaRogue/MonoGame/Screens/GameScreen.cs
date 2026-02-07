namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Rendering;
using MechaRogue.Models;

/// <summary>
/// Base class for all game screens.
/// </summary>
public abstract class GameScreen
{
    protected DrawHelper Draw { get; }
    protected PixelFont Font { get; }
    protected GraphicsDevice GD { get; }

    protected GameScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
    {
        GD = gd;
        Draw = draw;
        Font = font;
    }

    public abstract void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse);

    public abstract void Render(SpriteBatch sb, int screenWidth, int screenHeight);

    /// <summary>Helper: was key just pressed this frame?</summary>
    protected static bool JustPressed(Keys key, KeyboardState kb, KeyboardState prev) =>
        kb.IsKeyDown(key) && !prev.IsKeyDown(key);

    /// <summary>Helper: was left mouse just clicked?</summary>
    protected static bool JustClicked(MouseState mouse, MouseState prev) =>
        mouse.LeftButton == ButtonState.Pressed && prev.LeftButton == ButtonState.Released;

    /// <summary>Helper: draw a button and check if clicked.</summary>
    protected bool DrawButton(SpriteBatch sb, string text, Rectangle rect,
        MouseState mouse, MouseState prevMouse, Color? color = null)
    {
        var btnColor = color ?? new Color(0x30, 0x60, 0xA0);
        bool hover = rect.Contains(mouse.Position);
        var drawColor = hover ? Color.Lerp(btnColor, Color.White, 0.2f) : btnColor;

        Draw.FillRect(sb, rect, drawColor);
        Draw.DrawRect(sb, rect, Color.White * 0.6f, 2);
        Font.DrawStringCentered(sb, text.ToUpperInvariant(), rect.Center.ToVector2(), Color.White, 2);

        return hover && JustClicked(mouse, prevMouse);
    }
}
