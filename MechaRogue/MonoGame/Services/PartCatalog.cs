namespace MechaRogue.Services;

using MechaRogue.Models;

/// <summary>
/// Catalog of all Medaparts and pre-built Medabot templates.
/// </summary>
public static class PartCatalog
{
    private static readonly Random _rng = new();

    // ═══════════════ HEAD PARTS ═══════════════
    public static MedaPart MissileHead => new()
    {
        Id = "HD-MISSILE", Name = "Missile Launcher", Slot = PartSlot.Head,
        Description = "Fires homing missiles (limited uses).",
        Action = ActionType.Shooting, Skill = PartSkill.Missile,
        MaxArmor = 25, Armor = 25, Power = 35, Accuracy = 90, Speed = 15,
        MaxUses = 3, RemainingUses = 3, Tier = 2
    };
    public static MedaPart RadarHead => new()
    {
        Id = "HD-RADAR", Name = "Radar Dish", Slot = PartSlot.Head,
        Description = "Scans enemy parts, boosting team accuracy.",
        Action = ActionType.Support, Skill = PartSkill.Scan,
        MaxArmor = 20, Armor = 20, Power = 0, Accuracy = 100, Speed = 25,
        MaxUses = 5, RemainingUses = 5, Tier = 1
    };
    public static MedaPart HornHead => new()
    {
        Id = "HD-HORN", Name = "Strike Horn", Slot = PartSlot.Head,
        Description = "Close-range headbutt attack.",
        Action = ActionType.Melee, Skill = PartSkill.Press,
        MaxArmor = 30, Armor = 30, Power = 40, Accuracy = 75, Speed = 20,
        MaxUses = 2, RemainingUses = 2, Tier = 2
    };
    public static MedaPart AntennaHead => new()
    {
        Id = "HD-ANTENNA", Name = "Antenna", Slot = PartSlot.Head,
        Description = "Disrupts enemy targeting systems.",
        Action = ActionType.Support, Skill = PartSkill.Disrupt,
        MaxArmor = 18, Armor = 18, Power = 0, Accuracy = 85, Speed = 30,
        MaxUses = 4, RemainingUses = 4, Tier = 1
    };
    public static MedaPart HeavyHead => new()
    {
        Id = "HD-HEAVY", Name = "Armored Visor", Slot = PartSlot.Head,
        Description = "Thick-plated head. No special ability.",
        Action = ActionType.None, Skill = PartSkill.None,
        MaxArmor = 50, Armor = 50, Power = 0, Accuracy = 0, Speed = 10,
        MaxUses = 0, RemainingUses = 0, Tier = 1
    };
    public static MedaPart SniperHead => new()
    {
        Id = "HD-SNIPE", Name = "Scope Eye", Slot = PartSlot.Head,
        Description = "Precision shot with very high accuracy.",
        Action = ActionType.Shooting, Skill = PartSkill.Snipe,
        MaxArmor = 15, Armor = 15, Power = 50, Accuracy = 95, Speed = 10,
        MaxUses = 2, RemainingUses = 2, Tier = 3
    };

    // ═══════════════ RIGHT ARM PARTS ═══════════════
    public static MedaPart Revolver => new()
    {
        Id = "RA-REVOLVER", Name = "Revolver", Slot = PartSlot.RightArm,
        Description = "Standard handgun. Reliable accuracy.",
        Action = ActionType.Shooting, Skill = PartSkill.Rifle,
        MaxArmor = 25, Armor = 25, Power = 22, Accuracy = 82, Speed = 20, Tier = 1
    };
    public static MedaPart Submachine => new()
    {
        Id = "RA-SMG", Name = "Submachine Gun", Slot = PartSlot.RightArm,
        Description = "Rapid-fire weapon. Lower accuracy, higher damage.",
        Action = ActionType.Shooting, Skill = PartSkill.Gatling,
        MaxArmor = 22, Armor = 22, Power = 28, Accuracy = 70, Speed = 22, Tier = 2
    };
    public static MedaPart SwordArm => new()
    {
        Id = "RA-SWORD", Name = "Chanbara Sword", Slot = PartSlot.RightArm,
        Description = "Razor-sharp blade for close combat.",
        Action = ActionType.Melee, Skill = PartSkill.Sword,
        MaxArmor = 28, Armor = 28, Power = 30, Accuracy = 78, Speed = 25, Tier = 2
    };
    public static MedaPart HammerArm => new()
    {
        Id = "RA-HAMMER", Name = "Power Hammer", Slot = PartSlot.RightArm,
        Description = "Massive crushing blow. Slow but devastating.",
        Action = ActionType.Melee, Skill = PartSkill.Hammer,
        MaxArmor = 30, Armor = 30, Power = 40, Accuracy = 65, Speed = 12, Tier = 2
    };
    public static MedaPart LaserArm => new()
    {
        Id = "RA-LASER", Name = "Beam Cannon", Slot = PartSlot.RightArm,
        Description = "High-energy laser. Great power, moderate accuracy.",
        Action = ActionType.Shooting, Skill = PartSkill.Laser,
        MaxArmor = 20, Armor = 20, Power = 35, Accuracy = 75, Speed = 18, Tier = 3
    };

