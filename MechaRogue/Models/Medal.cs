namespace MechaRogue.Models;

/// <summary>
/// A Medabot Medal – the brain/soul of the mech.
/// Determines Medaforce pool, stat bonuses, and affinity.
/// Medals evolve as they gain XP, unlocking stronger Medaforce attacks.
/// </summary>
public class Medal
{
    public string Name { get; init; } = "Medal";
    public MedalType Type { get; init; } = MedalType.Kabuto;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }

    // Medaforce
    public int MedaforceCharge { get; set; }
    public int MaxMedaforce { get; set; } = 100;

    /// <summary>Medaforce charges when the mech takes/deals damage.</summary>
    public void AddCharge(int amount)
    {
        MedaforceCharge = Math.Min(MaxMedaforce, MedaforceCharge + Math.Max(0, amount));
    }

    public bool CanUseMedaforce => MedaforceCharge >= MaxMedaforce;

    /// <summary>Consume all charge to fire Medaforce.</summary>
    public void SpendMedaforce()
    {
        MedaforceCharge = 0;
    }

    // ── Affinity bonuses ──────────────────────────────────
    /// <summary>Bonus accuracy % for compatible part actions.</summary>
    public int ShootingBonus => Type switch
    {
        MedalType.Kabuto => 10,
        MedalType.Dog => 15,
        MedalType.Tortoise => 10,
        MedalType.Dragon => 8,
        _ => 0
    };

    public int MeleeBonus => Type switch
    {
        MedalType.Kuwagata => 10,
        MedalType.Cat => 12,
        MedalType.Bear => 15,
        MedalType.Monkey => 12,
        MedalType.Devil => 10,
        MedalType.Dragon => 8,
        _ => 0
    };

    public int SupportBonus => Type switch
    {
        MedalType.Bird => 15,
        MedalType.Angel => 15,
        MedalType.Dragon => 8,
        _ => 0
    };

    public int SpeedBonus => Type switch
    {
        MedalType.Kuwagata => 5,
        MedalType.Cat => 8,
        MedalType.Bird => 5,
        MedalType.Alien => 10,
        _ => 0
    };

    // ── XP / Level ────────────────────────────────────────
    public int XpToNextLevel => Level * 50;

    public bool GainXp(int xp)
    {
        Experience += xp;
        if (Experience >= XpToNextLevel)
        {
            Experience -= XpToNextLevel;
            Level++;
            MaxMedaforce = 100 + (Level - 1) * 10;
            return true; // leveled up
        }
        return false;
    }

    // ── Medaforce attacks (unlock by level) ───────────────
    public List<MedaforceAttack> GetAvailableAttacks()
    {
        var attacks = new List<MedaforceAttack>();
        attacks.Add(new MedaforceAttack
        {
            Name = $"{Type} Strike",
            Description = "Powerful attack that ignores evasion",
            Power = 40 + Level * 5,
            HitsAll = false
        });

        if (Level >= 3)
        {
            attacks.Add(new MedaforceAttack
            {
                Name = $"{Type} Barrage",
                Description = "Hits all enemies for moderate damage",
                Power = 25 + Level * 3,
                HitsAll = true
            });
        }

        if (Level >= 5)
        {
            attacks.Add(new MedaforceAttack
            {
                Name = $"{Type} Annihilation",
                Description = "Devastating single target attack",
                Power = 60 + Level * 8,
                HitsAll = false
            });
        }

        return attacks;
    }

    public Medal Clone() => new()
    {
        Name = Name,
        Type = Type,
        Level = Level,
        Experience = Experience,
        MedaforceCharge = 0,
        MaxMedaforce = MaxMedaforce
    };
}

public class MedaforceAttack
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Power { get; init; }
    public bool HitsAll { get; init; }
}
