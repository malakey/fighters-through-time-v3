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
        /// <summary>A blocked hazard tick costs exactly one shield charge (design §10).</summary>
        public const int HazardBlockChargeCost = 1;
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
            bool creditInfluence = true,
            int blockChargeCost = 0,
            FP64 verticalKnockbackScale = default) {
            if (target.InvulnerabilityFrames > 0 || target.Stocks <= 0) return false;

            if (targetRuntime.AegisHits > 0) {
                targetRuntime.AegisHits--;
                return false;
            }

            // Pure tick/status pulses (zone effects) carry no impulse: they bypass
            // the front-facing shield and must not interrupt movement or zero the
            // target's velocity.
            bool carriesImpulse = knockback > FP64.Zero || hitstunFrames > 0;
            // The absorb requires the real grounded stance (mirrors Story's
            // Blocking state): no blocking while airborne, mid-swing, mid
            // roll, or inside hitstun/daze.
            bool targetBlocking = carriesImpulse
                && FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime);
            bool hitInFront = target.FacingRight != 0
                ? hitOriginX >= target.Position.x
                : hitOriginX <= target.Position.x;
            if (targetBlocking && hitInFront && attackClass != UltimateAttackClass && target.BlockCharges > 0) {
                // blockChargeCost > 0 overrides the class default (shield-shredding
                // specials like Joan's Divine Piercing deplete exactly 2 charges
                // instead of the special-class full shatter).
                int cost = blockChargeCost > 0
                    ? blockChargeCost
                    : attackClass == SpecialAttackClass ? target.BlockCharges : 1;
                target.BlockCharges -= cost;
                // A spent charge re-arms the regeneration interval, as Story does.
                targetRuntime.BlockRegenFrames = FTT.Combat.BasicComboRules.BlockChargeRegenFrames;
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
                // Gameplay-feel plan §2.5 — low-health knockback scaling. The
                // impulse is multiplied by 1 + the victim's missing-HP fraction
                // measured *after* this hit's damage, a linear 1x at full HP to
                // 2x at 0 HP. This is the one chokepoint every Fighter-side
                // source funnels through (basics, specials, ultimates,
                // projectiles, zones, constructs, and — via
                // ApplyEnvironmentHit — stage hazards), so the rule needs no
                // second copy. Story mirrors it in DamageCalculator.
                FP64 force = ScaleByMissingHP(knockback, in target) / (FP64.One + target.Weight);
                // Launcher-class impulses (Nassau's mortar) bias the impulse
                // upward; everything else keeps the symmetric 1:1 pulse.
                FP64 verticalScale = verticalKnockbackScale > FP64.Zero ? verticalKnockbackScale : FP64.One;
                target.Velocity.x = hitOriginX <= target.Position.x ? force : -force;
                target.Velocity.y = force * verticalScale;
                target.IsGrounded = 0;
                target.HitstunFrames = hitstunFrames;
            }

            ApplyStatus(ref target, ref targetRuntime, statusType, statusFrames, statusIntensity);
            // Being hit into hitstun cancels the victim's swing and resets their
            // chain in both modes (design 752). Hyper-armored hits carry no
            // hitstun and leave the string running, matching Story.
            if (target.HitstunFrames > 0) {
                FighterBasicAttackRules.CancelString(ref targetRuntime);
            }
            if (target.CurrentHP <= 0) {
                FighterSimulationRules.ApplyStockLoss(ref target, ref targetRuntime, in targetTuning);
            }
            return true;
        }

        /// <summary>
        /// The fixed-point half of <c>BasicComboRules.LowHealthKnockbackScale</c>:
        /// <c>knockback * (2 * MaxHP - CurrentHP) / MaxHP</c>. The ratio is
        /// formed first so the intermediate never leaves the [1, 2] band, and
        /// the whole computation stays in FP64 — no float math on the
        /// deterministic path.
        /// </summary>
        internal static FP64 ScaleByMissingHP(FP64 knockback, in FighterStateComponent target) {
            int maxHP = target.MaxHP;
            if (maxHP <= 0) return knockback;
            int currentHP = target.CurrentHP < 0 ? 0 : target.CurrentHP > maxHP ? maxHP : target.CurrentHP;
            FP64 scale = FP64.FromInt(2 * maxHP - currentHP) / FP64.FromInt(maxHP);
            return knockback * scale;
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

        /// <summary>
        /// Environment/hazard damage. Per `design-godot.md` §10 ("Block
        /// Compatibility"), a hazard tick is treated as a basic attack: an active
        /// front-facing block absorbs it for exactly one block charge, which is
        /// why <c>blockChargeCost</c> is pinned to 1 here rather than left to the
        /// attack-class default.
        /// </summary>
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
            FP64 statusIntensity,
            FP64 verticalKnockbackScale = default) {
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
                false,
                HazardBlockChargeCost,
                verticalKnockbackScale);
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

                // Story's Blocking state ignores ability inputs entirely; specials
                // remain usable mid-swing because they cancel the basic string
                // (design 1051 — resolved below via the cooldown edge).
                bool blockStance = FighterBasicAttackRules.IsBlockStance(in fighter, in runtime);
                if (blockStance) continue;
                int specialOneCooldownBefore = runtime.SpecialOneCooldownFrames;
                int specialTwoCooldownBefore = runtime.SpecialTwoCooldownFrames;

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
                            ref frame, ref fighter, 1,
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
                            ref frame, ref fighter, 2,
                            modes.SpecialTwoMaxActiveObjects, modes.SpecialTwoPersistentLifetimeFrames,
                            modes.SpecialTwoTickIntervalFrames, tuning.SpecialTwoDamage,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames,
                            tuning.SpecialTwoStatusIntensity);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    }
                }

                if (runtime.SpecialOneCooldownFrames != specialOneCooldownBefore
                    || runtime.SpecialTwoCooldownFrames != specialTwoCooldownBefore) {
                    // An executed special cancels an in-progress basic and resets
                    // the chain, matching Story and the melee-intent path.
                    FighterBasicAttackRules.CancelString(ref runtime);
                }

                // Story never polls the movement ability during a swing.
                if (runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                    && (runtime.PressedButtons & MovementButton) != 0
                    && runtime.MovementCooldownFrames <= 0) {
                    ApplyMovement(ref frame, ref fighter, ref runtime, in tuning, in modes);
                }
            }
        }

        private static int PositiveCooldown(int frames) => frames > 0 ? frames : 1;

        internal static void SpawnProjectile(
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

        internal static void SpawnPersistent(
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

        private static readonly FP64 TempestLiftSpeed = FP64.FromInt(10);

        internal static void SpawnZone(
            ref Frame frame,
            ref FighterStateComponent owner,
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
            // Shakespeare's Tempest (design Section 5) lifts the caster into the
            // air as the storm spawns; the storm itself only shoves the opponent
            // (see FighterZoneSystem's per-type pulse impulse).
            if (zoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + 2) {
                owner.Velocity.y = TempestLiftSpeed;
                owner.IsGrounded = 0;
            }
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
        /// Shakespeare's Tempest is a wind storm centered on the caster.
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
            if (zoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + 2) {
                halfExtents = new FPVector2(FP64.FromDouble(2.0), FP64.FromDouble(1.5));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Joan's Grand Crusade (ultimate-slot zone type 13): the cavalry
            // charge lane — wide and forward-offset so the stampede tramples
            // everything ahead of Joan, well beyond melee range.
            if (zoneTypeID == (int)FighterCharacterID.Joan * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(3.5), FP64.One);
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Einstein's Cosmological Constant (ultimate zone type 3): the
            // screen-clearing micro black hole is a wide singularity centered on
            // the caster; FighterZoneSystem pulls the opponent toward it and
            // fires the final launch when it expires.
            if (zoneTypeID == (int)FighterCharacterID.Einstein * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(6), FP64.FromInt(3));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Cleopatra's Wrath of the Nile (zone type 43): the ultimate
            // sandstorm engulfs the whole arena (half-width 10 units) around
            // Cleopatra, tall enough to catch airborne opponents.
            if (zoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(10), FP64.FromInt(4));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Leonardo's Vitruvian Matrix (ultimate zone type 23): the thrown
            // trap sphere resolves as a wide circle 2 units ahead of the caster
            // (radius 2.5 units = the Story matrix's 150 px), so the trap-and-
            // bombard ultimate lands well beyond melee range.
            if (zoneTypeID == (int)FighterCharacterID.Leonardo * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(2.0));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Lincoln's Union Indestructible (ultimate-slot zone type 33): the
            // split-rail fence pen raised in front of Lincoln — wide and tall
            // enough to hold the trapped opponent through the smash sequence.
            if (zoneTypeID == (int)FighterCharacterID.Lincoln * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(1.25));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Tesla's Wardenclyffe Cataclysm (ultimate zone type 53): the massive
            // alternating-current column centered on Tesla — wider than melee
            // range so the ultimate connects beyond arm's reach, and tall so
            // launched targets stay inside; FighterZoneSystem drags the opponent
            // toward the column center while it lives.
            if (zoneTypeID == (int)FighterCharacterID.Tesla * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(3.0));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Pocahontas's Tidewater Tempest ultimate (zone type 83): a wide
            // spirit storm centered on the caster. The design's screen-engulfing
            // storm is approximated by an 8x5-unit footprint (480x300 px, matching
            // the Story hitbox) so the storm stays escapable at the arena edges.
            if (zoneTypeID == (int)FighterCharacterID.Pocahontas * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(4.0), FP64.FromDouble(2.5));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Mozart's Symphony of Sorrow (ultimate zone type 73): piano-key
            // meteors rain across the entire stage, so the bombardment footprint
            // spans the full 20-unit arena width and reaches jump height —
            // there is nowhere on stage to walk out of it.
            if (zoneTypeID == (int)FighterCharacterID.Mozart * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(10), FP64.FromInt(6));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Shakespeare's All the World's a Stage ultimate (zone type 63): the
            // summoned Globe Theatre set is a wide owner-centered stage (12 x 4
            // units, mirroring the authored 720 x 240 px HitboxSize) on which the
            // tragic phantoms strike; the strike cadence/count come from
            // FighterUltimateRules.
            if (zoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(6.0), FP64.FromDouble(2.0));
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

            if (modes.MovementType == 1) {
                // Glide (Leonardo's Ornithopter, Joan's Ascendant Wings,
                // Shakespeare's Prospero's Flight, Pocahontas's Breeze Glide):
                // a forward-and-upward boost that cancels into a reduced-gravity
                // float for the authored duration (design: up to 3 s = 180
                // frames). FloatFrames is a snapshotted FighterRuntimeComponent
                // field, so this stays rollback-safe.
                fighter.Velocity.x = speed * FP64.FromInt(facing);
                fighter.Velocity.y = speed / FP64.FromInt(2);
                fighter.IsGrounded = 0;
                runtime.FloatFrames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 180;
            } else if (modes.MovementType == 5) {
                // Float (Mozart's Sonata Drift pop): upward velocity only.
                fighter.Velocity.y = speed / FP64.FromInt(2);
                fighter.IsGrounded = 0;
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

    /// <summary>
    /// Canonical era-hazard identities (`design-godot.md` §10 stage table). The ID
    /// is authored on <c>FighterStageData.HazardTypeID</c> and carried into the
    /// simulation through <c>FighterMatchComponent.StageHazardTypeID</c>.
    /// </summary>
    public static class FighterHazardTypeID {
        public const int FlorenceSteamPipe = 1;
        public const int OrleansTrebuchetDebris = 2;
        public const int ChicagoTeslaInduction = 3;
        public const int ParisDampeningBeam = 4;
        public const int VesuviusRockfall = 5;
        public const int NassauMortar = 6;
        public const int AlexandriaSinkhole = 7;
        public const int BerlinSearchlight = 8;
        public const int GlobeAudienceHeckle = 9;
        public const int GettysburgArtillery = 10;
        public const int Count = 10;
    }

    /// <summary>
    /// Deterministic era hazards. Every hazard runs warning → active → recovery;
    /// only the active phase damages. Per-type behaviour (rolling debris, sweeping
    /// beams, falling rocks and their residue pools, dwell timers, idle punishment,
    /// one-shot artillery) is authored in <see cref="FighterHazardSpec"/> and driven
    /// here. Hazard damage ticks are basic-attack class, so an active front-facing
    /// block absorbs a tick for one shield charge (design §10 "Block Compatibility").
    ///
    /// <para>All movement is integrated in units per frame from the component's
    /// <c>Velocity</c> field, and all randomness threads
    /// <c>FighterMatchComponent.RandomState0/1</c> through
    /// <see cref="DeterministicRandom"/> inside <c>SpawnHazard</c> in a fixed draw
    /// order, so hazards stay rollback-identical.</para>
    /// </summary>
    public sealed class FighterHazardSystem : ISystem {
        internal const int DefaultWarningFrames = 90;
        internal const int RecoveryFrames = 60;
        internal const int DamageTickFrames = 30;
        internal const int WarningPhase = 0;
        internal const int ActivePhase = 1;
        internal const int RecoveryPhase = 2;
        /// <summary>Vesuvius sub-states: the falling rock, then its ground pool.</summary>
        internal const int RockFallingSubType = 0;
        internal const int RockPoolSubType = 1;
        internal const int RockPoolFrames = 180;
        /// <summary>Berlin's drone fires after this many consecutive frames in the beam.</summary>
        internal const int SearchlightDwellFrames = 90;
        /// <summary>The Globe crowd pelts a fighter that has stood still this long.</summary>
        internal const int HeckleIdleFrames = 120;
        private const int HitstunFrames = 10;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);
        private static readonly FP64 DebrisSpeed = FP64.FromDouble(0.09);
        private static readonly FP64 BeamSweepSpeed = FP64.FromDouble(0.05);
        private static readonly FP64 RockFallSpeed = FP64.FromDouble(0.15);
        private static readonly FP64 InfluenceDrainPerTick = FP64.FromInt(5);
        /// <summary>Below this |velocity.x| a grounded fighter counts as idle for the Globe crowd.</summary>
        private static readonly FP64 IdleSpeedThreshold = FP64.FromDouble(0.5);
        /// <summary>The mortar is a launcher: its vertical impulse is doubled.</summary>
        private static readonly FP64 MortarVerticalScale = FP64.FromInt(2);

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

                if (hazard.Phase == WarningPhase) {
                    if (hazard.PhaseFramesRemaining <= 0) BeginActivePhase(ref hazard);
                    continue;
                }
                if (hazard.Phase == RecoveryPhase) {
                    if (hazard.PhaseFramesRemaining <= 0) frame.DestroyEntity(hazardEntity);
                    continue;
                }

                AdvanceActiveHazard(ref frame, ref hazard);
                if (hazard.TickFramesRemaining > 0) hazard.TickFramesRemaining--;
                if (hazard.TickFramesRemaining <= 0) {
                    ApplyPeriodicTick(ref frame, ref hazard);
                    hazard.TickFramesRemaining = DamageTickFrames;
                }
                if (hazard.PhaseFramesRemaining <= 0) BeginRecoveryPhase(ref hazard);
            }
        }

        private void SpawnHazard(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            // Authored stages pick a deterministic anchor; the legacy arena keeps
            // the historical random spawn range. Both draws always run, in this
            // order, so the RNG stream does not depend on the hazard identity.
            FP64 positionX = _geometry.HazardAnchorXs.Length > 0
                ? _geometry.HazardAnchorXs[random.NextInt(0, _geometry.HazardAnchorXs.Length)]
                : random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7));
            int hazardType = match.StageHazardTypeID >= 1 && match.StageHazardTypeID <= FighterHazardTypeID.Count
                ? match.StageHazardTypeID
                : FighterHazardTypeID.FlorenceSteamPipe;
            int steamDamage = random.NextIntInclusive(5, 10);
            (match.RandomState0, match.RandomState1) = random.GetFullState();

            FighterHazardSpec spec = FighterHazardSpec.For(hazardType, steamDamage);

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new FighterHazardComponent {
                EntityID = match.NextEntityID++,
                HazardTypeID = hazardType,
                SubTypeID = 0,
                Phase = WarningPhase,
                PhaseFramesRemaining = spec.WarningFrames,
                Damage = spec.Damage,
                TickFramesRemaining = DamageTickFrames,
                WarningFrames = spec.WarningFrames,
                ActiveFrames = spec.ActiveFrames,
                CooldownFrames = RecoveryFrames,
                HitMask = 0,
                DwellFramesPlayerOne = 0,
                DwellFramesPlayerTwo = 0,
                Position = new FPVector2(positionX, spec.CenterY),
                HalfExtents = spec.HalfExtents,
                Knockback = new FPVector2(spec.Knockback, spec.Knockback),
                Velocity = FPVector2.Zero
            });
        }

        /// <summary>
        /// Warning → active. Types that move or relocate at ignition (rolling
        /// debris, the sweeping beam, the rock dropping from the ceiling) set their
        /// travel state here; everything else simply arms.
        /// </summary>
        private void BeginActivePhase(ref FighterHazardComponent hazard) {
            hazard.Phase = ActivePhase;
            hazard.PhaseFramesRemaining = hazard.ActiveFrames;
            hazard.TickFramesRemaining = 1;
            hazard.HitMask = 0;

            if (hazard.HazardTypeID == FighterHazardTypeID.OrleansTrebuchetDebris) {
                // Debris rolls away from the wall it was launched over.
                hazard.Velocity = new FPVector2(
                    hazard.Position.x < FP64.Zero ? DebrisSpeed : -DebrisSpeed, FP64.Zero);
            } else if (hazard.HazardTypeID == FighterHazardTypeID.ParisDampeningBeam) {
                hazard.Velocity = new FPVector2(
                    hazard.Position.x <= FP64.Zero ? BeamSweepSpeed : -BeamSweepSpeed, FP64.Zero);
            } else if (hazard.HazardTypeID == FighterHazardTypeID.VesuviusRockfall) {
                // The ground marker becomes the rock, which now falls from the sky.
                hazard.Position = new FPVector2(hazard.Position.x, _geometry.Ceiling);
                hazard.HalfExtents = new FPVector2(FP64.FromDouble(0.6), FP64.FromDouble(0.6));
                hazard.Velocity = new FPVector2(FP64.Zero, -RockFallSpeed);
            }
        }

        private static void BeginRecoveryPhase(ref FighterHazardComponent hazard) {
            hazard.Phase = RecoveryPhase;
            hazard.PhaseFramesRemaining = hazard.CooldownFrames > 0 ? hazard.CooldownFrames : RecoveryFrames;
            hazard.Velocity = FPVector2.Zero;
        }

        /// <summary>
        /// Per-frame active behaviour: movement, contact hits for one-shot hazards,
        /// and the dwell/idle counters. The periodic damage tick runs separately.
        /// </summary>
        private void AdvanceActiveHazard(ref Frame frame, ref FighterHazardComponent hazard) {
            switch (hazard.HazardTypeID) {
                case FighterHazardTypeID.OrleansTrebuchetDebris:
                    hazard.Position += hazard.Velocity;
                    ApplyOneShotContact(ref frame, ref hazard, FP64.Zero);
                    // Rolled off the far side of the ramparts.
                    if (hazard.Position.x - hazard.HalfExtents.x > _geometry.RightWall
                        || hazard.Position.x + hazard.HalfExtents.x < _geometry.LeftWall) {
                        hazard.PhaseFramesRemaining = 0;
                    }
                    break;

                case FighterHazardTypeID.ParisDampeningBeam:
                    hazard.Position += hazard.Velocity;
                    if (hazard.Position.x + hazard.HalfExtents.x >= _geometry.RightWall && hazard.Velocity.x > FP64.Zero) {
                        hazard.Velocity = new FPVector2(-hazard.Velocity.x, FP64.Zero);
                    } else if (hazard.Position.x - hazard.HalfExtents.x <= _geometry.LeftWall && hazard.Velocity.x < FP64.Zero) {
                        hazard.Velocity = new FPVector2(-hazard.Velocity.x, FP64.Zero);
                    }
                    break;

                case FighterHazardTypeID.VesuviusRockfall:
                    if (hazard.SubTypeID == RockFallingSubType) {
                        hazard.Position += hazard.Velocity;
                        ApplyOneShotContact(ref frame, ref hazard, FP64.Zero);
                        if (hazard.Position.y <= FP64.Zero) {
                            // Impact: the rock shatters into a time-dilation pool.
                            hazard.Position = new FPVector2(hazard.Position.x, FP64.Zero);
                            hazard.Velocity = FPVector2.Zero;
                            hazard.SubTypeID = RockPoolSubType;
                            hazard.HalfExtents = new FPVector2(FP64.One, FP64.FromDouble(0.5));
                            hazard.PhaseFramesRemaining = RockPoolFrames;
                            hazard.TickFramesRemaining = 1;
                        }
                    }
                    break;

                case FighterHazardTypeID.NassauMortar:
                case FighterHazardTypeID.GettysburgArtillery:
                    ApplyOneShotContact(
                        ref frame, ref hazard,
                        hazard.HazardTypeID == FighterHazardTypeID.NassauMortar
                            ? MortarVerticalScale
                            : FP64.Zero);
                    break;

                case FighterHazardTypeID.BerlinSearchlight:
                    AdvanceSearchlightDwell(ref frame, ref hazard);
                    break;

                case FighterHazardTypeID.GlobeAudienceHeckle:
                    AdvanceHeckleIdle(ref frame, ref hazard);
                    break;
            }
        }

        /// <summary>
        /// The periodic 0.5 s damage tick shared by the dwell-free damage-over-time
        /// hazards. One-shot and counter-driven identities deliberately opt out.
        /// </summary>
        private void ApplyPeriodicTick(ref Frame frame, ref FighterHazardComponent hazard) {
            int type = hazard.HazardTypeID;
            bool rockPool = type == FighterHazardTypeID.VesuviusRockfall && hazard.SubTypeID == RockPoolSubType;
            bool ticks = type == FighterHazardTypeID.FlorenceSteamPipe
                || type == FighterHazardTypeID.ChicagoTeslaInduction
                || type == FighterHazardTypeID.ParisDampeningBeam
                || type == FighterHazardTypeID.AlexandriaSinkhole
                || rockPool;
            if (!ticks) return;

            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                if (!FighterEntityQueries.Overlaps(
                        in hazard.Position, in hazard.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;

                // The dampening beam is pure meter denial: no damage, no impulse.
                // The drain honours the same pre-effect gates ApplyFighterHit
                // applies (audit M-11): a fighter pinned on the respawn platform is
                // "completely invulnerable" (design-godot.md ~1570) and loses
                // nothing, a dead fighter is skipped, and an Aegis charge absorbs
                // the tick exactly as it absorbs any other incoming effect.
                if (type == FighterHazardTypeID.ParisDampeningBeam) {
                    if (fighter.InvulnerabilityFrames > 0 || fighter.Stocks <= 0) continue;
                    ref FighterRuntimeComponent beamRuntime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                    if (beamRuntime.AegisHits > 0) {
                        beamRuntime.AegisHits--;
                        continue;
                    }
                    fighter.Influence = FP64.Max(FP64.Zero, fighter.Influence - InfluenceDrainPerTick);
                    continue;
                }
                // Quicksand only grips fighters standing in it.
                if (type == FighterHazardTypeID.AlexandriaSinkhole && fighter.IsGrounded == 0) continue;

                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                ResolveTickStatus(type, rockPool, out int statusType, out int statusFrames, out FP64 statusIntensity);
                // The residue pool slows, it does not wound.
                int damage = rockPool ? 0 : hazard.Damage;
                FP64 knockback = rockPool ? FP64.Zero : hazard.Knockback.x;
                int hitstun = rockPool ? 0 : HitstunFrames;
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, in tuning,
                    damage, knockback, hitstun, hazard.Position.x,
                    statusType, statusFrames, statusIntensity);
            }
        }

        private static void ResolveTickStatus(
            int hazardType,
            bool rockPool,
            out int statusType,
            out int statusFrames,
            out FP64 statusIntensity) {
            if (rockPool) {
                statusType = (int)StatusType.TimeDilation;
                statusFrames = 120;
                statusIntensity = FP64.FromDouble(0.5);
                return;
            }
            if (hazardType == FighterHazardTypeID.ChicagoTeslaInduction) {
                statusType = (int)StatusType.StaticCharge;
                statusFrames = 30;
                statusIntensity = FP64.FromDouble(0.5);
                return;
            }
            if (hazardType == FighterHazardTypeID.AlexandriaSinkhole) {
                // The design's "reducing speed by 50%" is TimeDilation at full intensity.
                statusType = (int)StatusType.TimeDilation;
                statusFrames = 60;
                statusIntensity = FP64.One;
                return;
            }
            statusType = (int)StatusType.None;
            statusFrames = 0;
            statusIntensity = FP64.FromDouble(0.5);
        }

        /// <summary>
        /// One damaging contact per fighter for the whole hazard instance. The mask
        /// is consumed on the first overlapping frame whether or not the hit landed
        /// (a blocked mortar shell is spent, and so is one the fighter rolled
        /// through) so that a single shell can never chain-hit across frames.
        /// </summary>
        private static void ApplyOneShotContact(
            ref Frame frame,
            ref FighterHazardComponent hazard,
            FP64 verticalKnockbackScale) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                int bit = 1 << fighter.PlayerID;
                if ((hazard.HitMask & bit) != 0) continue;
                if (!FighterEntityQueries.Overlaps(
                        in hazard.Position, in hazard.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;

                hazard.HitMask |= bit;
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, in tuning,
                    hazard.Damage, hazard.Knockback.x, HitstunFrames, hazard.Position.x,
                    (int)StatusType.None, 0, FP64.One, verticalKnockbackScale);
            }
        }

        /// <summary>
        /// Berlin: the searchlight itself is harmless. Staying inside its column for
        /// 1.5 consecutive seconds calls down a drone laser on that fighter alone;
        /// stepping out of the light resets the counter.
        /// </summary>
        private static void AdvanceSearchlightDwell(ref Frame frame, ref FighterHazardComponent hazard) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                bool inside = FighterEntityQueries.Overlaps(
                    in hazard.Position, in hazard.HalfExtents,
                    in fighter.Position, in FighterHalfExtents);
                int dwell = ReadDwell(in hazard, fighter.PlayerID);
                if (!inside) {
                    WriteDwell(ref hazard, fighter.PlayerID, 0);
                    continue;
                }
                dwell++;
                if (dwell < SearchlightDwellFrames) {
                    WriteDwell(ref hazard, fighter.PlayerID, dwell);
                    continue;
                }
                WriteDwell(ref hazard, fighter.PlayerID, 0);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, in tuning,
                    hazard.Damage, hazard.Knockback.x, HitstunFrames, hazard.Position.x,
                    (int)StatusType.None, 0, FP64.One);
            }
        }

        /// <summary>
        /// Globe: the crowd punishes camping. A grounded fighter that has barely
        /// moved for two seconds is pelted from the nearest gallery anchor; a moving
        /// fighter is never hit, and the hazard has no damaging region at all.
        /// </summary>
        private void AdvanceHeckleIdle(ref Frame frame, ref FighterHazardComponent hazard) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                bool idle = fighter.IsGrounded != 0 && FP64.Abs(fighter.Velocity.x) < IdleSpeedThreshold;
                int idleFrames = ReadDwell(in hazard, fighter.PlayerID);
                if (!idle) {
                    WriteDwell(ref hazard, fighter.PlayerID, 0);
                    continue;
                }
                idleFrames++;
                if (idleFrames < HeckleIdleFrames) {
                    WriteDwell(ref hazard, fighter.PlayerID, idleFrames);
                    continue;
                }
                WriteDwell(ref hazard, fighter.PlayerID, 0);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, in tuning,
                    hazard.Damage, hazard.Knockback.x, HitstunFrames,
                    NearestGalleryAnchorX(fighter.Position.x),
                    (int)StatusType.None, 0, FP64.One);
            }
        }

        private FP64 NearestGalleryAnchorX(FP64 fighterX) {
            if (_geometry.HazardAnchorXs.Length == 0) return fighterX;
            FP64 best = _geometry.HazardAnchorXs[0];
            FP64 bestDistance = FP64.Abs(best - fighterX);
            for (int index = 1; index < _geometry.HazardAnchorXs.Length; index++) {
                FP64 candidate = _geometry.HazardAnchorXs[index];
                FP64 distance = FP64.Abs(candidate - fighterX);
                if (distance < bestDistance) {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static int ReadDwell(in FighterHazardComponent hazard, int playerID) =>
            playerID == 0 ? hazard.DwellFramesPlayerOne : hazard.DwellFramesPlayerTwo;

        private static void WriteDwell(ref FighterHazardComponent hazard, int playerID, int frames) {
            if (playerID == 0) hazard.DwellFramesPlayerOne = frames;
            else hazard.DwellFramesPlayerTwo = frames;
        }
    }

    /// <summary>
    /// Authored per-identity hazard tuning (damage, impulse, footprint, phase
    /// lengths). Kept beside the system rather than in a `.tres` because
    /// hazard geometry is deterministic simulation data that must be identical on
    /// every peer without loading a Godot resource.
    /// </summary>
    internal readonly struct FighterHazardSpec {
        public readonly int Damage;
        public readonly int WarningFrames;
        public readonly int ActiveFrames;
        public readonly FP64 Knockback;
        public readonly FP64 CenterY;
        public readonly FPVector2 HalfExtents;

        private FighterHazardSpec(
            int damage, int warningFrames, int activeFrames,
            FP64 knockback, FP64 centerY, FPVector2 halfExtents) {
            Damage = damage;
            WarningFrames = warningFrames;
            ActiveFrames = activeFrames;
            Knockback = knockback;
            CenterY = centerY;
            HalfExtents = halfExtents;
        }

        /// <param name="steamDamage">The Florence pipe's authored random 5–10 roll.</param>
        public static FighterHazardSpec For(int hazardType, int steamDamage) => hazardType switch {
            // Orléans: a narrow ground-band boulder that rolls the whole stage.
            FighterHazardTypeID.OrleansTrebuchetDebris => new FighterHazardSpec(
                7, 90, 240, FP64.FromInt(3), FP64.Zero,
                new FPVector2(FP64.FromDouble(0.8), FP64.One)),
            // Chicago: a wide static induction grid across stage centre.
            FighterHazardTypeID.ChicagoTeslaInduction => new FighterHazardSpec(
                6, 90, 360, FP64.FromInt(3), FP64.Zero,
                new FPVector2(FP64.FromInt(4), FP64.FromInt(2))),
            // Paris: a narrow full-height sweeping column; 0 damage, drains meter.
            FighterHazardTypeID.ParisDampeningBeam => new FighterHazardSpec(
                0, 90, 360, FP64.Zero, FP64.FromInt(3),
                new FPVector2(FP64.One, FP64.FromInt(6))),
            // Vesuvius: the ground warning marker before the rock drops.
            FighterHazardTypeID.VesuviusRockfall => new FighterHazardSpec(
                8, 90, 240, FP64.FromInt(4), FP64.Zero,
                new FPVector2(FP64.One, FP64.FromDouble(0.25))),
            // Nassau: a targeting grid, then a half-second launching explosion.
            FighterHazardTypeID.NassauMortar => new FighterHazardSpec(
                10, 90, 30, FP64.FromInt(5), FP64.Zero,
                new FPVector2(FP64.FromInt(2), FP64.FromInt(2))),
            // Alexandria: a long-lived quicksand patch that only grips the grounded.
            FighterHazardTypeID.AlexandriaSinkhole => new FighterHazardSpec(
                2, 90, 480, FP64.One, FP64.Zero,
                new FPVector2(FP64.FromInt(2), FP64.One)),
            // Berlin: a tall narrow light column; harmless until you loiter in it.
            FighterHazardTypeID.BerlinSearchlight => new FighterHazardSpec(
                8, 90, 480, FP64.FromInt(3), FP64.FromInt(3),
                new FPVector2(FP64.FromDouble(1.2), FP64.FromInt(5))),
            // Globe: no damaging region at all — the crowd watches the whole stage.
            FighterHazardTypeID.GlobeAudienceHeckle => new FighterHazardSpec(
                5, 90, 600, FP64.FromInt(2), FP64.Zero,
                new FPVector2(FP64.FromInt(10), FP64.FromInt(10))),
            // Gettysburg: the widest band, a 2 s sight line, then a heavy strike.
            FighterHazardTypeID.GettysburgArtillery => new FighterHazardSpec(
                12, 120, 30, FP64.FromInt(6), FP64.FromDouble(0.75),
                new FPVector2(FP64.FromInt(5), FP64.FromDouble(0.75))),
            // Florence (and the legacy fallback): the shipped steam pipe.
            _ => new FighterHazardSpec(
                steamDamage, 90, 360, FP64.FromInt(3), FP64.Zero,
                new FPVector2(FP64.FromInt(2), FP64.FromInt(2)))
        };
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

                int picker = FindPicker(ref frame, ref match, in orb);
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

        private static int FindPicker(ref Frame frame, ref FighterMatchComponent match, in FighterOrbComponent orb) {
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
            if (playerOne && playerTwo) return ResolveContestedPicker(ref match);
            if (playerOne) return 0;
            if (playerTwo) return 1;
            return -1;
        }

        /// <summary>
        /// Design ~3134: a same-tick contested orb resolves by a seeded coin flip,
        /// not the old <c>EntityID % 2</c> parity (with the first orb entity ID
        /// fixed, that parity always awarded the same slot). The draw threads
        /// <c>FighterMatchComponent.RandomState0/1</c> through
        /// <see cref="DeterministicRandom"/> like every other match draw, so it is
        /// random per contest yet bit-identical under rollback resimulation.
        /// </summary>
        internal static int ResolveContestedPicker(ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            int picker = random.NextInt(0, 2);
            (match.RandomState0, match.RandomState1) = random.GetFullState();
            return picker;
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
    /// Pulses carry no knockback or hitstun, so they never interrupt movement —
    /// with one per-type exception: Shakespeare's Tempest pulses shove the
    /// opponent away from the storm center with real knockback and zero damage.
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
                    // Einstein's Cosmological Constant (ultimate zone type 3)
                    // ends with the final explosive launch away from the
                    // collapsed singularity toward the blast zones.
                    if (zone.ZoneTypeID == (int)FighterCharacterID.Einstein * 10 + FighterUltimateRules.UltimateSlot) {
                        ApplyCosmologicalLaunch(ref frame, in zone);
                    }
                    // Leonardo's Vitruvian Matrix (ultimate zone type 23) closes
                    // with the massive final explosion: a zero-damage ultimate-
                    // class knockback pulse away from the circle center (the 8
                    // bombardment ticks deliver all the damage beforehand).
                    if (zone.ZoneTypeID == (int)FighterCharacterID.Leonardo * 10 + FighterUltimateRules.UltimateSlot) {
                        ApplyMatrixExpiryExplosion(ref frame, in zone);
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

                // Einstein's Cosmological Constant sucks the opponent toward the
                // singularity every frame: the same impulse-free positional drag
                // as the vortex, at black-hole strength.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Einstein * 10 + FighterUltimateRules.UltimateSlot) {
                    ApplySingularityPull(ref frame, in zone);
                }
                // Cleopatra's Wrath of the Nile (ultimate-slot zone type 43)
                // reuses the same impulse-free positional drag toward the
                // storm's eye for its flood current.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + FighterUltimateRules.UltimateSlot) {
                    ApplyVortexPull(ref frame, in zone);
                }
                // Tesla's Wardenclyffe Cataclysm column (ultimate zone type 53)
                // draws the opponent toward its center with the same impulse-free
                // positional drag as the Sandstorm Vortex, so its pull stays
                // snapshot-safe with no extra state.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Tesla * 10 + FighterUltimateRules.UltimateSlot) {
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

                // Two zones carry real impulse on their pulses; every other zone
                // stays an impulse-free tick by design. Lincoln's Emancipator
                // (zone type 31) knocks the target up with the authored Special 1
                // knockback; Shakespeare's Tempest (zone type 62) shoves the
                // opponent away from the storm center (hitOriginX at the zone
                // center yields the outward direction) with the authored Special 2
                // knockback.
                FP64 pulseKnockback = FP64.Zero;
                int pulseHitstunFrames = 0;
                if (zone.ZoneTypeID == (int)FighterCharacterID.Lincoln * 10 + 1) {
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).SpecialOneKnockback;
                    pulseHitstunFrames = EmancipatorHitstunFrames;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + 2) {
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).SpecialTwoKnockback;
                    pulseHitstunFrames = TempestHitstunFrames;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Joan * 10 + FighterUltimateRules.UltimateSlot
                    && zone.LifetimeFrames <= zone.TickIntervalFrames) {
                    // Grand Crusade: only the FINAL trample pulse carries the
                    // authored ultimate knockback (carrying the opponent toward
                    // the blast zone); earlier pulses stay impulse-free so the
                    // full multi-hit total lands.
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
                    pulseHitstunFrames = GrandCrusadeFinalHitstunFrames;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Lincoln * 10 + FighterUltimateRules.UltimateSlot
                    && zone.LifetimeFrames <= zone.TickIntervalFrames) {
                    // Union Indestructible's final smash shatters the fence pen:
                    // only the last tick of the ultimate zone carries the massive
                    // authored finisher knockback (earlier smashes stay
                    // impulse-free so the Root pen keeps holding).
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
                    pulseHitstunFrames = UnionFinisherHitstunFrames;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Pocahontas * 10 + FighterUltimateRules.UltimateSlot
                    && zone.LifetimeFrames < zone.TickIntervalFrames) {
                    // Pocahontas's Tidewater Tempest (zone type 83): the storm's
                    // intermediate ticks are impulse-free; only the final surge —
                    // the tick with less than one full interval of lifetime left —
                    // throws the target outward from the storm center with the
                    // authored ultimate knockback.
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
                    pulseHitstunFrames = TidewaterSurgeHitstunFrames;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Mozart * 10 + FighterUltimateRules.UltimateSlot
                    && zone.LifetimeFrames <= zone.TickIntervalFrames) {
                    // Symphony of Sorrow: earlier meteors pin the target inside
                    // the bombardment impulse-free; only the closing strike
                    // launches with the authored ultimate knockback.
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
                    pulseHitstunFrames = SymphonyFinalHitstunFrames;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + FighterUltimateRules.UltimateSlot
                    && zone.LifetimeFrames <= zone.TickIntervalFrames) {
                    // All the World's a Stage (zone type 63): only the closing
                    // phantom strike carries impulse — when no further pulse fits
                    // in the remaining lifetime, this pulse is Hamlet's finale and
                    // launches with the authored ultimate knockback. Earlier
                    // phantom strikes stay impulse-free ultimate-class ticks.
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
                    pulseHitstunFrames = StageFinaleHitstunFrames;
                }

                // Ultimate-slot zones (ZoneTypeID % 10 == FighterUltimateRules
                // .UltimateSlot) hit with the ultimate attack class so they
                // bypass shields like every other ultimate.
                int pulseAttackClass = zone.ZoneTypeID % 10 == FighterUltimateRules.UltimateSlot
                    ? FighterDamageRules.UltimateAttackClass
                    : FighterDamageRules.SpecialAttackClass;
                FighterDamageRules.ApplyFighterHit(
                    ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in targetTuning,
                    pulseAttackClass, pulseDamage, pulseKnockback, pulseHitstunFrames,
                    zone.StatusType, zone.StatusFrames, zone.StatusIntensity, zone.Position.x);
            }
        }

        private const int CoilArcDamage = 5;
        private const int CoilObjectTypeID = 1;
        private const int EmancipatorHitstunFrames = 18;
        private const int TempestHitstunFrames = 10;
        // Union Indestructible finisher: 0.5 s, matching the authored
        // HitstunDuration on lincoln/ultimate.tres.
        private const int UnionFinisherHitstunFrames = 30;
        private const int TidewaterSurgeHitstunFrames = 24;
        private const int SymphonyFinalHitstunFrames = 30;
        // Matches the generic melee ultimate's 30-frame hitstun.
        private const int StageFinaleHitstunFrames = 30;
        private const int GrandCrusadeFinalHitstunFrames = 24;

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

        // 0.08 world units per frame — a stronger drag than the sandstorm
        // vortex's 0.05, befitting a black hole. Mirrored by the Story
        // singularity's positional pull.
        private static readonly FP64 SingularityPullPerFrame = FP64.FromDouble(0.08);
        private const int CosmologicalLaunchHitstunFrames = 30;

        /// <summary>
        /// Cosmological Constant per-frame pull (mirrors ApplyVortexPull): an
        /// impulse-free horizontal positional drag toward the singularity center
        /// that never touches velocity, so it stays snapshot-safe with no extra
        /// state.
        /// </summary>
        private static void ApplySingularityPull(ref Frame frame, in FighterZoneComponent zone) {
            int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.InvulnerabilityFrames > 0) return;
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterHalfExtents)) return;
            FP64 dx = zone.Position.x - target.Position.x;
            if (dx > SingularityPullPerFrame) target.Position.x += SingularityPullPerFrame;
            else if (dx < -SingularityPullPerFrame) target.Position.x -= SingularityPullPerFrame;
            else target.Position.x = zone.Position.x;
        }

        /// <summary>
        /// Cosmological Constant expiry launch (the expiry-knockback pattern of
        /// ApplySpiralExpiryKnockback): a zero-damage ultimate-class hit that
        /// blasts the opponent away from the collapsed singularity with the
        /// attacker's authored UltimateKnockback. Ultimate class means it
        /// bypasses shields and hyper-armor like every other ultimate hit.
        /// </summary>
        private static void ApplyCosmologicalLaunch(ref Frame frame, in FighterZoneComponent zone) {
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
            FP64 launchKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
            FighterDamageRules.ApplyFighterHit(
                ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in targetTuning,
                FighterDamageRules.UltimateAttackClass, 0, launchKnockback, CosmologicalLaunchHitstunFrames,
                (int)StatusType.None, 0, FP64.One, zone.Position.x);
        }

        private const int MatrixExpiryHitstunFrames = 18;

        /// <summary>
        /// Vitruvian Matrix expiry pulse: a zero-damage ultimate-class knockback
        /// hit launching the opponent away from the circle center with the
        /// attacker's authored ultimate knockback (the final "massive explosion";
        /// ultimate-class impulses also pierce hyper-armor and block).
        /// </summary>
        private static void ApplyMatrixExpiryExplosion(ref Frame frame, in FighterZoneComponent zone) {
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
            FP64 explosionKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
            FighterDamageRules.ApplyFighterHit(
                ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in targetTuning,
                FighterDamageRules.UltimateAttackClass, 0, explosionKnockback, MatrixExpiryHitstunFrames,
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
