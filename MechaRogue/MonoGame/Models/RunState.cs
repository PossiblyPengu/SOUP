namespace MechaRogue.Models;

/// <summary>
/// Roguelike map node for the run.
/// </summary>
public class RunNode
{
    public int Id { get; set; }
    public NodeType Type { get; set; }
    public string Label { get; set; } = "";
    public int Depth { get; set; }
    public List<int> NextNodes { get; set; } = [];
    public bool Visited { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>
/// Full state of a roguelike run.
/// </summary>
public class RunState
{
    public int Floor { get; set; } = 1;
    public int MaxFloors { get; set; } = 15;
    public int Credits { get; set; }
    public List<Medabot> Squad { get; set; } = [];
    public List<MedaPart> SpareParts { get; set; } = [];
    public List<RunNode> Map { get; set; } = [];
    public int CurrentNodeId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    public bool IsGameOver => Losses >= 3;
    public bool IsVictory => Floor > MaxFloors;
}
