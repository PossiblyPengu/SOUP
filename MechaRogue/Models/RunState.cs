namespace MechaRogue.Models;

/// <summary>
/// Represents a node on the roguelike run map.
/// </summary>
public enum NodeType
{
    Battle,
    EliteBattle,
    Shop,
    Rest,
    Event,
    Boss
}

public class RunNode
{
    public int Id { get; init; }
    public NodeType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Depth { get; init; }           // floor level
    public List<int> NextNodes { get; init; } = [];
    public bool Visited { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>
/// State for a roguelike run – persists across battles.
/// </summary>
public class RunState
{
    public int Floor { get; set; } = 1;
    public int MaxFloors { get; set; } = 15;
    public int Credits { get; set; } = 100;
    public List<Medabot> Squad { get; set; } = [];    // player's team (up to 3)
    public List<MedaPart> SpareParts { get; set; } = []; // inventory
    public List<RunNode> Map { get; set; } = [];
    public int CurrentNodeId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public bool IsGameOver => Squad.Count == 0 || Squad.All(m => m.IsKnockedOut);
    public bool IsVictory => Floor > MaxFloors;
}
