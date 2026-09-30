using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Per-character deterministic Ultimate cinematics (audit gap X7).
    ///
    /// <para><b>A02 (Package 13 W6).</b> Nothing here runs on the button any
    /// more. <see cref="FighterCombatSystem"/> accepts the press, spends the
    /// meter and plays the activation strike
    /// (<see cref="FighterUltimateActivationRules"/>); only a strike that
    /// CONNECTS calls <see cref="TryExecute"/>, with the victim already held at
    /// <c>anchor</c>. Every cinematic zone is therefore spawned on the victim,
    /// and the combat system delivers the D15 finale when the hold ends.</para>
    ///
    /// <para><b>D15.</b> The sequence's timing comes from the loadout
    /// (<see cref="FighterUltimateData"/>, projected from each character's
    /// <c>ultimate.tres</c>): <c>HitCount</c> regular pulses of the authored
    /// per-hit damage at <c>DamageTickIntervalFrames</c>. The per-character
    /// lifetime/interval constants this file used to restate from the resources
    /// are gone — there is one canonical value, on the resource. A
    /// damage-over-time status rides the finale instead of the pulses
    /// (<see cref="FTT.Combat.UltimateActivationRules.HitCarriesStatus"/>), so
    /// such a zone is spawned without one.</para>
    ///
    /// <para>Cases compose the existing deterministic primitives —
    /// <see cref="FighterAbilityEntitySystem.SpawnZone"/> with the ultimate slot
    /// index 3 — so zone type IDs are CharacterID * 10 + 3, and
    /// FighterZoneSystem applies UltimateAttackClass (shield-bypassing) to those
    /// zones automatically.</para>
    /// </summary>
    internal static class FighterUltimateRules {

        public const int UltimateSlot = 3;

        public static bool TryExecute(
            ref Frame frame,
            EntityRef attackerEntity,
            EntityRef targetEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning,
            in FighterUltimateData data,
            in FPVector2 anchor) {
            switch ((FighterCharacterID)attacker.CharacterID) {
                case FighterCharacterID.Einstein:
                case FighterCharacterID.Cleopatra:
                case FighterCharacterID.Leonardo:
                case FighterCharacterID.Lincoln:
                case FighterCharacterID.Shakespeare:
                case FighterCharacterID.Tubman:
                    // The Cosmological Constant (zone 3: the singularity's pull),
                    // Wrath of the Nile (43: the storm's drag), the Vitruvian
                    // Matrix (23), Union Indestructible (33: the Root pen and the
                    // final smash's knockback), All the World's a Stage (63) and
                    // The Freedom Line (93, Package 13 W5: seven 8-damage lantern
                    // pulses, then the 20-damage final rush as the D15 finale):
                    // the per-type rules live in FighterZoneSystem; the sequence
                    // is the loadout's. Zone 83 (Tidewater Tempest) is retired.
                    SpawnCinematicZone(ref frame, ref attacker, in tuning, in data, in anchor);
                    return true;
                case FighterCharacterID.Joan:
                    return SpawnGrandCrusade(ref frame, ref attacker, in tuning, in data, in anchor);
                case FighterCharacterID.Tesla:
                    return SpawnWardenclyffeCataclysm(
                        ref frame, attackerEntity, targetEntity, ref attacker, ref attackerRuntime,
                        in tuning, in data, in anchor);
                case FighterCharacterID.Mozart:
                    return SpawnSymphonyOfSorrow(ref frame, ref attacker, ref attackerRuntime, in tuning, in data, in anchor);
                default:
                    return false; // The combat system lands a single generic Ultimate hit.
            }
        }

        /// <summary>
        /// The shared cinematic: one ultimate-slot zone on the held victim whose
        /// <c>HitCount</c> pulses deal the authored per-hit damage every
        /// <c>DamageTickIntervalFrames</c>, carrying the status unless it is a
        /// damage-over-time status that rides the finale.
        /// </summary>
        private static void SpawnCinematicZone(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning,
            in FighterUltimateData data,
            in FPVector2 anchor) {
            bool statusOnPulses = !FTT.Combat.UltimateActivationRules.StatusRidesFinale(
                (FTT.Core.StatusType)tuning.UltimateStatusType, data.FinaleDamage);
            FighterAbilityEntitySystem.SpawnZone(
                ref frame, ref attacker, UltimateSlot,
                maxActive: 1,
                lifetimeFrames: data.SequenceFrames,
                tickIntervalFrames: data.TickIntervalFrames,
                damage: tuning.UltimateDamage,
                statusType: statusOnPulses ? tuning.UltimateStatusType : (int)FTT.Core.StatusType.None,
                statusFrames: statusOnPulses ? tuning.UltimateStatusFrames : 0,
                statusIntensity: tuning.UltimateStatusIntensity);
            AnchorZone(ref frame, attacker.PlayerID, attacker.CharacterID * 10 + UltimateSlot, in anchor);
        }

        /// <summary>Moves the owner's just-spawned ultimate zone onto the held victim.</summary>
        private static void AnchorZone(ref Frame frame, int ownerPlayerID, int zoneTypeID, in FPVector2 anchor) {
            var filter = frame.Filter<FighterZoneComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterZoneComponent zone = ref frame.Get<FighterZoneComponent>(entity);
                if (zone.OwnerPlayerID != ownerPlayerID || zone.ZoneTypeID != zoneTypeID) continue;
                zone.Position = anchor;
            }
        }

        private static readonly FP64 GrandCrusadeDashSpeed = FP64.FromInt(12);

        /// <summary>
        /// Joan's Grand Crusade (design Section 5, J05): the cavalry stampede
        /// tramples the held victim — eight 7-damage pulses, then the combat
        /// system's 20-damage final charge — while Joan rides it forward.
        /// </summary>
        private static bool SpawnGrandCrusade(
            ref Frame frame,
            ref FighterStateComponent attacker,
            in FighterTuningComponent tuning,
            in FighterUltimateData data,
            in FPVector2 anchor) {
            SpawnCinematicZone(ref frame, ref attacker, in tuning, in data, in anchor);
            attacker.Velocity.x = attacker.FacingRight != 0
                ? GrandCrusadeDashSpeed
                : -GrandCrusadeDashSpeed;
            return true;
        }

        private const int TeslaCoilObjectTypeID = 1;

        /// <summary>
        /// Tesla — Wardenclyffe Cataclysm (T03): the alternating-current column
        /// on the held victim (five 10-damage pulses, then the 20-damage final
        /// strike), and every live coil detonates: each deals
        /// <see cref="FTT.Combat.UltimateActivationRules.CoilDetonationDamage"/>
        /// when the victim is inside that coil's own arc radius, and every
        /// detonated coil is destroyed. Only a connected strike reaches here, so
        /// a whiff detonates nothing.
        /// </summary>
        private static bool SpawnWardenclyffeCataclysm(
            ref Frame frame,
            EntityRef attackerEntity,
            EntityRef targetEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning,
            in FighterUltimateData data,
            in FPVector2 anchor) {
            SpawnCinematicZone(ref frame, ref attacker, in tuning, in data, in anchor);

            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            ref FighterVerbComponent attackerVerb = ref frame.Get<FighterVerbComponent>(attackerEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef coilEntity)) {
                ref readonly FighterPersistentObjectComponent coil =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(coilEntity);
                if (coil.OwnerPlayerID != attacker.PlayerID
                    || coil.ObjectTypeID != TeslaCoilObjectTypeID
                    || coil.CurrentHP <= 0
                    || coil.LifetimeFrames <= 0) continue;
                if (FP64.Abs(anchor.x - coil.Position.x) <= coil.AttackRange
                    && FP64.Abs(anchor.y - coil.Position.y) <= coil.AttackRange) {
                    // V7.6 D03h: Ultimate-triggered coil explosions are
                    // Ultimate-origin damage and earn Tesla no meter; construct
                    // damage reclaims no Rally. Impulse-free and hitstop-free:
                    // the victim is held for the cinematic.
                    FighterDamageRules.ApplyFighterHit(
                        ref attacker, ref attackerRuntime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                        FighterDamageRules.UltimateAttackClass,
                        FTT.Combat.UltimateActivationRules.CoilDetonationDamage,
                        FP64.Zero, 0, (int)FTT.Core.StatusType.None, 0, FP64.One, coil.Position.x,
                        creditInfluence: false,
                        collectsEcho: false,
                        appliesHitstop: false);
                }
                frame.DestroyEntity(coilEntity);
            }
            return true;
        }

        private static readonly FP64 SymphonyHoverRiseSpeed = FP64.FromInt(4);

        /// <summary>
        /// Mozart's Symphony of Sorrow (M05): eight 7-damage piano-key meteors
        /// on the held victim, then the combat system's 24-damage grand-chord
        /// finale, while Mozart rises into a reduced-gravity hover
        /// (FloatFrames, snapshotted) that lasts the whole cinematic.
        /// </summary>
        private static bool SpawnSymphonyOfSorrow(
            ref Frame frame,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning,
            in FighterUltimateData data,
            in FPVector2 anchor) {
            SpawnCinematicZone(ref frame, ref attacker, in tuning, in data, in anchor);
            attacker.Velocity.y = SymphonyHoverRiseSpeed;
            attacker.IsGrounded = 0;
            attackerRuntime.FloatFrames = data.CinematicFrames;
            return true;
        }
    }
}
