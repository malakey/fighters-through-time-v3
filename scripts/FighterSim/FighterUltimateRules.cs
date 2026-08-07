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
                case FighterCharacterID.Pocahontas:
                    return SpawnTidewaterTempest(ref frame, ref attacker, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // Tidewater Tempest cadence mirrors the authored ultimate.tres numbers
        // (HitCount 8, DamageTickIntervalFrames 21, Lifetime 2.8 s = 168 frames);
        // the tuning component only carries the ultimate's damage/knockback/status,
        // so the frame structure is authored here alongside the resource.
        private const int TidewaterTempestHitCount = 8;
        private const int TidewaterTempestTickIntervalFrames = 21;
        private const int TidewaterTempestLifetimeFrames =
            TidewaterTempestHitCount * TidewaterTempestTickIntervalFrames;

        /// <summary>
        /// Pocahontas — Tidewater Tempest: a surging spirit storm centered on the
        /// caster (zone type 83). The storm ticks the authored per-hit ultimate
        /// damage 8 times at the authored 21-frame cadence; the final surge throws
        /// the target outward with the authored ultimate knockback (see the
        /// per-type impulse branch in FighterZoneSystem). Ultimate-slot zones hit
        /// with UltimateAttackClass, bypassing shields.
        /// </summary>
        private static bool SpawnTidewaterTempest(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot,
                maxActive: 1,
                lifetimeFrames: TidewaterTempestLifetimeFrames,
                tickIntervalFrames: TidewaterTempestTickIntervalFrames,
                damage: tuning.UltimateDamage,
                statusType: tuning.UltimateStatusType,
                statusFrames: tuning.UltimateStatusFrames,
                statusIntensity: tuning.UltimateStatusIntensity);
            return true;
        }
    }
}
