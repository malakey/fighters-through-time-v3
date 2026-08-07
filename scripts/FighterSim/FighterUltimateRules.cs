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
                case FighterCharacterID.Einstein:
                    return SpawnCosmologicalConstant(ref frame, ref attacker, in tuning);
                case FighterCharacterID.Joan:
                    return SpawnGrandCrusade(ref frame, ref attacker, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // Grand Crusade structural constants mirror the authored joan/ultimate.tres
        // (HitCount 6, DamageTickIntervalFrames 6); the content suite asserts the
        // resource, and the zone spawns with TickFramesRemaining = 1 so the six
        // pulses land at lifetimes 35/29/23/17/11/5 within the 36-frame charge.
        private const int GrandCrusadeTickIntervalFrames = 6;
        private const int GrandCrusadeLifetimeFrames = 36;
        private static readonly FP64 GrandCrusadeDashSpeed = FP64.FromInt(12);

        /// <summary>
        /// Joan's Grand Crusade (design Section 5): a directional cavalry charge
        /// trampling everything in its path. Composed as a wide forward-offset
        /// ultimate-slot zone (type 13) whose six per-tick pulses each deal
        /// tuning.UltimateDamage with the shield-bypassing ultimate class, plus a
        /// forward dash impulse on Joan herself so she rides the stampede
        /// (mirroring how Tempest lifts its caster). The final pulse carries the
        /// authored ultimate knockback via FighterZoneSystem's per-type impulse
        /// branch, carrying the opponent toward the blast zone.
        /// </summary>
        private static bool SpawnGrandCrusade(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot,
                1, GrandCrusadeLifetimeFrames, GrandCrusadeTickIntervalFrames,
                tuning.UltimateDamage, tuning.UltimateStatusType,
                tuning.UltimateStatusFrames, tuning.UltimateStatusIntensity);
            attacker.Velocity.x = attacker.FacingRight != 0
                ? GrandCrusadeDashSpeed
                : -GrandCrusadeDashSpeed;
            return true;
        }

        // The Cosmological Constant: a screen-clearing micro black hole. These
        // constants mirror the authored einstein/ultimate.tres structure
        // (HitCount 5 x DamageTickIntervalFrames 18 = the 90-frame Lifetime);
        // damage and launch knockback flow from the deterministic tuning baked
        // off that same resource.
        private const int CosmologicalConstantLifetimeFrames = 90;
        private const int CosmologicalConstantTickIntervalFrames = 18;

        /// <summary>
        /// Einstein's Cosmological Constant: spawns a single owner-centered
        /// ultimate-slot zone (zone type 3). FighterZoneSystem drags the opponent
        /// toward the singularity every frame, lands 5 shield-bypassing ticks of
        /// tuning.UltimateDamage over the 1.5 s lifetime, and fires the final
        /// explosive launch (tuning.UltimateKnockback) when the zone expires.
        /// </summary>
        private static bool SpawnCosmologicalConstant(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame,
                ref attacker,
                UltimateSlot,
                maxActive: 1,
                CosmologicalConstantLifetimeFrames,
                CosmologicalConstantTickIntervalFrames,
                tuning.UltimateDamage,
                tuning.UltimateStatusType,
                tuning.UltimateStatusFrames,
                tuning.UltimateStatusIntensity);
            return true;
        }
    }
}
