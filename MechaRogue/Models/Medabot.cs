namespace MechaRogue.Models;

/// <summary>
/// A fully assembled Medabot – medal + 4 parts.
/// Head destruction = instant knockout regardless of other parts.
/// </summary>
public class Medabot
{
    public string Name { get; set; } = "Medabot";
    public string ModelId { get; set; } = "KBT-1";
    public Medal Medal { get; set; } = new();
    public MedaPart Head { get; set; } = null!;
    public MedaPart RightArm { get; set; } = null!;
    public MedaPart LeftArm { get; set; } = null!;
    public MedaPart Legs { get; set; } = null!;
    public bool IsPlayerOwned { get; set; }
    public bool IsLeader { get; set; }

    // ── Derived state ─────────────────────────────────────
    public bool IsKnockedOut => Head.IsDestroyed;
    public bool IsFullyDestroyed => Head.IsDestroyed && RightArm.IsDestroyed
                                    && LeftArm.IsDestroyed && Legs.IsDestroyed;

    /// <summary>Total remaining armor across all parts.</summary>
    public int TotalArmor => Head.Armor + RightArm.Armor + LeftArm.Armor + Legs.Armor;
    public int TotalMaxArmor => Head.MaxArmor + RightArm.MaxArmor + LeftArm.MaxArmor + Legs.MaxArmor;
    public double HealthPercent => TotalMaxArmor > 0 ? (double)TotalArmor / TotalMaxArmor : 0;

    /// <summary>Effective speed = legs speed + medal bonus. 0 if legs destroyed.</summary>
    public int EffectiveSpeed
    {
        get
        {
            if (Legs.IsDestroyed) return 1; // crippled but not zero
            return Legs.Speed + Medal.SpeedBonus;
        }
    }

    /// <summary>Evasion = legs evasion. 0 if legs destroyed.</summary>
    public int EffectiveEvasion => Legs.IsDestroyed ? 0 : Legs.Evasion;

    /// <summary>Parts the mech can still use for actions.</summary>
    public IEnumerable<MedaPart> UsableParts
    {
        get
        {
            if (!Head.IsDestroyed && Head.Action != ActionType.None && Head.RemainingUses > 0)
                yield return Head;
            if (!RightArm.IsDestroyed && RightArm.Action != ActionType.None)
                yield return RightArm;
            if (!LeftArm.IsDestroyed && LeftArm.Action != ActionType.None)
                yield return LeftArm;
        }
    }

    public MedaPart[] AllParts => [Head, RightArm, LeftArm, Legs];

    public MedaPart? GetPart(PartSlot slot) => slot switch
    {
        PartSlot.Head => Head,
        PartSlot.RightArm => RightArm,
        PartSlot.LeftArm => LeftArm,
        PartSlot.Legs => Legs,
        _ => null
    };

    /// <summary>Full restore between battles.</summary>
    public void FullRestore()
    {
        foreach (var p in AllParts) p.FullRestore();
        Medal.MedaforceCharge = 0;
    }

    /// <summary>Partial heal between encounters (roguelike rest).</summary>
    public void RestHeal(double percent = 0.3)
    {
        foreach (var p in AllParts)
        {
            int heal = (int)(p.MaxArmor * percent);
            p.Repair(heal);
        }
    }

    public Medabot Clone() => new()
    {
        Name = Name,
        ModelId = ModelId,
        Medal = Medal.Clone(),
        Head = Head.Clone(),
        RightArm = RightArm.Clone(),
        LeftArm = LeftArm.Clone(),
        Legs = Legs.Clone(),
        IsPlayerOwned = IsPlayerOwned,
        IsLeader = IsLeader
    };

    public override string ToString() => $"{Name} ({ModelId}) [{(IsKnockedOut ? "KO" : $"{TotalArmor}/{TotalMaxArmor}")}]";
}
