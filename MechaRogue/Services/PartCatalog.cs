using MechaRogue.Models;

namespace MechaRogue.Services;

/// <summary>
/// Provides the catalog of all available parts in the game.
/// 100 unique parts across 4 slots, 3 types, and 4 rarities.
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
    
    private static void Add(Part p)
    {
        p.CurrentDurability = p.MaxDurability;
        _allParts.Add(p);
    }
    
    private static void InitializeParts()
    {
        // ============================================================
        // HEADS - 25 total (Control center, special abilities)
        // ============================================================
        
        // === COMMON HEADS (7) ===
        Add(new Part { Id = "head_horn", Name = "Battle Horn", Description = "Armored horn for ramming attacks.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 12, Defense = 15, Speed = 0, MaxDurability = 100, SpecialAbility = "Head Bash", SpecialCost = 30 });
        
        Add(new Part { Id = "head_scout", Name = "Scout Visor", Description = "Basic targeting system.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 0, Defense = 10, Speed = 5, MaxDurability = 80, SpecialAbility = "Scan", SpecialCost = 20 });
        
        Add(new Part { Id = "head_basic", Name = "Standard Helm", Description = "Reliable all-purpose head unit.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 8, Defense = 12, Speed = 0, MaxDurability = 90, SpecialAbility = "Focus", SpecialCost = 25 });
        
        Add(new Part { Id = "head_sensor", Name = "Sensor Array", Description = "Enhanced detection equipment.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 5, Defense = 8, Speed = 3, MaxDurability = 70, SpecialAbility = "Lock On", SpecialCost = 20 });
        
        Add(new Part { Id = "head_mono", Name = "Mono-Eye", Description = "Single optical sensor, cheap but effective.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 10, Defense = 6, Speed = 2, MaxDurability = 65, SpecialAbility = "Precision", SpecialCost = 25 });
        
        Add(new Part { Id = "head_plate", Name = "Armored Plate", Description = "Heavy face guard for defense.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 5, Defense = 20, Speed = -2, MaxDurability = 120, SpecialAbility = "Brace", SpecialCost = 15 });
        
        Add(new Part { Id = "head_comm", Name = "Comm Unit", Description = "Basic communication relay.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 0, Defense = 8, Speed = 2, MaxDurability = 75, SpecialAbility = "Coordinate", SpecialCost = 30 });
        
        // === UNCOMMON HEADS (7) ===
        Add(new Part { Id = "head_cannon", Name = "Beam Cannon", Description = "Head-mounted laser cannon.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 18, Defense = 8, Speed = 0, MaxDurability = 70, SpecialAbility = "Beam Blast", SpecialCost = 40 });
        
        Add(new Part { Id = "head_crown", Name = "Battle Crown", Description = "Command unit with boosted signals.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 5, Defense = 15, Speed = 3, MaxDurability = 85, SpecialAbility = "Rally", SpecialCost = 35 });
        
        Add(new Part { Id = "head_drill", Name = "Drill Spike", Description = "Rotating horn drill.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 22, Defense = 10, Speed = -1, MaxDurability = 90, SpecialAbility = "Drill Rush", SpecialCost = 45 });
        
        Add(new Part { Id = "head_radar", Name = "Radar Dome", Description = "360-degree threat detection.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 0, Defense = 12, Speed = 8, MaxDurability = 65, SpecialAbility = "Alert", SpecialCost = 25 });
        
        Add(new Part { Id = "head_vulcan", Name = "Vulcan Pods", Description = "Rapid-fire head guns.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 14, Defense = 10, Speed = 2, MaxDurability = 75, SpecialAbility = "Barrage", SpecialCost = 35 });
        
        Add(new Part { Id = "head_crest", Name = "Warrior Crest", Description = "Intimidating battle decoration.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 15, Defense = 18, Speed = 0, MaxDurability = 95, SpecialAbility = "Intimidate", SpecialCost = 30 });
        
        Add(new Part { Id = "head_scope", Name = "Sniper Scope", Description = "Long-range precision optics.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 20, Defense = 5, Speed = -1, MaxDurability = 55, SpecialAbility = "Headshot", SpecialCost = 50 });
        
        // === RARE HEADS (7) ===
        Add(new Part { Id = "head_antenna", Name = "Quantum Antenna", Description = "Advanced comm for healing allies.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 5, Defense = 18, Speed = 5, MaxDurability = 80, SpecialAbility = "Repair Wave", SpecialCost = 50 });
        
        Add(new Part { Id = "head_plasma", Name = "Plasma Caster", Description = "Superheated projectile launcher.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 28, Defense = 10, Speed = 0, MaxDurability = 65, SpecialAbility = "Plasma Burst", SpecialCost = 55 });
        
        Add(new Part { Id = "head_samurai", Name = "Samurai Kabuto", Description = "Ancient warrior helm with blade.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 25, Defense = 20, Speed = 2, MaxDurability = 100, SpecialAbility = "Bushido", SpecialCost = 45 });
        
        Add(new Part { Id = "head_holo", Name = "Holo Projector", Description = "Creates decoy illusions.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 8, Defense = 15, Speed = 10, MaxDurability = 60, SpecialAbility = "Decoy", SpecialCost = 40 });
        
        Add(new Part { Id = "head_ion", Name = "Ion Blaster", Description = "EMP disruption cannon.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 22, Defense = 12, Speed = 3, MaxDurability = 70, SpecialAbility = "EMP Blast", SpecialCost = 60 });
        
        Add(new Part { Id = "head_ram", Name = "Siege Ram", Description = "Reinforced battering head.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 30, Defense = 25, Speed = -3, MaxDurability = 130, SpecialAbility = "Siege Break", SpecialCost = 50 });
        
        Add(new Part { Id = "head_neural", Name = "Neural Link", Description = "Direct brain interface.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 10, Defense = 10, Speed = 15, MaxDurability = 50, SpecialAbility = "Overclock", SpecialCost = 40 });
        
        // === EPIC HEADS (4) ===
        Add(new Part { Id = "head_omega", Name = "Omega Crown", Description = "Ultimate command unit. Boosts all allies.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 15, Defense = 25, Speed = 10, MaxDurability = 100, SpecialAbility = "Omega Rally", SpecialCost = 60 });
        
        Add(new Part { Id = "head_nova", Name = "Nova Cannon", Description = "Devastating energy blast.",
            Slot = PartSlot.Head, Type = PartType.Ranged, Rarity = Rarity.Epic,
            Attack = 40, Defense = 15, Speed = 0, MaxDurability = 80, SpecialAbility = "Nova Blast", SpecialCost = 70 });
        
        Add(new Part { Id = "head_dragon", Name = "Dragon Skull", Description = "Legendary melee head with fire breath.",
            Slot = PartSlot.Head, Type = PartType.Melee, Rarity = Rarity.Epic,
            Attack = 35, Defense = 30, Speed = 5, MaxDurability = 120, SpecialAbility = "Dragon Flame", SpecialCost = 55 });
        
        Add(new Part { Id = "head_void", Name = "Void Helm", Description = "Warps space around attacks.",
            Slot = PartSlot.Head, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 20, Defense = 20, Speed = 20, MaxDurability = 70, SpecialAbility = "Phase Shift", SpecialCost = 50 });
        
        // ============================================================
        // RIGHT ARMS - 25 total (Primary attack weapons)
        // ============================================================
        
        // === COMMON RIGHT ARMS (7) ===
        Add(new Part { Id = "rarm_punch", Name = "Power Fist", Description = "Standard combat fist.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 18, Defense = 8, Speed = 0, MaxDurability = 90, SpecialAbility = "Power Strike", SpecialCost = 25 });
        
        Add(new Part { Id = "rarm_pistol", Name = "Arm Pistol", Description = "Basic ranged weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 14, Defense = 5, Speed = 3, MaxDurability = 70, SpecialAbility = "Quick Shot", SpecialCost = 20 });
        
        Add(new Part { Id = "rarm_claw", Name = "Combat Claw", Description = "Sharp slashing weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 16, Defense = 6, Speed = 2, MaxDurability = 80, SpecialAbility = "Rend", SpecialCost = 25 });
        
        Add(new Part { Id = "rarm_rifle", Name = "Assault Rifle", Description = "Medium-range firearm.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 15, Defense = 6, Speed = 1, MaxDurability = 75, SpecialAbility = "Burst Fire", SpecialCost = 30 });
        
        Add(new Part { Id = "rarm_hammer", Name = "War Hammer", Description = "Heavy crushing weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 20, Defense = 10, Speed = -2, MaxDurability = 100, SpecialAbility = "Crush", SpecialCost = 30 });
        
        Add(new Part { Id = "rarm_needle", Name = "Needle Gun", Description = "Rapid-fire needle launcher.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 12, Defense = 4, Speed = 5, MaxDurability = 60, SpecialAbility = "Needle Storm", SpecialCost = 25 });
        
        Add(new Part { Id = "rarm_wrench", Name = "Mech Wrench", Description = "Tool converted to weapon. Can repair.",
            Slot = PartSlot.RightArm, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 10, Defense = 8, Speed = 0, MaxDurability = 85, SpecialAbility = "Field Repair", SpecialCost = 35 });
        
        // === UNCOMMON RIGHT ARMS (7) ===
        Add(new Part { Id = "rarm_blade", Name = "Laser Blade", Description = "Energy sword with high damage.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 24, Defense = 8, Speed = 2, MaxDurability = 80, SpecialAbility = "Blade Rush", SpecialCost = 35 });
        
        Add(new Part { Id = "rarm_cannon", Name = "Arm Cannon", Description = "Heavy projectile launcher.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 22, Defense = 10, Speed = -1, MaxDurability = 85, SpecialAbility = "Cannon Blast", SpecialCost = 40 });
        
        Add(new Part { Id = "rarm_spear", Name = "Pile Bunker", Description = "Piercing stake driver.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 28, Defense = 5, Speed = -1, MaxDurability = 70, SpecialAbility = "Pierce", SpecialCost = 45 });
        
        Add(new Part { Id = "rarm_missile", Name = "Missile Pod", Description = "Guided explosive launcher.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 25, Defense = 8, Speed = 0, MaxDurability = 75, SpecialAbility = "Missile Volley", SpecialCost = 45 });
        
        Add(new Part { Id = "rarm_chain", Name = "Chain Whip", Description = "Flexible striking weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 20, Defense = 6, Speed = 4, MaxDurability = 85, SpecialAbility = "Lash", SpecialCost = 30 });
        
        Add(new Part { Id = "rarm_flamer", Name = "Flamethrower", Description = "Short-range fire weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 18, Defense = 6, Speed = 0, MaxDurability = 70, SpecialAbility = "Inferno", SpecialCost = 35 });
        
        Add(new Part { Id = "rarm_taser", Name = "Shock Prod", Description = "Electrical stunning weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 15, Defense = 10, Speed = 2, MaxDurability = 80, SpecialAbility = "Stun", SpecialCost = 30 });
        
        // === RARE RIGHT ARMS (7) ===
        Add(new Part { Id = "rarm_katana", Name = "Beam Katana", Description = "Elegant energy blade.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 32, Defense = 12, Speed = 5, MaxDurability = 75, SpecialAbility = "Iai Strike", SpecialCost = 40 });
        
        Add(new Part { Id = "rarm_railgun", Name = "Railgun", Description = "Electromagnetic accelerator.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 35, Defense = 8, Speed = -2, MaxDurability = 65, SpecialAbility = "Rail Shot", SpecialCost = 55 });
        
        Add(new Part { Id = "rarm_drill", Name = "Giga Drill", Description = "Massive rotating drill arm.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 38, Defense = 15, Speed = -3, MaxDurability = 100, SpecialAbility = "Drill Break", SpecialCost = 50 });
        
        Add(new Part { Id = "rarm_laser", Name = "Laser Array", Description = "Multi-beam targeting system.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 30, Defense = 10, Speed = 3, MaxDurability = 70, SpecialAbility = "Multi-Beam", SpecialCost = 45 });
        
        Add(new Part { Id = "rarm_axe", Name = "Plasma Axe", Description = "Heavy energy chopping weapon.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 35, Defense = 18, Speed = -2, MaxDurability = 95, SpecialAbility = "Cleave", SpecialCost = 45 });
        
        Add(new Part { Id = "rarm_grenade", Name = "Grenade Launcher", Description = "Explosive area damage.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 28, Defense = 12, Speed = 0, MaxDurability = 80, SpecialAbility = "Bombardment", SpecialCost = 50 });
        
        Add(new Part { Id = "rarm_nano", Name = "Nano Injector", Description = "Heals allies, damages enemies.",
            Slot = PartSlot.RightArm, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 18, Defense = 15, Speed = 5, MaxDurability = 65, SpecialAbility = "Nanite Swarm", SpecialCost = 55 });
        
        // === EPIC RIGHT ARMS (4) ===
        Add(new Part { Id = "rarm_excalibur", Name = "Excalibur", Description = "Legendary blade of light.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Epic,
            Attack = 45, Defense = 20, Speed = 8, MaxDurability = 90, SpecialAbility = "Holy Blade", SpecialCost = 50 });
        
        Add(new Part { Id = "rarm_bfg", Name = "BFG-9000", Description = "Big. Friendly. Gun.",
            Slot = PartSlot.RightArm, Type = PartType.Ranged, Rarity = Rarity.Epic,
            Attack = 50, Defense = 10, Speed = -5, MaxDurability = 75, SpecialAbility = "Annihilate", SpecialCost = 80 });
        
        Add(new Part { Id = "rarm_gauntlet", Name = "Titan Gauntlet", Description = "Godlike crushing power.",
            Slot = PartSlot.RightArm, Type = PartType.Melee, Rarity = Rarity.Epic,
            Attack = 42, Defense = 30, Speed = -3, MaxDurability = 120, SpecialAbility = "Titan Crush", SpecialCost = 55 });
        
        Add(new Part { Id = "rarm_genesis", Name = "Genesis Arm", Description = "Creates and destroys matter.",
            Slot = PartSlot.RightArm, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 35, Defense = 25, Speed = 10, MaxDurability = 85, SpecialAbility = "Reality Warp", SpecialCost = 60 });
        
        // ============================================================
        // LEFT ARMS - 25 total (Secondary/defensive weapons)
        // ============================================================
        
        // === COMMON LEFT ARMS (7) ===
        Add(new Part { Id = "larm_shield", Name = "Arm Shield", Description = "Basic defensive barrier.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 5, Defense = 20, Speed = 0, MaxDurability = 100, SpecialAbility = "Guard", SpecialCost = 20 });
        
        Add(new Part { Id = "larm_claw", Name = "Rip Claw", Description = "Sharp secondary weapon.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 14, Defense = 8, Speed = 2, MaxDurability = 80, SpecialAbility = "Shred", SpecialCost = 25 });
        
        Add(new Part { Id = "larm_pistol", Name = "Side Arm", Description = "Backup ranged weapon.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 12, Defense = 6, Speed = 3, MaxDurability = 70, SpecialAbility = "Snap Shot", SpecialCost = 20 });
        
        Add(new Part { Id = "larm_buckler", Name = "Buckler", Description = "Light, fast shield.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 8, Defense = 15, Speed = 3, MaxDurability = 75, SpecialAbility = "Parry", SpecialCost = 15 });
        
        Add(new Part { Id = "larm_knife", Name = "Combat Knife", Description = "Fast slashing attacks.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 12, Defense = 5, Speed = 5, MaxDurability = 65, SpecialAbility = "Slash", SpecialCost = 20 });
        
        Add(new Part { Id = "larm_smg", Name = "Sub-Machine Gun", Description = "Rapid-fire backup.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 10, Defense = 5, Speed = 4, MaxDurability = 60, SpecialAbility = "Spray", SpecialCost = 25 });
        
        Add(new Part { Id = "larm_repair", Name = "Repair Kit", Description = "Field maintenance tools.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 5, Defense = 10, Speed = 0, MaxDurability = 80, SpecialAbility = "Patch Up", SpecialCost = 30 });
        
        // === UNCOMMON LEFT ARMS (7) ===
        Add(new Part { Id = "larm_tower", Name = "Tower Shield", Description = "Heavy defensive barrier.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 5, Defense = 30, Speed = -3, MaxDurability = 130, SpecialAbility = "Fortress", SpecialCost = 25 });
        
        Add(new Part { Id = "larm_hook", Name = "Grapple Hook", Description = "Pull enemies close.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 16, Defense = 10, Speed = 3, MaxDurability = 85, SpecialAbility = "Drag", SpecialCost = 30 });
        
        Add(new Part { Id = "larm_shotgun", Name = "Scatter Gun", Description = "Close-range devastation.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 22, Defense = 8, Speed = 0, MaxDurability = 75, SpecialAbility = "Scatter Shot", SpecialCost = 35 });
        
        Add(new Part { Id = "larm_saber", Name = "Energy Saber", Description = "Fast energy blade.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 20, Defense = 8, Speed = 4, MaxDurability = 70, SpecialAbility = "Swift Cut", SpecialCost = 30 });
        
        Add(new Part { Id = "larm_launcher", Name = "Smoke Launcher", Description = "Creates cover.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 8, Defense = 18, Speed = 2, MaxDurability = 80, SpecialAbility = "Smokescreen", SpecialCost = 25 });
        
        Add(new Part { Id = "larm_beam", Name = "Beam Shield", Description = "Energy barrier projector.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 10, Defense = 25, Speed = 0, MaxDurability = 90, SpecialAbility = "Deflect", SpecialCost = 30 });
        
        Add(new Part { Id = "larm_crossbow", Name = "Arm Crossbow", Description = "Silent but deadly.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 18, Defense = 6, Speed = 2, MaxDurability = 65, SpecialAbility = "Bolt", SpecialCost = 25 });
        
        // === RARE LEFT ARMS (7) ===
        Add(new Part { Id = "larm_mirror", Name = "Mirror Shield", Description = "Reflects projectiles.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 15, Defense = 28, Speed = 2, MaxDurability = 85, SpecialAbility = "Reflect", SpecialCost = 40 });
        
        Add(new Part { Id = "larm_plasma", Name = "Plasma Cutter", Description = "Industrial cutting tool.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 28, Defense = 10, Speed = 2, MaxDurability = 75, SpecialAbility = "Sever", SpecialCost = 40 });
        
        Add(new Part { Id = "larm_sniper", Name = "Sniper Arm", Description = "Long-range precision.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 30, Defense = 5, Speed = -1, MaxDurability = 60, SpecialAbility = "Deadeye", SpecialCost = 50 });
        
        Add(new Part { Id = "larm_whip", Name = "Energy Whip", Description = "Flexible energy weapon.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 25, Defense = 8, Speed = 6, MaxDurability = 70, SpecialAbility = "Lash", SpecialCost = 35 });
        
        Add(new Part { Id = "larm_aegis", Name = "Aegis Barrier", Description = "Mythical protection.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 10, Defense = 35, Speed = 0, MaxDurability = 110, SpecialAbility = "Aegis", SpecialCost = 45 });
        
        Add(new Part { Id = "larm_gatling", Name = "Mini Gatling", Description = "Sustained fire support.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 26, Defense = 10, Speed = -1, MaxDurability = 80, SpecialAbility = "Suppression", SpecialCost = 40 });
        
        Add(new Part { Id = "larm_medic", Name = "Medic Arm", Description = "Advanced healing system.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 8, Defense = 20, Speed = 3, MaxDurability = 75, SpecialAbility = "Heal Beam", SpecialCost = 50 });
        
        // === EPIC LEFT ARMS (4) ===
        Add(new Part { Id = "larm_infinity", Name = "Infinity Shield", Description = "Regenerating barrier.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 15, Defense = 45, Speed = 5, MaxDurability = 150, SpecialAbility = "Eternal Guard", SpecialCost = 40 });
        
        Add(new Part { Id = "larm_scythe", Name = "Death Scythe", Description = "Reaper's weapon.",
            Slot = PartSlot.LeftArm, Type = PartType.Melee, Rarity = Rarity.Epic,
            Attack = 40, Defense = 15, Speed = 5, MaxDurability = 85, SpecialAbility = "Soul Rend", SpecialCost = 50 });
        
        Add(new Part { Id = "larm_antimatter", Name = "Antimatter Gun", Description = "Destroys on contact.",
            Slot = PartSlot.LeftArm, Type = PartType.Ranged, Rarity = Rarity.Epic,
            Attack = 45, Defense = 10, Speed = 0, MaxDurability = 70, SpecialAbility = "Obliterate", SpecialCost = 70 });
        
        Add(new Part { Id = "larm_chronos", Name = "Chronos Hand", Description = "Manipulates time.",
            Slot = PartSlot.LeftArm, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 25, Defense = 30, Speed = 20, MaxDurability = 80, SpecialAbility = "Time Stop", SpecialCost = 60 });
        
        // ============================================================
        // LEGS - 25 total (Mobility and stability)
        // ============================================================
        
        // === COMMON LEGS (7) ===
        Add(new Part { Id = "legs_bipedal", Name = "Standard Legs", Description = "Balanced humanoid legs.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 8, Defense = 15, Speed = 10, MaxDurability = 100, SpecialAbility = "Dash", SpecialCost = 20 });
        
        Add(new Part { Id = "legs_tank", Name = "Tank Treads", Description = "Slow but stable.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 10, Defense = 25, Speed = 3, MaxDurability = 130, SpecialAbility = "Anchor", SpecialCost = 15 });
        
        Add(new Part { Id = "legs_light", Name = "Light Frame", Description = "Fast but fragile.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 12, Defense = 8, Speed = 18, MaxDurability = 70, SpecialAbility = "Sprint", SpecialCost = 15 });
        
        Add(new Part { Id = "legs_hover", Name = "Hover Units", Description = "Floating mobility.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 5, Defense = 10, Speed = 15, MaxDurability = 75, SpecialAbility = "Float", SpecialCost = 20 });
        
        Add(new Part { Id = "legs_quad", Name = "Quad Walker", Description = "Four-legged stability.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Common,
            Attack = 8, Defense = 20, Speed = 8, MaxDurability = 110, SpecialAbility = "Brace", SpecialCost = 15 });
        
        Add(new Part { Id = "legs_wheels", Name = "Wheel Base", Description = "Road-based mobility.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Common,
            Attack = 6, Defense = 12, Speed = 20, MaxDurability = 80, SpecialAbility = "Accelerate", SpecialCost = 20 });
        
        Add(new Part { Id = "legs_armored", Name = "Armored Legs", Description = "Heavy protective legs.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Common,
            Attack = 12, Defense = 22, Speed = 5, MaxDurability = 120, SpecialAbility = "Stomp", SpecialCost = 25 });
        
        // === UNCOMMON LEGS (7) ===
        Add(new Part { Id = "legs_jump", Name = "Jump Jets", Description = "High-altitude jumps.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 10, Defense = 12, Speed = 18, MaxDurability = 85, SpecialAbility = "Leap", SpecialCost = 25 });
        
        Add(new Part { Id = "legs_spider", Name = "Spider Legs", Description = "Multi-jointed climbing legs.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 15, Defense = 15, Speed = 14, MaxDurability = 90, SpecialAbility = "Skitter", SpecialCost = 25 });
        
        Add(new Part { Id = "legs_siege", Name = "Siege Platform", Description = "Immobile but powerful.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 15, Defense = 30, Speed = 0, MaxDurability = 150, SpecialAbility = "Deploy", SpecialCost = 30 });
        
        Add(new Part { Id = "legs_ninja", Name = "Ninja Legs", Description = "Silent and swift.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 18, Defense = 10, Speed = 22, MaxDurability = 75, SpecialAbility = "Vanish", SpecialCost = 30 });
        
        Add(new Part { Id = "legs_thrust", Name = "Thruster Pack", Description = "Burst movement.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Uncommon,
            Attack = 8, Defense = 10, Speed = 25, MaxDurability = 70, SpecialAbility = "Boost", SpecialCost = 20 });
        
        Add(new Part { Id = "legs_heavy", Name = "Heavy Strider", Description = "Slow crushing steps.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Uncommon,
            Attack = 20, Defense = 28, Speed = 4, MaxDurability = 140, SpecialAbility = "Quake", SpecialCost = 35 });
        
        Add(new Part { Id = "legs_gyro", Name = "Gyro Stabilizers", Description = "Perfect balance.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Uncommon,
            Attack = 10, Defense = 18, Speed = 15, MaxDurability = 95, SpecialAbility = "Stabilize", SpecialCost = 20 });
        
        // === RARE LEGS (7) ===
        Add(new Part { Id = "legs_flight", Name = "Flight System", Description = "True aerial mobility.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 12, Defense = 15, Speed = 28, MaxDurability = 80, SpecialAbility = "Soar", SpecialCost = 35 });
        
        Add(new Part { Id = "legs_centaur", Name = "Centaur Frame", Description = "Powerful charging legs.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 25, Defense = 20, Speed = 18, MaxDurability = 110, SpecialAbility = "Charge", SpecialCost = 40 });
        
        Add(new Part { Id = "legs_fortress", Name = "Fortress Base", Description = "Ultimate stability.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 10, Defense = 40, Speed = 2, MaxDurability = 180, SpecialAbility = "Bunker", SpecialCost = 30 });
        
        Add(new Part { Id = "legs_accel", Name = "Accelerator Legs", Description = "Extreme speed.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 20, Defense = 12, Speed = 30, MaxDurability = 85, SpecialAbility = "Blitz", SpecialCost = 30 });
        
        Add(new Part { Id = "legs_artillery", Name = "Artillery Platform", Description = "Mobile siege unit.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Rare,
            Attack = 25, Defense = 25, Speed = 5, MaxDurability = 130, SpecialAbility = "Bombard", SpecialCost = 45 });
        
        Add(new Part { Id = "legs_mantis", Name = "Mantis Legs", Description = "Deadly kicking attacks.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Rare,
            Attack = 30, Defense = 15, Speed = 20, MaxDurability = 90, SpecialAbility = "Mantis Kick", SpecialCost = 35 });
        
        Add(new Part { Id = "legs_phase", Name = "Phase Walker", Description = "Phases through attacks.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Rare,
            Attack = 12, Defense = 20, Speed = 25, MaxDurability = 75, SpecialAbility = "Phase", SpecialCost = 40 });
        
        // === EPIC LEGS (4) ===
        Add(new Part { Id = "legs_omega", Name = "Omega Strider", Description = "Perfect balance of all stats.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 25, Defense = 35, Speed = 25, MaxDurability = 130, SpecialAbility = "Omega Rush", SpecialCost = 40 });
        
        Add(new Part { Id = "legs_dragon", Name = "Dragon Talons", Description = "Legendary crushing claws.",
            Slot = PartSlot.Legs, Type = PartType.Melee, Rarity = Rarity.Epic,
            Attack = 40, Defense = 25, Speed = 22, MaxDurability = 110, SpecialAbility = "Dragon Stomp", SpecialCost = 45 });
        
        Add(new Part { Id = "legs_quantum", Name = "Quantum Drive", Description = "Teleportation capability.",
            Slot = PartSlot.Legs, Type = PartType.Ranged, Rarity = Rarity.Epic,
            Attack = 20, Defense = 20, Speed = 40, MaxDurability = 90, SpecialAbility = "Teleport", SpecialCost = 50 });
        
        Add(new Part { Id = "legs_titan", Name = "Titan Base", Description = "Unmovable foundation.",
            Slot = PartSlot.Legs, Type = PartType.Support, Rarity = Rarity.Epic,
            Attack = 30, Defense = 50, Speed = 5, MaxDurability = 200, SpecialAbility = "Titan Stand", SpecialCost = 35 });
    }
    
    /// <summary>Gets a random part appropriate for the given floor.</summary>
    public static Part GetRandomDrop(int floor, Random? rng = null)
    {
        rng ??= Random.Shared;
        var targetRarity = GetRarityForFloor(floor, rng);
        
        var candidates = GetByRarity(targetRarity).ToList();
        if (candidates.Count == 0)
            candidates = GetByRarity(Rarity.Common).ToList();
            
        return candidates[rng.Next(candidates.Count)].Clone();
    }
    
    /// <summary>Gets a random part of a specific slot appropriate for the given floor.</summary>
    public static Part GetRandomDropBySlot(PartSlot slot, int floor, Random? rng = null)
    {
        rng ??= Random.Shared;
        var targetRarity = GetRarityForFloor(floor, rng);
        
        var candidates = GetBySlot(slot).Where(p => p.Rarity == targetRarity).ToList();
        if (candidates.Count == 0)
            candidates = GetBySlot(slot).Where(p => p.Rarity == Rarity.Common).ToList();
        if (candidates.Count == 0)
            candidates = GetBySlot(slot).ToList();
            
        return candidates[rng.Next(candidates.Count)].Clone();
    }
    
    private static Rarity GetRarityForFloor(int floor, Random rng)
    {
        var roll = rng.Next(100);
        return floor switch
        {
            >= 6 when roll < 15 => Rarity.Epic,
            >= 6 when roll < 40 => Rarity.Rare,
            >= 4 when roll < 10 => Rarity.Epic,
            >= 4 when roll < 35 => Rarity.Rare,
            >= 2 when roll < 25 => Rarity.Rare,
            _ when roll < 30 => Rarity.Uncommon,
            _ => Rarity.Common
        };
    }
}
