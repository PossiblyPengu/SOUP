namespace MechaRogue.ViewModels;

using MechaRogue.Models;
using MechaRogue.Services;
using System.Windows.Threading;

/// <summary>
/// Main orchestrator ViewModel — real-time timer-based battle like GBA Medabots.
/// Each Medabot has a charge gauge that fills based on speed. When full, it acts.
/// Player picks action when their gauge fills; AI acts automatically.
/// </summary>
public partial class GameViewModel : ObservableObject
{
    private readonly BattleEngine _engine = new();
    private readonly Random _rng = new();

    // ── Run State ─────────────────────────────────────────
    [ObservableProperty] private RunState? _run;
    [ObservableProperty] private string _screenState = "Title";

    // ── Battle State ──────────────────────────────────────
    [ObservableProperty] private BattlePhase _battlePhase;
    [ObservableProperty] private Medabot? _playerActiveMech;
    [ObservableProperty] private Medabot? _enemyActiveMech;
    [ObservableProperty] private List<Medabot> _enemySquad = [];
    [ObservableProperty] private int _turnNumber;
    [ObservableProperty] private ObservableCollection<string> _battleLog = [];
    [ObservableProperty] private bool _isPlayerTurn;
    [ObservableProperty] private MedaPart? _selectedPart;
    [ObservableProperty] private PartSlot _selectedTargetSlot = PartSlot.Head;

    // ── Charge Gauges (0–100) ─────────────────────────────
    [ObservableProperty] private double _playerCharge;
    [ObservableProperty] private double _enemyCharge;
    private const double MaxCharge = 100.0;
    private const double BaseChargeRate = 0.6;  // multiplied by speed per tick

    // ── Real-time battle timer ────────────────────────────
    private DispatcherTimer? _battleTimer;
    private const int BattleTickMs = 50;  // 20 ticks/sec
    private bool _animationLock;          // true while an attack animation plays

    // ── Shop State ────────────────────────────────────────
    [ObservableProperty] private List<MedaPart> _shopParts = [];

    // ── Map State ─────────────────────────────────────────
    [ObservableProperty] private List<RunNode> _availableNodes = [];

    // ── UI Notifications ──────────────────────────────────
    public event Action<ActionResult>? OnActionResolved;
    public event Action<string>? OnScreenChanged;

    // ════════════════════════════════════════════════════════
    //  GAME START
    // ════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartNewRun()
    {
        var starter = PartCatalog.MakeMetabee();
        starter.IsPlayerOwned = true;
        starter.IsLeader = true;
        starter.Name = "Metabee";

        Run = new RunState
        {
            Floor = 1,
            Credits = 120,
            Squad = [starter],
            Map = MapGenerator.Generate(15)
        };

        RefreshAvailableNodes();
        ScreenState = "Map";
        OnScreenChanged?.Invoke("Map");
    }

    [RelayCommand]
    private void StartWithRokusho()
    {
        var starter = PartCatalog.MakeRokusho();
        starter.IsPlayerOwned = true;
        starter.IsLeader = true;

        Run = new RunState
        {
            Floor = 1,
            Credits = 120,
            Squad = [starter],
            Map = MapGenerator.Generate(15)
        };

        RefreshAvailableNodes();
        ScreenState = "Map";
        OnScreenChanged?.Invoke("Map");
    }

    private void RefreshAvailableNodes()
    {
        if (Run == null) return;
        AvailableNodes = Run.Map.Where(n => n.Depth == Run.Floor && !n.Visited).ToList();
    }

    // ════════════════════════════════════════════════════════
    //  MAP NAVIGATION
    // ════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectNode(RunNode? node)
    {
        if (node == null || Run == null) return;

        node.Visited = true;
        node.IsCurrent = true;
        Run.CurrentNodeId = node.Id;

        switch (node.Type)
        {
            case NodeType.Battle:
                StartBattle(isBoss: false, isElite: false);
                break;
            case NodeType.EliteBattle:
                StartBattle(isBoss: false, isElite: true);
                break;
            case NodeType.Boss:
                StartBattle(isBoss: true, isElite: false);
                break;
            case NodeType.Shop:
                EnterShop();
                break;
            case NodeType.Rest:
                EnterRest();
                break;
            case NodeType.Event:
                EnterEvent();
                break;
        }
    }

