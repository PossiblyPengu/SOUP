namespace MechaRogue.Models;

/// <summary>
/// The slot where a part can be equipped on a Mech.
/// </summary>
public enum PartSlot
{
    /// <summary>Head - determines special ability and targeting.</summary>
    Head,
    
    /// <summary>Right Arm - primary attack weapon.</summary>
    RightArm,
    
    /// <summary>Left Arm - secondary/support weapon.</summary>
    LeftArm,
    
    /// <summary>Legs - mobility, evasion, and terrain bonuses.</summary>
    Legs
}
