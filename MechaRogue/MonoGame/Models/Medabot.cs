namespace MechaRogue.Models;

/// <summary>
/// A complete Medabot: Medal (AI core) + 4 parts.
/// Head destruction = instant KO (classic Medabots rule).
/// </summary>
public class Medabot
{
    public string Name { get; set; } = "";
    public string ModelId { get; set; } = "";

    public Medal Medal { get; set; } = new();

    public MedaPart Head { get; set; } = new();
    public MedaPart RightArm { get; set; } = new();
    public MedaPart LeftArm { get; set; } = new();
    public MedaPart Legs { get; set; } = new();

    public bool IsPlayerOwned { get; set; }
    public bool IsLeader { get; set; }

    /// <summary>Guard stance — halves incoming damage until this bot's next action.</summary>
    public bool IsDefending { get; set; }

    /// <summary>Per-bot charge gauge for 3v3 battles (0–100).</summary>
    public double ChargeGauge { get; set; }

    /// <summary>Head destroyed = KO (classic Medabots rule).</summary>
    public bool IsKnockedOut => Head.IsDestroyed;

    public int EffectiveSpeed => Legs.IsDestroyed ? 1 : Legs.Speed + Medal.SpeedBonus;
    public int EffectiveEvasion => Legs.IsDestroyed ? 0 : Legs.Evasion;

    public double TotalArmor => Head.Armor + RightArm.Armor + LeftArm.Armor + Legs.Armor;
    public double MaxTotalArmor => Head.MaxArmor + RightArm.MaxArmor + LeftArm.MaxArmor + Legs.MaxArmor;
    public double HealthPercent => MaxTotalArmor > 0 ? TotalArmor / MaxTotalArmor : 0;

    /// <summary>Parts that can still act (not destroyed, and have uses if head).</summary>
    public IEnumerable<MedaPart> UsableParts
    {
        get
        {
            if (!Head.IsDestroyed && Head.Action != ActionType.None && Head.RemainingUses > 0)
                yield return Head;
            if (!RightArm.IsDestroyed)
                yield return RightArm;
            if (!LeftArm.IsDestroyed)
                yield return LeftArm;
        }
    }

    public IEnumerable<MedaPart> AllParts
    {
        get
        {
            yield return Head;
            yield return RightArm;
            yield return LeftArm;
            yield return Legs;
        }
    }

    public MedaPart? GetPart(PartSlot slot) => slot switch
    {
        PartSlot.Head => Head,
        PartSlot.RightArm => RightArm,
        PartSlot.LeftArm => LeftArm,
        PartSlot.Legs => Legs,
        _ => null
    };

    public void FullRestore()
    {
        foreach (var p in AllParts) p.FullRestore();
        Medal.MedaforceCharge = 0;
        IsDefending = false;
        ChargeGauge = 0;
    }

    public void RestHeal(double percent)
    {
        foreach (var p in AllParts)
        {
            if (!p.IsDestroyed)
                p.Repair((int)(p.MaxArmor * percent));
        }
    }

    public Medabot Clone() => new()
    {
        Name = Name,
        ModelId = ModelId,
        Medal = new Medal { Name = Medal.Name, Type = Medal.Type, Level = Medal.Level },
        Head = Head.Clone(),
        RightArm = RightArm.Clone(),
        LeftArm = LeftArm.Clone(),
        Legs = Legs.Clone(),
        IsPlayerOwned = IsPlayerOwned,
        IsLeader = IsLeader
    };
}