    private void AdvanceFloor()
    {
        if (Run == null) return;
        Run.Floor++;

        if (Run.Floor > Run.MaxFloors)
        {
            ScreenState = "Victory";
            OnScreenChanged?.Invoke("Victory");
            return;
        }

        RefreshAvailableNodes();
        ScreenState = "Map";
        OnScreenChanged?.Invoke("Map");
    }

    // ════════════════════════════════════════════════════════
    //  BATTLE — Real-time timer-based (GBA Medabots style)
    // ════════════════════════════════════════════════════════

    private void StartBattle(bool isBoss, bool isElite)
    {
        if (Run == null) return;

        int floor = Run.Floor;
        int enemyCount = isBoss ? 1 : (isElite ? _rng.Next(1, 3) : 1);

        EnemySquad = isBoss
            ? [PartCatalog.RandomBoss(floor)]
            : PartCatalog.RandomEnemySquad(floor, enemyCount);

        PlayerActiveMech = Run.Squad.FirstOrDefault(m => !m.IsKnockedOut);
        EnemyActiveMech = EnemySquad.FirstOrDefault(e => !e.IsKnockedOut);
        TurnNumber = 0;
        BattleLog = [];
        PlayerCharge = 0;
        EnemyCharge = 0;
        _animationLock = false;

        BattlePhase = BattlePhase.Charging;
        IsPlayerTurn = false;
        SelectedPart = PlayerActiveMech?.UsableParts.FirstOrDefault();

        ScreenState = "Battle";
        OnScreenChanged?.Invoke("Battle");
        AddLog($"── Robattle Start! Floor {floor} ──");
        AddLog($"Your {PlayerActiveMech?.Name} vs {EnemyActiveMech?.Name}!");
        AddLog("Charge gauges filling... fastest bot acts first!");

        // Start the real-time battle loop
        StartBattleTimer();
    }

