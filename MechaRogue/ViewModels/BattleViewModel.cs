using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechaRogue.Models;
using MechaRogue.Services;

namespace MechaRogue.ViewModels;

/// <summary>
/// Main ViewModel for the battle screen.
/// </summary>
public partial class BattleViewModel : ObservableObject
{
    private readonly BattleService _battleService = new();
    private readonly Random _rng = new();
    
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
    private ObservableCollection<Part> _lootDrops = [];
    
    public BattleViewModel()
    {
        StartNewRun();
    }
    
    [RelayCommand]
    private void StartNewRun()
    {
        CurrentFloor = 1;
        IsBattleOver = false;
        PlayerWon = false;
        BattleLog.Clear();
        LootDrops.Clear();
        
        // Create starter mech
        var starterMech = new Mech { Name = "Metabee" };
        starterMech.EquipPart(PartCatalog.GetById("head_horn")!.Clone());
        starterMech.EquipPart(PartCatalog.GetById("rarm_punch")!.Clone());
        starterMech.EquipPart(PartCatalog.GetById("larm_shield")!.Clone());
        starterMech.EquipPart(PartCatalog.GetById("legs_bipedal")!.Clone());
        
        PlayerSquad.Clear();
        PlayerSquad.Add(new MechViewModel(starterMech));
        SelectedPlayerMech = PlayerSquad.FirstOrDefault();
        
        StartBattle();
    }
    
    private void StartBattle()
    {
        EnemySquad.Clear();
        var enemies = _battleService.GenerateEnemySquad(CurrentFloor);
        foreach (var enemy in enemies)
        {
            EnemySquad.Add(new MechViewModel(enemy));
        }
        SelectedTargetMech = EnemySquad.FirstOrDefault();
        
        IsPlayerTurn = true;
        IsBattleOver = false;
        AddLog($"=== Floor {CurrentFloor} ===");
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
        
        var result = _battleService.ExecuteAttack(attacker, attacker.RightArm, target);
        AddLog(result.Description);
        
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
        
        var result = _battleService.ExecuteAttack(attacker, attacker.LeftArm, target);
        AddLog(result.Description);
        
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
        
        // Special abilities based on head type
        var result = _battleService.ExecuteAttack(attacker, attacker.Head, target);
        AddLog($"{attacker.Name} uses {attacker.Head.SpecialAbility}!");
        AddLog(result.Description);
        
        RefreshAll();
        EndPlayerTurn();
    }
    
    [RelayCommand]
    private void Defend()
    {
        if (!CanAct()) return;
        
        var mech = SelectedPlayerMech!.Model;
        mech.MedaforceCharge = Math.Min(100, mech.MedaforceCharge + 15);
        AddLog($"{mech.Name} defends and charges Medaforce! (+15, now {mech.MedaforceCharge})");
        
        RefreshAll();
        EndPlayerTurn();
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
            StatusMessage = "Select an operational Mech!";
            return false;
        }
        
        if (SelectedTargetMech == null || !SelectedTargetMech.IsOperational)
        {
            // Auto-select a valid target
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
        
        // Simple AI: each enemy attacks
        foreach (var enemyVm in EnemySquad.Where(e => e.IsOperational))
        {
            var enemy = enemyVm.Model;
            var playerTarget = PlayerSquad
                .Where(p => p.IsOperational)
                .OrderBy(_ => _rng.Next())
                .FirstOrDefault();
                
            if (playerTarget == null) break;
            
            // Pick a random attack
            var attackPart = _rng.Next(2) == 0 ? enemy.RightArm : enemy.LeftArm;
            attackPart ??= enemy.RightArm ?? enemy.LeftArm;
            
            if (attackPart != null)
            {
                var result = _battleService.ExecuteAttack(enemy, attackPart, playerTarget.Model);
                AddLog(result.Description);
            }
        }
        
        RefreshAll();
        
        if (CheckBattleEnd()) return;
        
        IsPlayerTurn = true;
        StatusMessage = "Your turn! Select an action.";
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
            return true;
        }
        
        if (!enemyAlive)
        {
            IsBattleOver = true;
            PlayerWon = true;
            AddLog($"=== VICTORY on Floor {CurrentFloor}! ===");
            
            // Generate loot
            LootDrops.Clear();
            var lootCount = _rng.Next(1, 3);
            for (int i = 0; i < lootCount; i++)
            {
                var drop = PartCatalog.GetRandomDrop(CurrentFloor);
                LootDrops.Add(drop);
                AddLog($"Obtained: {drop.Name} ({drop.Rarity} {drop.Slot})");
            }
            
            StatusMessage = "Victory! Choose loot or continue to next floor.";
            return true;
        }
        
        return false;
    }
    
    [RelayCommand]
    private void NextFloor()
    {
        if (!PlayerWon) return;
        
        CurrentFloor++;
        
        // Light repairs between floors
        foreach (var mechVm in PlayerSquad)
        {
            foreach (var part in mechVm.Model.GetParts())
            {
                part.CurrentDurability = Math.Min(part.MaxDurability, part.CurrentDurability + 20);
            }
        }
        
        AddLog($"Advancing to Floor {CurrentFloor}...");
        StartBattle();
    }
    
    [RelayCommand]
    private void EquipLoot(Part? part)
    {
        if (part == null || SelectedPlayerMech == null) return;
        
        var mech = SelectedPlayerMech.Model;
        var replaced = mech.EquipPart(part);
        
        AddLog($"Equipped {part.Name} on {mech.Name}");
        if (replaced != null)
        {
            AddLog($"Removed {replaced.Name}");
        }
        
        LootDrops.Remove(part);
        RefreshAll();
    }
    
    private void RefreshAll()
    {
        foreach (var m in PlayerSquad) m.Refresh();
        foreach (var m in EnemySquad) m.Refresh();
        
        // Auto-select valid targets
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
