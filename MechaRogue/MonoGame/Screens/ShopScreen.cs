namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;
using MechaRogue.Services;

/// <summary>
/// Shop screen — buy parts with credits.
/// </summary>
public class ShopScreen : GameScreen
{
    private List<MedaPart> _shopParts = [];
    private RunState? _run;
    private int _selectedIndex;

    public event Action? OnLeaveShop;
    public event Action<MedaPart>? OnBuyPart;

    public ShopScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
        : base(gd, draw, font) { }

    public void Setup(RunState run, List<MedaPart> parts)
    {
        _run = run;
        _shopParts = parts;
        _selectedIndex = 0;
    }

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse)
    {
        if (_run == null) return;

        if (JustPressed(Keys.Up, kb, prevKb) && _selectedIndex > 0) _selectedIndex--;
        if (JustPressed(Keys.Down, kb, prevKb) && _selectedIndex < _shopParts.Count - 1) _selectedIndex++;

        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
        {
            if (_selectedIndex < _shopParts.Count)
            {
                var part = _shopParts[_selectedIndex];
                int cost = GetCost(part);
                if (_run.Credits >= cost)
                {
                    _run.Credits -= cost;
                    _run.SpareParts.Add(part.Clone());
                    _shopParts.RemoveAt(_selectedIndex);
                    if (_selectedIndex >= _shopParts.Count)
                        _selectedIndex = Math.Max(0, _shopParts.Count - 1);
                    OnBuyPart?.Invoke(part);
                }
            }
        }

        if (JustPressed(Keys.Escape, kb, prevKb) || JustPressed(Keys.L, kb, prevKb))
            OnLeaveShop?.Invoke();
    }

    public override void Render(SpriteBatch sb, int sw, int sh) =>
        RenderWithInput(sb, sw, sh, Mouse.GetState(), Mouse.GetState());

    public void RenderWithInput(SpriteBatch sb, int sw, int sh, MouseState mouse, MouseState prevMouse)
    {
        Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
            new Color(0x0C, 0x14, 0x0C), new Color(0x10, 0x20, 0x10));

        Font.DrawStringWithShadow(sb, "PARTS SHOP", new Vector2(20, 15), new Color(0x50, 0xE0, 0x50), 3);
        Font.DrawString(sb, $"CREDITS: {_run?.Credits ?? 0}",
            new Vector2(sw - 180, 20), Color.White, 2);

        int y = 70;
        for (int i = 0; i < _shopParts.Count; i++)
        {
            var part = _shopParts[i];
            int cost = GetCost(part);
            bool selected = i == _selectedIndex;
            bool canAfford = (_run?.Credits ?? 0) >= cost;

            var bgColor = selected ? new Color(0x20, 0x40, 0x20, 180) : new Color(0, 0, 0, 60);
            Draw.FillRect(sb, new Rectangle(20, y, sw - 40, 50), bgColor);
            if (selected)
                Draw.DrawRect(sb, new Rectangle(20, y, sw - 40, 50), Color.White * 0.4f);

            var textColor = canAfford ? Color.White : Color.White * 0.4f;
            string prefix = selected ? "> " : "  ";
            Font.DrawString(sb, $"{prefix}{part.Name}", new Vector2(30, y + 5), textColor, 2);
            Font.DrawString(sb, $"  {part.Slot} | T{part.Tier} | PWR:{part.Power} ACC:{part.Accuracy} SPD:{part.Speed}",
                new Vector2(30, y + 25), textColor * 0.6f, 1);
            Font.DrawString(sb, $"{cost}C", new Vector2(sw - 80, y + 10), canAfford ? Color.Yellow : Color.Red, 2);

            y += 58;
        }

        y += 20;
        Font.DrawString(sb, "UP/DOWN=SELECT  ENTER=BUY  ESC=LEAVE",
            new Vector2(20, y), Color.White * 0.4f, 1);
    }

    private static int GetCost(MedaPart part) => 30 + part.Tier * 20;
}
