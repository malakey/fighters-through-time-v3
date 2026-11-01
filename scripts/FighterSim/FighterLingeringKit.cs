using FTT.Combat;
using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    // The lingering-ability kit pass (2026-10-31, KL-SIM), Fighter side. Every number is read
    // through FighterLingeringKitRules (the fixed-point view of LingeringKitRules); nothing here
    // uses a float or a double, and no component or field was added: the charges live in 303
    // RemainingAttacks, the zone states in 309 ExecutionFlags / CollapseTargetMask.

    /// <summary>
    /// A construct's CHARGES (plan 1.4). 303 <c>RemainingAttacks</c> is the charge count for a coil,
    /// a turret and a nest (-1 stays "unlimited" for every other object). Only a synced strike, the
    /// Lorentz discharge and the turret's glide bolt spend one; an own attack never does. At 0 the
    /// construct is destroyed by the persistent-object system's existing destroy path.
    /// </summary>
    internal static class FighterConstructCharges {
        public const int CoilObjectTypeID = 1;
        public const int TurretObjectTypeID = 2;
        public const int NestObjectTypeID = 3;

        public static bool IsChargeConstruct(int objectTypeID) =>
            objectTypeID == CoilObjectTypeID || objectTypeID == TurretObjectTypeID || objectTypeID == NestObjectTypeID;

        /// <summary>The charges a freshly placed construct holds, or -1 for an object that has none.</summary>
        public static int InitialCharges(int objectTypeID) => objectTypeID switch {
            CoilObjectTypeID => LingeringKitRules.CoilCharges,
            TurretObjectTypeID => LingeringKitRules.TurretCharges,
            NestObjectTypeID => LingeringKitRules.NestCharges,
            _ => -1
        };

        /// <summary>True for a live construct that still has a charge to spend.</summary>
        public static bool HasCharge(in FighterPersistentObjectComponent construct) =>
            IsChargeConstruct(construct.ObjectTypeID)
            && construct.CurrentHP > 0
            && construct.LifetimeFrames > 0
            && construct.RemainingAttacks > 0;

        /// <summary>Spends one charge. The construct fizzles when the persistent-object system next sees 0.</summary>
        public static void Spend(ref FighterPersistentObjectComponent construct) {
            if (construct.RemainingAttacks > 0) construct.RemainingAttacks--;
        }
    }

    /// <summary>
    /// Plan 1.3: the synced strikes. The owner's landed basic hit or direct Special hit left an owed
    /// count on its verb component (310 <c>OwedSyncedStrikes</c>); <c>FighterPersistentObjectSystem</c>
    /// reads it at the top of its update, the same tick, and calls <see cref="Resolve"/>. For each owed
    /// hit every own construct (coil, turret, nest) with the opponent inside its synced reach, a 2D
    /// circle on squared fixed-point distance, fires once and spends one charge. A strike is a
    /// blockable construct hit: no knockback, no hitstop, no Rally, and its hitstun only lengthens a
    /// stun the victim already has (a launch or a tumble is never cancelled). It pays Momentum once
    /// per strike (the CountsAsCombo marker) and never triggers a strike itself.
    /// </summary>
    internal static class FighterSyncedStrikeRules {
        /// <summary>Projectile type slot digit of a turret's synced bolt (<c>CharacterID * 10 + 6</c>).</summary>
        public const int SyncedBoltSlot = 6;

        /// <summary>Frames a synced bolt lives (10 u of reach at 24 u/s is 25 frames).</summary>
        private const int SyncedBoltLifetimeFrames = 30;

        private static readonly FPVector2 BoltHalfExtents = new(FP64.FromDouble(0.2), FP64.FromDouble(0.2));
        private const int PendingBoltCapacity = 8;

        public static bool IsSyncedBolt(in FighterProjectileComponent projectile) =>
            projectile.UltimateOrigin == 0 && projectile.ProjectileTypeID % 10 == SyncedBoltSlot;

        public static void Resolve(ref Frame frame, int ownerID, int owed) {
            if (owed <= 0) return;
            int targetID = ownerID == 0 ? 1 : 0;
            for (int hit = 0; hit < owed; hit++) {
                bool anyFired = ResolveOneHit(ref frame, ownerID, targetID);
                if (!anyFired) break;
            }
        }

        private static bool ResolveOneHit(ref Frame frame, int ownerID, int targetID) {
            if (!FighterEntityQueries.TryFindFighter(ref frame, ownerID, out EntityRef ownerEntity)
                || !FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) return false;
            // Pending turret bolts are spawned after the filter pass (no structural change while iterating).
            FPVector2[] pendingBolts = null;
            int pendingCount = 0;
            bool fired = false;

            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef constructEntity)) {
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (target.Stocks <= 0 || target.CurrentHP <= 0 || target.RespawnFramesRemaining > 0) break;
                ref FighterPersistentObjectComponent construct = ref frame.Get<FighterPersistentObjectComponent>(constructEntity);
                if (construct.OwnerPlayerID != ownerID || !FighterConstructCharges.HasCharge(in construct)) continue;

                switch (construct.ObjectTypeID) {
                    case FighterConstructCharges.CoilObjectTypeID:
                        if (!FighterLingeringKitRules.WithinReach(
                                in construct.Position, in target.Position, FighterLingeringKitRules.CoilSyncedReachSquared)) continue;
                        FighterConstructCharges.Spend(ref construct);
                        fired = true;
                        PayStrike(ref frame, ownerEntity, targetEntity, FighterLingeringKitRules.Strike(
                            ref frame, ownerEntity, targetEntity,
                            LingeringKitRules.CoilSyncedArcDamage, LingeringKitRules.CoilSyncedArcHitstunFrames,
                            (int)StatusType.None, 0, FP64.One, construct.Position.x), LingeringKitRules.CoilSyncedArcDamage);
                        break;
                    case FighterConstructCharges.NestObjectTypeID:
                        if (!FighterLingeringKitRules.WithinReach(
                                in construct.Position, in target.Position, FighterLingeringKitRules.NestSyncedReachSquared)) continue;
                        FighterConstructCharges.Spend(ref construct);
                        fired = true;
                        PayStrike(ref frame, ownerEntity, targetEntity, FighterLingeringKitRules.Strike(
                            ref frame, ownerEntity, targetEntity,
                            LingeringKitRules.NestSyncedLungeDamage, LingeringKitRules.NestSyncedLungeHoldFrames,
                            (int)StatusType.Venom, LingeringKitRules.NestSyncedLungeVenomFrames,
                            FP64.FromInt(LingeringKitRules.NestSyncedLungeVenomIntensityPercent) / FP64.FromInt(100),
                            construct.Position.x), LingeringKitRules.NestSyncedLungeDamage);
                        break;
                    case FighterConstructCharges.TurretObjectTypeID:
                        if (!FighterLingeringKitRules.WithinReach(
                                in construct.Position, in target.Position, FighterLingeringKitRules.TurretSyncedReachSquared)) continue;
                        if (pendingCount >= PendingBoltCapacity) continue;
                        FighterConstructCharges.Spend(ref construct);
                        fired = true;
                        pendingBolts ??= new FPVector2[PendingBoltCapacity];
                        pendingBolts[pendingCount++] = construct.Position;
                        break;
                }
            }

            for (int index = 0; index < pendingCount; index++) {
                SpawnSyncedBolt(ref frame, ownerID, targetEntity, pendingBolts[index]);
            }
            return fired;
        }

        /// <summary>The CountsAsCombo marker for a synced strike: 0.05 pip once per landed strike, no D3 arming (plan 1.1).</summary>
        private static void PayStrike(ref Frame frame, EntityRef ownerEntity, EntityRef targetEntity, FighterHitOutcome outcome, int damage) {
            FighterLingeringKitRules.CountsAsCombo(
                ref frame.Get<FighterActionStateComponent>(ownerEntity),
                ref frame.Get<FighterVerbComponent>(ownerEntity),
                hitSlot: 0, sourceCastId: 0, outcome, damage,
                LingeringMomentumEvent.SyncedStrike, armD3: false);
        }

        /// <summary>The turret's covering fire: a fast straight bolt aimed at the target at fire time.</summary>
        private static void SpawnSyncedBolt(ref Frame frame, int ownerID, EntityRef targetEntity, in FPVector2 from) {
            if (!FighterEntityQueries.TryFindFighter(ref frame, ownerID, out EntityRef ownerEntity)) return;
            ref readonly FighterStateComponent target = ref frame.GetReadOnly<FighterStateComponent>(targetEntity);
            ref readonly FighterStateComponent owner = ref frame.GetReadOnly<FighterStateComponent>(ownerEntity);
            FP64 dx = target.Position.x - from.x;
            FP64 dy = target.Position.y - from.y;
            FP64 length = FighterKitGeometry.Sqrt(dx * dx + dy * dy);
            FP64 speed = FromMilliPerSecond(LingeringKitRules.TurretSyncedBoltSpeedMilli);
            FPVector2 velocity = length > FP64.Zero
                ? new FPVector2(dx * speed / length, dy * speed / length)
                : new FPVector2(owner.FacingRight != 0 ? speed : -speed, FP64.Zero);
            int characterID = owner.CharacterID;
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            EntityRef bolt = frame.CreateEntity();
            frame.Add(bolt, new FighterProjectileComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = ownerID,
                ProjectileTypeID = characterID * 10 + SyncedBoltSlot,
                LifetimeFrames = SyncedBoltLifetimeFrames,
                Damage = LingeringKitRules.TurretSyncedBoltDamage,
                AttackClass = FighterDamageRules.BasicAttackClass,
                StatusType = (int)StatusType.None,
                StatusFrames = 0,
                HitstunFrames = LingeringKitRules.TurretSyncedBoltHitstunFrames,
                UltimateOrigin = 0,
                StatusIntensity = FP64.One,
                GravityPerSecond = FP64.Zero,
                Position = from,
                Velocity = velocity,
                HalfExtents = BoltHalfExtents,
                Knockback = FPVector2.Zero
            });
        }

        // The projectile system integrates Velocity per second (Position += Velocity / 60).
        private static FP64 FromMilliPerSecond(int milli) => FighterLingeringKitRules.FromMilli(milli);
    }

    /// <summary>
    /// Decision 25: the Lorentz discharge replaces the mark chain. When Tesla's Lorentz Pulse fires
    /// (the first update of its zone), each of his coils within 8 u of him that has a recipient spends
    /// ALL its remaining charges as one burst (6 damage per charge, 20 frames of hitstun) and fizzles.
    /// The recipient is the opponent within the coil's own 4 u, or a Conductive-marked opponent within
    /// 8 u of the coil. The burst is a Detonation: 0.2 pip once per Pulse, and it arms D3 for the
    /// Pulse's own cast. (The Cataclysm Ultimate detonates every coil elsewhere, whatever its charges.)
    /// </summary>
    internal static class FighterLorentzDischargeRules {
        /// <summary>309 <c>ExecutionFlags</c> bit: this Pulse has already discharged.</summary>
        public const int FlagDischarged = 8;

        public static readonly int PulseZoneTypeID =
            (int)FighterCharacterID.Tesla * 10 + FighterHitContractTable.SlotSpecialTwo;

        public static bool IsPulse(in FighterZoneComponent zone) => zone.ZoneTypeID == PulseZoneTypeID;

        public static void Discharge(ref Frame frame, ref FighterZoneComponent zone) {
            if ((zone.ExecutionFlags & FlagDischarged) != 0) return;
            zone.ExecutionFlags |= FlagDischarged;
            int ownerID = zone.OwnerPlayerID;
            int targetID = ownerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, ownerID, out EntityRef ownerEntity)
                || !FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) return;
            bool paid = false;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef coilEntity)) {
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (target.Stocks <= 0 || target.CurrentHP <= 0 || target.RespawnFramesRemaining > 0) break;
                ref FighterPersistentObjectComponent coil = ref frame.Get<FighterPersistentObjectComponent>(coilEntity);
                if (coil.OwnerPlayerID != ownerID
                    || coil.ObjectTypeID != FighterConstructCharges.CoilObjectTypeID
                    || !FighterConstructCharges.HasCharge(in coil)) continue;
                ref readonly FighterStateComponent owner = ref frame.GetReadOnly<FighterStateComponent>(ownerEntity);
                if (!FighterLingeringKitRules.WithinReach(
                        in owner.Position, in coil.Position, FighterLingeringKitRules.DischargeOwnerRangeSquared)) continue;
                bool inOwnReach = FighterLingeringKitRules.WithinReach(
                    in coil.Position, in target.Position, FighterLingeringKitRules.DischargeCoilReachSquared);
                bool marked = !inOwnReach
                    && FighterConductiveRules.HasMarkFrom(in frame.GetReadOnly<FighterConductiveComponent>(targetEntity), ownerID)
                    && FighterLingeringKitRules.WithinReach(
                        in coil.Position, in target.Position, FighterLingeringKitRules.DischargeMarkedReachSquared);
                if (!inOwnReach && !marked) continue;

                int damage = LingeringKitRules.DischargeDamagePerCharge * coil.RemainingAttacks;
                FPVector2 coilPosition = coil.Position;
                coil.RemainingAttacks = 0; // the coil fizzles through the persistent system's destroy path
                FighterHitOutcome outcome = FighterLingeringKitRules.Strike(
                    ref frame, ownerEntity, targetEntity, damage, LingeringKitRules.DischargeHitstunFrames,
                    (int)StatusType.None, 0, FP64.One, coilPosition.x, FighterDamageRules.SpecialAttackClass);
                if (FighterLingeringKitRules.CountsAsCombo(
                        ref frame.Get<FighterActionStateComponent>(ownerEntity),
                        ref frame.Get<FighterVerbComponent>(ownerEntity),
                        FighterHitContractTable.SlotSpecialTwo, zone.CastId, outcome, damage,
                        LingeringMomentumEvent.Detonation, armD3: true, payoutAlreadyPaid: paid)) {
                    paid = true;
                }
            }
        }
    }

    /// <summary>
    /// Decisions 13 and 22: the Sandstorm Vortex pulls toward its centre at 7 u/s, faster than any run;
    /// only a roll's travel or an air dodge ignores the pull. It applies NO slow while a victim is inside:
    /// the lingering slow is applied once when the victim leaves its area or the Vortex ends. The "was
    /// inside" bit lives in 309 <c>CollapseTargetMask</c> (otherwise zero on a Vortex). The final pulse
    /// carries the Detonation marker (0.2 pip once, arms D3 for the cast).
    /// </summary>
    internal static class FighterVortexRules {
        /// <summary>309 <c>ExecutionFlags</c> bit: this zone's Momentum payout has been made.</summary>
        public const int FlagMomentumPaid = 4;

        public static bool IsVortex(in FighterZoneComponent zone) => zone.ZoneTypeID == FighterReachKitRules.SandstormVortexZone;

        /// <summary>True while a roll's travel or an air dodge runs: the pull does not move that fighter.</summary>
        public static bool IgnoresPull(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == (int)UniversalMovementPhase.RollTravel
            || FighterAirDodgeRules.IsAirDodge(runtime.UniversalMovementState);

        /// <summary>One frame: tracks inside / outside, pulls, and applies the exit slow.</summary>
        public static void Step(ref Frame frame, ref FighterZoneComponent zone) {
            int targetID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            int bit = 1 << targetID;
            bool wasInside = (zone.CollapseTargetMask & bit) != 0;
            bool inside = target.Stocks > 0 && target.RespawnFramesRemaining <= 0
                && FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents, in target.Position, in FighterHurtbox.HalfExtents);
            if (inside) zone.CollapseTargetMask |= bit; else zone.CollapseTargetMask &= ~bit;
            if (wasInside && !inside) ApplyExitSlow(ref target, ref targetRuntime, in zone);
            if (!inside || target.InvulnerabilityFrames > 0 || IgnoresPull(in targetRuntime)) return;
            FP64 step = FighterLingeringKitRules.VortexPullPerFrame;
            FP64 dx = zone.Position.x - target.Position.x;
            if (dx > step) target.Position.x += step;
            else if (dx < -step) target.Position.x -= step;
            else target.Position.x = zone.Position.x;
        }

        /// <summary>The Vortex ended with its victim still inside: the lingering slow starts now.</summary>
        public static void End(ref Frame frame, in FighterZoneComponent zone) {
            int targetID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if ((zone.CollapseTargetMask & (1 << targetID)) == 0) return;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ApplyExitSlow(ref target, ref targetRuntime, in zone);
        }

        private static void ApplyExitSlow(
            ref FighterStateComponent target, ref FighterRuntimeComponent targetRuntime, in FighterZoneComponent zone) {
            if (target.Stocks <= 0 || zone.StatusType == (int)StatusType.None || zone.StatusFrames <= 0) return;
            FighterDamageRules.ApplyStatus(ref target, ref targetRuntime, zone.StatusType, zone.StatusFrames, zone.StatusIntensity);
        }
    }

    /// <summary>
    /// Decision 24: Golden Ratio holds by pull. Each tick draws the target toward the spiral's centre by
    /// half the remaining distance, capped at 1.5 u a tick; tick 1 alone keeps a hitstun. The cast pays
    /// 0.05 pip once (HoldCast); its D3 arming as a direct hit is unchanged. Flags on 309 <c>ExecutionFlags</c>.
    /// </summary>
    internal static class FighterSpiralRules {
        public const int FlagFirstTickDone = 2;
        public const int FlagMomentumPaid = 4;

        /// <summary>Tick 1's hitstun, Story's <c>ChurnTickHitstunFrames</c>; the sim pulses had none before.</summary>
        public const int FirstTickHitstunFrames = LingeringKitRules.GoldenRatioFirstTickHitstunFrames;

        public static readonly int ZoneTypeID = (int)FighterCharacterID.Leonardo * 10 + FighterHitContractTable.SlotSpecialOne;

        public static bool IsSpiral(in FighterZoneComponent zone) => zone.ZoneTypeID == ZoneTypeID;

        /// <summary>Pulls <paramref name="target"/> toward <paramref name="centre"/> (a grounded target moves along the floor only).</summary>
        public static void Pull(ref FighterStateComponent target, in FPVector2 centre) {
            FP64 fraction = FighterLingeringKitRules.GoldenRatioPullFraction;
            FP64 pullX = (centre.x - target.Position.x) * fraction;
            FP64 pullY = target.IsGrounded != 0 ? FP64.Zero : (centre.y - target.Position.y) * fraction;
            FP64 cap = FighterLingeringKitRules.GoldenRatioPullCap;
            FP64 lengthSquared = pullX * pullX + pullY * pullY;
            if (lengthSquared > cap * cap) {
                FP64 scale = cap / FighterKitGeometry.Sqrt(lengthSquared);
                pullX *= scale;
                pullY *= scale;
            }
            target.Position.x += pullX;
            target.Position.y += pullY;
        }
    }
}
