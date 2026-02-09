namespace MechaRogue.Screens;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;
using MechaRogue.Services;

/// <summary>
/// 3v3 squad battle screen matching GBA Medabots:
///  - Each bot has its own charge gauge filling by speed
///  - When a player bot's gauge fills: ACTION MENU → part select → target bot → target part
///  - Enemy bots act via AI when their gauges fill
///  - Guard/Defend option halves incoming damage
///  - All 6 bots visible on the field with individual status
/// </summary>
public class BattleScreen : GameScreen
{
    private readonly BattleEngine _engine = new();
    private readonly BattlefieldRenderer _battlefield;
    private readonly Random _rng = new();

    // Battle state
    public List<Medabot> PlayerSquad { get; set; } = [];
    public List<Medabot> EnemySquad { get; set; } = [];
    public BattlePhase Phase { get; set; }
    public int TurnNumber { get; set; }

    // The bot whose gauge just filled and is prompting the player
    private Medabot? _activeBot;

    // Action menu state
    private int _menuIndex;
    private readonly string[] _menuOptions = ["RIGHT ARM", "LEFT ARM", "HEAD", "MEDAFORCE", "DEFEND"];

    // Part/target selection state
    private MedaPart? _selectedPart;
    private int _targetBotIndex;
    private int _targetPartIndex;
    private readonly PartSlot[] _targetSlots = [PartSlot.Head, PartSlot.RightArm, PartSlot.LeftArm, PartSlot.Legs];

    // Battle log
    private readonly List<string> _log = [];

    // Animation lock
    private bool _animLock;
    private float _animTimer;

    // Constants
    private const double MaxCharge = 100.0;
    private const double BaseChargeRate = 0.55;

    // Callbacks
    public event Action? OnBattleWon;
    public event Action? OnBattleLost;

    // Pending spoil from winning
    private MedaPart? _pendingSpoil;
    private int _pendingCredits;
    private int _pendingXp;

    // Reference to run state
    public RunState? Run { get; set; }

    public BattleScreen(GraphicsDevice gd, DrawHelper draw, PixelFont font, BattlefieldRenderer battlefield)
        : base(gd, draw, font)
    {
        _battlefield = battlefield;
    }

    public void StartBattle(RunState run, List<Medabot> enemySquad)
    {
        Run = run;
        PlayerSquad = run.Squad.Where(m => !m.IsKnockedOut).Take(3).ToList();
        EnemySquad = enemySquad;

        // Mark player bots for animation targeting
        foreach (var m in PlayerSquad) m.IsPlayerOwned = true;

        // Reset charge gauges
        foreach (var m in PlayerSquad) { m.ChargeGauge = 0; m.IsDefending = false; }
        foreach (var m in EnemySquad) { m.ChargeGauge = 0; m.IsDefending = false; }

        TurnNumber = 0;
        Phase = BattlePhase.Charging;
        _animLock = false;
        _animTimer = 0;
        _activeBot = null;
        _menuIndex = 0;
        _selectedPart = null;
        _targetBotIndex = 0;
        _targetPartIndex = 0;
        _log.Clear();
        _pendingSpoil = null;

        AddLog("-- ROBATTLE START! --");
        for (int i = 0; i < PlayerSquad.Count; i++)
            AddLog($"  ALLY {i + 1}: {PlayerSquad[i].Name}");
        AddLog("  VS");
        for (int i = 0; i < EnemySquad.Count; i++)
            AddLog($"  ENEMY {i + 1}: {EnemySquad[i].Name}");
        AddLog("Charge gauges filling...");

        _battlefield.InvalidateSprites();
    }

    // ═══════════════════════════════════════════════════════════
    //  UPDATE
    // ═══════════════════════════════════════════════════════════

    public override void Update(GameTime gameTime, KeyboardState kb, KeyboardState prevKb,
        MouseState mouse, MouseState prevMouse)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _battlefield.Update(dt);

        // Animation lock countdown
        if (_animLock)
        {
            _animTimer -= dt;
            if (_animTimer <= 0)
            {
                _animLock = false;
                _battlefield.InvalidateSprites();
                if (!CheckBattleEnd())
                    Phase = BattlePhase.Charging;
            }
            return;
        }

