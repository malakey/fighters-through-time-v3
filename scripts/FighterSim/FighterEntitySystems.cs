using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    public static class FighterSpawnIntervals {
        public static int OrbFrames(int frequency) => frequency switch {
            1 => 3600,
            2 => 1500,
            3 => 660,
            _ => 0
        };

        public static int HazardFrames(int frequency) => frequency switch {
            1 => 3600,
            2 => 2700,
            3 => 1800,
            _ => 0
        };
    }

    internal static class FighterEntityQueries {
        public static bool TryFindFighter(ref Frame frame, int playerID, out EntityRef found) {
            var filter = frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                if (frame.GetReadOnly<FighterStateComponent>(entity).PlayerID == playerID) {
                    found = entity;
                    return true;
                }
            }
            found = default;
            return false;
        }

        public static bool Overlaps(
            in FPVector2 firstPosition,
            in FPVector2 firstHalfExtents,
            in FPVector2 secondPosition,
            in FPVector2 secondHalfExtents) =>
            FP64.Abs(firstPosition.x - secondPosition.x) <= firstHalfExtents.x + secondHalfExtents.x
            && FP64.Abs(firstPosition.y - secondPosition.y) <= firstHalfExtents.y + secondHalfExtents.y;
    }

    internal static class FighterDamageRules {
        public const int BasicAttackClass = 1;
        public const int SpecialAttackClass = 2;
        public const int UltimateAttackClass = 3;
        public const int HazardAttackClass = 4;
        private const int BlockButton = 1 << 6;
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);

        public static bool ApplyFighterHit(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            in FighterTuningComponent targetTuning,
            int attackClass,
            int damage,
            FP64 knockback,
            int hitstunFrames,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            FP64 hitOriginX,
            bool creditInfluence = true) {
            if (target.InvulnerabilityFrames > 0 || target.Stocks <= 0) return false;

            if (targetRuntime.AegisHits > 0) {
                targetRuntime.AegisHits--;
                return false;
            }

            // Pure tick/status pulses (zone effects) carry no impulse: they bypass
            // the front-facing shield and must not interrupt movement or zero the
            // target's velocity.
            bool carriesImpulse = knockback > FP64.Zero || hitstunFrames > 0;
            bool targetBlocking = carriesImpulse
                && targetRuntime.UniversalMovementState == (int)UniversalMovementPhase.None
                && (targetRuntime.HeldButtons & BlockButton) != 0;
            bool hitInFront = target.FacingRight != 0
                ? hitOriginX >= target.Position.x
                : hitOriginX <= target.Position.x;
            if (targetBlocking && hitInFront && attackClass != UltimateAttackClass && target.BlockCharges > 0) {
                int cost = attackClass == SpecialAttackClass ? target.BlockCharges : 1;
                target.BlockCharges -= cost;
                if (target.BlockCharges <= 0) {
                    target.BlockCharges = 0;
                    target.DazeFrames = 60;
                    target.Velocity.x = target.FacingRight != 0 ? FP64.FromInt(-2) : FP64.FromInt(2);
                    target.Velocity.y = FP64.One;
                }
                return false;
            }

            int resolvedDamage = damage > 0 ? damage : 0;
            if (targetRuntime.StatusType == (int)StatusType.RadiantBurn) {
                FP64 multiplier = FP64.One + targetRuntime.StatusIntensity / FP64.FromInt(4);
                long numerator = (long)resolvedDamage * multiplier.RawValue + FP64.One.RawValue / 2;
                resolvedDamage = (int)(numerator / FP64.One.RawValue);
            }

            int previousHP = target.CurrentHP;
            int remainingHP = target.CurrentHP - resolvedDamage;
            target.CurrentHP = remainingHP > 0 ? remainingHP : 0;
            int actualDamage = previousHP - target.CurrentHP;
            if (creditInfluence) {
                attacker.Influence = FP64.Min(MaxInfluence, attacker.Influence + FP64.FromInt(actualDamage));
            }
            target.Influence = FP64.Min(
                MaxInfluence,
                target.Influence + FP64.FromInt(actualDamage) / FP64.FromInt(4));

            if (carriesImpulse) {
                FighterUniversalMovementRules.Cancel(ref targetRuntime);
            }

			if (carriesImpulse && (target.HyperArmorFrames <= 0 || attackClass == UltimateAttackClass)) {
                FP64 force = knockback / (FP64.One + target.Weight);
                target.Velocity.x = hitOriginX <= target.Position.x ? force : -force;
                target.Velocity.y = force;
                target.IsGrounded = 0;
                target.HitstunFrames = hitstunFrames;
            }

            ApplyStatus(ref target, ref targetRuntime, statusType, statusFrames, statusIntensity);
            if (target.CurrentHP <= 0) {
                FighterSimulationRules.ApplyStockLoss(ref target, ref targetRuntime, in targetTuning);
            }
            return true;
        }

        public static bool ApplyEnvironmentHit(
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            in FighterTuningComponent targetTuning,
            int damage,
            FP64 knockback,
            int hitstunFrames,
            FP64 hitOriginX) => ApplyEnvironmentHit(
                ref target,
                ref targetRuntime,
                in targetTuning,
                damage,
                knockback,
                hitstunFrames,
                hitOriginX,
                (int)StatusType.None,
                0,
                FP64.One);

        public static bool ApplyEnvironmentHit(
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            in FighterTuningComponent targetTuning,
            int damage,
            FP64 knockback,
            int hitstunFrames,
            FP64 hitOriginX,
            int statusType,
            int statusFrames,
            FP64 statusIntensity) {
            FighterStateComponent environment = default;
            FighterRuntimeComponent environmentRuntime = default;
            return ApplyFighterHit(
                ref environment,
                ref environmentRuntime,
                ref target,
                ref targetRuntime,
                in targetTuning,
                HazardAttackClass,
                damage,
                knockback,
                hitstunFrames,
                statusType,
                statusFrames,
                statusIntensity,
                hitOriginX,
                false);
        }

        private static void ApplyStatus(
            ref FighterStateComponent targetState,
            ref FighterRuntimeComponent targetRuntime,
            int statusType,
            int statusFrames,
            FP64 statusIntensity) {
            if (statusType == (int)StatusType.None || statusFrames <= 0) return;
            targetRuntime.StatusType = statusType;
            targetRuntime.StatusFrames = statusFrames;
            targetRuntime.StatusIntensity = statusIntensity > FP64.Zero ? statusIntensity : FP64.One;
            targetRuntime.StatusTickFrames = statusType == (int)StatusType.Venom ? 60 : 0;
            if (statusType == (int)StatusType.StaticCharge) {
                targetState.HitstunFrames = targetState.HitstunFrames > statusFrames
                    ? targetState.HitstunFrames
                    : statusFrames;
            }
        }
    }

    public sealed class FighterAbilityEntitySystem : ISystem {
        private const int SpecialOneButton = 1 << 3;
        private const int SpecialTwoButton = 1 << 4;
        private const int MovementButton = 1 << 5;
        private const int ProjectileExecutionType = 1;
        private const int AreaExecutionType = 2;
        private const int PersistentExecutionType = 3;

        public void Update(ref Frame frame) {
            ref readonly FighterMatchComponent match = ref frame.GetReadOnlySingleton<FighterMatchComponent>();
            if (match.MatchState != 1) return;

            var filter = frame.Filter<
                FighterStateComponent,
                FighterRuntimeComponent,
                FighterTuningComponent,
                FighterAbilityModeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);
                ref readonly FighterAbilityModeComponent modes = ref frame.GetReadOnly<FighterAbilityModeComponent>(entity);
                if (fighter.Stocks <= 0
                    || fighter.HitstunFrames > 0
                    || fighter.DazeFrames > 0
                    || FighterUniversalMovementRules.IsCombatLocked(in runtime)) continue;

                if ((runtime.PressedButtons & SpecialOneButton) != 0 && runtime.SpecialOneCooldownFrames <= 0) {
                    if (modes.SpecialOneExecutionType == ProjectileExecutionType) {
                        SpawnProjectile(
                            ref frame, in fighter, 1, tuning.SpecialOneDamage, tuning.SpecialOneKnockback,
                            tuning.SpecialOneStatusType, tuning.SpecialOneStatusFrames,
                            tuning.SpecialOneStatusIntensity, modes.SpecialOneProjectileLifetimeFrames,
                            modes.SpecialOneProjectileSpeed);
                        runtime.SpecialOneCooldownFrames = PositiveCooldown(tuning.SpecialOneCooldownFrames);
                    } else if (modes.SpecialOneExecutionType == PersistentExecutionType) {
                        SpawnPersistent(
                            ref frame, in fighter, modes.SpecialOnePersistentTypeID,
                            modes.SpecialOneMaxActiveObjects, modes.SpecialOnePersistentLifetimeFrames,
                            tuning.SpecialOneDamage, tuning.SpecialOneKnockback,
                            tuning.SpecialOneStatusType, tuning.SpecialOneStatusFrames);
                        runtime.SpecialOneCooldownFrames = PositiveCooldown(tuning.SpecialOneCooldownFrames);
                    } else if (modes.SpecialOneExecutionType == AreaExecutionType) {
                        SpawnZone(
                            ref frame, in fighter, 1,
                            modes.SpecialOneMaxActiveObjects, modes.SpecialOnePersistentLifetimeFrames,
                            modes.SpecialOneTickIntervalFrames, tuning.SpecialOneDamage,
                            tuning.SpecialOneStatusType, tuning.SpecialOneStatusFrames,
                            tuning.SpecialOneStatusIntensity);
                        runtime.SpecialOneCooldownFrames = PositiveCooldown(tuning.SpecialOneCooldownFrames);
                    }
                }

                if ((runtime.PressedButtons & SpecialTwoButton) != 0 && runtime.SpecialTwoCooldownFrames <= 0) {
                    if (modes.SpecialTwoExecutionType == ProjectileExecutionType) {
                        SpawnProjectile(
                            ref frame, in fighter, 2, tuning.SpecialTwoDamage, tuning.SpecialTwoKnockback,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames,
                            tuning.SpecialTwoStatusIntensity, modes.SpecialTwoProjectileLifetimeFrames,
                            modes.SpecialTwoProjectileSpeed);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    } else if (modes.SpecialTwoExecutionType == PersistentExecutionType) {
                        SpawnPersistent(
                            ref frame, in fighter, modes.SpecialTwoPersistentTypeID,
                            modes.SpecialTwoMaxActiveObjects, modes.SpecialTwoPersistentLifetimeFrames,
                            tuning.SpecialTwoDamage, tuning.SpecialTwoKnockback,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    } else if (modes.SpecialTwoExecutionType == AreaExecutionType) {
                        SpawnZone(
                            ref frame, in fighter, 2,
                            modes.SpecialTwoMaxActiveObjects, modes.SpecialTwoPersistentLifetimeFrames,
                            modes.SpecialTwoTickIntervalFrames, tuning.SpecialTwoDamage,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames,
                            tuning.SpecialTwoStatusIntensity);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    }
                }

                if ((runtime.PressedButtons & MovementButton) != 0 && runtime.MovementCooldownFrames <= 0) {
                    ApplyMovement(ref frame, ref fighter, ref runtime, in tuning, in modes);
                }
            }
        }

        private static int PositiveCooldown(int frames) => frames > 0 ? frames : 1;

        private static void SpawnProjectile(
            ref Frame frame,
            in FighterStateComponent owner,
            int projectileTypeID,
            int damage,
            FP64 knockback,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            int lifetimeFrames,
            FP64 speed) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FP64 resolvedSpeed = speed > FP64.Zero ? speed : FP64.FromInt(6);
            EntityRef projectile = frame.CreateEntity();
            frame.Add(projectile, new FighterProjectileComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ProjectileTypeID = owner.CharacterID * 10 + projectileTypeID,
                LifetimeFrames = lifetimeFrames > 0 ? lifetimeFrames : 180,
                Damage = damage,
                AttackClass = FighterDamageRules.SpecialAttackClass,
                StatusType = statusType,
                StatusFrames = statusFrames,
                HitstunFrames = 18,
                StatusIntensity = statusIntensity,
                Position = owner.Position + new FPVector2(FP64.FromInt(facing), FP64.One),
                Velocity = new FPVector2(resolvedSpeed * FP64.FromInt(facing), FP64.Zero),
                HalfExtents = new FPVector2(FP64.FromDouble(0.35), FP64.FromDouble(0.35)),
                Knockback = new FPVector2(knockback, knockback)
            });
        }

        private static void SpawnPersistent(
            ref Frame frame,
            in FighterStateComponent owner,
            int objectTypeID,
            int maxActive,
            int lifetimeFrames,
            int damage,
            FP64 knockback,
            int statusType,
            int statusFrames) {
            if (objectTypeID <= 0) return;
            int deployLimit = maxActive > 0 ? maxActive : 1;
            int activeCount = 0;
            int oldestID = int.MaxValue;
            EntityRef oldest = default;
            bool foundOldest = false;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef existing)) {
                ref readonly FighterPersistentObjectComponent persistent =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(existing);
                if (persistent.OwnerPlayerID != owner.PlayerID || persistent.ObjectTypeID != objectTypeID) continue;
                activeCount++;
                if (persistent.EntityID < oldestID) {
                    oldestID = persistent.EntityID;
                    oldest = existing;
                    foundOldest = true;
                }
            }
            if (activeCount >= deployLimit && foundOldest) frame.DestroyEntity(oldest);

            ResolvePersistentSpec(
                objectTypeID, lifetimeFrames, damage, knockback,
                out int hp, out int resolvedLifetime, out int resolvedDamage,
                out int actionCooldown, out int remainingAttacks,
                out FP64 attackRange, out FP64 resolvedKnockback);

            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            EntityRef created = frame.CreateEntity();
            frame.Add(created, new FighterPersistentObjectComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ObjectTypeID = objectTypeID,
                CurrentHP = hp,
                MaxHP = hp,
                LifetimeFrames = resolvedLifetime,
                ActionCooldownFrames = actionCooldown,
                MaxDeployLimit = deployLimit,
                Damage = resolvedDamage,
                BaseActionCooldownFrames = actionCooldown,
                RemainingAttacks = remainingAttacks,
                StatusType = statusType,
                StatusFrames = statusFrames,
                AttackRange = attackRange,
                Knockback = resolvedKnockback,
                Position = owner.Position,
                HalfExtents = new FPVector2(FP64.FromDouble(0.6), FP64.FromDouble(0.6))
            });
        }

        private static void ResolvePersistentSpec(
            int objectTypeID,
            int requestedLifetime,
            int requestedDamage,
            FP64 requestedKnockback,
            out int hp,
            out int lifetime,
            out int damage,
            out int actionCooldown,
            out int remainingAttacks,
            out FP64 attackRange,
            out FP64 knockback) {
            hp = objectTypeID == 1 ? 25 : objectTypeID == 2 ? 20 : 15;
            lifetime = requestedLifetime > 0
                ? requestedLifetime
                : objectTypeID == 1 ? 1800 : objectTypeID == 2 ? 900 : 600;
            // Type 5 (Mozart's Sonata staff platform) is a harmless marker in the
            // deterministic sim: walkable platform collision is deferred, so it
            // must never attack.
            damage = objectTypeID == 1 || objectTypeID == 2
                ? 5
                : objectTypeID == 5 ? 0
                : requestedDamage > 0 ? requestedDamage : 4;
            actionCooldown = objectTypeID == 4 ? 30 : 120;
            remainingAttacks = objectTypeID == 2 ? 3 : -1;
            // Clockwork Turret (type 2) targets at the design's 30-unit range,
            // bounded by the visible arena (half-width 10 units).
            attackRange = objectTypeID == 4 ? FP64.FromInt(2)
                : objectTypeID == 2 ? FP64.FromInt(10)
                : FP64.FromInt(5);
            knockback = requestedKnockback > FP64.Zero ? requestedKnockback : FP64.FromInt(2);
        }

        private static void SpawnZone(
            ref Frame frame,
            in FighterStateComponent owner,
            int specialSlot,
            int maxActive,
            int lifetimeFrames,
            int tickIntervalFrames,
            int damage,
            int statusType,
            int statusFrames,
            FP64 statusIntensity) {
            int zoneTypeID = owner.CharacterID * 10 + specialSlot;
            int deployLimit = maxActive > 0 ? maxActive : 1;
            int activeCount = 0;
            int oldestID = int.MaxValue;
            EntityRef oldest = default;
            bool foundOldest = false;
            var filter = frame.Filter<FighterZoneComponent>();
            while (filter.Next(out EntityRef existing)) {
                ref readonly FighterZoneComponent zone = ref frame.GetReadOnly<FighterZoneComponent>(existing);
                if (zone.OwnerPlayerID != owner.PlayerID || zone.ZoneTypeID != zoneTypeID) continue;
                activeCount++;
                if (zone.EntityID < oldestID) {
                    oldestID = zone.EntityID;
                    oldest = existing;
                    foundOldest = true;
                }
            }
            if (activeCount >= deployLimit && foundOldest) frame.DestroyEntity(oldest);

            ResolveZoneSpec(zoneTypeID, out FPVector2 halfExtents, out int grantsOwnerSpeedBonus, out bool centersOnOwner);
            int resolvedTick = tickIntervalFrames > 0 ? tickIntervalFrames : 30;
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FPVector2 zonePosition = centersOnOwner
                ? owner.Position
                : owner.Position + new FPVector2(FP64.FromInt(facing * 2), FP64.Zero);
            EntityRef created = frame.CreateEntity();
            frame.Add(created, new FighterZoneComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ZoneTypeID = zoneTypeID,
                LifetimeFrames = lifetimeFrames > 0 ? lifetimeFrames : 300,
                TickIntervalFrames = resolvedTick,
                TickFramesRemaining = 1,
                Damage = damage,
                StatusType = statusType,
                StatusFrames = statusFrames,
                GrantsOwnerSpeedBonus = grantsOwnerSpeedBonus,
                StatusIntensity = statusIntensity,
                Position = zonePosition,
                HalfExtents = halfExtents
            });
        }

        /// <summary>
        /// Per-zone-identity deterministic tuning. Einstein's Relativity Rift
        /// (zone type 2) is wide and buffs the owner's movement while inside.
        /// Tesla's Lorentz Pulse is a radial burst centered on Tesla himself.
        /// Lincoln's Emancipator (zone type 31) is a wide, low forward ground wave.
        /// Cleopatra's Sandstorm Vortex is a forward zone whose pull runs in
        /// FighterZoneSystem.
        /// </summary>
        private static void ResolveZoneSpec(
            int zoneTypeID,
            out FPVector2 halfExtents,
            out int grantsOwnerSpeedBonus,
            out bool centersOnOwner) {
            if (zoneTypeID == (int)FighterCharacterID.Einstein * 10 + 2) {
                halfExtents = new FPVector2(FP64.FromDouble(2.0), FP64.FromDouble(1.5));
                grantsOwnerSpeedBonus = 1;
                centersOnOwner = false;
                return;
            }
            if (zoneTypeID == (int)FighterCharacterID.Tesla * 10 + 2) {
                halfExtents = new FPVector2(FP64.FromDouble(2.0), FP64.FromDouble(1.5));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            if (zoneTypeID == (int)FighterCharacterID.Lincoln * 10 + 1) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(0.75));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            if (zoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + 2) {
                halfExtents = new FPVector2(FP64.FromDouble(1.5), FP64.One);
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Leonardo's Golden Ratio (zone type 21): the expanding spiral is
            // approximated by its final footprint (Story max radius 120 px =
            // 2 units), centered on the cast point; the zone system applies a
            // radial knockback pulse when it expires.
            if (zoneTypeID == (int)FighterCharacterID.Leonardo * 10 + 1) {
                halfExtents = new FPVector2(FP64.FromDouble(2.0), FP64.FromDouble(1.5));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            halfExtents = new FPVector2(FP64.FromDouble(1.5), FP64.One);
            grantsOwnerSpeedBonus = 0;
            centersOnOwner = false;
        }

        private static void ApplyMovement(
            ref Frame frame,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            in FighterAbilityModeComponent modes) {
            runtime.MovementCooldownFrames = PositiveCooldown(modes.MovementCooldownFrames);
            int facing = fighter.FacingRight != 0 ? 1 : -1;
            FP64 distance = modes.MovementDistance > FP64.Zero ? modes.MovementDistance : FP64.FromInt(2);
            FP64 speed = modes.MovementSpeed > FP64.Zero ? modes.MovementSpeed : FP64.FromInt(8);

            if (modes.MovementType == 1 || modes.MovementType == 5) {
                fighter.Velocity.y = speed / FP64.FromInt(2);
                fighter.IsGrounded = 0;
                // Glide (MovementType 1, e.g. Leonardo's Ornithopter Flight): the
                // boost cancels into a reduced-gravity float for the authored
                // duration (design: up to 3 s = 180 frames). FloatFrames is already
                // a snapshotted FighterRuntimeComponent field, so this stays
                // rollback-safe.
                if (modes.MovementType == 1) {
                    runtime.FloatFrames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 180;
                }
            } else if (modes.MovementType == 2) {
                fighter.Velocity.x = speed * FP64.FromInt(facing);
            } else {
                // Blink/Teleport/Warp travel in the held input direction (world Y is
                // up, so a negative MoveY stick value means an upward warp).
                int directionX = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
                int directionY = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
                if (directionX == 0 && directionY == 0) directionX = facing;
                fighter.Position.x += distance * FP64.FromInt(directionX);
                fighter.Position.x = FP64.Clamp(fighter.Position.x, FP64.FromInt(-10), FP64.FromInt(10));
                if (directionY != 0) {
                    fighter.Position.y += distance * FP64.FromInt(directionY);
                    if (fighter.Position.y < FP64.Zero) fighter.Position.y = FP64.Zero;
                    if (directionY > 0) fighter.IsGrounded = 0;
                }
                // Warp cancels into a brief float glide (reduced gravity).
                if (modes.MovementType == 4) runtime.FloatFrames = 60;
            }

            if (modes.MovementResetsJump != 0) fighter.RemainingJumps = tuning.MaxJumpCount;
            if (modes.MovementGrantsHyperArmor != 0) {
                fighter.HyperArmorFrames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 12;
            }
            if (modes.MovementPersistentTypeID > 0) {
                SpawnPersistent(
                    ref frame, in fighter, modes.MovementPersistentTypeID,
                    modes.MovementMaxActiveObjects, modes.MovementPersistentLifetimeFrames,
                    0, FP64.Zero, (int)StatusType.None, 0);
            }
        }
    }

    public sealed class FighterProjectileSystem : ISystem {
        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(60);
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        public void Update(ref Frame frame) {
            var filter = frame.Filter<FighterProjectileComponent>();
            while (filter.Next(out EntityRef projectileEntity)) {
                ref FighterProjectileComponent projectile = ref frame.Get<FighterProjectileComponent>(projectileEntity);
                projectile.LifetimeFrames--;
                projectile.Position += projectile.Velocity * FixedDelta;
                if (projectile.LifetimeFrames <= 0 || FP64.Abs(projectile.Position.x) > FP64.FromInt(12)) {
                    frame.DestroyEntity(projectileEntity);
                    continue;
                }

                int targetPlayerID = projectile.OwnerPlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, projectile.OwnerPlayerID, out EntityRef ownerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;

                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (!FighterEntityQueries.Overlaps(
                        in projectile.Position, in projectile.HalfExtents,
                        in target.Position, in FighterHalfExtents)) continue;

                ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                FighterDamageRules.ApplyFighterHit(
                    ref owner, ref ownerRuntime, ref target, ref targetRuntime, in targetTuning,
                    projectile.AttackClass, projectile.Damage, projectile.Knockback.x,
                    projectile.HitstunFrames, projectile.StatusType, projectile.StatusFrames,
                    projectile.StatusIntensity, projectile.Position.x);
                frame.DestroyEntity(projectileEntity);
            }
        }
    }

    public sealed class FighterPersistentObjectSystem : ISystem {
        // Tesla Coil alternating-current link (design Section 4): two active coils
        // within 8 units connect into a fence dealing 8 basic damage per 0.5 s
        // tick and applying a brief StaticCharge to the fighter caught between
        // them. The lower-EntityID coil of the pair drives the tick.
        private const int CoilObjectTypeID = 1;
        private const int FenceTickFrames = 30;
        private const int FenceDamage = 8;
        private const int FenceHitstunFrames = 8;
        private const int FenceStaticChargeFrames = 30;
        // Cleopatra's Serpent Nest bite delivers the design's brief Root (1 s) as
        // hitstun so the Venom status carried on the bite is not immediately
        // replaced (single-status rule: the newest status replaces the previous).
        private const int NestObjectTypeID = 3;
        private const int NestBiteHitstunFrames = 60;
        private static readonly FP64 CoilLinkRangeSquared = FP64.FromInt(64);
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        public void Update(ref Frame frame) {
            UpdateCoilLinks(ref frame);
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef persistentEntity)) {
                ref FighterPersistentObjectComponent persistent =
                    ref frame.Get<FighterPersistentObjectComponent>(persistentEntity);
                persistent.LifetimeFrames--;
                if (persistent.CurrentHP <= 0 || persistent.LifetimeFrames <= 0 || persistent.RemainingAttacks == 0) {
                    frame.DestroyEntity(persistentEntity);
                    continue;
                }
                if (persistent.Damage <= 0) continue;
                if (persistent.ActionCooldownFrames > 0) {
                    persistent.ActionCooldownFrames--;
                    continue;
                }

                int targetPlayerID = persistent.OwnerPlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, persistent.OwnerPlayerID, out EntityRef ownerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (FP64.Abs(target.Position.x - persistent.Position.x) > persistent.AttackRange) continue;

                ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                FighterDamageRules.ApplyFighterHit(
                    ref owner, ref ownerRuntime, ref target, ref targetRuntime, in targetTuning,
                    FighterDamageRules.BasicAttackClass, persistent.Damage, persistent.Knockback,
                    persistent.ObjectTypeID == NestObjectTypeID ? NestBiteHitstunFrames : 10,
                    persistent.StatusType, persistent.StatusFrames, FP64.One, persistent.Position.x);
                persistent.ActionCooldownFrames = persistent.BaseActionCooldownFrames;
                if (persistent.RemainingAttacks > 0) persistent.RemainingAttacks--;
            }
        }

        private static void UpdateCoilLinks(ref Frame frame) {
            for (int ownerID = 0; ownerID <= 1; ownerID++) {
                // Deploy limit for coils is 2, so tracking the two lowest-ID live
                // coils fully describes the possible link.
                EntityRef firstCoil = default, secondCoil = default;
                int firstID = int.MaxValue, secondID = int.MaxValue;
                int found = 0;
                var filter = frame.Filter<FighterPersistentObjectComponent>();
                while (filter.Next(out EntityRef entity)) {
                    ref readonly FighterPersistentObjectComponent coil =
                        ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                    if (coil.OwnerPlayerID != ownerID
                        || coil.ObjectTypeID != CoilObjectTypeID
                        || coil.CurrentHP <= 0
                        || coil.LifetimeFrames <= 0) continue;
                    found++;
                    if (coil.EntityID < firstID) {
                        secondID = firstID; secondCoil = firstCoil;
                        firstID = coil.EntityID; firstCoil = entity;
                    } else if (coil.EntityID < secondID) {
                        secondID = coil.EntityID; secondCoil = entity;
                    }
                }
                if (found < 2) continue;

                FPVector2 firstPosition = frame.GetReadOnly<FighterPersistentObjectComponent>(firstCoil).Position;
                FPVector2 secondPosition = frame.GetReadOnly<FighterPersistentObjectComponent>(secondCoil).Position;
                FP64 dx = firstPosition.x - secondPosition.x;
                FP64 dy = firstPosition.y - secondPosition.y;
                if (dx * dx + dy * dy > CoilLinkRangeSquared) continue;

                ref FighterPersistentObjectComponent driver = ref frame.Get<FighterPersistentObjectComponent>(firstCoil);
                if (driver.LinkTickFramesRemaining > 0) {
                    driver.LinkTickFramesRemaining--;
                    continue;
                }
                driver.LinkTickFramesRemaining = FenceTickFrames;

                FPVector2 fenceCenter = new(
                    (firstPosition.x + secondPosition.x) / FP64.FromInt(2),
                    (firstPosition.y + secondPosition.y) / FP64.FromInt(2));
                FPVector2 fenceHalfExtents = new(
                    FP64.Abs(dx) / FP64.FromInt(2) + FP64.FromDouble(0.3),
                    FP64.Max(FP64.Abs(dy) / FP64.FromInt(2), FP64.One));

                int targetPlayerID = ownerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, ownerID, out EntityRef ownerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (!FighterEntityQueries.Overlaps(
                        in fenceCenter, in fenceHalfExtents,
                        in target.Position, in FighterHalfExtents)) continue;

                ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                FighterDamageRules.ApplyFighterHit(
                    ref owner, ref ownerRuntime, ref target, ref targetRuntime, in targetTuning,
                    FighterDamageRules.BasicAttackClass, FenceDamage, FP64.One, FenceHitstunFrames,
                    (int)StatusType.StaticCharge, FenceStaticChargeFrames, FP64.FromDouble(0.5),
                    fenceCenter.x);
            }
        }
    }

    public sealed class FighterHazardSystem : ISystem {
        private const int WarningFrames = 90;
        private const int ActiveFrames = 360;
        private const int DamageTickFrames = 30;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        private readonly FighterStageGeometry _geometry;

        public FighterHazardSystem(FighterStageGeometry geometry = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1 || match.HazardsEnabled == 0 || match.HazardFrequency <= 0) return;

            if (match.NextHazardSpawnFrames > 0) match.NextHazardSpawnFrames--;
            if (match.NextHazardSpawnFrames <= 0) {
                SpawnHazard(ref frame, ref match);
                match.NextHazardSpawnFrames = FighterSpawnIntervals.HazardFrames(match.HazardFrequency);
            }

            var filter = frame.Filter<FighterHazardComponent>();
            while (filter.Next(out EntityRef hazardEntity)) {
                ref FighterHazardComponent hazard = ref frame.Get<FighterHazardComponent>(hazardEntity);
                hazard.PhaseFramesRemaining--;
                if (hazard.Phase == 0) {
                    if (hazard.PhaseFramesRemaining <= 0) {
                        hazard.Phase = 1;
                        hazard.PhaseFramesRemaining = hazard.ActiveFrames;
                        hazard.TickFramesRemaining = 1;
                    }
                    continue;
                }

                hazard.TickFramesRemaining--;
                if (hazard.TickFramesRemaining <= 0) {
                    ApplyHazardTick(ref frame, in hazard);
                    hazard.TickFramesRemaining = DamageTickFrames;
                }
                if (hazard.PhaseFramesRemaining <= 0) frame.DestroyEntity(hazardEntity);
            }
        }

        private void SpawnHazard(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            // Authored stages pick a deterministic anchor; the legacy arena keeps
            // the historical random spawn range.
            FP64 positionX = _geometry.HazardAnchorXs.Length > 0
                ? _geometry.HazardAnchorXs[random.NextInt(0, _geometry.HazardAnchorXs.Length)]
                : random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7));
            int hazardType = match.StageHazardTypeID >= 1 && match.StageHazardTypeID <= 10
                ? match.StageHazardTypeID
                : 1;
            int baseDamage = random.NextIntInclusive(5, 10);
            int damage = hazardType switch {
                4 => 0,
                5 => 8,
                6 => 10,
                7 => 2,
                8 => 8,
                9 => 5,
                10 => 12,
                _ => baseDamage
            };
            int knockback = hazardType switch {
                6 => 5,
                10 => 6,
                7 => 1,
                _ => 3
            };
            int halfWidth = hazardType == 10 ? 3 : hazardType == 2 || hazardType == 4 ? 1 : 2;
            (match.RandomState0, match.RandomState1) = random.GetFullState();

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new FighterHazardComponent {
                EntityID = match.NextEntityID++,
                HazardTypeID = hazardType,
                Phase = 0,
                PhaseFramesRemaining = WarningFrames,
                Damage = damage,
                TickFramesRemaining = DamageTickFrames,
                WarningFrames = WarningFrames,
                ActiveFrames = ActiveFrames,
                CooldownFrames = FighterSpawnIntervals.HazardFrames(match.HazardFrequency),
                Position = new FPVector2(positionX, FP64.Zero),
                HalfExtents = new FPVector2(FP64.FromInt(halfWidth), FP64.FromInt(2)),
                Knockback = new FPVector2(FP64.FromInt(knockback), FP64.FromInt(knockback))
            });
        }

        private static void ApplyHazardTick(ref Frame frame, in FighterHazardComponent hazard) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                if (!FighterEntityQueries.Overlaps(
                        in hazard.Position, in hazard.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                if (hazard.HazardTypeID == 4) {
                    fighter.Influence = FP64.Max(FP64.Zero, fighter.Influence - FP64.FromInt(5));
                    continue;
                }
                int statusType = hazard.HazardTypeID switch {
                    3 => (int)StatusType.StaticCharge,
                    5 => (int)StatusType.TimeDilation,
                    7 => (int)StatusType.TimeDilation,
                    _ => (int)StatusType.None
                };
                int statusFrames = hazard.HazardTypeID switch {
                    3 => 30,
                    5 => 120,
                    7 => 60,
                    _ => 0
                };
                FP64 statusIntensity = hazard.HazardTypeID == 7
                    ? FP64.One
                    : FP64.FromDouble(0.5);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, in tuning,
                    hazard.Damage, hazard.Knockback.x, 10, hazard.Position.x,
                    statusType, statusFrames, statusIntensity);
            }
        }
    }

    public sealed class FighterOrbSystem : ISystem {
        private const int OrbLifetimeFrames = 900;
        private const int BuffDurationFrames = 480;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        private readonly FighterStageGeometry _geometry;

        public FighterOrbSystem(FighterStageGeometry geometry = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1 || match.ItemsEnabled == 0 || match.ItemFrequency <= 0) return;

            if (match.NextOrbSpawnFrames > 0) match.NextOrbSpawnFrames--;
            if (match.NextOrbSpawnFrames <= 0) {
                SpawnOrb(ref frame, ref match);
                match.NextOrbSpawnFrames = FighterSpawnIntervals.OrbFrames(match.ItemFrequency);
            }

            var filter = frame.Filter<FighterOrbComponent>();
            while (filter.Next(out EntityRef orbEntity)) {
                ref FighterOrbComponent orb = ref frame.Get<FighterOrbComponent>(orbEntity);
                orb.LifetimeFrames--;
                if (orb.LifetimeFrames <= 0) {
                    frame.DestroyEntity(orbEntity);
                    continue;
                }

                int picker = FindPicker(ref frame, in orb);
                if (picker < 0 || !FighterEntityQueries.TryFindFighter(ref frame, picker, out EntityRef fighterEntity)) continue;
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ApplyEffect(ref fighter, ref runtime, orb.EffectType);
                frame.DestroyEntity(orbEntity);
            }
        }

        private void SpawnOrb(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            int effect = random.NextInt(0, 4);
            // Authored stages pick a deterministic anchor; the legacy arena keeps
            // the historical random spawn range.
            FPVector2 position = _geometry.OrbAnchors.Length > 0
                ? _geometry.OrbAnchors[random.NextInt(0, _geometry.OrbAnchors.Length)]
                : new FPVector2(
                    random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7)),
                    FP64.FromDouble(0.5));
            (match.RandomState0, match.RandomState1) = random.GetFullState();

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new FighterOrbComponent {
                EntityID = match.NextEntityID++,
                EffectType = effect,
                LifetimeFrames = OrbLifetimeFrames,
                Position = position,
                HalfExtents = new FPVector2(FP64.FromDouble(0.45), FP64.FromDouble(0.45))
            });
        }

        private static int FindPicker(ref Frame frame, in FighterOrbComponent orb) {
            bool playerOne = false;
            bool playerTwo = false;
            var filter = frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (!FighterEntityQueries.Overlaps(
                        in orb.Position, in orb.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;
                if (fighter.PlayerID == 0) playerOne = true;
                else if (fighter.PlayerID == 1) playerTwo = true;
            }
            if (playerOne && playerTwo) return orb.EntityID % 2;
            if (playerOne) return 0;
            if (playerTwo) return 1;
            return -1;
        }

        private static void ApplyEffect(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            int effectType) {
            if (effectType == 0) {
                int heal = fighter.MaxHP * 20 / 100;
                int healed = fighter.CurrentHP + heal;
                fighter.CurrentHP = healed < fighter.MaxHP ? healed : fighter.MaxHP;
            } else if (effectType == 1) {
                runtime.SpeedBuffFrames = BuffDurationFrames;
            } else if (effectType == 2) {
                runtime.JumpBuffFrames = BuffDurationFrames;
            } else {
                runtime.AegisHits = 1;
            }
        }
    }

    /// <summary>
    /// Deterministic Area-execution effects. Zones periodically pulse damage and/or
    /// status onto the opponent standing inside them and can grant the owner a
    /// movement-speed bonus while the owner overlaps (Einstein's Relativity Rift).
    /// Pulses carry no knockback or hitstun, so they never interrupt movement.
    /// </summary>
    public sealed class FighterZoneSystem : ISystem {
        // Refreshed every frame the owner overlaps; decays one frame at a time in
        // TickCounters, so the bonus expires immediately after leaving the zone.
        private const int OwnerBonusRefreshFrames = 2;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        public void Update(ref Frame frame) {
            var filter = frame.Filter<FighterZoneComponent>();
            while (filter.Next(out EntityRef zoneEntity)) {
                ref FighterZoneComponent zone = ref frame.Get<FighterZoneComponent>(zoneEntity);
                zone.LifetimeFrames--;
                if (zone.LifetimeFrames <= 0) {
                    // Leonardo's Golden Ratio (zone type 21) ends with a radial
                    // knockback pulse shoving the fighter away from the spiral
                    // center (damage-free; the 3 damage ticks land beforehand).
                    if (zone.ZoneTypeID == (int)FighterCharacterID.Leonardo * 10 + 1) {
                        ApplySpiralExpiryKnockback(ref frame, in zone);
                    }
                    frame.DestroyEntity(zoneEntity);
                    continue;
                }

                if (zone.GrantsOwnerSpeedBonus != 0
                    && FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef ownerEntity)) {
                    ref readonly FighterStateComponent owner = ref frame.GetReadOnly<FighterStateComponent>(ownerEntity);
                    if (FighterEntityQueries.Overlaps(
                            in zone.Position, in zone.HalfExtents,
                            in owner.Position, in FighterHalfExtents)) {
                        ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                        ownerRuntime.ZoneSpeedBonusFrames = OwnerBonusRefreshFrames;
                    }
                }

                // Cleopatra's Sandstorm Vortex drags the opponent toward its
                // center every frame: a small positional shift that never touches
                // velocity, so the pull stays impulse-free and snapshot-safe with
                // no extra state.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + 2) {
                    ApplyVortexPull(ref frame, in zone);
                }

                if (zone.TickFramesRemaining > 0) zone.TickFramesRemaining--;
                if (zone.TickFramesRemaining > 0) continue;
                zone.TickFramesRemaining = zone.TickIntervalFrames;

                int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef attackerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (!FighterEntityQueries.Overlaps(
                        in zone.Position, in zone.HalfExtents,
                        in target.Position, in FighterHalfExtents)) continue;

                ref FighterStateComponent attacker = ref frame.Get<FighterStateComponent>(attackerEntity);
                ref FighterRuntimeComponent attackerRuntime = ref frame.Get<FighterRuntimeComponent>(attackerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);

                // Tesla's Lorentz Pulse chains lightning through active coils when
                // the target is primed with StaticCharge; the check runs before the
                // pulse applies its own status (newest status replaces previous).
                int pulseDamage = zone.Damage;
                if (zone.ZoneTypeID == (int)FighterCharacterID.Tesla * 10 + 2
                    && targetRuntime.StatusType == (int)StatusType.StaticCharge) {
                    pulseDamage += CoilArcDamage * CountLiveCoils(ref frame, zone.OwnerPlayerID);
                }

                // Lincoln's Emancipator (zone type 31) is the one zone whose pulse
                // carries real impulse: the ground wave knocks the target up using
                // the attacker's authored Special 1 knockback plus hitstun. Every
                // other zone stays an impulse-free tick by design.
                FP64 pulseKnockback = FP64.Zero;
                int pulseHitstunFrames = 0;
                if (zone.ZoneTypeID == (int)FighterCharacterID.Lincoln * 10 + 1) {
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).SpecialOneKnockback;
                    pulseHitstunFrames = EmancipatorHitstunFrames;
                }

                FighterDamageRules.ApplyFighterHit(
                    ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in targetTuning,
                    FighterDamageRules.SpecialAttackClass, pulseDamage, pulseKnockback, pulseHitstunFrames,
                    zone.StatusType, zone.StatusFrames, zone.StatusIntensity, zone.Position.x);
            }
        }

        private const int CoilArcDamage = 5;
        private const int CoilObjectTypeID = 1;
        private const int EmancipatorHitstunFrames = 18;

        // 0.05 world units per frame (3 px at 60 px/unit), mirrored by the Story
        // vortex's 180 px/s positional drag.
        private static readonly FP64 VortexPullPerFrame = FP64.FromDouble(0.05);

        private static void ApplyVortexPull(ref Frame frame, in FighterZoneComponent zone) {
            int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.InvulnerabilityFrames > 0) return;
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterHalfExtents)) return;
            FP64 dx = zone.Position.x - target.Position.x;
            if (dx > VortexPullPerFrame) target.Position.x += VortexPullPerFrame;
            else if (dx < -VortexPullPerFrame) target.Position.x -= VortexPullPerFrame;
            else target.Position.x = zone.Position.x;
        }

        private const int SpiralExpiryHitstunFrames = 12;
        private static readonly FP64 SpiralExpiryKnockback = FP64.FromInt(3);

        /// <summary>
        /// Golden Ratio expiry pulse: a zero-damage knockback hit pushing the
        /// opponent away from the spiral center (ApplyFighterHit resolves the push
        /// direction from the hit origin, giving the design's radial shove).
        /// </summary>
        private static void ApplySpiralExpiryKnockback(ref Frame frame, in FighterZoneComponent zone) {
            int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef attackerEntity)
                || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterHalfExtents)) return;

            ref FighterStateComponent attacker = ref frame.Get<FighterStateComponent>(attackerEntity);
            ref FighterRuntimeComponent attackerRuntime = ref frame.Get<FighterRuntimeComponent>(attackerEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            FighterDamageRules.ApplyFighterHit(
                ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in targetTuning,
                FighterDamageRules.SpecialAttackClass, 0, SpiralExpiryKnockback, SpiralExpiryHitstunFrames,
                (int)StatusType.None, 0, FP64.One, zone.Position.x);
        }

        private static int CountLiveCoils(ref Frame frame, int ownerPlayerID) {
            int count = 0;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterPersistentObjectComponent persistent =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                if (persistent.OwnerPlayerID == ownerPlayerID
                    && persistent.ObjectTypeID == CoilObjectTypeID
                    && persistent.CurrentHP > 0
                    && persistent.LifetimeFrames > 0) count++;
            }
            return count;
        }
    }
}
