namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;

/// <summary>
/// Roguelike map screen — shows nodes for current floor, squad info, spare parts.
/// </summary>
public class MapScreen : GameScreen
{
    public event Action<RunNode>? OnNodeSelected;

    public MapScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
        : base(gd, draw, font) { }

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse) { }

    public void RenderWithInput(SpriteBatch sb, int sw, int sh, RunState run,
        List<RunNode> availableNodes, MouseState mouse, MouseState prevMouse)
    {
        // Background
        Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
            new Color(0x0C, 0x10, 0x1C), new Color(0x10, 0x18, 0x28));

        // Header
        Font.DrawStringWithShadow(sb, $"FLOOR {run.Floor} / {run.MaxFloors}",
            new Vector2(20, 15), new Color(0xD2, 0x99, 0x22), 3);
        Font.DrawStringWithShadow(sb, $"CREDITS: {run.Credits}",
            new Vector2(sw - 200, 15), Color.White, 2);
        Font.DrawStringWithShadow(sb, $"WINS: {run.Wins}  LOSSES: {run.Losses}",
            new Vector2(sw - 200, 40), Color.White * 0.7f, 1);

        // Divider
        Draw.FillRect(sb, new Rectangle(0, 60, sw, 2), Color.White * 0.2f);

        // Available nodes
        Font.DrawStringWithShadow(sb, "CHOOSE YOUR PATH:", new Vector2(20, 75), Color.White, 2);

        int nodeY = 110;
        int nodeW = 250, nodeH = 45, gap = 12;
        int startX = 20;

        for (int i = 0; i < availableNodes.Count; i++)
        {
            var node = availableNodes[i];
            var rect = new Rectangle(startX, nodeY + i * (nodeH + gap), nodeW, nodeH);
            var nodeColor = GetNodeColor(node.Type);

            if (DrawButton(sb, $"{GetNodeIcon(node.Type)} {node.Type}", rect, mouse, prevMouse, nodeColor))
            {
                OnNodeSelected?.Invoke(node);
            }
        }

        // Squad info (right side)
        int panelX = sw - 280;
        DrawSquadPanel(sb, panelX, 75, 260, sh - 90, run);
    }

    private void DrawSquadPanel(SpriteBatch sb, int x, int y, int w, int h, RunState run)
    {
        Draw.FillRect(sb, new Rectangle(x, y, w, h), new Color(0, 0, 0, 100));
        Draw.DrawRect(sb, new Rectangle(x, y, w, h), Color.White * 0.2f);

        Font.DrawString(sb, "SQUAD", new Vector2(x + 10, y + 8), Color.White, 2);

        int sy = y + 35;
        foreach (var bot in run.Squad)
        {
            var color = bot.IsKnockedOut ? Color.Red * 0.6f : Color.White;
            Font.DrawString(sb, bot.Name.ToUpperInvariant(), new Vector2(x + 10, sy), color, 2);
            sy += 18;

            // HP bars per part
            foreach (var part in bot.AllParts)
            {
                var barColor = part.IsDestroyed ? Color.Red : Color.Lerp(Color.Red, Color.Green, (float)part.ArmorPercent);
                Draw.DrawBar(sb, new Rectangle(x + 15, sy, w - 40, 6),
                    (float)part.ArmorPercent, barColor, new Color(30, 30, 30), Color.White * 0.3f);
                Font.DrawString(sb, $"{part.Slot}: {part.Armor}/{part.MaxArmor}",
                    new Vector2(x + 15, sy + 8), Color.White * 0.6f, 1);
                sy += 20;
            }
            sy += 10;
        }

        // Spare parts
        if (run.SpareParts.Count > 0)
        {
            sy += 5;
            Font.DrawString(sb, "SPARE PARTS", new Vector2(x + 10, sy), Color.White * 0.7f, 1);
            sy += 14;
            foreach (var part in run.SpareParts.Take(8))
            {
                Font.DrawString(sb, $"{part.Name} ({part.Slot})",
                    new Vector2(x + 15, sy), Color.White * 0.5f, 1);
                sy += 12;
            }
        }
    }

    private static Color GetNodeColor(NodeType type) => type switch
    {
        NodeType.Battle => new Color(0x50, 0x60, 0x80),
        NodeType.EliteBattle => new Color(0x80, 0x50, 0x80),
        NodeType.Shop => new Color(0x50, 0x80, 0x50),
        NodeType.Rest => new Color(0x40, 0x70, 0x90),
        NodeType.Event => new Color(0x80, 0x80, 0x40),
        NodeType.Boss => new Color(0xA0, 0x30, 0x30),
        _ => new Color(0x50, 0x50, 0x50)
    };

    private static string GetNodeIcon(NodeType type) => type switch
    {
        NodeType.Battle => "[!]",
        NodeType.EliteBattle => "[!!]",
        NodeType.Shop => "[$]",
        NodeType.Rest => "[ZZ]",
        NodeType.Event => "[?]",
        NodeType.Boss => "[***]",
        _ => "[.]"
    };

    public override void Render(SpriteBatch sb, int screenWidth, int screenHeight) { }
}
