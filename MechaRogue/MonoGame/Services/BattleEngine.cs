namespace MechaRogue.Services;

using MechaRogue.Models;

/// <summary>
/// Core battle resolution engine implementing classic Medabots mechanics:
///  - Success-rate hit/miss with accuracy vs evasion
///  - Part-targeted damage with armor depletion
///  - Head destruction = instant knockout
///  - Medaforce charging on damage dealt/received
///  - Critical hits that can one-shot parts
/// </summary>
public class BattleEngine
{
    private static readonly Random _rng = new();

    private const double CritChance = 0.08;
    private const double CritMultiplier = 2.5;
    private const int MedaforceChargeOnHit = 8;
    private const int MedaforceChargeOnTakeDamage = 12;
    private const double MeleeRangeBonus = 1.15;
    private const double DestroyedLegPenalty = 0.5;
    private const double DefendDamageMultiplier = 0.5;

    public List<ActionResult> ResolveTurn(List<BattleAction> actions)
    {
        var ordered = actions
            .OrderByDescending(a => a.Priority + _rng.Next(0, 5))
            .ToList();

        var results = new List<ActionResult>();
        foreach (var action in ordered)
        {
            if (action.Attacker.IsKnockedOut) continue;
            if (action.UsingPart.IsDestroyed && !action.IsMedaforce) continue;
            results.Add(ResolveAction(action));
        }
        return results;
    }

    public ActionResult ResolveAction(BattleAction action)
    {
        // Attacker drops guard stance when they take their turn
        action.Attacker.IsDefending = false;

        if (action.IsDefend) return ResolveDefend(action);
        if (action.IsMedaforce) return ResolveMedaforce(action);

        var attacker = action.Attacker;
        var target = action.Target;
        var part = action.UsingPart;

        if (part.Action == ActionType.Support) return ResolveSupport(action);

        // Hit calculation
        int accuracy = part.Accuracy;
        accuracy += part.Action == ActionType.Shooting
            ? attacker.Medal.ShootingBonus
            : attacker.Medal.MeleeBonus;
        accuracy -= target.EffectiveEvasion;
        if (attacker.Legs.IsDestroyed) accuracy -= 15;
        accuracy = Math.Clamp(accuracy, 5, 98);

        bool hit = _rng.Next(100) < accuracy;
        bool crit = hit && _rng.NextDouble() < CritChance;

        var result = new ActionResult
        {
            Attacker = attacker,
            Target = target,
            UsingPart = part,
            TargetedSlot = action.TargetedSlot,
            Hit = hit,
            Critical = crit
        };

        if (!hit)
        {
            result.Narration = $"{attacker.Name}'s {part.Name} missed {target.Name}!";
            attacker.Medal.AddCharge(2);
            return result;
        }

        // Damage calculation
        double baseDmg = part.Power;
        if (part.Action == ActionType.Melee) baseDmg *= MeleeRangeBonus;
        if (attacker.Legs.IsDestroyed) baseDmg *= DestroyedLegPenalty;
        baseDmg *= 0.85 + _rng.NextDouble() * 0.30;
        if (crit) baseDmg *= CritMultiplier;
        if (target.IsDefending) baseDmg *= DefendDamageMultiplier;
        int damage = Math.Max(1, (int)baseDmg);

        // Apply damage
        var targetPart = target.GetPart(action.TargetedSlot);
        if (targetPart == null || targetPart.IsDestroyed)
        {
            var alive = target.AllParts.Where(p => !p.IsDestroyed).ToArray();
            if (alive.Length == 0)
            {
                result.Narration = $"{target.Name} is already fully destroyed!";
                return result;
            }
            targetPart = alive[_rng.Next(alive.Length)];
            result.TargetedSlot = targetPart.Slot;
        }

        int dealt = targetPart.TakeDamage(damage);
        result.Damage = dealt;
        result.PartDestroyed = targetPart.IsDestroyed;
        result.TargetKnockedOut = target.IsKnockedOut;

        if (part.IsHead && part.MaxUses > 0)
            part.RemainingUses = Math.Max(0, part.RemainingUses - 1);

        attacker.Medal.AddCharge(MedaforceChargeOnHit);
        target.Medal.AddCharge(MedaforceChargeOnTakeDamage);

        string critText = crit ? " CRITICAL HIT!" : "";
        result.Narration = targetPart.IsDestroyed
            ? $"{attacker.Name}'s {part.Name} -> {target.Name}'s {targetPart.Slot}! {dealt} dmg!{critText} PART DESTROYED!"
            : $"{attacker.Name}'s {part.Name} -> {target.Name}'s {targetPart.Slot} for {dealt} dmg.{critText}";

        if (target.IsKnockedOut)
            result.Narration += $" {target.Name}'s head is destroyed - KNOCKOUT!";

        return result;
    }

