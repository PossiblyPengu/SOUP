namespace MechaRogue.Models;

/// <summary>
/// Persistent progression that carries between runs.
/// </summary>
public class MetaProgression
{
    /// <summary>Total medals earned across all runs.</summary>
    public int TotalMedals { get; set; }
    
    /// <summary>Parts unlocked for the starting pool.</summary>
    public List<string> UnlockedPartIds { get; set; } = [];
    
    /// <summary>Total runs attempted.</summary>
    public int TotalRuns { get; set; }
    
    /// <summary>Total victories.</summary>
    public int Victories { get; set; }
    
    /// <summary>Highest floor reached.</summary>
    public int HighestFloor { get; set; }
    
    /// <summary>Permanent upgrades purchased.</summary>
    public Dictionary<string, int> Upgrades { get; set; } = [];
}
