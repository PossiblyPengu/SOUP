using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechaRogue.Models;
using MechaRogue.Services;

namespace MechaRogue.ViewModels;

/// <summary>
/// ViewModel for a single Mech, providing observable properties for UI binding.
/// </summary>
public partial class MechViewModel : ObservableObject
{
    private readonly Mech _mech;
    private readonly bool _isEnemy;
    private readonly int _floor;
    
    public MechViewModel(Mech mech, bool isEnemy = false, int floor = 1)
    {
        _mech = mech;
        _isEnemy = isEnemy;
        _floor = floor;
    }
    
    public Mech Model => _mech;
    public string Name => _mech.Name;
    public bool IsOperational => _mech.IsOperational;
    public int MedaforceCharge => _mech.MedaforceCharge;
    public int TotalAttack => _mech.TotalAttack;
    public int TotalDefense => _mech.TotalDefense;
    public int Speed => _mech.Speed;
    
    public Part? Head => _mech.Head;
    public Part? RightArm => _mech.RightArm;
    public Part? LeftArm => _mech.LeftArm;
    public Part? Legs => _mech.Legs;
    
    /// <summary>Gets the pixel sprite for this mech.</summary>
    public PixelSprite Sprite => _isEnemy 
        ? MechSprites.GetEnemySprite(_mech.Name, _floor)
        : MechSprites.GetPlayerSprite(_mech.Name);
    
    /// <summary>Whether to flip the sprite horizontally (enemies face left).</summary>
    public bool IsFlipped => _isEnemy;
    
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsOperational));
        OnPropertyChanged(nameof(MedaforceCharge));
        OnPropertyChanged(nameof(TotalAttack));
        OnPropertyChanged(nameof(TotalDefense));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(Head));
        OnPropertyChanged(nameof(RightArm));
        OnPropertyChanged(nameof(LeftArm));
        OnPropertyChanged(nameof(Legs));
    }
}
