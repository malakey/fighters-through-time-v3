using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Per-character deterministic ultimate dispatch (audit gap X7). The combat
    /// system calls <see cref="TryExecute"/> when the Ultimate button is pressed
    /// with a full meter, BEFORE the generic melee-range ultimate intent is
    /// built; a character case that returns true consumes the meter and replaces
    /// the generic hit entirely (and fires regardless of melee range). Cases
    /// compose the existing deterministic primitives — SpawnZone/SpawnProjectile/
    /// SpawnPersistent on <see cref="FighterAbilityEntitySystem"/> with the
    /// ultimate slot index 3 — so zone type IDs are CharacterID * 10 + 3, and
    /// FighterZoneSystem applies UltimateAttackClass (shield-bypassing) to those
    /// zones automatically.
    ///
    /// Each character's ultimate pass fills exactly one case plus one private
    /// helper method in this file; keep every addition self-contained so
    /// parallel branches merge as unions.
    /// </summary>
    internal static class FighterUltimateRules {

        public const int UltimateSlot = 3;

        public static bool TryExecute(
            ref Frame frame,
            EntityRef attackerEntity,
            EntityRef targetEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning) {
            switch ((FighterCharacterID)attacker.CharacterID) {
                // Ultimate kit passes add one case per character here, e.g.:
                // case FighterCharacterID.Einstein:
                //     return SpawnCosmologicalConstant(ref frame, ref attacker, in tuning);
                case FighterCharacterID.Lincoln:
                    return SpawnUnionIndestructible(ref frame, ref attacker, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // Lincoln — Union Indestructible: matches the authored ultimate.tres
        // (Lifetime 2.5 s, DamageTickIntervalFrames 30, StatusDuration 0.6 s).
        private const int UnionIndestructibleLifetimeFrames = 150;
        private const int UnionIndestructibleTickFrames = 30;

        /// <summary>
        /// Lincoln — Union Indestructible (design Section 5): a line of split-rail
        /// fence barriers pens the opponent in front of Lincoln, then the rail
        /// smash sequence lands across the trap window and the fence-shattering
        /// finisher delivers the massive knockback. Composed as one forward
        /// ultimate-slot zone (type 33): each 0.5 s tick smashes for
        /// tuning.UltimateDamage with the shield-bypassing ultimate class and
        /// re-applies the authored Root (the pen); FighterZoneSystem gives the
        /// final tick the heavy tuning.UltimateKnockback finisher impulse.
        /// </summary>
        private static bool SpawnUnionIndestructible(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot,
                1, UnionIndestructibleLifetimeFrames, UnionIndestructibleTickFrames,
                tuning.UltimateDamage,
                tuning.UltimateStatusType, tuning.UltimateStatusFrames, tuning.UltimateStatusIntensity);
            return true;
        }
    }
}
