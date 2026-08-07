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
                case FighterCharacterID.Tesla:
                    return SpawnWardenclyffeCataclysm(
                        ref frame, targetEntity, ref attacker, ref attackerRuntime, in tuning);
                default:
                    return false; // Fall back to the generic melee-range ultimate.
            }
        }

        // Tesla — Wardenclyffe Cataclysm (design Section 5): the tower's massive
        // column of alternating current is an owner-centered ultimate-slot zone
        // (type 53) ticking the authored per-hit ultimate damage every 30 frames
        // for 120 frames — the authored 4-hit multi-hit total — while
        // FighterZoneSystem drags the opponent toward the column center with the
        // vortex-style impulse-free pull. The lifetime/interval constants mirror
        // resources/Abilities/tesla/ultimate.tres (HitCount 4 x
        // DamageTickIntervalFrames 30); the deterministic loadout does not carry
        // ultimate zone timing, so the resource numbers are restated here.
        private const int CataclysmLifetimeFrames = 120;
        private const int CataclysmTickIntervalFrames = 30;
        private const int TeslaCoilObjectTypeID = 1;
        // Each coil detonates for double its 5-damage arc, per the Story-side
        // TeslaCoilNode.Explode contract.
        private const int CoilChainExplosionDamage = 10;
        private const int CoilChainHitstunFrames = 12;
        private static readonly FP64 CoilChainKnockback = FP64.FromInt(3);

        private static bool SpawnWardenclyffeCataclysm(
            ref Frame frame,
            EntityRef targetEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning) {
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot, 1,
                CataclysmLifetimeFrames, CataclysmTickIntervalFrames, tuning.UltimateDamage,
                tuning.UltimateStatusType, tuning.UltimateStatusFrames, tuning.UltimateStatusIntensity);

            // Chain reaction: every live coil detonates — one shield-bypassing
            // ultimate-class strike against an opponent within the coil's attack
            // range — and the coil is destroyed.
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef coilEntity)) {
                ref readonly FighterPersistentObjectComponent coil =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(coilEntity);
                if (coil.OwnerPlayerID != attacker.PlayerID
                    || coil.ObjectTypeID != TeslaCoilObjectTypeID
                    || coil.CurrentHP <= 0
                    || coil.LifetimeFrames <= 0) continue;
                if (FP64.Abs(target.Position.x - coil.Position.x) <= coil.AttackRange) {
                    FighterDamageRules.ApplyFighterHit(
                        ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in targetTuning,
                        FighterDamageRules.UltimateAttackClass, CoilChainExplosionDamage, CoilChainKnockback,
                        CoilChainHitstunFrames, (int)FTT.Core.StatusType.None, 0, FP64.One, coil.Position.x);
                }
                frame.DestroyEntity(coilEntity);
            }
            return true;
        }
    }
}
