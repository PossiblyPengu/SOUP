using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechaRogue.Models;
using MechaRogue.Services;

namespace MechaRogue.ViewModels;

/// <summary>
/// Screen states for navigation.
/// </summary>
public enum GameScreen
{
    Title,
    Battle,
    Victory,
    GameOver
}

/// <summary>
/// Main ViewModel for the battle screen.
/// </summary>
public partial class BattleViewModel : ObservableObject
{
    private readonly BattleService _battleService = new();
    private readonly SoundService _soundService = new();
    private readonly Random _rng = new();
    private readonly DispatcherTimer _autoBattleTimer;
    
    [ObservableProperty]
    private GameScreen _currentScreen = GameScreen.Title;
    
    [ObservableProperty]
    private bool _isFirstRun = true;
    
    [ObservableProperty]
    private bool _showTutorial;
    
    [ObservableProperty]
    private int _tutorialStep;
    
    [ObservableProperty]
    private bool _autoBattleEnabled;
    
    [ObservableProperty]
    private int _autoBattleSpeed = 500; // ms between actions
    
    [ObservableProperty]
    private ObservableCollection<MechViewModel> _playerSquad = [];
    
    [ObservableProperty]
    private ObservableCollection<MechViewModel> _enemySquad = [];
    
    [ObservableProperty]
    private MechViewModel? _selectedPlayerMech;
    
    [ObservableProperty]
    private MechViewModel? _selectedTargetMech;
    
    [ObservableProperty]
    private ObservableCollection<string> _battleLog = [];
    
    [ObservableProperty]
    private bool _isPlayerTurn = true;
    
    [ObservableProperty]
    private bool _isBattleOver;
    
    [ObservableProperty]
    private bool _playerWon;
    
    [ObservableProperty]
    private string _statusMessage = "Select your Mech and choose an action!";
    
    [ObservableProperty]
    private int _currentFloor = 1;
    
    [ObservableProperty]
    private int _maxFloors = 7;
    
    [ObservableProperty]
    private ObservableCollection<Part> _lootDrops = [];
    
    [ObservableProperty]
    private ObservableCollection<Part> _inventory = [];
    
    [ObservableProperty]
    private MetaProgression _progression = new();
    
    [ObservableProperty]
    private int _runMedals;
    
    [ObservableProperty]
    private bool _hasSavedRun;
    
    [ObservableProperty]
    private bool _showUpgradePanel;
    
    [ObservableProperty]
    private bool _isRunActive;
    
    // Computed stats from upgrades
    private int StartingSquadSize => 1 + Progression.GetUpgradeLevel("max_squad");
    private int StartingMedals => Progression.GetUpgradeLevel("starting_medals") * 50;
    private int RepairBonus => 20 + Progression.GetUpgradeLevel("repair_bonus") * 10;
    private int StartingMedaforce => Progression.GetUpgradeLevel("medaforce_start") * 20;
    private int LootBonus => Progression.GetUpgradeLevel("loot_bonus");
    private int DefenseBonus => Progression.GetUpgradeLevel("defense_bonus") * 3;
    private int CritBonus => Progression.GetUpgradeLevel("crit_bonus") * 5;
    
