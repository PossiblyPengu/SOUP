using System;

namespace FriendshipDungeonMG;

public enum Direction { North, East, South, West }
public enum TileType { Floor, Wall, Door, StairsDown, Chest, Trap, Shrine }
public enum EnemyType { SmileDog, MeatChild, GrandmasTwin, ManInWall, FriendlyHelper, YourReflection, ItsListening, TheHost }
public enum GameState { Exploring, Combat, GameOver, Victory }
public enum SpriteType { Stairs, Chest, Trap, Shrine, Torch, Pillar }
public enum WeaponType { SharpCrayon, TeddyMaw, JackInTheGun, MyFirstNailer, SippyCannon, MusicBoxDancer }
public enum WeaponRarity { Common, Uncommon, Rare, Epic, Legendary }

public class Weapon
{
    public string Name { get; set; }
    public string Description { get; set; }
    public WeaponType Type { get; set; }
    public WeaponRarity Rarity { get; set; }
    public int BaseDamage { get; set; }
    public int AttackBonus { get; set; }
    public int CritChance { get; set; } // Percentage
    public float CritMultiplier { get; set; }
    public string SpecialEffect { get; set; } // "lifesteal", "bleed", "mana", etc.

    public Weapon(string name, string description, WeaponType type, WeaponRarity rarity, 
                  int baseDamage, int attackBonus = 0, int critChance = 5, float critMultiplier = 1.5f, 
                  string specialEffect = "")
    {
        Name = name;
        Description = description;
        Type = type;
        Rarity = rarity;
        BaseDamage = baseDamage;
        AttackBonus = attackBonus;
        CritChance = critChance;
        CritMultiplier = critMultiplier;
        SpecialEffect = specialEffect;
    }

    public static Weapon SharpCrayon() => new(
        "Sharp Crayon", "Burnt Sienna. Sharpened to a point. Someone wrote 'HELP' on the wall.",
        WeaponType.SharpCrayon, WeaponRarity.Common, 5, 0, 5, 1.5f, "");

    public static Weapon TeddyMaw() => new(
        "Mr. Huggles", "His mouth opens too wide. Something comes out. He still loves you.",
        WeaponType.TeddyMaw, WeaponRarity.Common, 8, 2, 10, 1.5f, "");

    public static Weapon JackInTheGun() => new(
        "Pop Goes The...", "Wind the handle. The song plays. Something worse than a clown comes out.",
        WeaponType.JackInTheGun, WeaponRarity.Uncommon, 12, 3, 15, 2.0f, "");

    public static Weapon MyFirstNailer() => new(
        "My First Nailer™", "Ages 3+. Real nails! The box shows a smiling child. The child isn't smiling.",
        WeaponType.MyFirstNailer, WeaponRarity.Uncommon, 10, 5, 20, 1.5f, "bleed");

    public static Weapon SippyCannon() => new(
        "Sippy Cup", "Spill-proof. Leak-proof. It's not milk inside. It was never milk.",
        WeaponType.SippyCannon, WeaponRarity.Rare, 18, 4, 10, 2.5f, "");

    public static Weapon MusicBoxDancer() => new(
        "The Ballerina", "She still dances. The music stopped years ago. Her pirouettes never end.",
        WeaponType.MusicBoxDancer, WeaponRarity.Legendary, 25, 8, 25, 3.0f, "lifesteal");

    public static Weapon GetRandomWeapon(Random random, int floor)
    {
        // Higher floors = better weapon chances
        int roll = random.Next(100);
        int floorBonus = floor * 5;

        if (roll + floorBonus >= 95) return MusicBoxDancer();
        if (roll + floorBonus >= 80) return SippyCannon();
        if (roll + floorBonus >= 60) return random.Next(2) == 0 ? JackInTheGun() : MyFirstNailer();
        if (roll + floorBonus >= 30) return TeddyMaw();
        return TeddyMaw(); // Always at least teddy from chests
    }
}

public class Sprite
{
    public double X { get; set; }
    public double Y { get; set; }
    public SpriteType Type { get; set; }

    public Sprite(double x, double y, SpriteType type)
    {
        X = x;
        Y = y;
        Type = type;
    }
}

public class Enemy
{
    public string Name { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int XPReward { get; set; }
    public int GoldReward { get; set; }
    public EnemyType Type { get; set; }

    public Enemy(string name, int health, int maxHealth, int attack, int defense, int xpReward, int goldReward, EnemyType type = EnemyType.SmileDog)
    {
        Name = name;
        Health = health;
        MaxHealth = maxHealth;
        Attack = attack;
        Defense = defense;
        XPReward = xpReward;
        GoldReward = goldReward;
        Type = type;
    }
}

public class InventoryItem
{
    public string Icon { get; set; }
    public string Name { get; set; }
    public int Quantity { get; set; }

    public InventoryItem(string icon, string name, int quantity)
    {
        Icon = icon;
        Name = name;
        Quantity = quantity;
    }
}