    // ═══════════════ LEFT ARM PARTS ═══════════════
    public static MedaPart Shield => new()
    {
        Id = "LA-SHIELD", Name = "Guard Shield", Slot = PartSlot.LeftArm,
        Description = "Defensive shield. Protects allies.",
        Action = ActionType.Support, Skill = PartSkill.Shield,
        MaxArmor = 35, Armor = 35, Power = 15, Accuracy = 100, Speed = 15, Tier = 1
    };
    public static MedaPart GrappleClaw => new()
    {
        Id = "LA-GRAPPLE", Name = "Grapple Claw", Slot = PartSlot.LeftArm,
        Description = "Seize and crush. Ignores some evasion.",
        Action = ActionType.Melee, Skill = PartSkill.Grapple,
        MaxArmor = 28, Armor = 28, Power = 25, Accuracy = 85, Speed = 18, Tier = 2
    };
    public static MedaPart MissileArm => new()
    {
        Id = "LA-MISSILE", Name = "Arm Launcher", Slot = PartSlot.LeftArm,
        Description = "Secondary missile pod.",
        Action = ActionType.Shooting, Skill = PartSkill.Missile,
        MaxArmor = 22, Armor = 22, Power = 30, Accuracy = 80, Speed = 16, Tier = 2
    };
    public static MedaPart RepairArm => new()
    {
        Id = "LA-REPAIR", Name = "Repair Arm", Slot = PartSlot.LeftArm,
        Description = "Field repairs for ally Medabots.",
        Action = ActionType.Support, Skill = PartSkill.Heal,
        MaxArmor = 20, Armor = 20, Power = 25, Accuracy = 100, Speed = 20, Tier = 2
    };
    public static MedaPart SubSword => new()
    {
        Id = "LA-BLADE", Name = "Sub Blade", Slot = PartSlot.LeftArm,
        Description = "Secondary melee weapon.",
        Action = ActionType.Melee, Skill = PartSkill.Sword,
        MaxArmor = 25, Armor = 25, Power = 24, Accuracy = 80, Speed = 22, Tier = 1
    };

    // ═══════════════ LEG PARTS ═══════════════
    public static MedaPart BipedalLegs => new()
    {
        Id = "LG-BIPEDAL", Name = "Balanced Legs", Slot = PartSlot.Legs,
        Action = ActionType.None, Skill = PartSkill.Movement,
        Description = "Standard bipedal locomotion.",
        MaxArmor = 30, Armor = 30, Speed = 22, Evasion = 12, Propulsion = 15,
        LegType = LegType.Bipedal, Tier = 1
    };
    public static MedaPart TankTreads => new()
    {
        Id = "LG-TANK", Name = "Tank Treads", Slot = PartSlot.Legs,
        Action = ActionType.None, Skill = PartSkill.Movement,
        Description = "Heavy armor, slow speed.",
        MaxArmor = 50, Armor = 50, Speed = 10, Evasion = 3, Propulsion = 8,
        LegType = LegType.Tank, Tier = 2
    };
    public static MedaPart HoverPods => new()
    {
        Id = "LG-HOVER", Name = "Hover Pods", Slot = PartSlot.Legs,
        Action = ActionType.None, Skill = PartSkill.Movement,
        Description = "Fast hovering. Low armor.",
        MaxArmor = 18, Armor = 18, Speed = 35, Evasion = 22, Propulsion = 25,
        LegType = LegType.Hover, Tier = 2
    };
    public static MedaPart FlightUnit => new()
    {
        Id = "LG-FLIGHT", Name = "Flight Wings", Slot = PartSlot.Legs,
        Action = ActionType.None, Skill = PartSkill.Movement,
        Description = "Airborne unit. Fastest but fragile.",
        MaxArmor = 12, Armor = 12, Speed = 42, Evasion = 28, Propulsion = 30,
        LegType = LegType.Flight, Tier = 3
    };
    public static MedaPart MultiLegs => new()
    {
        Id = "LG-MULTI", Name = "Spider Legs", Slot = PartSlot.Legs,
        Action = ActionType.None, Skill = PartSkill.Movement,
        Description = "Stable multi-legged platform.",
        MaxArmor = 40, Armor = 40, Speed = 16, Evasion = 8, Propulsion = 12,
        LegType = LegType.MultiLegged, Tier = 2
    };

