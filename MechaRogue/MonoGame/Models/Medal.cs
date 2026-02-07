namespace MechaRogue.Models;

/// <summary>
/// Medabot Medal — the AI core. Determines affinity bonuses, level,
/// Medaforce charge, and available Medaforce attacks.
/// </summary>
public class Medal
{
    public string Name { get; set; } = "Medal";
    public MedalType Type { get; set; }
    public int Level { get; set; } = 1;
    public int XP { get; set; }

    // Medaforce
    public int MedaforceCharge { get; set; }
    public int MaxMedaforce { get; set; } = 100;
    public bool CanUseMedaforce => MedaforceCharge >= MaxMedaforce;

    // Affinity bonuses by medal type
    public int ShootingBonus => Type switch
    {
        MedalType.Kabuto => 5 + Level,
        MedalType.Phoenix => 4 + Level,
        MedalType.Dragon => 3 + Level,
        MedalType.Alien => 3 + Level,
        _ => 0
    };

    public int MeleeBonus => Type switch
    {
        MedalType.Kuwagata => 5 + Level,
        MedalType.Bear => 4 + Level,
        MedalType.Cat => 3 + Level,
        MedalType.Knight => 3 + Level,
        _ => 0
    };

    public int SupportBonus => Type switch
    {
        MedalType.Angel => 5 + Level,
        MedalType.Tortoise => 4 + Level,
        MedalType.Monkey => 3 + Level,
        MedalType.Devil => 3 + Level,
        _ => 0
    };

    public int SpeedBonus => Type switch
    {
        MedalType.Cat => 4 + Level,
        MedalType.Phoenix => 3 + Level,
        MedalType.Alien => 3 + Level,
        _ => 0
    };

    public void AddCharge(int amount) =>
        MedaforceCharge = Math.Min(MaxMedaforce, MedaforceCharge + amount);

    public void SpendMedaforce() => MedaforceCharge = 0;

    /// <summary>Gain XP. Returns true if leveled up.</summary>
    public bool GainXp(int amount)
    {
        XP += amount;
        int needed = Level * 50;
        if (XP >= needed)
        {
            XP -= needed;
            Level++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns available Medaforce attacks based on medal level.
    /// Higher levels unlock stronger attacks.
    /// </summary>
    public List<MedaforceAttack> GetAvailableAttacks()
    {
        var attacks = new List<MedaforceAttack>();

        // Tier 1: available from level 1
        attacks.Add(new MedaforceAttack
        {
            Name = $"{Type} Strike",
            Description = "Focused Medaforce blast.",
            Power = 40 + Level * 8,
            HitsAll = false
        });

        // Tier 2: level 3+
        if (Level >= 3)
        {
            attacks.Add(new MedaforceAttack
            {
                Name = $"{Type} Barrage",
                Description = "Multi-target Medaforce wave.",
                Power = 30 + Level * 6,
                HitsAll = true
            });
        }

        // Tier 3: level 5+
        if (Level >= 5)
        {
            attacks.Add(new MedaforceAttack
            {
                Name = $"{Type} Annihilation",
                Description = "Ultimate Medaforce!",
                Power = 60 + Level * 10,
                HitsAll = false
            });
        }

        return attacks;
    }
}

/// <summary>
/// A specific Medaforce attack option.
/// </summary>
public class MedaforceAttack
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Power { get; set; }
    public bool HitsAll { get; set; }
}
