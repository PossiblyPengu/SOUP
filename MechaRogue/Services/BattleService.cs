using MechaRogue.Models;

namespace MechaRogue.Services;

/// <summary>
/// Handles battle resolution and damage calculations.
/// </summary>
public class BattleService
{
    private readonly Random _rng = new();
    
    /// <summary>
    /// Calculates type advantage multiplier.
    /// Melee > Ranged > Support > Melee
    /// </summary>
    public float GetTypeAdvantage(PartType attacker, PartType defender)
    {
        if (attacker == defender) return 1.0f;
        
        return (attacker, defender) switch
        {
            (PartType.Melee, PartType.Ranged) => 1.5f,
            (PartType.Ranged, PartType.Support) => 1.5f,
            (PartType.Support, PartType.Melee) => 1.5f,
            (PartType.Melee, PartType.Support) => 0.75f,
            (PartType.Ranged, PartType.Melee) => 0.75f,
            (PartType.Support, PartType.Ranged) => 0.75f,
            _ => 1.0f
        };
    }
    
    /// <summary>
    /// Executes an attack action.
    /// </summary>
    public ActionResult ExecuteAttack(Mech attacker, Part attackPart, Mech defender, Part? targetPart = null)
    {
        if (attackPart.IsBroken)
        {
            return new ActionResult
            {
                Description = $"{attacker.Name}'s {attackPart.Name} is broken!",
                DamageDealt = 0
            };
        }
        
        // Pick a random target part if not specified
        targetPart ??= GetRandomTargetPart(defender);
        
        if (targetPart == null)
        {
            return new ActionResult
            {
                Description = $"{defender.Name} has no parts to target!",
                DamageDealt = 0
            };
        }
        
        // Evasion check based on speed difference
        var evasionChance = Math.Max(0, (defender.Speed - attacker.Speed) / 2);
        if (_rng.Next(100) < evasionChance)
        {
            return new ActionResult
            {
                Description = $"{defender.Name} evaded {attacker.Name}'s attack!",
                DamageDealt = 0,
                WasEvaded = true
            };
        }
        
        // Calculate damage
        var typeAdvantage = GetTypeAdvantage(attackPart.Type, targetPart.Type);
        var baseDamage = attackPart.Attack;
        var defense = targetPart.Defense;
        var isCritical = _rng.Next(100) < 10; // 10% crit chance
        
        var damage = (int)((baseDamage - defense * 0.5) * typeAdvantage * (isCritical ? 1.5 : 1.0));
        damage = Math.Max(1, damage); // Always deal at least 1 damage
        
        // Apply damage to target part
        targetPart.CurrentDurability -= damage;
        var partDestroyed = targetPart.CurrentDurability <= 0;
        
        if (partDestroyed)
        {
            targetPart.CurrentDurability = 0;
        }
        
        // Build charge for defender (Medaforce)
        defender.MedaforceCharge = Math.Min(100, defender.MedaforceCharge + damage / 2);
        
        // Degrade attacker's weapon slightly
        attackPart.CurrentDurability -= _rng.Next(1, 4);
        
        var typeText = typeAdvantage > 1 ? " (Super Effective!)" : typeAdvantage < 1 ? " (Not Very Effective...)" : "";
        var critText = isCritical ? " CRITICAL HIT!" : "";
        var destroyText = partDestroyed ? $" {targetPart.Name} destroyed!" : "";
        
        return new ActionResult
        {
            Description = $"{attacker.Name} attacks {defender.Name}'s {targetPart.Name} for {damage} damage!{critText}{typeText}{destroyText}",
            DamageDealt = damage,
            PartDestroyed = partDestroyed,
            AffectedPart = targetPart,
            IsCritical = isCritical,
            TypeAdvantage = typeAdvantage
        };
    }
    
    /// <summary>
    /// Gets a random part from the defender that can be targeted.
    /// </summary>
    private Part? GetRandomTargetPart(Mech defender)
    {
        var validParts = defender.GetParts().Where(p => !p.IsBroken).ToList();
        if (validParts.Count == 0) return null;
        
        // Weight towards legs (60%) to simulate targeting mobility first
        if (defender.Legs is { IsBroken: false } && _rng.Next(100) < 30)
        {
            return defender.Legs;
        }
        
        return validParts[_rng.Next(validParts.Count)];
    }
    
    /// <summary>
    /// Determines turn order for a round.
    /// </summary>
    public List<Mech> GetTurnOrder(IEnumerable<Mech> allMechs)
    {
        return allMechs
            .Where(m => m.IsOperational)
            .OrderByDescending(m => m.Speed + _rng.Next(-10, 11)) // Add some randomness
            .ToList();
    }
    
    /// <summary>
    /// Generates an enemy squad for a given floor.
    /// </summary>
    public List<Mech> GenerateEnemySquad(int floor)
    {
        var count = floor switch
        {
            <= 2 => 1,
            <= 4 => 2,
            <= 6 => _rng.Next(2, 4),
            _ => 3 // Boss floor
        };
        
        var squad = new List<Mech>();
        
        for (int i = 0; i < count; i++)
        {
            var mech = new Mech
            {
                Name = $"Enemy {GetEnemyPrefix(floor)}-{i + 1}"
            };
            
            // Equip random parts appropriate for floor
            mech.Head = PartCatalog.GetRandomDrop(floor, _rng);
            while (mech.Head.Slot != PartSlot.Head)
                mech.Head = PartCatalog.GetRandomDrop(floor, _rng);
                
            mech.RightArm = PartCatalog.GetRandomDrop(floor, _rng);
            while (mech.RightArm.Slot != PartSlot.RightArm)
                mech.RightArm = PartCatalog.GetRandomDrop(floor, _rng);
                
            mech.LeftArm = PartCatalog.GetRandomDrop(floor, _rng);
            while (mech.LeftArm.Slot != PartSlot.LeftArm)
                mech.LeftArm = PartCatalog.GetRandomDrop(floor, _rng);
                
            mech.Legs = PartCatalog.GetRandomDrop(floor, _rng);
            while (mech.Legs.Slot != PartSlot.Legs)
                mech.Legs = PartCatalog.GetRandomDrop(floor, _rng);
            
            squad.Add(mech);
        }
        
        return squad;
    }
    
    private static string GetEnemyPrefix(int floor) => floor switch
    {
        1 => "Grunt",
        2 => "Scout",
        3 => "Soldier",
        4 => "Elite",
        5 => "Veteran",
        6 => "Champion",
        _ => "Boss"
    };
}
