namespace MechaRogue.Models;

/// <summary>
/// A single Medabot part (head, arm, leg).
/// Contains stats, durability (armor), and special skill.
/// </summary>
public class MedaPart
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public PartSlot Slot { get; set; }
    public ActionType Action { get; set; }
    public PartSkill Skill { get; set; }
    public int Tier { get; set; } = 1;

    // Durability
    public int MaxArmor { get; set; }
    public int Armor { get; set; }

    // Combat stats
    public int Power { get; set; }
    public int Accuracy { get; set; }
    public int Speed { get; set; }

    // Head parts have limited uses per battle
    public int MaxUses { get; set; }
    public int RemainingUses { get; set; }

    // Legs-specific
    public LegType LegType { get; set; }
    public int Evasion { get; set; }
    public int Propulsion { get; set; }

    public bool IsDestroyed => Armor <= 0;
    public bool IsHead => Slot == PartSlot.Head;
    public double ArmorPercent => MaxArmor > 0 ? (double)Armor / MaxArmor : 0;

    public int TakeDamage(int amount)
    {
        int dealt = Math.Min(Armor, amount);
        Armor = Math.Max(0, Armor - amount);
        return dealt;
    }

    public void Repair(int amount) => Armor = Math.Min(MaxArmor, Armor + amount);
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
        Armor = Armor,
        Power = Power,
        Accuracy = Accuracy,
        Speed = Speed,
        MaxUses = MaxUses,
        RemainingUses = RemainingUses,
        LegType = LegType,
        Evasion = Evasion,
        Propulsion = Propulsion
    };
}
