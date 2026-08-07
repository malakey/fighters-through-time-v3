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
                case FighterCharacterID.Cleopatra:
                    return SpawnWrathOfTheNile(ref frame, ref attacker, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // Wrath of the Nile: 10 impulse-free ticks over 3.5 s (210 frames at a
        // 21-frame interval), mirroring the authored HitCount / Lifetime /
        // DamageTickIntervalFrames in cleopatra/ultimate.tres — the loadout does
        // not carry ultimate tick timing, so these two constants are the
        // deterministic mirror of that resource.
        private const int WrathOfTheNileLifetimeFrames = 210;
        private const int WrathOfTheNileTickIntervalFrames = 21;

        /// <summary>
        /// Cleopatra's Wrath of the Nile: an arena-engulfing sandstorm zone in
        /// the ultimate slot (zone type 43). Each tick deals the authored
        /// per-hit ultimate damage and carries the authored heavy Venom through
        /// the zone status args; FighterZoneSystem applies the shield-bypassing
        /// UltimateAttackClass to ultimate-slot zones automatically and adds the
        /// storm's vortex pull toward its eye.
        /// </summary>
        private static bool SpawnWrathOfTheNile(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame,
                ref attacker,
                UltimateSlot,
                1,
                WrathOfTheNileLifetimeFrames,
                WrathOfTheNileTickIntervalFrames,
                tuning.UltimateDamage,
                tuning.UltimateStatusType,
                tuning.UltimateStatusFrames,
                tuning.UltimateStatusIntensity);
            return true;
        }
    }
}
