namespace MechaRogue.Models;

/// <summary>
/// A single Medapart – can be head, arm or legs.
/// Each part has armor (HP), a skill, and stats that feed into battle calculations.
/// </summary>
public class MedaPart
{
    // ── Identity ──────────────────────────────────────────
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PartSlot Slot { get; init; }
    public ActionType Action { get; init; }
    public PartSkill Skill { get; init; }
    public int Tier { get; init; } = 1;             // 1-5 rarity

    // ── Combat stats ──────────────────────────────────────
    public int MaxArmor { get; set; } = 30;         // part HP
    public int Armor { get; set; } = 30;              // current HP
    public int Power { get; set; } = 20;             // base damage
    public int Accuracy { get; set; } = 80;          // base hit % (0-100)
    public int Speed { get; set; } = 20;             // determines turn order contribution

    // ── Head-specific ─────────────────────────────────────
    /// <summary>Max uses for head special ability. 0 = unlimited (arms/legs).</summary>
    public int MaxUses { get; init; }
    public int RemainingUses { get; set; }

    // ── Leg-specific ──────────────────────────────────────
    public LegType LegType { get; init; } = LegType.Bipedal;
    /// <summary>Evasion bonus from legs (0-30).</summary>
    public int Evasion { get; init; }
    /// <summary>Propulsion stat – how fast the mech moves to melee range.</summary>
    public int Propulsion { get; init; } = 10;

    // ── Derived ───────────────────────────────────────────
    public bool IsDestroyed => Armor <= 0;
    public bool IsHead => Slot == PartSlot.Head;
    public double ArmorPercent => MaxArmor > 0 ? (double)Armor / MaxArmor : 0;

    /// <summary>Take damage. Returns actual damage dealt.</summary>
    public int TakeDamage(int amount)
    {
        int actual = Math.Min(Armor, Math.Max(0, amount));
        Armor -= actual;
        return actual;
    }

    /// <summary>Repair this part by the given amount.</summary>
    public void Repair(int amount)
    {
        Armor = Math.Min(MaxArmor, Armor + Math.Max(0, amount));
    }

    /// <summary>Full restore (between battles).</summary>
    public void FullRestore()
    {
        Armor = MaxArmor;
        RemainingUses = MaxUses;
    }

    public MedaPart Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Slot = Slot,
        Action = Action,
        Skill = Skill,
        Tier = Tier,
        MaxArmor = MaxArmor,
        Armor = MaxArmor,
        Power = Power,
        Accuracy = Accuracy,
        Speed = Speed,
        MaxUses = MaxUses,
        RemainingUses = MaxUses,
        LegType = LegType,
        Evasion = Evasion,
        Propulsion = Propulsion
    };

    public override string ToString() => $"{Name} [{Slot}] ({Armor}/{MaxArmor})";
}
