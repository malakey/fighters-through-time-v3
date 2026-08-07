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
                case FighterCharacterID.Shakespeare:
                    return SpawnAllTheWorldsAStage(ref frame, ref attacker, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // All the World's a Stage (design Section 5): a Globe Theatre set rises
        // around Shakespeare and tragic phantoms — the three Witches, Romeo &
        // Juliet, and Hamlet — deliver six sequential strikes. The constants
        // mirror resources/Abilities/shakespeare/ultimate.tres (HitCount 6 at a
        // 20-frame cadence, 2.0 s Lifetime spanning the sequence): the zone's
        // first pulse lands on spawn and the 120-frame lifetime yields exactly
        // six pulses, the last of which FighterZoneSystem upgrades to the
        // impulse-carrying finale using tuning.UltimateKnockback. The wide
        // owner-centered stage footprint lives in ResolveZoneSpec (zone type 63).
        private const int StageStrikeIntervalFrames = 20;
        private const int StageLifetimeFrames = 120;

        private static bool SpawnAllTheWorldsAStage(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot, 1,
                StageLifetimeFrames, StageStrikeIntervalFrames, tuning.UltimateDamage,
                tuning.UltimateStatusType, tuning.UltimateStatusFrames, tuning.UltimateStatusIntensity);
            return true;
        }
    }
}
