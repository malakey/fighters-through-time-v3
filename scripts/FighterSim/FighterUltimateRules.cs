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
                case FighterCharacterID.Leonardo:
                    return SpawnVitruvianMatrix(ref frame, ref attacker, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // --- Leonardo: The Vitruvian Matrix (design Section 5) ---------------
        // A thrown geometric trap locks the opponent inside the Vitruvian circle
        // while clockwork cannons bombard them: an ultimate-slot zone (type 23)
        // whose 8 ticks each carry tuning.UltimateDamage plus the authored Root
        // hold (refreshed every tick, so it lapses shortly after the last hit),
        // and whose expiry branch in FighterZoneSystem delivers the final-
        // explosion knockback. The loadout does not carry ultimate lifetime or
        // tick data, so the authored ultimate.tres numbers (2.4 s window, 18-
        // frame interval) are mirrored here as constants.
        private const int VitruvianMatrixLifetimeFrames = 144;
        private const int VitruvianMatrixTickIntervalFrames = 18;

        private static bool SpawnVitruvianMatrix(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot,
                1, VitruvianMatrixLifetimeFrames, VitruvianMatrixTickIntervalFrames,
                tuning.UltimateDamage,
                tuning.UltimateStatusType, tuning.UltimateStatusFrames,
                tuning.UltimateStatusIntensity);
            return true;
        }
    }
}