    private void StartBattleTimer()
    {
        StopBattleTimer();
        _battleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(BattleTickMs) };
        _battleTimer.Tick += BattleTick;
        _battleTimer.Start();
    }

    private void StopBattleTimer()
    {
        if (_battleTimer != null)
        {
            _battleTimer.Stop();
            _battleTimer.Tick -= BattleTick;
            _battleTimer = null;
        }
    }

    /// <summary>
    /// Core real-time battle tick — runs 20x/sec.
    /// Fills charge gauges based on each bot's speed.
    /// When a gauge fills, that bot gets to act.
    /// </summary>
    private void BattleTick(object? sender, EventArgs e)
    {
        if (BattlePhase == BattlePhase.BattleOver) { StopBattleTimer(); return; }
        if (BattlePhase == BattlePhase.ActionSelect) return; // paused for player input
        if (_animationLock) return;  // wait for animation to finish
        if (PlayerActiveMech == null || EnemyActiveMech == null) return;

        // ── Fill gauges ──
        double playerSpeed = Math.Max(1, PlayerActiveMech.EffectiveSpeed);
        double enemySpeed = Math.Max(1, EnemyActiveMech.EffectiveSpeed);

        PlayerCharge = Math.Min(MaxCharge, PlayerCharge + playerSpeed * BaseChargeRate);
        EnemyCharge = Math.Min(MaxCharge, EnemyCharge + enemySpeed * BaseChargeRate);

        // ── Check who acts first ──
        if (EnemyCharge >= MaxCharge && PlayerCharge >= MaxCharge)
        {
            // Both full: faster bot goes first
            if (enemySpeed >= playerSpeed)
                ExecuteEnemyAction();
            else
                PauseForPlayerAction();
        }
        else if (EnemyCharge >= MaxCharge)
        {
            ExecuteEnemyAction();
        }
        else if (PlayerCharge >= MaxCharge)
        {
            PauseForPlayerAction();
        }
    }

    /// <summary>
    /// Pause the timer and let the player pick an action.
    /// </summary>
    private void PauseForPlayerAction()
    {
        BattlePhase = BattlePhase.ActionSelect;
        IsPlayerTurn = true;

        if (SelectedPart == null || SelectedPart.IsDestroyed)
            SelectedPart = PlayerActiveMech?.UsableParts.FirstOrDefault();
    }

    /// <summary>
    /// Enemy's gauge is full — AI picks and executes immediately.
    /// </summary>
    private void ExecuteEnemyAction()
    {
        if (Run == null || EnemyActiveMech == null || PlayerActiveMech == null) return;

        EnemyCharge = 0;
        TurnNumber++;
        _animationLock = true;
        BattlePhase = BattlePhase.Executing;

        var action = _engine.GenerateAiAction(EnemyActiveMech, Run.Squad.Where(s => !s.IsKnockedOut).ToList());
        var result = _engine.ResolveAction(action);

        AddLog(result.Narration);
        OnActionResolved?.Invoke(result);

        int animDelay = GetAnimationDelay(result);
        DelayAction(animDelay, () =>
        {
            _animationLock = false;
            BattlePhase = BattlePhase.Charging;
            CheckBattleEnd();
        });
    }

    /// <summary>
    /// Player confirmed their action — execute it.
    /// </summary>
    private void ExecutePlayerAction(BattleAction action)
    {
        if (PlayerActiveMech == null || EnemyActiveMech == null) return;

        PlayerCharge = 0;
        TurnNumber++;
        _animationLock = true;
        BattlePhase = BattlePhase.Executing;
        IsPlayerTurn = false;

        var result = _engine.ResolveAction(action);
        AddLog(result.Narration);
        OnActionResolved?.Invoke(result);

        int animDelay = GetAnimationDelay(result);
        DelayAction(animDelay, () =>
        {
            _animationLock = false;
            BattlePhase = BattlePhase.Charging;

            if (SelectedPart?.IsDestroyed == true)
                SelectedPart = PlayerActiveMech?.UsableParts.FirstOrDefault();

            CheckBattleEnd();
        });
    }

    private int GetAnimationDelay(ActionResult result)
    {
        if (result.IsMedaforce) return 900;
        if (result.TargetKnockedOut) return 800;
        if (result.UsingPart?.Action == ActionType.Melee) return 650;
        if (!result.Hit) return 300;
        return 450;
    }

    [RelayCommand]
    private void SelectAttackPart(MedaPart? part)
    {
        if (part == null) return;
        SelectedPart = part;
    }

    [RelayCommand]
    private void SelectTarget(string? slotName)
    {
        if (Enum.TryParse<PartSlot>(slotName, out var slot))
            SelectedTargetSlot = slot;
    }

    [RelayCommand]
    private void ExecuteAttack()
    {
        if (Run == null || PlayerActiveMech == null || EnemyActiveMech == null) return;
        if (BattlePhase != BattlePhase.ActionSelect || !IsPlayerTurn) return;
        if (SelectedPart == null) return;

        var action = new BattleAction
        {
            Attacker = PlayerActiveMech,
            UsingPart = SelectedPart,
            Target = EnemyActiveMech,
            TargetedSlot = SelectedTargetSlot,
            Priority = PlayerActiveMech.EffectiveSpeed
        };

        ExecutePlayerAction(action);
    }

    [RelayCommand]
    private void UseMedaforce()
    {
        if (Run == null || PlayerActiveMech == null || EnemyActiveMech == null) return;
        if (!PlayerActiveMech.Medal.CanUseMedaforce) return;
        if (BattlePhase != BattlePhase.ActionSelect || !IsPlayerTurn) return;

        var mfAttacks = PlayerActiveMech.Medal.GetAvailableAttacks();
        var mfAtk = mfAttacks.LastOrDefault();
        if (mfAtk == null) return;

        var action = new BattleAction
        {
            Attacker = PlayerActiveMech,
            UsingPart = PlayerActiveMech.Head,
            Target = EnemyActiveMech,
            TargetedSlot = SelectedTargetSlot,
            IsMedaforce = true,
            MedaforceAttack = mfAtk,
            Priority = PlayerActiveMech.EffectiveSpeed + 20
        };

        ExecutePlayerAction(action);
    }

    private void CheckBattleEnd()
    {
        if (Run == null) return;

        if (EnemyActiveMech?.IsKnockedOut == true)
        {
            var nextEnemy = EnemySquad.FirstOrDefault(e => !e.IsKnockedOut && e != EnemyActiveMech);
            if (nextEnemy != null)
            {
                EnemyActiveMech = nextEnemy;
                EnemyCharge = 0;
                AddLog($"Next opponent: {nextEnemy.Name}!");
                OnScreenChanged?.Invoke("Battle");
            }
            else
            {
                WinBattle();
                return;
            }
        }

        if (PlayerActiveMech?.IsKnockedOut == true)
        {
            var nextPlayer = Run.Squad.FirstOrDefault(m => !m.IsKnockedOut && m != PlayerActiveMech);
            if (nextPlayer != null)
            {
                PlayerActiveMech = nextPlayer;
                PlayerCharge = 0;
                SelectedPart = nextPlayer.UsableParts.FirstOrDefault();
                AddLog($"Next Medabot: {nextPlayer.Name}!");
                OnScreenChanged?.Invoke("Battle");
            }
            else
            {
                LoseBattle();
                return;
            }
        }
    }

    private void WinBattle()
    {
        if (Run == null) return;

        StopBattleTimer();
        Run.Wins++;
        BattlePhase = BattlePhase.BattleOver;
        IsPlayerTurn = false;

        int xp = 20 + Run.Floor * 5;
        foreach (var m in Run.Squad.Where(m => !m.IsKnockedOut))
        {
            bool leveled = m.Medal.GainXp(xp);
            if (leveled) AddLog($"🏅 {m.Name}'s medal leveled up to {m.Medal.Level}!");
        }

        var spoil = PartCatalog.RandomPartReward(Run.Floor);
        Run.SpareParts.Add(spoil);
        AddLog($"── VICTORY! Won {spoil.Name} ({spoil.Slot})! ──");

        int credits = 30 + Run.Floor * 10;
        Run.Credits += credits;
        AddLog($"Earned {credits} credits. (Total: {Run.Credits})");
    }

    private void LoseBattle()
    {
        if (Run == null) return;

        StopBattleTimer();
        Run.Losses++;
        BattlePhase = BattlePhase.BattleOver;
        IsPlayerTurn = false;
        AddLog("── DEFEAT! All Medabots destroyed! ──");

        if (Run.IsGameOver)
        {
            ScreenState = "GameOver";
            OnScreenChanged?.Invoke("GameOver");
        }
    }

    [RelayCommand]
    private void ContinueAfterBattle()
    {
        if (Run == null) return;
        StopBattleTimer();
        foreach (var m in Run.Squad)
            m.RestHeal(0.25);
        AdvanceFloor();
    }

    // ════════════════════════════════════════════════════════
    //  SHOP
    // ════════════════════════════════════════════════════════

    private void EnterShop()
    {
        if (Run == null) return;
        ShopParts = PartCatalog.GetShopParts(Run.Floor, 4);
        ScreenState = "Shop";
        OnScreenChanged?.Invoke("Shop");
    }

    [RelayCommand]
    private void BuyPart(MedaPart? part)
    {
        if (part == null || Run == null) return;
        int cost = 30 + part.Tier * 20;
        if (Run.Credits < cost) return;

        Run.Credits -= cost;
        Run.SpareParts.Add(part.Clone());
        ShopParts = ShopParts.Where(p => p != part).ToList();
        OnPropertyChanged(nameof(Run));
    }

    [RelayCommand]
    private void LeaveShop() => AdvanceFloor();

    // ════════════════════════════════════════════════════════
    //  REST
    // ════════════════════════════════════════════════════════

    private void EnterRest()
    {
        ScreenState = "Rest";
        OnScreenChanged?.Invoke("Rest");
    }

    [RelayCommand]
    private void RestAndRepair()
    {
        if (Run == null) return;
        foreach (var m in Run.Squad)
            m.RestHeal(0.5);
        AdvanceFloor();
    }

    // ════════════════════════════════════════════════════════
    //  EVENT
    // ════════════════════════════════════════════════════════

    [ObservableProperty] private string _eventText = "";
    [ObservableProperty] private string _eventChoice1 = "";
    [ObservableProperty] private string _eventChoice2 = "";
    private int _eventType;

    private void EnterEvent()
    {
        if (Run == null) return;
        _eventType = _rng.Next(4);
        switch (_eventType)
        {
            case 0:
                EventText = "You find a damaged Medabot by the road. Its medal is still intact.";
                EventChoice1 = "Salvage parts (+random part)";
                EventChoice2 = "Recruit it (add to squad if < 3)";
                break;
            case 1:
                EventText = "A shady dealer offers a trade: one of your spare parts for credits.";
                EventChoice1 = "Trade a spare part (+80 credits)";
                EventChoice2 = "Decline";
                break;
            case 2:
                EventText = "A mysterious technician offers to upgrade your Medabot's medal.";
                EventChoice1 = "Accept (+XP to active medal)";
                EventChoice2 = "Decline";
                break;
            default:
                EventText = "A Medabot parts vending machine! Insert credits for a random part.";
                EventChoice1 = "Insert 50 credits";
                EventChoice2 = "Walk away";
                break;
        }
        ScreenState = "Event";
        OnScreenChanged?.Invoke("Event");
    }

    [RelayCommand]
    private void EventOption1()
    {
        if (Run == null) return;
        switch (_eventType)
        {
            case 0:
                Run.SpareParts.Add(PartCatalog.RandomPartReward(Run.Floor));
                break;
            case 1:
                if (Run.SpareParts.Count > 0)
                {
                    Run.SpareParts.RemoveAt(Run.SpareParts.Count - 1);
                    Run.Credits += 80;
                }
                break;
            case 2:
                Run.Squad.FirstOrDefault(m => !m.IsKnockedOut)?.Medal.GainXp(40);
                break;
            default:
                if (Run.Credits >= 50)
                {
                    Run.Credits -= 50;
                    Run.SpareParts.Add(PartCatalog.RandomPartReward(Run.Floor + 1));
                }
                break;
        }
        AdvanceFloor();
    }

    [RelayCommand]
    private void EventOption2()
    {
        if (Run == null) return;
        if (_eventType == 0 && Run.Squad.Count < 3)
        {
            var recruit = PartCatalog.RandomEnemy(Math.Max(1, Run.Floor - 1));
            recruit.IsPlayerOwned = true;
            recruit.IsLeader = false;
            recruit.FullRestore();
            Run.Squad.Add(recruit);
        }
        AdvanceFloor();
    }

    // ════════════════════════════════════════════════════════
    //  EQUIP / SWAP PARTS
    // ════════════════════════════════════════════════════════

    [RelayCommand]
    private void EquipPart(MedaPart? part)
    {
        if (part == null || Run == null) return;

        var mech = PlayerActiveMech ?? Run.Squad.FirstOrDefault();
        if (mech == null) return;

        var old = mech.GetPart(part.Slot);

        switch (part.Slot)
        {
            case PartSlot.Head: mech.Head = part; break;
            case PartSlot.RightArm: mech.RightArm = part; break;
            case PartSlot.LeftArm: mech.LeftArm = part; break;
            case PartSlot.Legs: mech.Legs = part; break;
        }

        Run.SpareParts.Remove(part);
        if (old != null) Run.SpareParts.Add(old);

        OnPropertyChanged(nameof(PlayerActiveMech));
        OnPropertyChanged(nameof(Run));
    }

    // ════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════

    [RelayCommand]
    private void ReturnToTitle()
    {
        StopBattleTimer();
        ScreenState = "Title";
        OnScreenChanged?.Invoke("Title");
    }

    private void AddLog(string msg)
    {
        BattleLog.Add(msg);
        while (BattleLog.Count > 100)
            BattleLog.RemoveAt(0);
    }

    private void DelayAction(int ms, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }
}
