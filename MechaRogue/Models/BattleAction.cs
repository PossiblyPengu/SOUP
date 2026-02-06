namespace MechaRogue.Models;

/// <summary>
/// Types of actions a Mech can take in battle.
/// </summary>
public enum BattleActionType
{
    /// <summary>Attack with right arm.</summary>
    AttackRight,
    
    /// <summary>Attack with left arm.</summary>
    AttackLeft,
    
    /// <summary>Use head's special ability (costs Medaforce).</summary>
    Special,
    
    /// <summary>Defend to reduce incoming damage.</summary>
    Defend,
    
    /// <summary>Charge Medaforce manually.</summary>
    Charge
}

/// <summary>
/// A queued action in the battle system.
/// </summary>
public class BattleAction
{
    public required Mech Actor { get; init; }
    public required BattleActionType ActionType { get; init; }
    public Mech? Target { get; init; }
    public Part? TargetPart { get; init; }
}
