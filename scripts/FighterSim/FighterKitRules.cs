using FTT.Combat;
using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Package 13 W7a — deterministic geometry helpers for the Einstein,
    /// Leonardo, Tesla and Shakespeare kit rules. Pure fixed-point; nothing here
    /// reads a Story modifier or Godot state.
    /// </summary>
    public static class FighterKitGeometry {
        /// <summary>The fighter hit box every entity system tests against (half extents).</summary>
        public static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);
        private static readonly FP64 InverseSqrtTwo = FP64.FromDouble(0.70710678118654752);
        private static readonly FP64 Half = FP64.One / FP64.FromInt(2);

        /// <summary>
        /// Deterministic square root by a fixed number of Newton steps on FP64 —
        /// no float, identical on every machine and across a rollback. Accurate
        /// well past the precision the kit geometry needs for the arena's
        /// distances (≤ ~30 units).
        /// </summary>
        public static FP64 Sqrt(FP64 value) {
            if (value <= FP64.Zero) return FP64.Zero;
            FP64 estimate = value > FP64.One ? value : FP64.One;
            for (int step = 0; step < 32; step++) {
                FP64 next = (estimate + value / estimate) * Half;
                if (next == estimate) break;
                estimate = next;
            }
            return estimate;
        }

        /// <summary>True when a circle overlaps an axis-aligned box (closest-point test).</summary>
        public static bool CircleOverlapsBox(
            in FPVector2 center, FP64 radius, in FPVector2 boxCenter, in FPVector2 boxHalfExtents) {
            FP64 dx = FP64.Abs(center.x - boxCenter.x) - boxHalfExtents.x;
            FP64 dy = FP64.Abs(center.y - boxCenter.y) - boxHalfExtents.y;
            if (dx < FP64.Zero) dx = FP64.Zero;
            if (dy < FP64.Zero) dy = FP64.Zero;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>
        /// E03 ground snap: the highest walkable surface at <paramref name="x"/> at
        /// or below <paramref name="fromY"/> — a one-way platform or the main
        /// floor where it has support. <paramref name="fallback"/> when there is
        /// none (a pit).
        /// </summary>
        public static FP64 GroundBelow(FighterStageGeometry geometry, FP64 x, FP64 fromY, FP64 fallback) {
            if (geometry == null) return fromY >= FP64.Zero ? FP64.Zero : fallback;
            FP64 tolerance = FP64.FromDouble(0.05);
            bool found = false;
            FP64 best = FP64.Zero;
            for (int index = 0; index < geometry.Platforms.Length; index++) {
                FighterStagePlatform platform = geometry.Platforms[index];
                if (!platform.Supports(x) || platform.SurfaceY > fromY + tolerance) continue;
                if (!found || platform.SurfaceY > best) {
                    best = platform.SurfaceY;
                    found = true;
                }
            }
            if (geometry.HasFloorSupport(x) && FP64.Zero <= fromY + tolerance && (!found || best < FP64.Zero)) {
                best = FP64.Zero;
                found = true;
            }
            return found ? best : fallback;
        }

        /// <summary>
        /// Clamps a straight displacement of <paramref name="distance"/> from
        /// <paramref name="start"/> along the 8-way direction (dirX, dirY; world Y
        /// up) so the end point keeps full-body clearance inside the stage: within
        /// the side walls, at or below the ceiling and not beneath the floor
        /// plane. The walls/ceiling/floor box is convex, so the farthest valid
        /// point along the ray is the smallest per-axis ratio.
        /// </summary>
        public static FPVector2 ClampedDisplacement(
            FighterStageGeometry geometry, in FPVector2 start, int dirX, int dirY, FP64 distance) {
            FP64 axis = dirX != 0 && dirY != 0 ? distance * InverseSqrtTwo : distance;
            FP64 wantX = axis * FP64.FromInt(dirX);
            FP64 wantY = axis * FP64.FromInt(dirY);
            FP64 left = geometry?.LeftWall ?? FP64.FromInt(-10);
            FP64 right = geometry?.RightWall ?? FP64.FromInt(10);
            FP64 ceiling = geometry?.Ceiling ?? FP64.FromInt(9);
            FP64 ratio = FP64.One;
            int bindingAxis = 0; // 0 none, 1 x, 2 y
            FP64 allowedX = FP64.Zero, allowedY = FP64.Zero;
            if (dirX != 0) {
                allowedX = dirX > 0 ? right - start.x : start.x - left;
                if (allowedX < FP64.Zero) allowedX = FP64.Zero;
                FP64 wanted = FP64.Abs(wantX);
                if (wanted > allowedX) {
                    FP64 candidate = allowedX / wanted;
                    if (candidate < ratio) { ratio = candidate; bindingAxis = 1; }
                }
            }
            if (dirY != 0) {
                allowedY = dirY > 0 ? ceiling - start.y : start.y;
                if (allowedY < FP64.Zero) allowedY = FP64.Zero;
                FP64 wanted = FP64.Abs(wantY);
                if (wanted > allowedY) {
                    FP64 candidate = allowedY / wanted;
                    if (candidate < ratio) { ratio = candidate; bindingAxis = 2; }
                }
            }
            // The binding axis lands exactly on its limit; the other scales.
            FP64 offsetX = bindingAxis == 1 ? allowedX * FP64.FromInt(dirX) : wantX * ratio;
            FP64 offsetY = bindingAxis == 2 ? allowedY * FP64.FromInt(dirY) : wantY * ratio;
            return new FPVector2(offsetX, offsetY);
        }
    }

    /// <summary>
    /// Package 13 W7a — the burst-on-terrain projectile primitive (the sim half;
    /// Story's is <c>PlaceholderProjectile.BurstsOnTerrain</c>). A projectile
    /// whose slot contract authors a burst radius is two-stage: a direct contact
    /// deals the contract's <c>ContactDamage</c> and no status, then the burst
    /// — the projectile's own spawn damage, knockback, hitstun, launch flag and
    /// status — lands on every opponent inside the radius. With
    /// <c>BurstsOnTerrain</c> the burst also fires where the shot strikes a wall,
    /// the ceiling or the floor, or reaches its maximum range. Einstein's burst
    /// inside his own active Relativity Rift triggers Rift Collapse (E01).
    /// </summary>
    public static class FighterProjectileBurstRules {

        /// <summary>
        /// True when the projectile has left the playable space: a side wall, the
        /// ceiling, or the floor plane where the stage has floor under it.
        /// </summary>
        public static bool StruckTerrain(FighterStageGeometry geometry, in FighterProjectileComponent projectile) {
            FP64 left = geometry?.LeftWall ?? FP64.FromInt(-10);
            FP64 right = geometry?.RightWall ?? FP64.FromInt(10);
            FP64 ceiling = geometry?.Ceiling ?? FP64.FromInt(9);
            if (projectile.Position.x - projectile.HalfExtents.x <= left) return true;
            if (projectile.Position.x + projectile.HalfExtents.x >= right) return true;
            if (projectile.Position.y + projectile.HalfExtents.y >= ceiling) return true;
            bool floorHere = geometry == null || geometry.HasFloorSupport(projectile.Position.x);
            return floorHere && projectile.Position.y - projectile.HalfExtents.y <= FP64.Zero
                && projectile.Velocity.y <= FP64.Zero;
        }

        /// <summary>
        /// Resolves the burst stage at <paramref name="at"/>. The burst is a
        /// direct Special-class hit (it reclaims Rally and earns meter like any
        /// direct Special) of the projectile's own damage.
        /// </summary>
        public static void Burst(
            ref Frame frame,
            in FighterProjectileComponent projectile,
            in FighterAbilityHitData contract,
            in FPVector2 at) {
            int ownerID = projectile.OwnerPlayerID;
            int targetID = ownerID == 0 ? 1 : 0;
            // E01: the same E=mc² execution collapses Einstein's own active
            // rift. Who is caught is decided at the detonation instant, before
            // the burst resolves — so a blocker the burst then shatters was
            // still a blocker, and is not pulled.
            if (projectile.ProjectileTypeID == (int)FighterCharacterID.Einstein * 10 + FighterHitContractTable.SlotSpecialOne) {
                FighterRiftRules.TryCollapse(ref frame, ownerID, in at);
            }
            if (FighterEntityQueries.TryFindFighter(ref frame, ownerID, out EntityRef ownerEntity)
                && FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) {
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (FighterKitGeometry.CircleOverlapsBox(
                        in at, contract.BurstRadius, in target.Position, in FighterKitGeometry.FighterHalfExtents)) {
                    ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                    ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                    ref FighterVerbComponent ownerVerb = ref frame.Get<FighterVerbComponent>(ownerEntity);
                    ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                    ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
                    ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
                    ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                    FighterDamageRules.ApplyFighterHit(
                        ref owner, ref ownerRuntime, ref ownerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                        projectile.AttackClass, projectile.Damage,
                        contract.HasKnockbackVector ? contract.KnockbackX : projectile.Knockback.x,
                        contract.HitstunFrames > 0 ? contract.HitstunFrames : projectile.HitstunFrames,
                        projectile.StatusType, projectile.StatusFrames, projectile.StatusIntensity, at.x,
                        creditInfluence: projectile.UltimateOrigin == 0,
                        launches: contract.HitstunFrames > 0 ? contract.Launches : true,
                        shieldBreaker: projectile.AttackClass == FighterDamageRules.SpecialAttackClass
                            && contract.ShieldBreaker,
                        hasKnockbackVector: contract.HasKnockbackVector,
                        knockbackVertical: contract.KnockbackY);
                }
            }
        }
    }

    /// <summary>
    /// Package 13 W7a — Einstein's Relativity Rift (E01, E03) in the sim.
    /// <list type="bullet">
    /// <item>E03: thrown up to 5 units ahead (inside the walls), ground-snapped
    /// when cast grounded, mid-air when airborne; 1.5-unit radius; Time Dilation
    /// refreshed every frame an opponent stands inside and lingering 0.5 s
    /// (the authored <c>StatusDuration</c>) after leaving — re-entry refreshes,
    /// never stacks (the shared stronger-wins status rule).</item>
    /// <item>E01 Rift Collapse: an E=mc² burst inside his own active rift ends
    /// it at once (ticks and speed buff stop) and pulls every opponent inside
    /// to the centre over 6 frames, then launches them upward — a 0-damage,
    /// Special-class hit of the same execution: blockers and invulnerable
    /// targets are not caught, it spends no block charge and earns no meter or
    /// Rally. State lives on the rift's own zone entity
    /// (<c>CollapseFramesRemaining</c>, <c>CollapseTargetMask</c>).</item>
    /// </list>
    /// </summary>
    public static class FighterRiftRules {
        public static readonly int RiftZoneTypeID =
            (int)FighterCharacterID.Einstein * 10 + FighterHitContractTable.SlotSpecialTwo;
        public static readonly FP64 ThrowDistance = FP64.FromDouble(KitMotionRules.RelativityRiftThrowUnits);
        public static readonly FP64 Radius = FP64.FromDouble(KitMotionRules.RelativityRiftRadiusUnits);
        private static readonly FP64 LaunchSpeed = FP64.FromDouble(KitMotionRules.RiftCollapseLaunchUnits);
        private const int CollapseLaunchHitstunFrames = 30;

        public static bool IsRift(in FighterZoneComponent zone) => zone.ZoneTypeID == RiftZoneTypeID;
        public static bool IsCollapsing(in FighterZoneComponent zone) => zone.CollapseFramesRemaining > 0;

        /// <summary>E03 placement: 5 units ahead inside the walls, ground-snapped when grounded.</summary>
        public static FPVector2 Placement(FighterStageGeometry geometry, in FighterStateComponent owner) {
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FPVector2 offset = FighterKitGeometry.ClampedDisplacement(geometry, in owner.Position, facing, 0, ThrowDistance);
            FP64 x = owner.Position.x + offset.x;
            FP64 y = owner.IsGrounded != 0
                ? FighterKitGeometry.GroundBelow(geometry, x, owner.Position.y, owner.Position.y)
                : owner.Position.y;
            return new FPVector2(x, y);
        }

        /// <summary>
        /// E01 trigger. True when a live, non-collapsing rift of this owner
        /// contains the burst point; the rift then collapses on the spot.
        /// </summary>
        public static bool TryCollapse(ref Frame frame, int ownerID, in FPVector2 burstPoint) {
            var filter = frame.Filter<FighterZoneComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterZoneComponent zone = ref frame.Get<FighterZoneComponent>(entity);
                if (zone.OwnerPlayerID != ownerID || !IsRift(in zone) || IsCollapsing(in zone)
                    || zone.LifetimeFrames <= 0) continue;
                FP64 dx = burstPoint.x - zone.Position.x;
                FP64 dy = burstPoint.y - zone.Position.y;
                if (dx * dx + dy * dy > zone.HalfExtents.x * zone.HalfExtents.x) continue;

                int mask = 0;
                int targetID = ownerID == 0 ? 1 : 0;
                if (FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) {
                    ref readonly FighterStateComponent target = ref frame.GetReadOnly<FighterStateComponent>(targetEntity);
                    ref readonly FighterRuntimeComponent targetRuntime = ref frame.GetReadOnly<FighterRuntimeComponent>(targetEntity);
                    ref readonly FighterVerbComponent targetVerb = ref frame.GetReadOnly<FighterVerbComponent>(targetEntity);
                    ref readonly FighterDefenseComponent targetDefense = ref frame.GetReadOnly<FighterDefenseComponent>(targetEntity);
                    if (CanBeCaught(in target, in targetRuntime, in targetVerb, in targetDefense)
                        && FighterEntityQueries.Overlaps(
                            in zone.Position, in zone.HalfExtents,
                            in target.Position, in FighterKitGeometry.FighterHalfExtents)) {
                        mask |= 1 << targetID;
                    }
                }
                zone.CollapseFramesRemaining = KitMotionRules.RiftCollapsePullFrames;
                zone.CollapseTargetMask = mask;
                zone.GrantsOwnerSpeedBonus = 0;
                return true;
            }
            return false;
        }

        private static bool CanBeCaught(
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            in FighterVerbComponent targetVerb,
            in FighterDefenseComponent targetDefense) =>
            target.Stocks > 0
            && target.RespawnFramesRemaining <= 0
            && target.InvulnerabilityFrames <= 0
            && !FighterDefenseRules.IsDefyProtected(in targetDefense)
            && !FighterGrabRules.IsBusy(in targetVerb)
            && !FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime, in targetVerb);

        /// <summary>
        /// One collapse frame: each caught opponent moves 1/remaining of the way to
        /// the centre (so it arrives exactly on the last frame) with its velocity
        /// held at zero; on the last frame the launch lands and the rift is gone.
        /// Returns true when the zone should be destroyed.
        /// </summary>
        public static bool AdvanceCollapse(
            ref Frame frame, ref FighterZoneComponent zone, FighterHitContractTable contracts) {
            int targetID = zone.OwnerPlayerID == 0 ? 1 : 0;
            bool caught = (zone.CollapseTargetMask & (1 << targetID)) != 0;
            if (caught && FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) {
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (target.Stocks > 0 && target.RespawnFramesRemaining <= 0) {
                    FP64 remaining = FP64.FromInt(zone.CollapseFramesRemaining);
                    target.Position = new FPVector2(
                        target.Position.x + (zone.Position.x - target.Position.x) / remaining,
                        target.Position.y + (zone.Position.y - target.Position.y) / remaining);
                    target.Velocity = FPVector2.Zero;
                    if (zone.CollapseFramesRemaining == 1) Launch(ref frame, in zone, targetEntity, contracts);
                }
            }
            zone.CollapseFramesRemaining--;
            return zone.CollapseFramesRemaining <= 0;
        }

        private static void Launch(
            ref Frame frame, in FighterZoneComponent zone, EntityRef targetEntity, FighterHitContractTable contracts) {
            if (!FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef ownerEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            // A target who raised the stance mid-pull is not launched: the
            // collapse never costs a block charge of its own.
            if (FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime, in targetVerb)) return;
            ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
            ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
            ref FighterVerbComponent ownerVerb = ref frame.Get<FighterVerbComponent>(ownerEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            FighterDamageRules.ApplyFighterHit(
                ref owner, ref ownerRuntime, ref ownerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                FighterDamageRules.SpecialAttackClass, 0, FP64.Zero, CollapseLaunchHitstunFrames,
                (int)StatusType.None, 0, FP64.One, zone.Position.x,
                creditInfluence: false,
                collectsEcho: false,
                appliesHitstop: false,
                launches: true,
                hasKnockbackVector: true,
                knockbackVertical: LaunchSpeed);
        }

        /// <summary>
        /// E03 linger: every frame an eligible opponent overlaps the live rift it
        /// is (re)given the rift's Time Dilation for the authored linger
        /// (<c>zone.StatusFrames</c>), so it lasts exactly that long after it
        /// leaves; re-entry refreshes through the stronger-wins rule.
        /// </summary>
        public static void RefreshLinger(ref Frame frame, in FighterZoneComponent zone) {
            if (zone.StatusType == (int)StatusType.None || zone.StatusFrames <= 0) return;
            int targetID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.InvulnerabilityFrames > 0) return;
            ref readonly FighterDefenseComponent defense = ref frame.GetReadOnly<FighterDefenseComponent>(targetEntity);
            if (FighterDefenseRules.IsDefyProtected(in defense)) return;
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterKitGeometry.FighterHalfExtents)) return;
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            FighterDamageRules.ApplyStatus(
                ref target, ref targetRuntime, zone.StatusType, zone.StatusFrames, zone.StatusIntensity);
        }
    }

    /// <summary>
    /// Package 13 W7a (S01/S02) — The Tempest is a WINDBOX, not a hit. For the
    /// storm's life (the authored 12 active frames) every opponent inside the
    /// 2-unit radius, blocking or not, is pushed outward 3/12 of a unit per
    /// frame — about 3 units in all — by a direct positional shift. No
    /// <c>ApplyFighterHit</c>: no block charge, hitstun, hitstop, shieldstun,
    /// DI, tumble, meter or Rally. A fighter in a grab state is not moved.
    /// </summary>
    public static class FighterTempestRules {
        public static readonly int TempestZoneTypeID =
            (int)FighterCharacterID.Shakespeare * 10 + FighterHitContractTable.SlotSpecialTwo;
        public static readonly FP64 Radius = FP64.FromDouble(KitMotionRules.TempestRadiusUnits);
        public static readonly FP64 PushPerFrame =
            FP64.FromDouble(KitMotionRules.TempestPushUnits / KitMotionRules.TempestPushFrames);
        /// <summary>Launch speed that rises exactly <c>TempestLiftUnits</c> under the sim's gravity (v = √(2gh)).</summary>
        public static readonly FP64 LiftSpeed = FP64.FromDouble(
            System.Math.Sqrt(2.0 * 30.0 * KitMotionRules.TempestLiftUnits));

        public static bool IsTempest(in FighterZoneComponent zone) => zone.ZoneTypeID == TempestZoneTypeID;

        /// <summary>
        /// One windbox frame. An opponent the storm touches is caught (its bit in
        /// the zone's <c>CollapseTargetMask</c>, reused here as the Tempest's
        /// caught set) and pushed every remaining frame of the storm — so the
        /// shove carries the full ~3 units, not just to the radius edge.
        /// </summary>
        public static void Push(ref Frame frame, ref FighterZoneComponent zone) {
            int targetID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.RespawnFramesRemaining > 0) return;
            ref readonly FighterVerbComponent targetVerb = ref frame.GetReadOnly<FighterVerbComponent>(targetEntity);
            if (FighterGrabRules.IsBusy(in targetVerb)) return;
            int bit = 1 << targetID;
            if ((zone.CollapseTargetMask & bit) == 0) {
                if (!FighterKitGeometry.CircleOverlapsBox(
                        in zone.Position, zone.HalfExtents.x, in target.Position, in FighterKitGeometry.FighterHalfExtents)) return;
                zone.CollapseTargetMask |= bit;
            }
            int sign = target.Position.x > zone.Position.x ? 1
                : target.Position.x < zone.Position.x ? -1
                : target.FacingRight != 0 ? -1 : 1;
            target.Position.x += PushPerFrame * FP64.FromInt(sign);
        }
    }

    /// <summary>
    /// Package 13 W7a (L03, D14) — Leonardo's Clockwork Turret fires real bolts:
    /// straight projectiles at the authored bolt speed toward the nearest target
    /// in range, only while one is in range (so the lifetime is the idle cap and
    /// the 4 bolts the ammunition), and the Ornithopter glide's once-per-flight
    /// bonus bolt spends one of them. Line of sight: the sim's stages are an open
    /// box with pass-through one-way platforms, so any in-range target inside
    /// the walls is in sight.
    /// </summary>
    public static class FighterTurretRules {
        public const int TurretObjectTypeID = 2;
        /// <summary>Projectile type slot digit for construct bolts (Basic-class, non-launching, no Rally, no hitstop).</summary>
        public const int ConstructBoltSlot = 5;
        private const int BoltHitstunFrames = 10;
        private static readonly FP64 DefaultBoltSpeed = FP64.FromDouble(KitMotionRules.TurretBoltSpeedUnits);
        private static readonly FPVector2 BoltHalfExtents = new(FP64.FromDouble(0.2), FP64.FromDouble(0.2));
        private const int BasicButton = 1 << 2;

        public static bool IsConstructBolt(in FighterProjectileComponent projectile) =>
            projectile.UltimateOrigin == 0 && projectile.ProjectileTypeID % 10 == ConstructBoltSlot;

        /// <summary>
        /// Fires one bolt from <paramref name="turret"/> at the opponent when in
        /// range. Returns false (spending nothing) with no target in range.
        /// </summary>
        public static bool TryFireBolt(ref Frame frame, ref FighterPersistentObjectComponent turret) {
            int targetID = turret.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetID, out EntityRef targetEntity)
                || !FighterEntityQueries.TryFindFighter(ref frame, turret.OwnerPlayerID, out EntityRef ownerEntity)) return false;
            ref readonly FighterStateComponent target = ref frame.GetReadOnly<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.RespawnFramesRemaining > 0) return false;
            if (FP64.Abs(target.Position.x - turret.Position.x) > turret.AttackRange) return false;

            ref readonly FighterStateComponent owner = ref frame.GetReadOnly<FighterStateComponent>(ownerEntity);
            ref readonly FighterAbilityModeComponent modes = ref frame.GetReadOnly<FighterAbilityModeComponent>(ownerEntity);
            bool slotTwo = modes.SpecialTwoPersistentTypeID == TurretObjectTypeID;
            FP64 speed = slotTwo ? modes.SpecialTwoProjectileSpeed : modes.SpecialOneProjectileSpeed;
            if (speed <= FP64.Zero) speed = DefaultBoltSpeed;
            int lifetime = slotTwo ? modes.SpecialTwoProjectileLifetimeFrames : modes.SpecialOneProjectileLifetimeFrames;
            if (lifetime <= 0) lifetime = 150;

            FP64 dx = target.Position.x - turret.Position.x;
            FP64 dy = target.Position.y - turret.Position.y;
            FP64 length = FighterKitGeometry.Sqrt(dx * dx + dy * dy);
            FPVector2 velocity = length > FP64.Zero
                ? new FPVector2(dx * speed / length, dy * speed / length)
                : new FPVector2(owner.FacingRight != 0 ? speed : -speed, FP64.Zero);

            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            EntityRef bolt = frame.CreateEntity();
            frame.Add(bolt, new FighterProjectileComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = turret.OwnerPlayerID,
                ProjectileTypeID = owner.CharacterID * 10 + ConstructBoltSlot,
                LifetimeFrames = lifetime,
                Damage = turret.Damage,
                AttackClass = FighterDamageRules.BasicAttackClass,
                StatusType = turret.StatusType,
                StatusFrames = turret.StatusFrames,
                HitstunFrames = BoltHitstunFrames,
                UltimateOrigin = 0,
                StatusIntensity = FP64.One,
                GravityPerSecond = FP64.Zero,
                Position = turret.Position,
                Velocity = velocity,
                HalfExtents = BoltHalfExtents,
                Knockback = FPVector2.Zero
            });
            if (turret.RemainingAttacks > 0) turret.RemainingAttacks--;
            turret.ActionCooldownFrames = turret.BaseActionCooldownFrames;
            return true;
        }

        /// <summary>A new Ornithopter flight re-arms the once-per-flight glide bolt on every turret its owner has.</summary>
        public static void BeginFlight(ref Frame frame, int ownerID) {
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterPersistentObjectComponent persistent = ref frame.Get<FighterPersistentObjectComponent>(entity);
                if (persistent.OwnerPlayerID == ownerID && persistent.ObjectTypeID == TurretObjectTypeID) {
                    persistent.GlideBoltSpent = 0;
                }
            }
        }

        /// <summary>
        /// D14: during Leonardo's Ornithopter glide a basic-attack press commands
        /// his turret to fire one bolt now, once per flight, spending one of its
        /// four. A refused command (no turret, no ammunition, no target in range,
        /// already spent) spends nothing.
        /// </summary>
        public static bool TryCommandGlideBolt(
            ref Frame frame, in FighterStateComponent fighter, in FighterRuntimeComponent runtime,
            in FighterAbilityModeComponent modes) {
            if (fighter.CharacterID != (int)FighterCharacterID.Leonardo
                || modes.MovementType != (int)MovementType.Glide
                || runtime.FloatFrames <= 0
                || fighter.IsGrounded != 0
                || fighter.HitstunFrames > 0
                || fighter.DazeFrames > 0
                || (runtime.PressedButtons & BasicButton) == 0) return false;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterPersistentObjectComponent turret = ref frame.Get<FighterPersistentObjectComponent>(entity);
                if (turret.OwnerPlayerID != fighter.PlayerID || turret.ObjectTypeID != TurretObjectTypeID
                    || turret.CurrentHP <= 0 || turret.LifetimeFrames <= 0 || turret.RemainingAttacks == 0
                    || turret.GlideBoltSpent != 0) continue;
                if (!TryFireBolt(ref frame, ref turret)) continue;
                turret.GlideBoltSpent = 1;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Package 13 W7a (T01) — the Lorentz chain's designed effect: each of that
    /// Tesla's own active coils within 8 units of the marked target fires an
    /// instant 8-damage arc at it (max 16 with both coils). Chain arcs are direct
    /// Special-class hits with no added launch; they earn meter and reclaim Rally
    /// normally and never chain recursively (they create no mark).
    /// </summary>
    public static class FighterLorentzChainRules {
        public const int CoilObjectTypeID = 1;
        public const int ArcDamage = KitMotionRules.LorentzChainArcDamage;
        private const int ArcHitstunFrames = 6;
        private static readonly FP64 RangeSquared = FP64.FromDouble(
            KitMotionRules.LorentzChainCoilRangeUnits * KitMotionRules.LorentzChainCoilRangeUnits);

        /// <summary>Number of the owner's live coils within 8 units of <paramref name="targetPosition"/>.</summary>
        public static int EligibleCoils(ref Frame frame, int ownerID, in FPVector2 targetPosition) {
            int count = 0;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterPersistentObjectComponent coil = ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                if (IsEligible(in coil, ownerID, in targetPosition)) count++;
            }
            return count;
        }

        public static bool IsEligible(in FighterPersistentObjectComponent coil, int ownerID, in FPVector2 targetPosition) {
            if (coil.OwnerPlayerID != ownerID || coil.ObjectTypeID != CoilObjectTypeID
                || coil.CurrentHP <= 0 || coil.LifetimeFrames <= 0) return false;
            FP64 dx = coil.Position.x - targetPosition.x;
            FP64 dy = coil.Position.y - targetPosition.y;
            return dx * dx + dy * dy <= RangeSquared;
        }

        /// <summary>Fires one arc per eligible coil (called once the chain has consumed the mark).</summary>
        public static void FireArcs(
            ref Frame frame,
            int ownerID,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning) {
            // Coil positions are read up front: ApplyFighterHit never touches
            // persistent objects, but the filter must not be held across it.
            FPVector2 first = default, second = default;
            int count = 0;
            FPVector2 targetPosition = target.Position;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterPersistentObjectComponent coil = ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                if (!IsEligible(in coil, ownerID, in targetPosition)) continue;
                if (count == 0) first = coil.Position;
                else if (count == 1) second = coil.Position;
                count++;
            }
            for (int index = 0; index < count && index < 2; index++) {
                FPVector2 origin = index == 0 ? first : second;
                FighterDamageRules.ApplyFighterHit(
                    ref attacker, ref attackerRuntime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    FighterDamageRules.SpecialAttackClass, ArcDamage, FP64.Zero, ArcHitstunFrames,
                    (int)StatusType.None, 0, FP64.One, origin.x,
                    appliesHitstop: false,
                    launches: false);
            }
        }
    }
}
