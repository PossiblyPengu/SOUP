namespace MechaRogue.Models;

/// <summary>
/// Tracks the current state of a roguelite run.
/// </summary>
public class RunState
{
    /// <summary>Current floor/battle number (1-based).</summary>
    public int CurrentFloor { get; set; } = 1;
    
    /// <summary>Maximum floors before the boss.</summary>
    public int MaxFloors { get; set; } = 7;
    
    /// <summary>Player's squad of Mechs.</summary>
    public List<Mech> PlayerSquad { get; set; } = [];
    
    /// <summary>Spare parts in inventory.</summary>
    public List<Part> Inventory { get; set; } = [];
    
    /// <summary>Currency earned this run.</summary>
    public int Medals { get; set; }
    
    /// <summary>Whether the run is still active.</summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>Whether the player won the run.</summary>
    public bool Victory { get; set; }
    
    /// <summary>Total enemies defeated this run.</summary>
    public int EnemiesDefeated { get; set; }
    
    /// <summary>Total damage dealt this run.</summary>
    public int TotalDamageDealt { get; set; }
}
