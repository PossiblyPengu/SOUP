namespace MechaRogue.Services;

using MechaRogue.Models;

/// <summary>
/// Core battle resolution engine implementing classic Medabots mechanics:
///  - Simultaneous action selection → speed-ordered execution
///  - Success-rate hit/miss with accuracy vs evasion
///  - Part-targeted damage with armor depletion
///  - Head destruction = instant knockout
///  - Medaforce charging on damage dealt/received
///  - Critical hits that can one-shot parts
/// </summary>
public class BattleEngine
{
    private static readonly Random _rng = new();

    // ── Constants ─────────────────────────────────────────
    private const double CritChance = 0.08;       // 8 % base crit
    private const double CritMultiplier = 2.5;
    private const int MedaforceChargeOnHit = 8;
    private const int MedaforceChargeOnTakeDamage = 12;
    private const double MeleeRangeBonus = 1.15;  // melee gets 15 % more damage
    private const double DestroyedLegPenalty = 0.5;

    /// <summary>
    /// Resolve a full turn: take a list of queued BattleActions,
    /// sort by priority (speed), resolve each sequentially.
    /// Returns the results in execution order.
    /// </summary>
    public List<ActionResult> ResolveTurn(List<BattleAction> actions)
    {
        // Sort by speed descending; ties broken randomly
        var ordered = actions
            .OrderByDescending(a => a.Priority + _rng.Next(0, 5))
            .ToList();

        var results = new List<ActionResult>();

        foreach (var action in ordered)
        {
            // Skip if attacker was knocked out earlier this turn
            if (action.Attacker.IsKnockedOut) continue;
            // Skip if the part being used was destroyed this turn
            if (action.UsingPart.IsDestroyed && !action.IsMedaforce) continue;

            var result = ResolveAction(action);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Resolve a single action: hit check → damage → effects.
    /// </summary>
    public ActionResult ResolveAction(BattleAction action)
    {
        if (action.IsMedaforce)
            return ResolveMedaforce(action);

        var attacker = action.Attacker;
        var target = action.Target;
        var part = action.UsingPart;

        // Support actions
        if (part.Action == ActionType.Support)
            return ResolveSupport(action);

        // ── Hit Calculation ───────────────────────────────
        int accuracy = part.Accuracy;
        // Medal affinity bonus
        accuracy += part.Action == ActionType.Shooting
            ? attacker.Medal.ShootingBonus
            : attacker.Medal.MeleeBonus;
        // Evasion penalty
        accuracy -= target.EffectiveEvasion;
        // Legs destroyed = harder to aim
        if (attacker.Legs.IsDestroyed)
            accuracy -= 15;
        // Clamp
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
            // Even misses charge a tiny bit
            attacker.Medal.AddCharge(2);
            return result;
        }

        // ── Damage Calculation ────────────────────────────
        double baseDmg = part.Power;

        // Melee range bonus
        if (part.Action == ActionType.Melee)
            baseDmg *= MeleeRangeBonus;

        // Legs destroyed = weaker output
        if (attacker.Legs.IsDestroyed)
            baseDmg *= DestroyedLegPenalty;

        // Random variance ±15 %
        baseDmg *= 0.85 + _rng.NextDouble() * 0.30;

        if (crit)
            baseDmg *= CritMultiplier;

        int damage = Math.Max(1, (int)baseDmg);

        // ── Apply Damage to Targeted Part ─────────────────
        var targetPart = target.GetPart(action.TargetedSlot);
        if (targetPart == null || targetPart.IsDestroyed)
        {
            // Re-target: pick a non-destroyed part at random
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

        // Head special: consume a use
        if (part.IsHead && part.MaxUses > 0)
            part.RemainingUses = Math.Max(0, part.RemainingUses - 1);

        // ── Medaforce Charge ──────────────────────────────
        attacker.Medal.AddCharge(MedaforceChargeOnHit);
        target.Medal.AddCharge(MedaforceChargeOnTakeDamage);

        // ── Narration ─────────────────────────────────────
        string critText = crit ? " CRITICAL HIT!" : "";
        result.Narration = targetPart.IsDestroyed
            ? $"{attacker.Name}'s {part.Name} → {target.Name}'s {targetPart.Slot}! {dealt} dmg!{critText} PART DESTROYED!"
            : $"{attacker.Name}'s {part.Name} → {target.Name}'s {targetPart.Slot} for {dealt} dmg.{critText}";

        if (target.IsKnockedOut)
            result.Narration += $" {target.Name}'s head is destroyed – KNOCKOUT!";

        return result;
    }

    /// <summary>
    /// Resolve a Medaforce attack – ignores evasion, very powerful.
    /// </summary>
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
        {
            // Handled externally (hit all enemies); here we just resolve single
            result.Narration = $"{attacker.Name} unleashes {atk.Name}!";
        }

        int damage = atk.Power;
        damage = (int)(damage * (0.9 + _rng.NextDouble() * 0.2));

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

        result.Narration = $"⚡ MEDAFORCE! {attacker.Name}'s {atk.Name} → {action.Target.Name}'s {targetPart.Slot} for {dealt}!";
        if (action.Target.IsKnockedOut)
            result.Narration += " HEAD DESTROYED – KNOCKOUT!";

        return result;
    }

    /// <summary>
    /// Resolve a support action (heal, shield, scan).
    /// </summary>
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
                // Temporary armor boost – simplified as heal on all parts
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
                result.Narration = $"{attacker.Name}'s {part.Name} scans {target.Name} – weaknesses revealed!";
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

    /// <summary>
    /// Simple enemy AI: pick the best action for this mech.
    /// </summary>
    public BattleAction GenerateAiAction(Medabot ai, List<Medabot> enemies)
    {
        var targets = enemies.Where(e => !e.IsKnockedOut).ToList();
        if (targets.Count == 0)
            return MakeDefaultAction(ai, ai); // shouldn't happen

        // Pick target: prefer leader, then lowest HP
        var target = targets.FirstOrDefault(t => t.IsLeader && !t.IsKnockedOut)
                     ?? targets.OrderBy(t => t.TotalArmor).First();

        // Pick part to use: prefer arms, head only if arms gone
        var usable = ai.UsableParts.ToList();
        if (usable.Count == 0)
        {
            // Desperate: even destroyed parts can't act, just "struggle"
            return MakeDefaultAction(ai, target);
        }

        // Medaforce check
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
                    Priority = ai.EffectiveSpeed + 10 // Medaforce is fast
                };
            }
        }

        // Use strongest available part
        var bestPart = usable.OrderByDescending(p => p.Power).First();

        // If support-type, consider healing self if low
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
        // 40 % chance to target head, rest split among other parts
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
