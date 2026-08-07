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
                case FighterCharacterID.Mozart:
                    return SpawnSymphonyOfSorrow(ref frame, ref attacker, ref attackerRuntime, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // Symphony of Sorrow (Mozart): these mirror the authored ultimate.tres —
        // Lifetime 3 s (180 frames), DamageTickIntervalFrames 18, HitCount 10 —
        // so 10 meteor pulses at tuning.UltimateDamage each land across the
        // authored window (the content tests pin the resource to these values).
        private const int SymphonyDurationFrames = 180;
        private const int SymphonyMeteorIntervalFrames = 18;
        private static readonly FP64 SymphonyHoverRiseSpeed = FP64.FromInt(4);

        /// <summary>
        /// Mozart's Symphony of Sorrow: Mozart rises into a reduced-gravity
        /// hover (FloatFrames, snapshotted and rollback-safe) while a
        /// stage-wide ultimate-slot zone (zone type 73) rains piano-key
        /// meteors — one shield-bypassing UltimateDamage pulse per authored
        /// cadence tick, 10 in total; FighterZoneSystem gives the final pulse
        /// the authored ultimate knockback. Implementation choice: option (a)
        /// stage-wide zone rather than a projectile burst, because the design's
        /// bombardment covers the whole stage regardless of range or facing.
        /// </summary>
        private static bool SpawnSymphonyOfSorrow(
            ref Frame frame,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot,
                maxActive: 1,
                lifetimeFrames: SymphonyDurationFrames,
                tickIntervalFrames: SymphonyMeteorIntervalFrames,
                damage: tuning.UltimateDamage,
                statusType: tuning.UltimateStatusType,
                statusFrames: tuning.UltimateStatusFrames,
                statusIntensity: tuning.UltimateStatusIntensity);
            // The hover: rise off the ground and float for the bombardment.
            attacker.Velocity.y = SymphonyHoverRiseSpeed;
            attacker.IsGrounded = 0;
            attackerRuntime.FloatFrames = SymphonyDurationFrames;
            return true;
        }
    }
}