    public BattleViewModel()
    {
        LoadProgression();
        HasSavedRun = SaveService.HasSavedRun();
        IsFirstRun = Progression.TotalRuns == 0;
        
        // Setup auto-battle timer
        _autoBattleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoBattleSpeed)
        };
        _autoBattleTimer.Tick += OnAutoBattleTick;
    }
    
    partial void OnAutoBattleEnabledChanged(bool value)
    {
        if (value && IsPlayerTurn && !IsBattleOver && CurrentScreen == GameScreen.Battle)
        {
            _autoBattleTimer.Start();
        }
        else
        {
            _autoBattleTimer.Stop();
        }
    }
    
    partial void OnAutoBattleSpeedChanged(int value)
    {
        _autoBattleTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, value));
    }
    
    private void OnAutoBattleTick(object? sender, EventArgs e)
    {
        if (!AutoBattleEnabled || !IsPlayerTurn || IsBattleOver || CurrentScreen != GameScreen.Battle)
        {
            _autoBattleTimer.Stop();
            return;
        }
        
        ExecuteAutoBattleAction();
    }
    
    private void ExecuteAutoBattleAction()
    {
        // Select best operational mech
        SelectedPlayerMech = PlayerSquad
            .Where(p => p.IsOperational)
            .OrderByDescending(p => p.TotalAttack)
            .FirstOrDefault();
            
        if (SelectedPlayerMech == null) return;
        
        // Select target with lowest HP
        SelectedTargetMech = EnemySquad
            .Where(e => e.IsOperational)
            .OrderBy(e => e.Head?.CurrentDurability ?? 999)
            .FirstOrDefault();
            
        if (SelectedTargetMech == null) return;
        
        var mech = SelectedPlayerMech.Model;
        
        // AI: Use special if available and charged
        if (mech.Head?.SpecialAbility != null && 
            mech.MedaforceCharge >= mech.Head.SpecialCost &&
            !mech.Head.IsBroken)
        {
            UseSpecial();
            return;
        }
        
        // Attack with strongest non-broken arm
        var rightDamage = mech.RightArm?.IsBroken == false ? mech.RightArm.Attack : 0;
        var leftDamage = mech.LeftArm?.IsBroken == false ? mech.LeftArm.Attack : 0;
        
        if (rightDamage >= leftDamage && rightDamage > 0)
        {
            AttackRight();
        }
        else if (leftDamage > 0)
        {
            AttackLeft();
        }
        else
        {
            Defend();
        }
    }
    
    [RelayCommand]
    private void ShowTitleScreen()
    {
        CurrentScreen = GameScreen.Title;
        AutoBattleEnabled = false;
        _autoBattleTimer.Stop();
    }
    
    [RelayCommand]
    private void StartTutorial()
    {
        ShowTutorial = true;
        TutorialStep = 0;
    }
    
    [RelayCommand]
    private void NextTutorialStep()
    {
        TutorialStep++;
        if (TutorialStep > 5) // 6 tutorial steps (0-5)
        {
            ShowTutorial = false;
            IsFirstRun = false;
            Progression.TotalRuns = 0; // Will be incremented on first real run
            SaveProgression();
        }
    }
    
    [RelayCommand]
    private void SkipTutorial()
    {
        ShowTutorial = false;
        IsFirstRun = false;
        SaveProgression();
    }
    
    [RelayCommand]
    private void ToggleAutoBattle()
    {
        AutoBattleEnabled = !AutoBattleEnabled;
        _soundService.Play(SoundEffect.Select);
        AddLog(AutoBattleEnabled ? "Auto-Battle: ON" : "Auto-Battle: OFF");
    }
    
    [RelayCommand]
    private void SetAutoBattleSpeed(string? speed)
    {
        AutoBattleSpeed = speed switch
        {
            "slow" => 800,
            "normal" => 500,
            "fast" => 200,
            "turbo" => 50,
            _ => 500
        };
        _soundService.Play(SoundEffect.Select);
    }
    
    private void LoadProgression()
    {
        Progression = SaveService.LoadProgression();
    }
    
    private void SaveProgression()
    {
        SaveService.SaveProgression(Progression);
    }
    
    [RelayCommand]
    private void StartNewRun()
    {
        SaveService.DeleteRunState();
        Progression.TotalRuns++;
        SaveProgression();
        
        CurrentScreen = GameScreen.Battle;
        CurrentFloor = 1;
        IsBattleOver = false;
        PlayerWon = false;
        IsRunActive = true;
        AutoBattleEnabled = false;
        BattleLog.Clear();
        LootDrops.Clear();
        Inventory.Clear();
        RunMedals = StartingMedals;
        
        PlayerSquad.Clear();
        
        // Create starter mechs based on upgrade level
        var mechNames = new[] { "Metabee", "Rokusho", "Sumilidon" };
        for (int i = 0; i < Math.Min(StartingSquadSize, 3); i++)
        {
            var mech = CreateStarterMech(mechNames[i]);
            mech.MedaforceCharge = StartingMedaforce;
            PlayerSquad.Add(new MechViewModel(mech, isEnemy: false, floor: CurrentFloor));
        }
        
        SelectedPlayerMech = PlayerSquad.FirstOrDefault();
        
        _soundService.Play(SoundEffect.RunStart);
        AddLog($"=== NEW RUN STARTED ===");
        AddLog($"Squad Size: {PlayerSquad.Count} | Starting Medals: {RunMedals}");
        
        StartBattle();
        SaveRunState();
    }
    
    [RelayCommand]
    private void ContinueRun()
    {
        var savedRun = SaveService.LoadRunState();
        if (savedRun == null)
        {
            HasSavedRun = false;
            return;
        }
        
        CurrentScreen = GameScreen.Battle;
        CurrentFloor = savedRun.CurrentFloor;
        MaxFloors = savedRun.MaxFloors;
        RunMedals = savedRun.Medals;
        IsRunActive = true;
        IsBattleOver = false;
        PlayerWon = false;
        AutoBattleEnabled = false;
        BattleLog.Clear();
        LootDrops.Clear();
        
        PlayerSquad.Clear();
        foreach (var mechSave in savedRun.PlayerSquad)
        {
            PlayerSquad.Add(new MechViewModel(mechSave.ToMech(), isEnemy: false, floor: CurrentFloor));
        }
        
        Inventory.Clear();
        foreach (var partSave in savedRun.Inventory)
        {
            var part = partSave.ToPart();
            if (part != null) Inventory.Add(part);
        }
        
        SelectedPlayerMech = PlayerSquad.FirstOrDefault(p => p.IsOperational);
        
        AddLog($"=== RUN CONTINUED ===");
        AddLog($"Floor {CurrentFloor} | Medals: {RunMedals}");
        
        StartBattle();
    }
    
    private Mech CreateStarterMech(string name)
    {
        var mech = new Mech { Name = name };
        mech.EquipPart(PartCatalog.GetById("head_horn")!.Clone());
        mech.EquipPart(PartCatalog.GetById("rarm_punch")!.Clone());
        mech.EquipPart(PartCatalog.GetById("larm_shield")!.Clone());
        mech.EquipPart(PartCatalog.GetById("legs_bipedal")!.Clone());
        
        // Apply defense bonus from upgrades
        if (DefenseBonus > 0)
        {
            foreach (var part in mech.GetParts())
            {
                // Can't modify init-only, so we just note this in the log
            }
        }
        
        return mech;
    }
    
    private void SaveRunState()
    {
        if (!IsRunActive) return;
        
        var state = new RunStateSave
        {
            CurrentFloor = CurrentFloor,
            MaxFloors = MaxFloors,
            Medals = RunMedals,
            PlayerSquad = PlayerSquad.Select(m => MechSave.FromMech(m.Model)).ToList(),
            Inventory = Inventory.Select(PartSave.FromPart).ToList()
        };
        
        SaveService.SaveRunState(state);
        HasSavedRun = true;
    }
    
    private void EndRun(bool victory)
    {
        IsRunActive = false;
        AutoBattleEnabled = false;
        _autoBattleTimer.Stop();
        SaveService.DeleteRunState();
        HasSavedRun = false;
        
        CurrentScreen = victory ? GameScreen.Victory : GameScreen.GameOver;
        
        // Award medals to progression
        var earnedMedals = RunMedals;
        if (victory)
        {
            earnedMedals += 500; // Victory bonus
            Progression.Victories++;
        }
        
        Progression.TotalMedals += earnedMedals;
        if (CurrentFloor > Progression.HighestFloor)
        {
            Progression.HighestFloor = CurrentFloor;
        }
        
        SaveProgression();
        
        _soundService.Play(victory ? SoundEffect.Victory : SoundEffect.Defeat);
        AddLog($"Medals earned: {earnedMedals} (Total: {Progression.TotalMedals})");
    }
    
    private void StartBattle()
    {
        EnemySquad.Clear();
        var enemies = _battleService.GenerateEnemySquad(CurrentFloor);
        foreach (var enemy in enemies)
        {
            EnemySquad.Add(new MechViewModel(enemy, isEnemy: true, floor: CurrentFloor));
        }
        SelectedTargetMech = EnemySquad.FirstOrDefault();
        
        IsPlayerTurn = true;
        IsBattleOver = false;
        _soundService.Play(SoundEffect.BattleStart);
        AddLog($"=== Floor {CurrentFloor}/{MaxFloors} ===");
        AddLog($"Enemies appeared: {string.Join(", ", enemies.Select(e => e.Name))}");
        StatusMessage = "Your turn! Select an action.";
    }
    
    [RelayCommand]
    private void AttackRight()
    {
        if (!CanAct()) return;
        
        var attacker = SelectedPlayerMech!.Model;
        var target = SelectedTargetMech!.Model;
        
        if (attacker.RightArm == null)
        {
            AddLog($"{attacker.Name} has no right arm!");
            return;
        }
        
        _soundService.Play(SoundEffect.Attack);
        var result = _battleService.ExecuteAttack(attacker, attacker.RightArm, target);
        AddLog(result.Description);
        
        if (result.PartDestroyed) _soundService.Play(SoundEffect.PartBreak);
        if (result.IsCritical) _soundService.Play(SoundEffect.Critical);
        
        RefreshAll();
        EndPlayerTurn();
    }
    
    [RelayCommand]
    private void AttackLeft()
    {
        if (!CanAct()) return;
        
        var attacker = SelectedPlayerMech!.Model;
        var target = SelectedTargetMech!.Model;
        
        if (attacker.LeftArm == null)
        {
            AddLog($"{attacker.Name} has no left arm!");
            return;
        }
        
        _soundService.Play(SoundEffect.Attack);
        var result = _battleService.ExecuteAttack(attacker, attacker.LeftArm, target);
        AddLog(result.Description);
        
        if (result.PartDestroyed) _soundService.Play(SoundEffect.PartBreak);
        if (result.IsCritical) _soundService.Play(SoundEffect.Critical);
        
        RefreshAll();
        EndPlayerTurn();
    }
    
    [RelayCommand]
    private void UseSpecial()
    {
        if (!CanAct()) return;
        
        var attacker = SelectedPlayerMech!.Model;
        var target = SelectedTargetMech!.Model;
        
        if (attacker.Head?.SpecialAbility == null)
        {
            AddLog($"{attacker.Name} has no special ability!");
            return;
        }
        
        if (attacker.MedaforceCharge < attacker.Head.SpecialCost)
        {
            AddLog($"Not enough Medaforce! ({attacker.MedaforceCharge}/{attacker.Head.SpecialCost})");
            return;
        }
        
        attacker.MedaforceCharge -= attacker.Head.SpecialCost;
        
        _soundService.Play(SoundEffect.Special);
        var result = _battleService.ExecuteAttack(attacker, attacker.Head, target);
        AddLog($"{attacker.Name} uses {attacker.Head.SpecialAbility}!");
        AddLog(result.Description);
        
        if (result.PartDestroyed) _soundService.Play(SoundEffect.PartBreak);
        
        RefreshAll();
        EndPlayerTurn();
    }
    
    [RelayCommand]
    private void Defend()
    {
        if (!CanAct()) return;
        
        var mech = SelectedPlayerMech!.Model;
        mech.MedaforceCharge = Math.Min(100, mech.MedaforceCharge + 15);
        _soundService.Play(SoundEffect.Defend);
        AddLog($"{mech.Name} defends and charges Medaforce! (+15, now {mech.MedaforceCharge})");
        
        RefreshAll();
        EndPlayerTurn();
    }
    
    [RelayCommand]
    private void SelectMech(MechViewModel? mech)
    {
        if (mech != null && mech.IsOperational)
        {
            SelectedPlayerMech = mech;
            _soundService.Play(SoundEffect.Select);
        }
    }
    
    [RelayCommand]
    private void SelectTarget(MechViewModel? target)
    {
        if (target != null && target.IsOperational)
        {
            SelectedTargetMech = target;
            _soundService.Play(SoundEffect.Select);
        }
    }
    
    private bool CanAct()
    {
        if (IsBattleOver)
        {
            StatusMessage = "Battle is over!";
            return false;
        }
        
        if (!IsPlayerTurn)
        {
            StatusMessage = "Wait for your turn!";
            return false;
        }
        
        if (SelectedPlayerMech == null || !SelectedPlayerMech.IsOperational)
        {
            SelectedPlayerMech = PlayerSquad.FirstOrDefault(p => p.IsOperational);
            if (SelectedPlayerMech == null)
            {
                StatusMessage = "No operational Mechs!";
                return false;
            }
        }
        
        if (SelectedTargetMech == null || !SelectedTargetMech.IsOperational)
        {
            SelectedTargetMech = EnemySquad.FirstOrDefault(e => e.IsOperational);
            if (SelectedTargetMech == null)
            {
                CheckBattleEnd();
                return false;
            }
        }
        
        return true;
    }
    
    private void EndPlayerTurn()
    {
        if (CheckBattleEnd()) return;
        
        IsPlayerTurn = false;
        StatusMessage = "Enemy turn...";
        
        // AI: each enemy attacks
        foreach (var enemyVm in EnemySquad.Where(e => e.IsOperational))
        {
            var enemy = enemyVm.Model;
            var playerTarget = PlayerSquad
                .Where(p => p.IsOperational)
                .OrderBy(_ => _rng.Next())
                .FirstOrDefault();
                
            if (playerTarget == null) break;
            
            var attackPart = _rng.Next(2) == 0 ? enemy.RightArm : enemy.LeftArm;
            attackPart ??= enemy.RightArm ?? enemy.LeftArm;
            
            if (attackPart != null)
            {
                var result = _battleService.ExecuteAttack(enemy, attackPart, playerTarget.Model);
                AddLog(result.Description);
                if (result.PartDestroyed) _soundService.Play(SoundEffect.PartBreak);
            }
        }
        
        RefreshAll();
        
        if (CheckBattleEnd()) return;
        
        IsPlayerTurn = true;
        StatusMessage = "Your turn! Select an action.";
        SaveRunState();
    }
    
    private bool CheckBattleEnd()
    {
        var playerAlive = PlayerSquad.Any(p => p.IsOperational);
        var enemyAlive = EnemySquad.Any(e => e.IsOperational);
        
        if (!playerAlive)
        {
            IsBattleOver = true;
            PlayerWon = false;
            AddLog("=== DEFEAT ===");
            StatusMessage = "You lost... Press 'New Run' to try again!";
            EndRun(false);
            return true;
        }
        
        if (!enemyAlive)
        {
            IsBattleOver = true;
            PlayerWon = true;
            
            // Award medals for victory
            var floorMedals = 50 + CurrentFloor * 25;
            RunMedals += floorMedals;
            
            AddLog($"=== VICTORY on Floor {CurrentFloor}! ===");
            AddLog($"+{floorMedals} medals (Total: {RunMedals})");
            
            // Generate loot
            LootDrops.Clear();
            var lootCount = 1 + _rng.Next(1, 3) + LootBonus;
            for (int i = 0; i < lootCount; i++)
            {
                var drop = PartCatalog.GetRandomDrop(CurrentFloor);
                LootDrops.Add(drop);
                AddLog($"Obtained: {drop.Name} ({drop.Rarity} {drop.Slot})");
            }
            
            _soundService.Play(SoundEffect.Victory);
            
            // Check if run complete
            if (CurrentFloor >= MaxFloors)
            {
                AddLog("=== RUN COMPLETE! ===");
                StatusMessage = "You beat the final floor! Amazing!";
                EndRun(true);
            }
            else
            {
                StatusMessage = "Victory! Equip loot or continue.";
                SaveRunState();
            }
            
            return true;
        }
        
        return false;
    }
    
    [RelayCommand]
    private void NextFloor()
    {
        if (!PlayerWon || CurrentFloor >= MaxFloors) return;
        
        CurrentFloor++;
        
        // Repairs between floors (with upgrade bonus)
        foreach (var mechVm in PlayerSquad)
        {
            foreach (var part in mechVm.Model.GetParts())
            {
                part.CurrentDurability = Math.Min(part.MaxDurability, part.CurrentDurability + RepairBonus);
            }
        }
        
        _soundService.Play(SoundEffect.NextFloor);
        AddLog($"Advancing to Floor {CurrentFloor}...");
        StartBattle();
        SaveRunState();
    }
    
    [RelayCommand]
    private void EquipLoot(Part? part)
    {
        if (part == null || SelectedPlayerMech == null) return;
        
        var mech = SelectedPlayerMech.Model;
        var replaced = mech.EquipPart(part);
        
        _soundService.Play(SoundEffect.Equip);
        AddLog($"Equipped {part.Name} on {mech.Name}");
        
        if (replaced != null)
        {
            Inventory.Add(replaced);
            AddLog($"Stored {replaced.Name} in inventory");
        }
        
        LootDrops.Remove(part);
        RefreshAll();
        SaveRunState();
    }
    
    [RelayCommand]
    private void AddToInventory(Part? part)
    {
        if (part == null) return;
        
        Inventory.Add(part);
        LootDrops.Remove(part);
        _soundService.Play(SoundEffect.Select);
        AddLog($"Stored {part.Name} in inventory");
        SaveRunState();
    }
    
    [RelayCommand]
    private void EquipFromInventory(Part? part)
    {
        if (part == null || SelectedPlayerMech == null) return;
        
        var mech = SelectedPlayerMech.Model;
        var replaced = mech.EquipPart(part);
        
        _soundService.Play(SoundEffect.Equip);
        AddLog($"Equipped {part.Name} from inventory on {mech.Name}");
        
        Inventory.Remove(part);
        
        if (replaced != null)
        {
            Inventory.Add(replaced);
            AddLog($"Stored {replaced.Name} in inventory");
        }
        
        RefreshAll();
        SaveRunState();
    }
    
    [RelayCommand]
    private void ToggleUpgradePanel()
    {
        ShowUpgradePanel = !ShowUpgradePanel;
        _soundService.Play(SoundEffect.Select);
    }
    
    [RelayCommand]
    private void PurchaseUpgrade(string? upgradeId)
    {
        if (string.IsNullOrEmpty(upgradeId)) return;
        
        var upgrade = MetaUpgrades.GetById(upgradeId);
        if (upgrade == null) return;
        
        var currentLevel = Progression.GetUpgradeLevel(upgradeId);
        if (currentLevel >= upgrade.MaxLevel) return;
        
        var cost = upgrade.GetCostForLevel(currentLevel + 1);
        if (Progression.TotalMedals < cost) return;
        
        Progression.TotalMedals -= cost;
        Progression.SetUpgradeLevel(upgradeId, currentLevel + 1);
        SaveProgression();
        
        _soundService.Play(SoundEffect.Upgrade);
        OnPropertyChanged(nameof(Progression));
        
        AddLog($"Upgraded {upgrade.Name} to level {currentLevel + 1}!");
    }
    
    private void RefreshAll()
    {
        foreach (var m in PlayerSquad) m.Refresh();
        foreach (var m in EnemySquad) m.Refresh();
        
        if (SelectedTargetMech is not { IsOperational: true })
        {
            SelectedTargetMech = EnemySquad.FirstOrDefault(e => e.IsOperational);
        }
        
        if (SelectedPlayerMech is not { IsOperational: true })
        {
            SelectedPlayerMech = PlayerSquad.FirstOrDefault(p => p.IsOperational);
        }
    }
    
    private void AddLog(string message)
    {
        BattleLog.Insert(0, message);
        if (BattleLog.Count > 100)
        {
            BattleLog.RemoveAt(BattleLog.Count - 1);
        }
    }
}
