namespace MechaRogue.Models;

/// <summary>Which slot a MedaPart occupies on a Medabot.</summary>
public enum PartSlot
{
    Head,
    RightArm,
    LeftArm,
    Legs
}

/// <summary>Broad action category of a part.</summary>
public enum ActionType
{
    None,
    Shooting,
    Melee,
    Support
}

/// <summary>Specific skill/weapon type.</summary>
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
    Charge,
    Radar,
    Disrupt,
    AntiAir,
    // Legs / passive
    Movement,
    None
}

/// <summary>Locomotion type determines evasion/speed profile.</summary>
public enum LegType
{
    Bipedal,
    MultiLegged,
    Tank,
    Hover,
    Flight,
    Submarine
}

/// <summary>Medal compatibility type — determines affinity bonuses.</summary>
public enum MedalType
{
    Kabuto,
    Kuwagata,
    Cat,
    Monkey,
    Bear,
    Dragon,
    Tortoise,
    Phoenix,
    Angel,
    Devil,
    Alien,
    Knight
}

/// <summary>Current phase of a battle.</summary>
public enum BattlePhase
{
    Charging,
    /// <summary>Player picks: Attack / Defend / Medaforce for the ready bot.</summary>
    ActionMenu,
    /// <summary>Player picks which arm/head to use.</summary>
    PartSelect,
    /// <summary>Player picks which enemy bot to target.</summary>
    TargetBotSelect,
    /// <summary>Player picks which part slot on the target.</summary>
    TargetPartSelect,
    /// <summary>Attack animation playing.</summary>
    Executing,
    BattleOver
}

/// <summary>Who the action targets.</summary>
public enum TargetMode
{
    SingleEnemy,
    AllEnemies,
    Self,
    SingleAlly,
    AllAllies
}

/// <summary>Roguelike map node types.</summary>
public enum NodeType
{
    Battle,
    EliteBattle,
    Shop,
    Rest,
    Event,
    Boss
}

/// <summary>Screen states for the game.</summary>
public enum ScreenState
{
    Title,
    Map,
    Battle,
    Shop,
    Rest,
    Event,
    GameOver,
    Victory
}