        switch (Phase)
        {
            case BattlePhase.BattleOver:
                HandleBattleOver(kb, prevKb);
                break;
            case BattlePhase.ActionMenu:
                HandleActionMenu(kb, prevKb);
                break;
            case BattlePhase.TargetBotSelect:
                HandleTargetBotSelect(kb, prevKb);
                break;
            case BattlePhase.TargetPartSelect:
                HandleTargetPartSelect(kb, prevKb);
                break;
            case BattlePhase.Charging:
                UpdateCharging(dt);
                break;
        }
    }

    // ────── CHARGING ──────

    private void UpdateCharging(float dt)
    {
        // Fill ALL gauges simultaneously (like the real game)
        var allBots = PlayerSquad.Concat(EnemySquad)
            .Where(m => !m.IsKnockedOut)
            .ToList();

        foreach (var bot in allBots)
        {
            double speed = Math.Max(1, bot.EffectiveSpeed);
            bot.ChargeGauge = Math.Min(MaxCharge, bot.ChargeGauge + speed * BaseChargeRate * dt * 60);
        }

        // Check who's ready
        var readyBots = allBots
            .Where(m => m.ChargeGauge >= MaxCharge)
            .OrderByDescending(m => m.EffectiveSpeed)
            .ThenBy(m => m.IsPlayerOwned ? 1 : 0)
            .ToList();

        if (readyBots.Count == 0) return;

        var next = readyBots[0];

        if (next.IsPlayerOwned)
        {
            _activeBot = next;
            Phase = BattlePhase.ActionMenu;
            _menuIndex = 0;
        }
        else
        {
            ExecuteEnemyAction(next);
        }
    }

    // ────── ACTION MENU ──────

    private void HandleActionMenu(KeyboardState kb, KeyboardState prevKb)
    {
        if (_activeBot == null) return;

        if (JustPressed(Keys.Up, kb, prevKb))
            _menuIndex = (_menuIndex - 1 + _menuOptions.Length) % _menuOptions.Length;
        if (JustPressed(Keys.Down, kb, prevKb))
            _menuIndex = (_menuIndex + 1) % _menuOptions.Length;

        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
        {
            switch (_menuIndex)
            {
                case 0: // RIGHT ARM
                    if (!_activeBot.RightArm.IsDestroyed)
                    {
                        _selectedPart = _activeBot.RightArm;
                        EnterTargetBotSelect();
                    }
                    break;
                case 1: // LEFT ARM
                    if (!_activeBot.LeftArm.IsDestroyed)
                    {
                        _selectedPart = _activeBot.LeftArm;
                        EnterTargetBotSelect();
                    }
                    break;
                case 2: // HEAD
                    if (!_activeBot.Head.IsDestroyed && _activeBot.Head.Action != ActionType.None
                        && _activeBot.Head.RemainingUses > 0)
                    {
                        _selectedPart = _activeBot.Head;
                        EnterTargetBotSelect();
                    }
                    break;
                case 3: // MEDAFORCE
                    if (_activeBot.Medal.CanUseMedaforce)
                    {
                        _selectedPart = _activeBot.Head;
                        EnterTargetBotSelect();
                    }
                    break;
                case 4: // DEFEND
                    ExecuteDefend();
                    break;
            }
        }
    }

    private void EnterTargetBotSelect()
    {
        var targets = (_selectedPart?.Action == ActionType.Support)
            ? PlayerSquad.Where(m => !m.IsKnockedOut).ToList()
            : EnemySquad.Where(m => !m.IsKnockedOut).ToList();

        if (targets.Count == 0) { Phase = BattlePhase.ActionMenu; return; }

        _targetBotIndex = 0;
        if (targets.Count == 1)
        {
            Phase = BattlePhase.TargetPartSelect;
            _targetPartIndex = 0;
        }
        else
        {
            Phase = BattlePhase.TargetBotSelect;
        }
    }

    // ────── TARGET BOT SELECT ──────

    private void HandleTargetBotSelect(KeyboardState kb, KeyboardState prevKb)
    {
        var targets = GetCurrentTargetList();
        if (targets.Count == 0) { Phase = BattlePhase.ActionMenu; return; }

        if (JustPressed(Keys.Left, kb, prevKb) || JustPressed(Keys.Up, kb, prevKb))
            _targetBotIndex = (_targetBotIndex - 1 + targets.Count) % targets.Count;
        if (JustPressed(Keys.Right, kb, prevKb) || JustPressed(Keys.Down, kb, prevKb))
            _targetBotIndex = (_targetBotIndex + 1) % targets.Count;

        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
        {
            Phase = BattlePhase.TargetPartSelect;
            _targetPartIndex = 0;
        }

        if (JustPressed(Keys.Escape, kb, prevKb) || JustPressed(Keys.Back, kb, prevKb))
            Phase = BattlePhase.ActionMenu;
    }

    // ────── TARGET PART SELECT ──────

    private void HandleTargetPartSelect(KeyboardState kb, KeyboardState prevKb)
    {
        if (JustPressed(Keys.Up, kb, prevKb))
            _targetPartIndex = (_targetPartIndex - 1 + _targetSlots.Length) % _targetSlots.Length;
        if (JustPressed(Keys.Down, kb, prevKb))
            _targetPartIndex = (_targetPartIndex + 1) % _targetSlots.Length;

        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
            ExecutePlayerAction();

        if (JustPressed(Keys.Escape, kb, prevKb) || JustPressed(Keys.Back, kb, prevKb))
        {
            var targets = GetCurrentTargetList();
            Phase = targets.Count > 1 ? BattlePhase.TargetBotSelect : BattlePhase.ActionMenu;
        }
    }

    // ────── BATTLE OVER ──────

    private void HandleBattleOver(KeyboardState kb, KeyboardState prevKb)
    {
        if (JustPressed(Keys.Enter, kb, prevKb) || JustPressed(Keys.Space, kb, prevKb))
        {
            bool won = EnemySquad.All(e => e.IsKnockedOut);
            if (won) OnBattleWon?.Invoke(); else OnBattleLost?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ACTION EXECUTION
    // ═══════════════════════════════════════════════════════════

    private void ExecutePlayerAction()
    {
        if (_activeBot == null || _selectedPart == null) return;

        var targets = GetCurrentTargetList();
        if (targets.Count == 0) return;
        var target = targets[Math.Clamp(_targetBotIndex, 0, targets.Count - 1)];

        bool isMedaforce = _menuIndex == 3;
        MedaforceAttack? mfAtk = null;
        if (isMedaforce)
        {
            var attacks = _activeBot.Medal.GetAvailableAttacks();
            mfAtk = attacks.LastOrDefault();
            if (mfAtk == null) { Phase = BattlePhase.ActionMenu; return; }
        }

        var action = new BattleAction
        {
            Attacker = _activeBot,
            UsingPart = _selectedPart,
            Target = target,
            TargetedSlot = _targetSlots[_targetPartIndex],
            IsMedaforce = isMedaforce,
            MedaforceAttack = mfAtk,
            Priority = _activeBot.EffectiveSpeed
        };

        _activeBot.ChargeGauge = 0;
        TurnNumber++;
        Phase = BattlePhase.Executing;
        _animLock = true;

        var result = _engine.ResolveAction(action);
        AddLog(result.Narration);
        PlayAnimation(result, true);
        _animTimer = GetAnimDelay(result);
    }

    private void ExecuteDefend()
    {
        if (_activeBot == null) return;

        var action = new BattleAction
        {
            Attacker = _activeBot,
            UsingPart = _activeBot.RightArm,
            Target = _activeBot,
            TargetedSlot = PartSlot.Head,
            IsDefend = true,
            Priority = _activeBot.EffectiveSpeed
        };

        _activeBot.ChargeGauge = 0;
        TurnNumber++;
        Phase = BattlePhase.Executing;
        _animLock = true;

        var result = _engine.ResolveAction(action);
        AddLog(result.Narration);
        _animTimer = 0.35f;
    }

    private void ExecuteEnemyAction(Medabot enemy)
    {
        var playerTargets = PlayerSquad.Where(s => !s.IsKnockedOut).ToList();
        if (playerTargets.Count == 0) return;

        enemy.ChargeGauge = 0;
        TurnNumber++;
        Phase = BattlePhase.Executing;
        _animLock = true;

        var action = _engine.GenerateAiAction(enemy, playerTargets);
        var result = _engine.ResolveAction(action);
        AddLog(result.Narration);
        PlayAnimation(result, false);
        _animTimer = GetAnimDelay(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  ANIMATION
    // ═══════════════════════════════════════════════════════════

    private void PlayAnimation(ActionResult result, bool playerAttacking)
    {
        if (result.IsMedaforce)
        {
            _battlefield.SpawnMedaforceAt(result.Attacker);
            if (result.Hit) _battlefield.SpawnExplosionAt(result.Target);
        }
        else if (result.UsingPart?.Action == ActionType.Melee)
        {
            _battlefield.TriggerMeleeDash(result.Attacker, result.Target);
            if (result.Hit) _battlefield.SpawnImpactAt(result.Target);
        }
        else if (result.UsingPart?.Action == ActionType.Shooting)
        {
            _battlefield.SpawnProjectileAt(result.Attacker, result.Target);
            if (result.Hit) _battlefield.SpawnImpactAt(result.Target);
        }
        else if (result.UsingPart?.Action == ActionType.Support)
        {
            if (result.HealAmount > 0)
                _battlefield.SpawnHealAt(result.Target);
        }

        if (result.Hit && result.Damage > 0)
        {
            _battlefield.SpawnDamagePopupAt(result.Target, result.Damage, result.Critical);
            if (result.TargetKnockedOut)
                _battlefield.SpawnExplosionAt(result.Target);
            else if (result.PartDestroyed)
                _battlefield.TriggerScreenShake();
        }
    }

    private float GetAnimDelay(ActionResult result)
    {
        if (result.IsMedaforce) return 0.9f;
        if (result.TargetKnockedOut) return 0.8f;
        if (result.UsingPart?.Action == ActionType.Melee) return 0.65f;
        if (!result.Hit) return 0.3f;
        return 0.45f;
    }

    // ═══════════════════════════════════════════════════════════
    //  BATTLE END CHECK
    // ═══════════════════════════════════════════════════════════

    private bool CheckBattleEnd()
    {
        if (Run == null) return false;

        bool allEnemiesDead = EnemySquad.All(e => e.IsKnockedOut);
        bool allPlayersDead = PlayerSquad.All(p => p.IsKnockedOut);

        if (allEnemiesDead) { WinBattle(); return true; }
        if (allPlayersDead) { LoseBattle(); return true; }

        // Log KOs
        foreach (var e in EnemySquad.Where(e => e.IsKnockedOut && e.ChargeGauge > -1))
        {
            AddLog($"{e.Name} FUNCTION CEASED!");
            e.ChargeGauge = -1;
        }
        foreach (var p in PlayerSquad.Where(p => p.IsKnockedOut && p.ChargeGauge > -1))
        {
            AddLog($"{p.Name} FUNCTION CEASED!");
            p.ChargeGauge = -1;
        }

        return false;
    }

    private void WinBattle()
    {
        Run!.Wins++;
        Phase = BattlePhase.BattleOver;

        _pendingXp = 20 + Run.Floor * 5;
        foreach (var m in Run.Squad.Where(m => !m.IsKnockedOut))
        {
            bool leveled = m.Medal.GainXp(_pendingXp);
            if (leveled) AddLog($"{m.Name}'s medal leveled up to {m.Medal.Level}!");
        }

        _pendingSpoil = PartCatalog.RandomPartReward(Run.Floor);
        Run.SpareParts.Add(_pendingSpoil);

        _pendingCredits = 30 + Run.Floor * 10;
        Run.Credits += _pendingCredits;

        AddLog($"-- VICTORY! Won {_pendingSpoil.Name}! +{_pendingCredits} credits --");
    }

    private void LoseBattle()
    {
        Run!.Losses++;
        Phase = BattlePhase.BattleOver;
        AddLog("-- DEFEAT! All Medabots destroyed! --");
        if (Run.IsGameOver) AddLog("GAME OVER");
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    private List<Medabot> GetCurrentTargetList()
    {
        if (_selectedPart?.Action == ActionType.Support)
            return PlayerSquad.Where(m => !m.IsKnockedOut).ToList();
        return EnemySquad.Where(m => !m.IsKnockedOut).ToList();
    }

    private void AddLog(string msg)
    {
        _log.Add(msg);
        if (_log.Count > 60) _log.RemoveAt(0);
    }

    // ═══════════════════════════════════════════════════════════
    //  RENDERING
    // ═══════════════════════════════════════════════════════════

    public override void Render(SpriteBatch sb, int sw, int sh) { }

    public void RenderWithInput(SpriteBatch sb, int sw, int sh, MouseState mouse, MouseState prevMouse)
    {
        int arenaH = 280;
        int bottomH = sh - arenaH;
        int sideW = 155;

        // ── Battlefield ──
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        _battlefield.DrawSquadBattle(sb, sw, arenaH, PlayerSquad, EnemySquad);
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // ── Left panel — Player squad stats ──
        DrawSquadPanel(sb, 0, 0, sideW, arenaH, PlayerSquad, true);

        // ── Right panel — Enemy squad stats ──
        DrawSquadPanel(sb, sw - sideW, 0, sideW, arenaH, EnemySquad, false);

        // ── Bottom area ──
        Draw.FillRect(sb, new Rectangle(0, arenaH, sw, bottomH), new Color(0x0A, 0x0C, 0x15));
        Draw.FillRect(sb, new Rectangle(0, arenaH, sw, 2), Color.White * 0.2f);

        switch (Phase)
        {
            case BattlePhase.ActionMenu:
                DrawActionMenu(sb, 10, arenaH + 5, 280, bottomH - 10);
                DrawActiveInfo(sb, 300, arenaH + 5, sw / 2 - 310, bottomH - 10);
                break;
            case BattlePhase.TargetBotSelect:
                DrawTargetBotSelect(sb, 10, arenaH + 5, sw / 2, bottomH - 10);
                break;
            case BattlePhase.TargetPartSelect:
                DrawTargetPartSelect(sb, 10, arenaH + 5, sw / 2, bottomH - 10);
                break;
            case BattlePhase.Charging:
                DrawChargingStatus(sb, 10, arenaH + 10);
                break;
            case BattlePhase.Executing:
                Font.DrawString(sb, "EXECUTING...", new Vector2(10, arenaH + 10), Color.Yellow, 2);
                break;
            case BattlePhase.BattleOver:
                DrawBattleOverPanel(sb, 10, arenaH + 10, sw - 20, bottomH - 15);
                break;
        }

        // ── Battle log (right half of bottom) ──
        DrawLogPanel(sb, sw / 2 + 10, arenaH + 5, sw / 2 - 20, bottomH - 10);
    }

    // ────── SQUAD STATUS PANELS ──────

    private void DrawSquadPanel(SpriteBatch sb, int x, int y, int w, int h,
        List<Medabot> squad, bool isPlayer)
    {
        Draw.FillRect(sb, new Rectangle(x, y, w, h), new Color(0x0A, 0x0C, 0x15, 200));
        Draw.DrawRect(sb, new Rectangle(x, y, w, h), Color.White * 0.15f);

        int py = y + 4;
        var headerColor = isPlayer ? new Color(0x58, 0xA6, 0xFF) : new Color(0xFF, 0x70, 0x70);
        Font.DrawString(sb, isPlayer ? "YOUR SQUAD" : "ENEMY SQUAD",
            new Vector2(x + 6, py), headerColor, 2);
        py += 20;

        foreach (var bot in squad)
        {
            var nameColor = bot.IsKnockedOut ? Color.Red * 0.5f
                : (bot == _activeBot ? Color.Yellow : Color.White * 0.9f);
            string koTag = bot.IsKnockedOut ? " [KO]" : "";
            string defTag = bot.IsDefending ? " [DEF]" : "";
            Font.DrawString(sb, $"{bot.Name}{koTag}{defTag}", new Vector2(x + 6, py), nameColor, 1);
            py += 11;

            if (!bot.IsKnockedOut)
            {
                // Charge gauge
                float chargePct = bot.ChargeGauge < 0 ? 0 : (float)(bot.ChargeGauge / MaxCharge);
                var chargeColor = chargePct >= 1.0f ? Color.Yellow
                    : Color.Lerp(Color.DarkGray, Color.Cyan, chargePct);
                Draw.DrawBar(sb, new Rectangle(x + 6, py, w - 16, 5), chargePct,
                    chargeColor, new Color(16, 16, 24), Color.White * 0.3f);
                py += 7;

                // Part health mini-bars
                foreach (var part in bot.AllParts)
                {
                    var barColor = part.IsDestroyed ? Color.Red * 0.3f
                        : Color.Lerp(Color.Red, Color.Green, (float)part.ArmorPercent);
                    string label = part.Slot switch
                    {
                        PartSlot.Head => "HD",
                        PartSlot.RightArm => "RA",
                        PartSlot.LeftArm => "LA",
                        PartSlot.Legs => "LG",
                        _ => "??"
                    };
                    Font.DrawString(sb, label, new Vector2(x + 6, py), Color.White * 0.4f, 1);
                    Draw.DrawBar(sb, new Rectangle(x + 24, py, w - 34, 4),
                        (float)part.ArmorPercent, barColor, new Color(16, 16, 24), Color.White * 0.2f);
                    py += 7;
                }

                // Medaforce indicator
                float mfPct = bot.Medal.MaxMedaforce > 0
                    ? (float)bot.Medal.MedaforceCharge / bot.Medal.MaxMedaforce : 0;
                Font.DrawString(sb, "MF", new Vector2(x + 6, py), new Color(0x80, 0x80, 0xFF) * 0.6f, 1);
                Draw.DrawBar(sb, new Rectangle(x + 24, py, w - 34, 4), mfPct,
                    new Color(0x80, 0x80, 0xFF), new Color(16, 16, 32), Color.White * 0.2f);
                py += 8;
            }
            py += 4;
        }
    }

    // ────── ACTION MENU ──────

    private void DrawActionMenu(SpriteBatch sb, int x, int y, int w, int h)
    {
        if (_activeBot == null) return;

        Font.DrawString(sb, $"{_activeBot.Name.ToUpperInvariant()}'S TURN:",
            new Vector2(x, y), Color.Yellow, 2);

        int by = y + 24;
        for (int i = 0; i < _menuOptions.Length; i++)
        {
            bool selected = i == _menuIndex;
            bool available = IsMenuOptionAvailable(i);
            var color = !available ? Color.White * 0.2f
                : selected ? Color.White : Color.White * 0.5f;
            string prefix = selected ? "> " : "  ";
            string extra = GetMenuOptionExtra(i);
            Font.DrawString(sb, $"{prefix}{_menuOptions[i]}{extra}", new Vector2(x, by), color, 2);
            by += 18;
        }

        by += 8;
        Font.DrawString(sb, "UP/DOWN = SELECT  ENTER = CONFIRM",
            new Vector2(x, by), Color.White * 0.3f, 1);
    }

    private bool IsMenuOptionAvailable(int index)
    {
        if (_activeBot == null) return false;
        return index switch
        {
            0 => !_activeBot.RightArm.IsDestroyed,
            1 => !_activeBot.LeftArm.IsDestroyed,
            2 => !_activeBot.Head.IsDestroyed && _activeBot.Head.Action != ActionType.None
                 && _activeBot.Head.RemainingUses > 0,
            3 => _activeBot.Medal.CanUseMedaforce,
            4 => true,
            _ => false
        };
    }

    private string GetMenuOptionExtra(int index)
    {
        if (_activeBot == null) return "";
        return index switch
        {
            0 => $" ({_activeBot.RightArm.Name} P:{_activeBot.RightArm.Power})",
            1 => $" ({_activeBot.LeftArm.Name} P:{_activeBot.LeftArm.Power})",
            2 when _activeBot.Head.Action != ActionType.None
                => $" ({_activeBot.Head.Name} x{_activeBot.Head.RemainingUses})",
            3 when _activeBot.Medal.CanUseMedaforce => " READY!",
            3 => $" ({(int)(_activeBot.Medal.MedaforceCharge * 100.0 / _activeBot.Medal.MaxMedaforce)}%)",
            _ => ""
        };
    }

    private void DrawActiveInfo(SpriteBatch sb, int x, int y, int w, int h)
    {
        if (_activeBot == null) return;
        Font.DrawString(sb, $"SPD:{_activeBot.EffectiveSpeed} EVA:{_activeBot.EffectiveEvasion}",
            new Vector2(x, y), Color.White * 0.5f, 1);
        Font.DrawString(sb, $"MEDAL: {_activeBot.Medal.Type} LV{_activeBot.Medal.Level}",
            new Vector2(x, y + 12), Color.White * 0.5f, 1);
    }

    // ────── TARGET BOT SELECT ──────

    private void DrawTargetBotSelect(SpriteBatch sb, int x, int y, int w, int h)
    {
        var targets = GetCurrentTargetList();
        Font.DrawString(sb, "SELECT TARGET:", new Vector2(x, y), Color.White, 2);

        int by = y + 24;
        for (int i = 0; i < targets.Count; i++)
        {
            bool selected = i == _targetBotIndex;
            var color = selected ? Color.Yellow : Color.White * 0.5f;
            string prefix = selected ? "> " : "  ";
            var t = targets[i];
            Font.DrawString(sb, $"{prefix}{t.Name} (HP:{(int)(t.HealthPercent * 100)}%)",
                new Vector2(x, by), color, 2);
            by += 18;
        }

        by += 8;
        Font.DrawString(sb, "UP/DOWN = SELECT  ENTER = CONFIRM  ESC = BACK",
            new Vector2(x, by), Color.White * 0.3f, 1);
    }

    // ────── TARGET PART SELECT ──────

    private void DrawTargetPartSelect(SpriteBatch sb, int x, int y, int w, int h)
    {
        var targets = GetCurrentTargetList();
        if (targets.Count == 0) return;
        var target = targets[Math.Clamp(_targetBotIndex, 0, targets.Count - 1)];

        Font.DrawString(sb, $"TARGET PART ON {target.Name.ToUpperInvariant()}:",
            new Vector2(x, y), Color.White, 2);

        int by = y + 24;
        for (int i = 0; i < _targetSlots.Length; i++)
        {
            var slot = _targetSlots[i];
            var part = target.GetPart(slot);
            bool selected = i == _targetPartIndex;
            bool destroyed = part?.IsDestroyed ?? true;

            var color = destroyed ? Color.Red * 0.4f
                : selected ? Color.Yellow : Color.White * 0.5f;
            string prefix = selected ? "> " : "  ";
            string status = destroyed ? "DESTROYED"
                : $"{part!.Armor}/{part.MaxArmor} ({part.Name})";
            Font.DrawString(sb, $"{prefix}{slot}: {status}",
                new Vector2(x, by), color, 2);
            by += 18;
        }

        by += 8;
        Font.DrawString(sb, "UP/DOWN = SELECT  ENTER = CONFIRM  ESC = BACK",
            new Vector2(x, by), Color.White * 0.3f, 1);
    }

    // ────── CHARGING STATUS ──────

    private void DrawChargingStatus(SpriteBatch sb, int x, int y)
    {
        Font.DrawString(sb, "CHARGING...", new Vector2(x, y), Color.White * 0.6f, 2);

        int by = y + 22;
        foreach (var bot in PlayerSquad.Concat(EnemySquad))
        {
            if (bot.IsKnockedOut) continue;
            bool isP = bot.IsPlayerOwned;
            var color = isP ? new Color(0x58, 0xA6, 0xFF) : new Color(0xFF, 0x70, 0x70);
            float pct = bot.ChargeGauge < 0 ? 0 : (float)(bot.ChargeGauge / MaxCharge);
            string name = bot.Name.Length > 10 ? bot.Name[..10] : bot.Name;
            Font.DrawString(sb, $"{name}: {(int)Math.Max(0, bot.ChargeGauge)}%",
                new Vector2(x, by), color * 0.6f, 1);
            by += 12;
        }
    }

    // ────── BATTLE OVER ──────

    private void DrawBattleOverPanel(SpriteBatch sb, int x, int y, int w, int h)
    {
        bool won = EnemySquad.All(e => e.IsKnockedOut);

        if (won)
        {
            Font.DrawString(sb, "VICTORY!", new Vector2(x, y), Color.Yellow, 3);
            int iy = y + 32;
            if (_pendingSpoil != null)
                Font.DrawString(sb, $"WON: {_pendingSpoil.Name} ({_pendingSpoil.Slot})",
                    new Vector2(x, iy), new Color(0x60, 0xFF, 0x60), 2);
            iy += 20;
            Font.DrawString(sb, $"+{_pendingCredits} CREDITS  +{_pendingXp} XP",
                new Vector2(x, iy), Color.White * 0.8f, 1);
            iy += 16;
            Font.DrawString(sb, "PRESS ENTER TO CONTINUE", new Vector2(x, iy), Color.White * 0.5f, 1);
        }
        else
        {
            Font.DrawString(sb, "DEFEAT!", new Vector2(x, y), Color.Red, 3);
            Font.DrawString(sb, "PRESS ENTER TO CONTINUE", new Vector2(x, y + 32), Color.White * 0.5f, 1);
        }
    }

    // ────── LOG ──────

    private void DrawLogPanel(SpriteBatch sb, int x, int y, int w, int h)
    {
        Draw.FillRect(sb, new Rectangle(x - 5, y - 2, w + 10, h + 4), new Color(0, 0, 0, 80));
        int ly = y;
        int maxLines = h / 12;
        int start = Math.Max(0, _log.Count - maxLines);
        for (int i = start; i < _log.Count; i++)
        {
            float alpha = 0.3f + 0.7f * ((float)(i - start) / maxLines);
            Font.DrawString(sb, _log[i], new Vector2(x, ly), Color.White * alpha, 1);
            ly += 12;
        }
    }
}
