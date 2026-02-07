namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;
using MechaRogue.Services;

/// <summary>
/// Title screen — choose your starter Medabot.
/// </summary>
public class TitleScreen : GameScreen
{
    public event Action<Medabot>? OnStarterSelected;

    public TitleScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
        : base(gd, draw, font) { }

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse)
    {
        // Input handled in Render via buttons
    }

    public override void Render(SpriteBatch sb, int sw, int sh)
    {
        // Use RenderWithInput instead — called from Game1
    }

    public void RenderWithInput(SpriteBatch sb, int sw, int sh, MouseState mouse, MouseState prevMouse)
    {
        // Background
        Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
            new Color(0x0A, 0x0C, 0x15), new Color(0x14, 0x20, 0x38));

        // Title
        Font.DrawStringCentered(sb, "MECHAROGUE", new Vector2(sw / 2, 60), new Color(0xD2, 0x99, 0x22), 4);
        Font.DrawStringCentered(sb, "MEDABOTS ROGUELIKE", new Vector2(sw / 2, 110), Color.White * 0.7f, 2);
        Font.DrawStringCentered(sb, "CHOOSE YOUR STARTER", new Vector2(sw / 2, 170), Color.White, 2);

        int bw = 200, bh = 50, gap = 30;
        int totalW = bw * 2 + gap;
        int startX = sw / 2 - totalW / 2;
        int y = 220;

        if (DrawButton(sb, "METABEE - SHOOTER", new Rectangle(startX, y, bw, bh), mouse, prevMouse,
            new Color(0xA0, 0x70, 0x10)))
        {
            var starter = PartCatalog.MakeMetabee();
            starter.IsPlayerOwned = true;
            starter.IsLeader = true;
            OnStarterSelected?.Invoke(starter);
        }

        if (DrawButton(sb, "ROKUSHO - MELEE", new Rectangle(startX + bw + gap, y, bw, bh), mouse, prevMouse,
            new Color(0x20, 0x50, 0xA0)))
        {
            var starter = PartCatalog.MakeRokusho();
            starter.IsPlayerOwned = true;
            starter.IsLeader = true;
            OnStarterSelected?.Invoke(starter);
        }

        // Info
        Font.DrawStringCentered(sb, "HEAD DESTRUCTION = INSTANT KO",
            new Vector2(sw / 2, y + bh + 40), new Color(0xFF, 0x60, 0x60), 1);
        Font.DrawStringCentered(sb, "WINNER TAKES LOSER'S PART",
            new Vector2(sw / 2, y + bh + 60), new Color(0x60, 0xFF, 0x60), 1);
        Font.DrawStringCentered(sb, "15 FLOORS TO VICTORY",
            new Vector2(sw / 2, y + bh + 80), new Color(0x60, 0x90, 0xFF), 1);
    }
}
