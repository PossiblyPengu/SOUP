namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Rendering;

/// <summary>
/// Game Over / Victory end screens.
/// </summary>
public class EndScreen : GameScreen
{
    private bool _isVictory;
    private int _wins, _losses, _floor;

    public event Action? OnReturnToTitle;

    public EndScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
        : base(gd, draw, font) { }

    public void Setup(bool isVictory, int wins, int losses, int floor)
    {
        _isVictory = isVictory;
        _wins = wins;
        _losses = losses;
        _floor = floor;
    }

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse)
    {
        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
            OnReturnToTitle?.Invoke();
    }

    public override void Render(SpriteBatch sb, int sw, int sh)
    {
        if (_isVictory)
        {
            Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
                new Color(0x14, 0x20, 0x08), new Color(0x08, 0x14, 0x04));

            Font.DrawStringCentered(sb, "VICTORY!", new Vector2(sw / 2, 80), Color.Yellow, 4);
            Font.DrawStringCentered(sb, "YOU CONQUERED ALL 15 FLOORS!",
                new Vector2(sw / 2, 150), Color.White, 2);
        }
        else
        {
            Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
                new Color(0x20, 0x08, 0x08), new Color(0x14, 0x04, 0x04));

            Font.DrawStringCentered(sb, "GAME OVER", new Vector2(sw / 2, 80), Color.Red, 4);
            Font.DrawStringCentered(sb, "YOUR MEDABOTS HAVE BEEN DEFEATED.",
                new Vector2(sw / 2, 150), Color.White * 0.7f, 2);
        }

        Font.DrawStringCentered(sb, $"REACHED FLOOR {_floor}  WINS: {_wins}  LOSSES: {_losses}",
            new Vector2(sw / 2, 210), Color.White * 0.6f, 2);

        Font.DrawStringCentered(sb, "PRESS ENTER TO RETURN TO TITLE",
            new Vector2(sw / 2, sh - 80), Color.White * 0.5f, 2);
    }
}
