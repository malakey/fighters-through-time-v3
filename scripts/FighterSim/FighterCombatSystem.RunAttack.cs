using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    public sealed partial class FighterCombatSystem {
        private static readonly FP64 RunAttackDamageMultiplier =
            FP64.FromDouble(FTT.Combat.BasicComboRules.RunAttackDamageMultiplier);
        private static readonly FP64 RunAttackKnockbackMultiplier =
            FP64.FromDouble(FTT.Combat.BasicComboRules.RunAttackKnockbackMultiplier);
        private static readonly FP64 RunAttackHalfReachBonus =
            FP64.FromDouble(FTT.Combat.BasicComboRules.RunAttackReachBonusUnits) / FP64.FromInt(2);

        /// <summary>
        /// GR-RUN: the running attack's box, string hit 1's with its far edge moved
        /// out by <see cref="FTT.Combat.BasicComboRules.RunAttackReachBonusUnits"/>
        /// and its near edge where it was. Story's
        /// <c>PlayerController.ResolveRunAttackBoxPixels</c> is the same box × 60.
        /// </summary>
        internal static void ResolveRunAttackBox(
            in FighterStateComponent attacker,
            in FTT.Combat.BasicStringProfile profile,
            bool facingRight,
            out FPVector2 center,
            out FPVector2 halfExtents) {
            ResolveStringBox(
                in attacker, in profile, FTT.Combat.BasicComboRules.RunAttackStringStep, facingRight,
                out FPVector2 stringCenter, out FPVector2 stringHalf);
            center = new FPVector2(
                stringCenter.x + (facingRight ? RunAttackHalfReachBonus : -RunAttackHalfReachBonus),
                stringCenter.y);
            halfExtents = new FPVector2(stringHalf.x + RunAttackHalfReachBonus, stringHalf.y);
        }

        /// <summary>
        /// GR-RUN (ruling 9b, 2026-11-03): the running attack's strike. A flat
        /// <see cref="FTT.Combat.BasicComboRules.RunAttackDamageMultiplier"/> basic,
        /// the 30-frame <see cref="FTT.Combat.BasicComboRules.RunAttackHitstunFrames"/>,
        /// no rider and no launch, on the air strike's hit model. Its hitstun stays
        /// block-cancelable like the other single strikes'. One attempt per swing: a
        /// box miss is Refused and tries again next tick; a Foresight counter is
        /// Refused.
        /// </summary>
        private static FighterHitOutcome ApplyRunAttackSwing(
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
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            ResolveRunAttackBox(
                in attacker, in profile, LatchedFacingRight(in attackerRuntime),
                out FPVector2 boxCenter, out FPVector2 boxHalf);
            if (!FighterEntityQueries.Overlaps(
                    in boxCenter, in boxHalf, in target.Position, in FighterHurtbox.HalfExtents)) {
                return FighterHitOutcome.Refused;
            }

            damage = ScaleDamage(attackerTuning.BasicDamage, RunAttackDamageMultiplier);
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
                attackerTuning.BasicKnockback * RunAttackKnockbackMultiplier,
                FTT.Combat.BasicComboRules.RunAttackHitstunFrames,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                attacker.Position.x,
                true,
                0,
                FP64.Zero,
                blockCancelableHitstun: true,
                launches: FTT.Combat.BasicComboRules.RunAttackLaunches);
        }
    }
}
