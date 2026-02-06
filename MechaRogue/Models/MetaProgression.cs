namespace MechaRogue.Models;

/// <summary>
/// Persistent progression that carries between runs.
/// </summary>
public class MetaProgression
{
    /// <summary>Total medals earned across all runs (currency).</summary>
    public int TotalMedals { get; set; }
    
    /// <summary>Parts unlocked for the starting pool.</summary>
    public List<string> UnlockedPartIds { get; set; } = [];
    
    /// <summary>Additional mechs unlocked for starting squad.</summary>
    public List<string> UnlockedMechSlots { get; set; } = [];
    
    /// <summary>Total runs attempted.</summary>
    public int TotalRuns { get; set; }
    
    /// <summary>Total victories.</summary>
    public int Victories { get; set; }
    
    /// <summary>Highest floor reached.</summary>
    public int HighestFloor { get; set; }
    
    /// <summary>Permanent upgrades purchased (upgrade ID -> level).</summary>
    public Dictionary<string, int> Upgrades { get; set; } = [];
    
    /// <summary>Gets the level of a specific upgrade.</summary>
    public int GetUpgradeLevel(string upgradeId) =>
        Upgrades.TryGetValue(upgradeId, out var level) ? level : 0;
    
    /// <summary>Sets the level of a specific upgrade.</summary>
    public void SetUpgradeLevel(string upgradeId, int level) =>
        Upgrades[upgradeId] = level;
}

/// <summary>
/// Available permanent upgrades.
/// </summary>
public static class MetaUpgrades
{
    public static readonly List<UpgradeDefinition> All =
    [
        new("max_squad", "Squad Size", "Start with additional Mechs", [500, 2000, 5000], 3),
        new("starting_medals", "Signing Bonus", "Start runs with bonus medals", [100, 300, 600, 1000], 50),
        new("repair_bonus", "Field Repair", "Heal more between floors", [200, 500, 1000], 10),
        new("medaforce_start", "Medaforce Training", "Start with Medaforce charge", [300, 800], 20),
        new("loot_bonus", "Treasure Hunter", "Find more loot drops", [400, 1200, 2500], 1),
        new("defense_bonus", "Armor Plating", "All parts get bonus defense", [250, 750, 1500], 3),
        new("crit_bonus", "Precision Targeting", "Increased critical hit chance", [350, 1000, 2000], 5),
    ];
    
    public static UpgradeDefinition? GetById(string id) => All.FirstOrDefault(u => u.Id == id);
}

/// <summary>
/// Definition of a purchasable upgrade.
/// </summary>
public record UpgradeDefinition(
    string Id,
    string Name,
    string Description,
    int[] CostsPerLevel,
    int BonusPerLevel)
{
    public int MaxLevel => CostsPerLevel.Length;
    
    public int GetCostForLevel(int level) =>
        level > 0 && level <= CostsPerLevel.Length ? CostsPerLevel[level - 1] : 0;
}