    private ActionResult ResolveMedaforce(BattleAction action)
    {
        var atk = action.MedaforceAttack!;
        var attacker = action.Attacker;
        attacker.Medal.SpendMedaforce();

        var result = new ActionResult
        {
            Attacker = attacker,
            Target = action.Target,
            Hit = true,
            IsMedaforce = true,
            TargetedSlot = action.TargetedSlot
        };

        if (atk.HitsAll)
            result.Narration = $"{attacker.Name} unleashes {atk.Name}!";

        int damage = atk.Power;
        damage = (int)(damage * (0.9 + _rng.NextDouble() * 0.2));
        if (action.Target.IsDefending) damage = (int)(damage * DefendDamageMultiplier);

        var targetPart = action.Target.GetPart(action.TargetedSlot);
        if (targetPart == null || targetPart.IsDestroyed)
        {
            var alive = action.Target.AllParts.Where(p => !p.IsDestroyed).ToArray();
            if (alive.Length == 0)
            {
                result.Narration = $"{action.Target.Name} already destroyed!";
                return result;
            }
            targetPart = alive[_rng.Next(alive.Length)];
            result.TargetedSlot = targetPart.Slot;
        }

        int dealt = targetPart.TakeDamage(damage);
        result.Damage = dealt;
        result.PartDestroyed = targetPart.IsDestroyed;
        result.TargetKnockedOut = action.Target.IsKnockedOut;

        result.Narration = $"MEDAFORCE! {attacker.Name}'s {atk.Name} -> {action.Target.Name}'s {targetPart.Slot} for {dealt}!";
        if (action.Target.IsKnockedOut)
            result.Narration += " HEAD DESTROYED - KNOCKOUT!";

        return result;
    }

    private ActionResult ResolveDefend(BattleAction action)
    {
        action.Attacker.IsDefending = true;
        return new ActionResult
        {
            Attacker = action.Attacker,
            Target = action.Attacker,
            Hit = true,
            Narration = $"{action.Attacker.Name} takes a defensive stance!"
        };
    }

    private ActionResult ResolveSupport(BattleAction action)
    {
        var attacker = action.Attacker;
        var target = action.Target;
        var part = action.UsingPart;

        var result = new ActionResult
        {
            Attacker = attacker,
            Target = target,
            UsingPart = part,
            Hit = true
        };

        switch (part.Skill)
        {
            case PartSkill.Heal:
                int healAmt = part.Power + attacker.Medal.SupportBonus;
                var worst = target.AllParts
                    .Where(p => !p.IsDestroyed && p.Armor < p.MaxArmor)
                    .OrderBy(p => p.ArmorPercent)
                    .FirstOrDefault();
                if (worst != null)
                {
                    worst.Repair(healAmt);
                    result.HealAmount = healAmt;
                    result.Narration = $"{attacker.Name}'s {part.Name} repairs {target.Name}'s {worst.Slot} for {healAmt}.";
                }
                else
                {
                    result.Narration = $"{target.Name} is already at full armor.";
                }
                break;

            case PartSkill.Shield:
                int shieldAmt = part.Power / 2;
                foreach (var p in target.AllParts.Where(p => !p.IsDestroyed))
                    p.Repair(shieldAmt);
                result.HealAmount = shieldAmt;
                result.Narration = $"{attacker.Name}'s {part.Name} shields {target.Name} (+{shieldAmt} all parts).";
                break;

            case PartSkill.Charge:
                attacker.Medal.AddCharge(part.Power);
                result.Narration = $"{attacker.Name}'s {part.Name} charges Medaforce! (+{part.Power})";
                break;

            case PartSkill.Scan:
                result.Narration = $"{attacker.Name}'s {part.Name} scans {target.Name} - weaknesses revealed!";
                break;

            default:
                result.Narration = $"{attacker.Name} uses {part.Name}.";
                break;
        }

        if (part.IsHead && part.MaxUses > 0)
            part.RemainingUses = Math.Max(0, part.RemainingUses - 1);

        return result;
    }

