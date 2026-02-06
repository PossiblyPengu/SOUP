namespace MechaRogue.Models;

/// <summary>
/// Result of a single attack or action.
/// </summary>
public class ActionResult
{
    public required string Description { get; init; }
    public int DamageDealt { get; init; }
    public bool PartDestroyed { get; init; }
    public Part? AffectedPart { get; init; }
    public bool IsCritical { get; init; }
    public bool WasEvaded { get; init; }
    public float TypeAdvantage { get; init; } = 1.0f;
}

/// <summary>
/// Result of an entire battle.
/// </summary>
public class BattleResult
{
    public required bool PlayerWon { get; init; }
    public required List<Part> LootDrops { get; init; }
    public required int MedalsEarned { get; init; }
    public required List<ActionResult> BattleLog { get; init; }
}
