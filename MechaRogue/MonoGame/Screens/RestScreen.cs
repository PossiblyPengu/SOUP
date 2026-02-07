namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;

/// <summary>
/// Rest stop — heal your squad.
/// </summary>
public class RestScreen : GameScreen
{
    private RunState? _run;
    public event Action? OnRestComplete;

    public RestScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
        : base(gd, draw, font) { }

    public void Setup(RunState run) => _run = run;

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse)
    {
        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
        {
            if (_run != null)
            {
                foreach (var m in _run.Squad)
                    m.RestHeal(0.5);
            }
            OnRestComplete?.Invoke();
        }
    }

    public override void Render(SpriteBatch sb, int sw, int sh)
    {
        Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
            new Color(0x08, 0x10, 0x20), new Color(0x10, 0x18, 0x30));

        Font.DrawStringCentered(sb, "REST STOP", new Vector2(sw / 2, 60), new Color(0x40, 0x90, 0xFF), 3);
        Font.DrawStringCentered(sb, "YOUR MEDABOTS WILL BE REPAIRED",
            new Vector2(sw / 2, 120), Color.White * 0.7f, 2);
        Font.DrawStringCentered(sb, "+50% ARMOR RESTORED",
            new Vector2(sw / 2, 160), new Color(0x40, 0xFF, 0x40), 2);

        // Show squad status
        if (_run != null)
        {
            int y = 210;
            foreach (var bot in _run.Squad)
            {
                Font.DrawStringCentered(sb, $"{bot.Name}: {(int)(bot.HealthPercent * 100)}% HP",
                    new Vector2(sw / 2, y), Color.White, 2);
                y += 28;
            }
        }

        Font.DrawStringCentered(sb, "PRESS ENTER TO REST",
            new Vector2(sw / 2, sh - 80), Color.White * 0.5f, 2);
    }
}
