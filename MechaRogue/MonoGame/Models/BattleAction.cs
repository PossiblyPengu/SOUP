namespace MechaRogue.Models;

/// <summary>
/// A queued battle action — who attacks, with what, targeting whom.
/// </summary>
public class BattleAction
{
    public Medabot Attacker { get; set; } = null!;
    public MedaPart UsingPart { get; set; } = null!;
    public Medabot Target { get; set; } = null!;
    public PartSlot TargetedSlot { get; set; } = PartSlot.Head;
    public bool IsMedaforce { get; set; }
    public bool IsDefend { get; set; }
    public MedaforceAttack? MedaforceAttack { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// Result of resolving a single action.
/// </summary>
public class ActionResult
{
    public Medabot Attacker { get; set; } = null!;
    public Medabot Target { get; set; } = null!;
    public MedaPart? UsingPart { get; set; }
    public PartSlot TargetedSlot { get; set; }
    public bool Hit { get; set; }
    public bool Critical { get; set; }
    public int Damage { get; set; }
    public int HealAmount { get; set; }
    public bool PartDestroyed { get; set; }
    public bool TargetKnockedOut { get; set; }
    public bool IsMedaforce { get; set; }
    public string Narration { get; set; } = "";
}

/// <summary>
/// Full result of a completed battle.
/// </summary>
public class BattleResult
{
    public bool PlayerWon { get; set; }
    public int TurnsElapsed { get; set; }
    public List<string> Log { get; set; } = [];
    public MedaPart? SpoilPart { get; set; }
    public int XpGained { get; set; }
    public int CreditsGained { get; set; }
}
