namespace MechaRogue.Models;

/// <summary>
/// Attack/defense type for rock-paper-scissors style advantages.
/// </summary>
public enum PartType
{
    /// <summary>Melee attacks - beats Ranged.</summary>
    Melee,
    
    /// <summary>Ranged attacks - beats Support.</summary>
    Ranged,
    
    /// <summary>Support abilities - beats Melee.</summary>
    Support
}