    // ── AI ────────────────────────────────────────────────

    public BattleAction GenerateAiAction(Medabot ai, List<Medabot> enemies)
    {
        var targets = enemies.Where(e => !e.IsKnockedOut).ToList();
        if (targets.Count == 0)
            return MakeDefaultAction(ai, ai);

        var target = targets.FirstOrDefault(t => t.IsLeader && !t.IsKnockedOut)
                     ?? targets.OrderBy(t => t.TotalArmor).First();

        var usable = ai.UsableParts.ToList();
        if (usable.Count == 0)
            return MakeDefaultAction(ai, target);

        if (ai.Medal.CanUseMedaforce)
        {
            var mfAttacks = ai.Medal.GetAvailableAttacks();
            if (mfAttacks.Count > 0)
            {
                var mfAtk = mfAttacks[_rng.Next(mfAttacks.Count)];
                return new BattleAction
                {
                    Attacker = ai,
                    UsingPart = usable[0],
                    Target = target,
                    TargetedSlot = PickTargetSlot(target),
                    IsMedaforce = true,
                    MedaforceAttack = mfAtk,
                    Priority = ai.EffectiveSpeed + 10
                };
            }
        }

        var bestPart = usable.OrderByDescending(p => p.Power).First();

        // AI defends when critically low on health
        if (ai.HealthPercent < 0.25 && _rng.NextDouble() < 0.4)
        {
            return new BattleAction
            {
                Attacker = ai,
                UsingPart = ai.RightArm,
                Target = ai,
                TargetedSlot = PartSlot.Head,
                IsDefend = true,
                Priority = ai.EffectiveSpeed
            };
        }

        if (bestPart.Action == ActionType.Support && bestPart.Skill == PartSkill.Heal
            && ai.HealthPercent < 0.4)
        {
            return new BattleAction
            {
                Attacker = ai,
                UsingPart = bestPart,
                Target = ai,
                TargetedSlot = PartSlot.Head,
                Priority = ai.EffectiveSpeed
            };
        }

        return new BattleAction
        {
            Attacker = ai,
            UsingPart = bestPart,
            Target = target,
            TargetedSlot = PickTargetSlot(target),
            Priority = ai.EffectiveSpeed
        };
    }

    private PartSlot PickTargetSlot(Medabot target)
    {
        if (_rng.NextDouble() < 0.40 && !target.Head.IsDestroyed)
            return PartSlot.Head;

        var slots = new[] { PartSlot.RightArm, PartSlot.LeftArm, PartSlot.Legs, PartSlot.Head }
            .Where(s => { var p = target.GetPart(s); return p != null && !p.IsDestroyed; })
            .ToArray();

        return slots.Length > 0 ? slots[_rng.Next(slots.Length)] : PartSlot.Head;
    }

    private static BattleAction MakeDefaultAction(Medabot ai, Medabot target) => new()
    {
        Attacker = ai,
        UsingPart = ai.RightArm,
        Target = target,
        TargetedSlot = PartSlot.Head,
        Priority = ai.EffectiveSpeed
    };
}
