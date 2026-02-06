namespace MechaRogue.Models;

/// <summary>
/// A single part that can be equipped on a Mech.
/// </summary>
public class Part
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required PartSlot Slot { get; init; }
    public required PartType Type { get; init; }
    public required Rarity Rarity { get; init; }
    
    // Stats
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Speed { get; init; }
    public int MaxDurability { get; init; } = 100;
    
    // Current state (mutable during battle)
    public int CurrentDurability { get; set; } = 100;
    
    /// <summary>Whether this part is broken and unusable.</summary>
    public bool IsBroken => CurrentDurability <= 0;
    
    /// <summary>Special ability name (mainly for Head parts).</summary>
    public string? SpecialAbility { get; init; }
    
    /// <summary>Medaforce charge required to use special (0-100).</summary>
    public int SpecialCost { get; init; }
    
    /// <summary>Creates a fresh copy of this part with full durability.</summary>
    public Part Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Slot = Slot,
        Type = Type,
        Rarity = Rarity,
        Attack = Attack,
        Defense = Defense,
        Speed = Speed,
        MaxDurability = MaxDurability,
        CurrentDurability = MaxDurability,
        SpecialAbility = SpecialAbility,
        SpecialCost = SpecialCost
    };
}
