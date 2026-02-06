using System.Media;
using System.Windows;
using System.Windows.Media;

namespace MechaRogue.Services;

/// <summary>
/// Sound effect types available in the game.
/// </summary>
public enum SoundEffect
{
    Attack,
    Special,
    Defend,
    PartBreak,
    Critical,
    Victory,
    Defeat,
    BattleStart,
    RunStart,
    NextFloor,
    Equip,
    Select,
    Upgrade
}

/// <summary>
/// Handles playing sound effects using system sounds and beeps.
/// Uses Windows system sounds for a simple implementation without external audio files.
/// </summary>
public class SoundService
{
    private bool _enabled = true;
    private readonly Dictionary<SoundEffect, Action> _soundActions;
    
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }
    
    public SoundService()
    {
        _soundActions = new Dictionary<SoundEffect, Action>
        {
            [SoundEffect.Attack] = () => PlayBeep(400, 50),
            [SoundEffect.Special] = () => PlaySequence([(600, 50), (800, 50), (1000, 100)]),
            [SoundEffect.Defend] = () => PlayBeep(300, 100),
            [SoundEffect.PartBreak] = () => PlaySequence([(500, 50), (300, 100), (200, 150)]),
            [SoundEffect.Critical] = () => PlaySequence([(800, 30), (1000, 30), (1200, 50)]),
            [SoundEffect.Victory] = () => PlaySequence([(523, 100), (659, 100), (784, 100), (1047, 200)]),
            [SoundEffect.Defeat] = () => PlaySequence([(400, 150), (350, 150), (300, 150), (250, 300)]),
            [SoundEffect.BattleStart] = () => PlaySequence([(440, 80), (550, 80), (660, 120)]),
            [SoundEffect.RunStart] = () => PlaySequence([(330, 60), (440, 60), (550, 60), (660, 100)]),
            [SoundEffect.NextFloor] = () => PlaySequence([(500, 80), (600, 80), (700, 120)]),
            [SoundEffect.Equip] = () => PlayBeep(600, 50),
            [SoundEffect.Select] = () => PlayBeep(500, 30),
            [SoundEffect.Upgrade] = () => PlaySequence([(400, 50), (600, 50), (800, 100)])
        };
    }
    
    /// <summary>
    /// Plays a sound effect asynchronously.
    /// </summary>
    public void Play(SoundEffect effect)
    {
        if (!_enabled) return;
        
        if (_soundActions.TryGetValue(effect, out var action))
        {
            // Run sound on background thread to not block UI
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch
                {
                    // Ignore audio errors
                }
            });
        }
    }
    
    /// <summary>
    /// Plays a simple beep.
    /// </summary>
    private static void PlayBeep(int frequency, int duration)
    {
        try
        {
            Console.Beep(frequency, duration);
        }
        catch
        {
            // Some systems don't support Console.Beep
        }
    }
    
    /// <summary>
    /// Plays a sequence of beeps.
    /// </summary>
    private static void PlaySequence((int freq, int duration)[] notes)
    {
        foreach (var (freq, duration) in notes)
        {
            PlayBeep(freq, duration);
        }
    }
}
