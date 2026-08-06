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

            bool targetBlocking = targetRuntime.UniversalMovementState == (int)UniversalMovementPhase.None
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

            FighterUniversalMovementRules.Cancel(ref targetRuntime);

			if (target.HyperArmorFrames <= 0 || attackClass == UltimateAttackClass) {
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
            damage = objectTypeID == 1 || objectTypeID == 2
                ? 5
                : requestedDamage > 0 ? requestedDamage : 4;
            actionCooldown = objectTypeID == 4 ? 30 : 120;
            remainingAttacks = objectTypeID == 2 ? 3 : -1;
            attackRange = objectTypeID == 4 ? FP64.FromInt(2) : FP64.FromInt(5);
            knockback = requestedKnockback > FP64.Zero ? requestedKnockback : FP64.FromInt(2);
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
            } else if (modes.MovementType == 2) {
                fighter.Velocity.x = speed * FP64.FromInt(facing);
            } else {
                fighter.Position.x += distance * FP64.FromInt(facing);
                fighter.Position.x = FP64.Clamp(fighter.Position.x, FP64.FromInt(-10), FP64.FromInt(10));
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
        public void Update(ref Frame frame) {
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
                    FighterDamageRules.BasicAttackClass, persistent.Damage, persistent.Knockback, 10,
                    persistent.StatusType, persistent.StatusFrames, FP64.One, persistent.Position.x);
                persistent.ActionCooldownFrames = persistent.BaseActionCooldownFrames;
                if (persistent.RemainingAttacks > 0) persistent.RemainingAttacks--;
            }
        }
    }

    public sealed class FighterHazardSystem : ISystem {
        private const int WarningFrames = 90;
        private const int ActiveFrames = 360;
        private const int DamageTickFrames = 30;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

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

        private static void SpawnHazard(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            FP64 positionX = random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7));
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

        private static void SpawnOrb(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            int effect = random.NextInt(0, 4);
            FP64 positionX = random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7));
            (match.RandomState0, match.RandomState1) = random.GetFullState();

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new FighterOrbComponent {
                EntityID = match.NextEntityID++,
                EffectType = effect,
                LifetimeFrames = OrbLifetimeFrames,
                Position = new FPVector2(positionX, FP64.FromDouble(0.5)),
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
}
