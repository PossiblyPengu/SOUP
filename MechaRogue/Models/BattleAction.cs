namespace MechaRogue.Models;

/// <summary>
/// Represents a chosen action for one turn.
/// </summary>
public class BattleAction
{
    public Medabot Attacker { get; init; } = null!;
    public MedaPart UsingPart { get; init; } = null!;
    public Medabot Target { get; init; } = null!;
    public PartSlot TargetedSlot { get; init; } = PartSlot.Head;
    public bool IsMedaforce { get; init; }
    public MedaforceAttack? MedaforceAttack { get; init; }

    /// <summary>Speed at the time the action was queued (for ordering).</summary>
    public int Priority { get; set; }
}

/// <summary>
/// Result of resolving a single action.
/// </summary>
public class ActionResult
{
    public Medabot Attacker { get; init; } = null!;
    public Medabot Target { get; init; } = null!;
    public MedaPart? UsingPart { get; init; }
    public PartSlot TargetedSlot { get; set; }
    public bool Hit { get; set; }
    public bool Critical { get; set; }
    public int Damage { get; set; }
    public int HealAmount { get; set; }
    public bool PartDestroyed { get; set; }
    public bool TargetKnockedOut { get; set; }
    public bool IsMedaforce { get; set; }
    public string Narration { get; set; } = string.Empty;
}

/// <summary>
/// Summary of a complete battle.
/// </summary>
public class BattleResult
{
    public bool PlayerWon { get; set; }
    public int TurnsElapsed { get; set; }
    public List<ActionResult> Log { get; } = [];
    public MedaPart? SpoilPart { get; set; }  // part won from the loser
    public int XpGained { get; set; }
    public int CreditsGained { get; set; }
}
