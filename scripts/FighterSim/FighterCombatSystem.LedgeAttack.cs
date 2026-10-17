using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    public sealed partial class FighterCombatSystem {
        private static readonly FP64 LedgeAttackDamageMultiplier =
            FP64.FromDouble(FTT.Combat.BasicComboRules.LedgeAttackDamageMultiplier);

        /// <summary>
        /// CM-MOVE (decision 5.2, 2026-10-16; plan §2.5.2): the ledge attack's
        /// strike, the swing <see cref="FighterBasicAttackRules.StartLedgeAttack"/>
        /// starts when the ledge attack's climb completes. String hit 1's box (in
        /// front of the facing latched at active start) and hit 1's knockback
        /// multiplier, a flat <see cref="FTT.Combat.BasicComboRules.LedgeAttackDamageMultiplier"/>
        /// basic, the 30-frame <see cref="FTT.Combat.BasicComboRules.LedgeAttackHitstunFrames"/>,
        /// no rider and no launch — the air strike's hit model on the ground. Its
        /// hitstun stays block-cancelable like the other single strikes'. One
        /// attempt per swing: a box miss is Refused and tries again next tick; a
        /// Foresight counter is Refused. The construct sweep treats it as any
        /// single strike (a flat basic through hit 1's box).
        /// </summary>
        private static FighterHitOutcome ApplyLedgeAttackSwing(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            in FighterTuningComponent attackerTuning,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            ref FighterCounterComponent targetCounter,
            ref FighterActionStateComponent targetAction,
            out int damage) {
            damage = 0;
            const int step = 0;
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            ResolveStringBox(
                in attacker, in profile, step, LatchedFacingRight(in attackerRuntime),
                out FPVector2 boxCenter, out FPVector2 boxHalf);
            if (!FighterEntityQueries.Overlaps(
                    in boxCenter, in boxHalf, in target.Position, in FighterHurtbox.HalfExtents)) {
                return FighterHitOutcome.Refused;
            }

            damage = ScaleDamage(attackerTuning.BasicDamage, LedgeAttackDamageMultiplier);
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            if (FighterForesightRules.TryCounter(ref targetCounter, ref target)) return FighterHitOutcome.Refused;
            return FighterDamageRules.ResolveFighterHit(
                ref targetAction,
                ref attacker,
                ref attackerRuntime,
                ref attackerVerb,
                ref target,
                ref targetRuntime,
                ref targetVerb,
                ref targetDefense,
                in targetTuning,
                FighterDamageRules.BasicAttackClass,
                damage,
                attackerTuning.BasicKnockback * BasicKnockbackMultipliers[step],
                FTT.Combat.BasicComboRules.LedgeAttackHitstunFrames,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                attacker.Position.x,
                true,
                0,
                FP64.Zero,
                blockCancelableHitstun: true,
                launches: FTT.Combat.BasicComboRules.LedgeAttackLaunches);
        }
    }
}