    // ═══════════════ TEMPLATES ═══════════════
    public static Medabot MakeMetabee() => new()
    {
        Name = "Metabee", ModelId = "KBT-1",
        Medal = new Medal { Name = "Kabuto Medal", Type = MedalType.Kabuto },
        Head = MissileHead.Clone(), RightArm = Revolver.Clone(),
        LeftArm = MissileArm.Clone(), Legs = BipedalLegs.Clone()
    };
    public static Medabot MakeRokusho() => new()
    {
        Name = "Rokusho", ModelId = "KWG-1",
        Medal = new Medal { Name = "Kuwagata Medal", Type = MedalType.Kuwagata },
        Head = HornHead.Clone(), RightArm = SwordArm.Clone(),
        LeftArm = SubSword.Clone(), Legs = HoverPods.Clone()
    };
    public static Medabot MakePeppercat() => new()
    {
        Name = "Peppercat", ModelId = "CAT-1",
        Medal = new Medal { Name = "Cat Medal", Type = MedalType.Cat },
        Head = AntennaHead.Clone(), RightArm = SwordArm.Clone(),
        LeftArm = GrappleClaw.Clone(), Legs = HoverPods.Clone()
    };
    public static Medabot MakeTotalizer() => new()
    {
        Name = "Totalizer", ModelId = "TOT-1",
        Medal = new Medal { Name = "Tortoise Medal", Type = MedalType.Tortoise },
        Head = HeavyHead.Clone(), RightArm = LaserArm.Clone(),
        LeftArm = Shield.Clone(), Legs = TankTreads.Clone()
    };
    public static Medabot MakeNeutranurse() => new()
    {
        Name = "Neutranurse", ModelId = "NAS-1",
        Medal = new Medal { Name = "Angel Medal", Type = MedalType.Angel },
        Head = RadarHead.Clone(), RightArm = Revolver.Clone(),
        LeftArm = RepairArm.Clone(), Legs = BipedalLegs.Clone()
    };
    public static Medabot MakeCyandog() => new()
    {
        Name = "Cyandog", ModelId = "DOG-0",
        Medal = new Medal { Name = "Monkey Medal", Type = MedalType.Monkey },
        Head = AntennaHead.Clone(), RightArm = Revolver.Clone(),
        LeftArm = MissileArm.Clone(), Legs = BipedalLegs.Clone()
    };
    public static Medabot MakeSumilidon() => new()
    {
        Name = "Sumilidon", ModelId = "STG-0",
        Medal = new Medal { Name = "Bear Medal", Type = MedalType.Bear },
        Head = HornHead.Clone(), RightArm = SwordArm.Clone(),
        LeftArm = GrappleClaw.Clone(), Legs = MultiLegs.Clone()
    };
    public static Medabot MakeArcbeetle() => new()
    {
        Name = "Arcbeetle", ModelId = "KBT-4",
        Medal = new Medal { Name = "Dragon Medal", Type = MedalType.Dragon, Level = 5 },
        Head = SniperHead.Clone(), RightArm = LaserArm.Clone(),
        LeftArm = MissileArm.Clone(), Legs = FlightUnit.Clone()
    };

    // ═══════════════ RANDOM GENERATION ═══════════════
    private static readonly Func<Medabot>[] _templates =
        [MakeMetabee, MakeRokusho, MakePeppercat, MakeTotalizer, MakeNeutranurse, MakeCyandog, MakeSumilidon];

    private static readonly Func<Medabot>[] _bossTemplates = [MakeArcbeetle, MakeSumilidon];

    public static Medabot RandomEnemy(int floor)
    {
        var bot = _templates[_rng.Next(_templates.Length)]();
        bot.IsPlayerOwned = false;
        bot.IsLeader = true;
        ScaleForFloor(bot, floor);
        return bot;
    }

    public static Medabot RandomBoss(int floor)
    {
        var bot = _bossTemplates[_rng.Next(_bossTemplates.Length)]();
        bot.IsPlayerOwned = false;
        bot.IsLeader = true;
        bot.Name = "* " + bot.Name;
        ScaleForFloor(bot, floor, isBoss: true);
        return bot;
    }

    public static List<Medabot> RandomEnemySquad(int floor, int count = 1)
    {
        var squad = new List<Medabot>();
        for (int i = 0; i < count; i++)
        {
            var bot = RandomEnemy(floor);
            bot.IsLeader = i == 0;
            squad.Add(bot);
        }
        return squad;
    }

    public static MedaPart RandomPartReward(int floor)
    {
        var allParts = new MedaPart[]
        {
            MissileHead, RadarHead, HornHead, AntennaHead, HeavyHead, SniperHead,
            Revolver, Submachine, SwordArm, HammerArm, LaserArm,
            Shield, GrappleClaw, MissileArm, RepairArm, SubSword,
            BipedalLegs, TankTreads, HoverPods, FlightUnit, MultiLegs
        };
        var candidates = allParts.Where(p => p.Tier <= 1 + floor / 4).ToArray();
        if (candidates.Length == 0) candidates = allParts;
        return candidates[_rng.Next(candidates.Length)].Clone();
    }

    private static void ScaleForFloor(Medabot bot, int floor, bool isBoss = false)
    {
        double scale = 1.0 + (floor - 1) * 0.12;
        if (isBoss) scale *= 1.4;
        foreach (var part in bot.AllParts)
        {
            part.MaxArmor = (int)(part.MaxArmor * scale);
            part.Armor = part.MaxArmor;
            part.Power = (int)(part.Power * scale);
        }
        bot.Medal.Level = Math.Max(1, floor / 3 + (isBoss ? 2 : 0));
    }

    public static List<MedaPart> GetShopParts(int floor, int count = 4)
    {
        var parts = new List<MedaPart>();
        for (int i = 0; i < count; i++)
            parts.Add(RandomPartReward(floor));
        return parts;
    }
}
