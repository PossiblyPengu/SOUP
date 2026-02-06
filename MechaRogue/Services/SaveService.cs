using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechaRogue.Models;

namespace MechaRogue.Services;

/// <summary>
/// Handles saving and loading game state to disk.
/// </summary>
public class SaveService
{
    private static readonly string SaveFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MechaRogue");
    
    private static readonly string ProgressionFile = Path.Combine(SaveFolder, "progression.json");
    private static readonly string RunStateFile = Path.Combine(SaveFolder, "run_state.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    /// <summary>
    /// Ensures the save folder exists.
    /// </summary>
    public static void EnsureSaveFolder()
    {
        if (!Directory.Exists(SaveFolder))
        {
            Directory.CreateDirectory(SaveFolder);
        }
    }
    
    /// <summary>
    /// Saves meta-progression data.
    /// </summary>
    public static void SaveProgression(MetaProgression progression)
    {
        EnsureSaveFolder();
        var json = JsonSerializer.Serialize(progression, JsonOptions);
        File.WriteAllText(ProgressionFile, json);
    }
    
    /// <summary>
    /// Loads meta-progression data.
    /// </summary>
    public static MetaProgression LoadProgression()
    {
        if (!File.Exists(ProgressionFile))
        {
            return new MetaProgression();
        }
        
        try
        {
            var json = File.ReadAllText(ProgressionFile);
            return JsonSerializer.Deserialize<MetaProgression>(json, JsonOptions) ?? new MetaProgression();
        }
        catch
        {
            return new MetaProgression();
        }
    }
    
    /// <summary>
    /// Saves the current run state (for continue functionality).
    /// </summary>
    public static void SaveRunState(RunStateSave runState)
    {
        EnsureSaveFolder();
        var json = JsonSerializer.Serialize(runState, JsonOptions);
        File.WriteAllText(RunStateFile, json);
    }
    
    /// <summary>
    /// Loads a saved run state.
    /// </summary>
    public static RunStateSave? LoadRunState()
    {
        if (!File.Exists(RunStateFile))
        {
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(RunStateFile);
            return JsonSerializer.Deserialize<RunStateSave>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Deletes the current run save (on run end).
    /// </summary>
    public static void DeleteRunState()
    {
        if (File.Exists(RunStateFile))
        {
            File.Delete(RunStateFile);
        }
    }
    
    /// <summary>
    /// Checks if there's a saved run to continue.
    /// </summary>
    public static bool HasSavedRun() => File.Exists(RunStateFile);
}

/// <summary>
/// Serializable version of RunState for saving.
/// </summary>
public class RunStateSave
{
    public int CurrentFloor { get; set; }
    public int MaxFloors { get; set; }
    public int Medals { get; set; }
    public int EnemiesDefeated { get; set; }
    public int TotalDamageDealt { get; set; }
    public List<MechSave> PlayerSquad { get; set; } = [];
    public List<PartSave> Inventory { get; set; } = [];
}

/// <summary>
/// Serializable version of Mech for saving.
/// </summary>
public class MechSave
{
    public required string Name { get; set; }
    public int MedaforceCharge { get; set; }
    public PartSave? Head { get; set; }
    public PartSave? RightArm { get; set; }
    public PartSave? LeftArm { get; set; }
    public PartSave? Legs { get; set; }
    
    public static MechSave FromMech(Mech mech) => new()
    {
        Name = mech.Name,
        MedaforceCharge = mech.MedaforceCharge,
        Head = mech.Head != null ? PartSave.FromPart(mech.Head) : null,
        RightArm = mech.RightArm != null ? PartSave.FromPart(mech.RightArm) : null,
        LeftArm = mech.LeftArm != null ? PartSave.FromPart(mech.LeftArm) : null,
        Legs = mech.Legs != null ? PartSave.FromPart(mech.Legs) : null
    };
    
    public Mech ToMech()
    {
        var mech = new Mech { Name = Name, MedaforceCharge = MedaforceCharge };
        if (Head != null) mech.Head = Head.ToPart();
        if (RightArm != null) mech.RightArm = RightArm.ToPart();
        if (LeftArm != null) mech.LeftArm = LeftArm.ToPart();
        if (Legs != null) mech.Legs = Legs.ToPart();
        return mech;
    }
}

/// <summary>
/// Serializable version of Part for saving (stores ID + current durability).
/// </summary>
public class PartSave
{
    public required string Id { get; set; }
    public int CurrentDurability { get; set; }
    
    public static PartSave FromPart(Part part) => new()
    {
        Id = part.Id,
        CurrentDurability = part.CurrentDurability
    };
    
    public Part? ToPart()
    {
        var template = PartCatalog.GetById(Id);
        if (template == null) return null;
        
        var part = template.Clone();
        part.CurrentDurability = CurrentDurability;
        return part;
    }
}
