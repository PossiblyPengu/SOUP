namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;
using MechaRogue.Services;

/// <summary>
/// Random event screen with two choices.
/// </summary>
public class EventScreen : GameScreen
{
    private RunState? _run;
    private int _eventType;
    private string _eventText = "";
    private string _choice1 = "";
    private string _choice2 = "";
    private readonly Random _rng = new();

    public event Action? OnEventComplete;

    public EventScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font)
        : base(gd, draw, font) { }

    public void Setup(RunState run)
    {
        _run = run;
        _eventType = _rng.Next(4);
        switch (_eventType)
        {
            case 0:
                _eventText = "YOU FIND A DAMAGED MEDABOT.\nITS MEDAL IS STILL INTACT.";
                _choice1 = "1: SALVAGE PARTS (+RANDOM PART)";
                _choice2 = "2: RECRUIT (IF SQUAD < 3)";
                break;
            case 1:
                _eventText = "A SHADY DEALER OFFERS A TRADE:\nONE SPARE PART FOR CREDITS.";
                _choice1 = "1: TRADE SPARE PART (+80 CREDITS)";
                _choice2 = "2: DECLINE";
                break;
            case 2:
                _eventText = "A TECHNICIAN OFFERS TO\nUPGRADE YOUR MEDAL.";
                _choice1 = "1: ACCEPT (+XP TO MEDAL)";
                _choice2 = "2: DECLINE";
                break;
            default:
                _eventText = "A PARTS VENDING MACHINE!\nINSERT CREDITS FOR A RANDOM PART.";
                _choice1 = "1: INSERT 50 CREDITS";
                _choice2 = "2: WALK AWAY";
                break;
        }
    }

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse)
    {
        if (_run == null) return;

        if (JustPressed(Keys.D1, kb, prevKb) || JustPressed(Keys.NumPad1, kb, prevKb))
        {
            ExecuteChoice1();
            OnEventComplete?.Invoke();
        }
        if (JustPressed(Keys.D2, kb, prevKb) || JustPressed(Keys.NumPad2, kb, prevKb))
        {
            ExecuteChoice2();
            OnEventComplete?.Invoke();
        }
    }

    private void ExecuteChoice1()
    {
        if (_run == null) return;
        switch (_eventType)
        {
            case 0:
                _run.SpareParts.Add(PartCatalog.RandomPartReward(_run.Floor));
                break;
            case 1:
                if (_run.SpareParts.Count > 0)
                {
                    _run.SpareParts.RemoveAt(_run.SpareParts.Count - 1);
                    _run.Credits += 80;
                }
                break;
            case 2:
                _run.Squad.FirstOrDefault(m => !m.IsKnockedOut)?.Medal.GainXp(40);
                break;
            default:
                if (_run.Credits >= 50)
                {
                    _run.Credits -= 50;
                    _run.SpareParts.Add(PartCatalog.RandomPartReward(_run.Floor + 1));
                }
                break;
        }
    }

    private void ExecuteChoice2()
    {
        if (_run == null) return;
        if (_eventType == 0 && _run.Squad.Count < 3)
        {
            var recruit = PartCatalog.RandomEnemy(Math.Max(1, _run.Floor - 1));
            recruit.IsPlayerOwned = true;
            recruit.IsLeader = false;
            recruit.FullRestore();
            _run.Squad.Add(recruit);
        }
    }

    public override void Render(SpriteBatch sb, int sw, int sh)
    {
        Draw.FillGradientV(sb, new Rectangle(0, 0, sw, sh),
            new Color(0x14, 0x14, 0x08), new Color(0x20, 0x1C, 0x10));

        Font.DrawStringCentered(sb, "EVENT", new Vector2(sw / 2, 40), new Color(0xFF, 0xD7, 0x00), 3);
        Font.DrawStringCentered(sb, _eventText, new Vector2(sw / 2, 120), Color.White, 2);

        int y = 220;
        Font.DrawString(sb, _choice1, new Vector2(40, y), new Color(0x60, 0xFF, 0x60), 2);
        Font.DrawString(sb, _choice2, new Vector2(40, y + 40), new Color(0x60, 0x90, 0xFF), 2);

        Font.DrawStringCentered(sb, "PRESS 1 OR 2",
            new Vector2(sw / 2, sh - 60), Color.White * 0.4f, 1);
    }
}
