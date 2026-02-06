namespace MechaRogue.Models;

/// <summary>
/// A combat robot assembled from 4 parts.
/// </summary>
public class Mech
{
    public required string Name { get; set; }
    
    // Equipped parts (null = empty slot)
    public Part? Head { get; set; }
    public Part? RightArm { get; set; }
    public Part? LeftArm { get; set; }
    public Part? Legs { get; set; }
    
    /// <summary>Medaforce charge (0-100), builds when taking damage.</summary>
    public int MedaforceCharge { get; set; }
    
    /// <summary>Whether this mech can still fight.</summary>
    public bool IsOperational => Head is { IsBroken: false };
    
    /// <summary>Total attack power from all functioning parts.</summary>
    public int TotalAttack => 
        (RightArm?.IsBroken == false ? RightArm.Attack : 0) +
        (LeftArm?.IsBroken == false ? LeftArm.Attack : 0);
    
    /// <summary>Total defense from all functioning parts.</summary>
    public int TotalDefense =>
        (Head?.IsBroken == false ? Head.Defense : 0) +
        (RightArm?.IsBroken == false ? RightArm.Defense : 0) +
        (LeftArm?.IsBroken == false ? LeftArm.Defense : 0) +
        (Legs?.IsBroken == false ? Legs.Defense : 0);
    
    /// <summary>Speed determines turn order, from Legs.</summary>
    public int Speed => Legs?.IsBroken == false ? Legs.Speed : 1;
    
    /// <summary>Gets all equipped parts.</summary>
    public IEnumerable<Part> GetParts()
    {
        if (Head != null) yield return Head;
        if (RightArm != null) yield return RightArm;
        if (LeftArm != null) yield return LeftArm;
        if (Legs != null) yield return Legs;
    }
    
    /// <summary>Equips a part in its appropriate slot.</summary>
    public Part? EquipPart(Part part)
    {
        var previous = part.Slot switch
        {
            PartSlot.Head => Head,
            PartSlot.RightArm => RightArm,
            PartSlot.LeftArm => LeftArm,
            PartSlot.Legs => Legs,
            _ => null
        };
        
        switch (part.Slot)
        {
            case PartSlot.Head: Head = part; break;
            case PartSlot.RightArm: RightArm = part; break;
            case PartSlot.LeftArm: LeftArm = part; break;
            case PartSlot.Legs: Legs = part; break;
        }
        
        return previous;
    }
    
    /// <summary>Repairs all parts to full durability.</summary>
    public void RepairAll()
    {
        if (Head != null) Head.CurrentDurability = Head.MaxDurability;
        if (RightArm != null) RightArm.CurrentDurability = RightArm.MaxDurability;
        if (LeftArm != null) LeftArm.CurrentDurability = LeftArm.MaxDurability;
        if (Legs != null) Legs.CurrentDurability = Legs.MaxDurability;
    }
}
