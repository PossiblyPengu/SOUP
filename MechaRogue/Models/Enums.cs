namespace MechaRogue.Models;

/// <summary>
/// Which slot a part occupies on a Medabot.
/// </summary>
public enum PartSlot
{
    Head,
    RightArm,
    LeftArm,
    Legs
}

/// <summary>
/// The category of attack a part uses.
/// Shooting = ranged, Melee = close-range, Support = heal/buff/trap.
/// </summary>
public enum ActionType
{
    None,       // passive / legs
    Shooting,
    Melee,
    Support
}

/// <summary>
/// Sub-specialisation within an ActionType.
/// </summary>
public enum PartSkill
{
    // Shooting
    Rifle,
    Gatling,
    Missile,
    Laser,
    Snipe,

    // Melee
    Sword,
    Hammer,
    Grapple,
    Press,

    // Support
    Heal,
    Shield,
    Scan,
    Trap,
    Charge,     // Medaforce charge boost

    // Head specials (limited uses)
    Radar,
    Disrupt,
    AntiAir,

    // Legs – passive
    Movement,

    None
}

/// <summary>
/// Mobility type granted by leg parts.
/// </summary>
public enum LegType
{
    Bipedal,    // balanced
    MultiLegged,// high stability, slow
    Tank,       // high armor, very slow
    Hover,      // fast, low armor
    Flight,     // fastest, fragile
    Submarine   // underwater (rare)
}

/// <summary>
/// Medal affinities – determines Medaforce pool and stat bonuses.
/// </summary>
public enum MedalType
{
    Kabuto,     // beetle – balanced, shooting affinity
    Kuwagata,   // stag beetle – speed, melee affinity
    Cat,        // speed, melee + evasion
    Dog,        // shooting affinity
    Bear,       // power, melee
    Monkey,     // melee specialist
    Tortoise,   // defense, shooting
    Bird,       // speed, support
    Dragon,     // rare – powerful all-round
    Devil,      // rare – brute force
    Angel,      // rare – support
    Alien       // rare – unique Medaforce
}

/// <summary>
/// The phase the battle is currently in.
/// Real-time timer-based like the GBA Medabots games.
/// </summary>
public enum BattlePhase
{
    Charging,       // real-time: gauges filling, waiting for someone to be ready
    ActionSelect,   // player's gauge is full – pick action
    Executing,      // an action is playing out (animation lock)
    BattleOver      // someone won / lost
}

/// <summary>
/// Target selection for an action.
/// </summary>
public enum TargetMode
{
    SingleEnemy,
    AllEnemies,
    Self,
    SingleAlly,
    AllAllies,
    Random
}
