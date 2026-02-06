using MechaRogue.Models;

namespace MechaRogue.Services;

/// <summary>
/// Provides the catalog of all available parts in the game.
/// </summary>
public static class PartCatalog
{
    private static readonly List<Part> _allParts = [];
    
    static PartCatalog()
    {
        InitializeParts();
    }
    
    public static IReadOnlyList<Part> AllParts => _allParts;
    
    public static Part? GetById(string id) => _allParts.FirstOrDefault(p => p.Id == id);
    
    public static IEnumerable<Part> GetBySlot(PartSlot slot) => _allParts.Where(p => p.Slot == slot);
    
    public static IEnumerable<Part> GetByRarity(Rarity rarity) => _allParts.Where(p => p.Rarity == rarity);
    
    /// <summary>Gets starter parts for new runs.</summary>
    public static List<string> StarterPartIds => ["head_horn", "rarm_punch", "larm_shield", "legs_bipedal"];
    
    private static void InitializeParts()
    {
        // ============================================================
        // HEADS (Special abilities) - 10 total
        // ============================================================
        _allParts.Add(new Part
        {
            Id = "head_scout",
            Name = "Scout Visor",
            Description = "Basic targeting system. Reveals enemy part durability.",
            Slot = PartSlot.Head,
            Type = PartType.Support,
            Rarity = Rarity.Common,
            Attack = 0,
            Defense = 10,
            Speed = 0,
            MaxDurability = 80,
            SpecialAbility = "Scan",
            SpecialCost = 20
        });
        
        _allParts.Add(new Part
        {
            Id = "head_cannon",
            Name = "Beam Cannon",
            Description = "Head-mounted laser. High damage special attack.",
            Slot = PartSlot.Head,
            Type = PartType.Ranged,
            Rarity = Rarity.Uncommon,
            Attack = 15,
            Defense = 8,
            Speed = 0,
            MaxDurability = 70,
            SpecialAbility = "Beam Blast",
            SpecialCost = 40
        });
        
        _allParts.Add(new Part
        {
            Id = "head_horn",
            Name = "Battle Horn",
            Description = "Armored horn for ramming. Melee special.",
            Slot = PartSlot.Head,
            Type = PartType.Melee,
            Rarity = Rarity.Common,
            Attack = 12,
            Defense = 15,
            Speed = 0,
            MaxDurability = 100,
            SpecialAbility = "Head Bash",
            SpecialCost = 30
        });
        
        _allParts.Add(new Part
        {
            Id = "head_antenna",
            Name = "Comm Antenna",
            Description = "Boosts ally coordination. Heals team.",
            Slot = PartSlot.Head,
            Type = PartType.Support,
            Rarity = Rarity.Rare,
            Attack = 0,
            Defense = 12,
            Speed = 0,
            MaxDurability = 60,
            SpecialAbility = "Repair Wave",
            SpecialCost = 50
        });
        
        _allParts.Add(new Part
        {
            Id = "head_radar",
            Name = "Radar Dome",
            Description = "Tracks all enemies. Boosts accuracy.",
            Slot = PartSlot.Head,
            Type = PartType.Support,
            Rarity = Rarity.Uncommon,
            Attack = 0,
            Defense = 15,
            Speed = 0,
            MaxDurability = 75,
            SpecialAbility = "Lock On",
            SpecialCost = 25
        });
        
        _allParts.Add(new Part
        {
            Id = "head_plasma",
            Name = "Plasma Core",
            Description = "Devastating plasma burst. High damage.",
            Slot = PartSlot.Head,
            Type = PartType.Ranged,
            Rarity = Rarity.Epic,
            Attack = 30,
            Defense = 5,
            Speed = 0,
            MaxDurability = 50,
            SpecialAbility = "Plasma Nova",
            SpecialCost = 60
        });
        
        _allParts.Add(new Part
        {
            Id = "head_emp",
            Name = "EMP Emitter",
            Description = "Disables enemy systems temporarily.",
            Slot = PartSlot.Head,
            Type = PartType.Support,
            Rarity = Rarity.Epic,
            Attack = 5,
            Defense = 10,
            Speed = 0,
            MaxDurability = 55,
            SpecialAbility = "EMP Blast",
            SpecialCost = 55
        });
        
        _allParts.Add(new Part
        {
            Id = "head_drill",
            Name = "Drill Horn",
            Description = "Spinning drill attack. Armor piercing.",
            Slot = PartSlot.Head,
            Type = PartType.Melee,
            Rarity = Rarity.Uncommon,
            Attack = 20,
            Defense = 12,
            Speed = 0,
            MaxDurability = 85,
            SpecialAbility = "Drill Dive",
            SpecialCost = 35
        });
        
        _allParts.Add(new Part
        {
            Id = "head_omega",
            Name = "Omega Brain",
            Description = "Ultimate AI. Predicts enemy moves.",
            Slot = PartSlot.Head,
            Type = PartType.Support,
            Rarity = Rarity.Legendary,
            Attack = 10,
            Defense = 20,
            Speed = 0,
            MaxDurability = 80,
            SpecialAbility = "Overclock",
            SpecialCost = 75
        });
        
        // ============================================================
        // RIGHT ARMS (Primary attack) - 10 total
        // ============================================================
        _allParts.Add(new Part
        {
            Id = "rarm_punch",
            Name = "Power Fist",
            Description = "Standard melee arm. Reliable damage.",
            Slot = PartSlot.RightArm,
            Type = PartType.Melee,
            Rarity = Rarity.Common,
            Attack = 25,
            Defense = 8,
            Speed = 0,
            MaxDurability = 90
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_rifle",
            Name = "Assault Rifle",
            Description = "Rapid-fire ranged weapon.",
            Slot = PartSlot.RightArm,
            Type = PartType.Ranged,
            Rarity = Rarity.Common,
            Attack = 20,
            Defense = 5,
            Speed = 0,
            MaxDurability = 70
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_hammer",
            Name = "Pile Bunker",
            Description = "Devastating pile driver. High damage, low durability.",
            Slot = PartSlot.RightArm,
            Type = PartType.Melee,
            Rarity = Rarity.Uncommon,
            Attack = 40,
            Defense = 5,
            Speed = 0,
            MaxDurability = 50
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_laser",
            Name = "Beam Saber",
            Description = "Energy blade. Ignores some defense.",
            Slot = PartSlot.RightArm,
            Type = PartType.Melee,
            Rarity = Rarity.Rare,
            Attack = 35,
            Defense = 10,
            Speed = 0,
            MaxDurability = 65
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_cannon",
            Name = "Heavy Cannon",
            Description = "Slow but powerful ranged weapon.",
            Slot = PartSlot.RightArm,
            Type = PartType.Ranged,
            Rarity = Rarity.Uncommon,
            Attack = 35,
            Defense = 8,
            Speed = 0,
            MaxDurability = 60
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_sniper",
            Name = "Sniper Railgun",
            Description = "Long range precision. High crit chance.",
            Slot = PartSlot.RightArm,
            Type = PartType.Ranged,
            Rarity = Rarity.Rare,
            Attack = 45,
            Defense = 3,
            Speed = 0,
            MaxDurability = 45
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_chainsaw",
            Name = "Chain Blade",
            Description = "Grinding melee damage over time.",
            Slot = PartSlot.RightArm,
            Type = PartType.Melee,
            Rarity = Rarity.Rare,
            Attack = 38,
            Defense = 5,
            Speed = 0,
            MaxDurability = 55
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_plasma",
            Name = "Plasma Launcher",
            Description = "Explosive plasma rounds. Area damage.",
            Slot = PartSlot.RightArm,
            Type = PartType.Ranged,
            Rarity = Rarity.Epic,
            Attack = 42,
            Defense = 5,
            Speed = 0,
            MaxDurability = 40
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_nova",
            Name = "Nova Gauntlet",
            Description = "Legendary melee. Devastating combos.",
            Slot = PartSlot.RightArm,
            Type = PartType.Melee,
            Rarity = Rarity.Legendary,
            Attack = 55,
            Defense = 15,
            Speed = 0,
            MaxDurability = 70
        });
        
        _allParts.Add(new Part
        {
            Id = "rarm_gravity",
            Name = "Gravity Cannon",
            Description = "Pulls enemies, reduces evasion.",
            Slot = PartSlot.RightArm,
            Type = PartType.Support,
            Rarity = Rarity.Epic,
            Attack = 25,
            Defense = 10,
            Speed = 0,
            MaxDurability = 50
        });
        
        // ============================================================
        // LEFT ARMS (Secondary/Support) - 10 total
        // ============================================================
        _allParts.Add(new Part
        {
            Id = "larm_shield",
            Name = "Guard Shield",
            Description = "Defensive arm. High protection.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Support,
            Rarity = Rarity.Common,
            Attack = 5,
            Defense = 25,
            Speed = 0,
            MaxDurability = 100
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_missile",
            Name = "Missile Pod",
            Description = "Lock-on missiles. Never miss.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Ranged,
            Rarity = Rarity.Uncommon,
            Attack = 18,
            Defense = 5,
            Speed = 0,
            MaxDurability = 55
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_claw",
            Name = "Grapple Claw",
            Description = "Grabs enemies, reducing their evasion.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Melee,
            Rarity = Rarity.Common,
            Attack = 15,
            Defense = 10,
            Speed = 0,
            MaxDurability = 80
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_repair",
            Name = "Repair Arm",
            Description = "Can restore ally part durability.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Support,
            Rarity = Rarity.Rare,
            Attack = 0,
            Defense = 10,
            Speed = 0,
            MaxDurability = 70
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_flamethrower",
            Name = "Flame Thrower",
            Description = "Close-range fire attack. Burns over time.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Melee,
            Rarity = Rarity.Uncommon,
            Attack = 22,
            Defense = 5,
            Speed = 0,
            MaxDurability = 55
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_tesla",
            Name = "Tesla Coil",
            Description = "Chain lightning hits multiple parts.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Ranged,
            Rarity = Rarity.Epic,
            Attack = 28,
            Defense = 8,
            Speed = 0,
            MaxDurability = 45
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_drone",
            Name = "Drone Bay",
            Description = "Deploys attack drones for support fire.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Support,
            Rarity = Rarity.Rare,
            Attack = 12,
            Defense = 15,
            Speed = 0,
            MaxDurability = 60
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_barrier",
            Name = "Barrier Generator",
            Description = "Projects energy shield. Blocks one hit.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Support,
            Rarity = Rarity.Epic,
            Attack = 0,
            Defense = 35,
            Speed = 0,
            MaxDurability = 40
        });
        
        _allParts.Add(new Part
        {
            Id = "larm_omega",
            Name = "Omega Blade",
            Description = "Legendary dual-wield. Massive damage.",
            Slot = PartSlot.LeftArm,
            Type = PartType.Melee,
            Rarity = Rarity.Legendary,
            Attack = 45,
            Defense = 12,
            Speed = 0,
            MaxDurability = 65
        });
        
        // ============================================================
        // LEGS (Speed/Evasion) - 10 total
        // ============================================================
        _allParts.Add(new Part
        {
            Id = "legs_bipedal",
            Name = "Standard Legs",
            Description = "Balanced mobility.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Common,
            Attack = 0,
            Defense = 15,
            Speed = 50,
            MaxDurability = 90
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_hover",
            Name = "Hover Jets",
            Description = "Fast but fragile.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Uncommon,
            Attack = 0,
            Defense = 8,
            Speed = 80,
            MaxDurability = 50
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_tank",
            Name = "Tank Treads",
            Description = "Slow but heavily armored.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Uncommon,
            Attack = 0,
            Defense = 30,
            Speed = 25,
            MaxDurability = 120
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_spider",
            Name = "Spider Legs",
            Description = "Multi-terrain stability.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Rare,
            Attack = 5,
            Defense = 20,
            Speed = 60,
            MaxDurability = 85
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_wheels",
            Name = "Racing Wheels",
            Description = "Maximum speed, minimum defense.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Common,
            Attack = 0,
            Defense = 5,
            Speed = 95,
            MaxDurability = 40
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_quad",
            Name = "Quad Walker",
            Description = "Four-legged stability. Good defense.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Uncommon,
            Attack = 0,
            Defense = 25,
            Speed = 45,
            MaxDurability = 100
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_jump",
            Name = "Jump Boosters",
            Description = "Aerial evasion specialist.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Rare,
            Attack = 0,
            Defense = 12,
            Speed = 70,
            MaxDurability = 60
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_stealth",
            Name = "Stealth Treads",
            Description = "Silent movement, increased evasion.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Epic,
            Attack = 0,
            Defense = 10,
            Speed = 75,
            MaxDurability = 55
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_fortress",
            Name = "Fortress Base",
            Description = "Immobile but near-invincible.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Epic,
            Attack = 0,
            Defense = 50,
            Speed = 5,
            MaxDurability = 150
        });
        
        _allParts.Add(new Part
        {
            Id = "legs_quantum",
            Name = "Quantum Phase",
            Description = "Teleports to evade. Legendary speed.",
            Slot = PartSlot.Legs,
            Type = PartType.Support,
            Rarity = Rarity.Legendary,
            Attack = 0,
            Defense = 15,
            Speed = 100,
            MaxDurability = 50
        });
    }
    
    /// <summary>Gets a random part appropriate for the given floor.</summary>
    public static Part GetRandomDrop(int floor, Random? rng = null)
    {
        rng ??= Random.Shared;
        
        // Higher floors = better rarity chances
        var roll = rng.Next(100);
        Rarity targetRarity = floor switch
        {
            >= 6 when roll < 15 => Rarity.Epic,
            >= 6 when roll < 40 => Rarity.Rare,
            >= 4 when roll < 10 => Rarity.Epic,
            >= 4 when roll < 35 => Rarity.Rare,
            >= 2 when roll < 25 => Rarity.Rare,
            _ when roll < 30 => Rarity.Uncommon,
            _ => Rarity.Common
        };
        
        var candidates = GetByRarity(targetRarity).ToList();
        if (candidates.Count == 0)
            candidates = GetByRarity(Rarity.Common).ToList();
            
        return candidates[rng.Next(candidates.Count)].Clone();
    }
}
