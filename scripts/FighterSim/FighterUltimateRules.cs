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
                case FighterCharacterID.Cleopatra:
                    return SpawnWrathOfTheNile(ref frame, ref attacker, in tuning);
                case FighterCharacterID.Leonardo:
                    return SpawnVitruvianMatrix(ref frame, ref attacker, in tuning);
                case FighterCharacterID.Lincoln:
                    return SpawnUnionIndestructible(ref frame, ref attacker, in tuning);
                case FighterCharacterID.Tesla:
                    return SpawnWardenclyffeCataclysm(
                        ref frame, targetEntity, ref attacker, ref attackerRuntime, in tuning);
                case FighterCharacterID.Pocahontas:
                    return SpawnTidewaterTempest(ref frame, ref attacker, in tuning);
                case FighterCharacterID.Mozart:
                    return SpawnSymphonyOfSorrow(ref frame, ref attacker, ref attackerRuntime, in tuning);
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