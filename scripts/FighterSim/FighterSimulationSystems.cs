using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    public sealed class FighterWorldSystem : IInitSystem, ISystem {
        private readonly FighterLoadout _playerOne;
        private readonly FighterLoadout _playerTwo;
        private readonly int _stockCount;
        private readonly int _matchFrames;
        private readonly int _seed;
        private readonly int _spawnDistance;
        private readonly FighterMatchRules _rules;

        public FighterWorldSystem(
            FighterLoadout playerOne,
            FighterLoadout playerTwo,
            int stockCount,
            int matchFrames,
            int seed,
            int spawnDistance,
            FighterMatchRules rules) {
            _playerOne = playerOne;
            _playerTwo = playerTwo;
            _stockCount = stockCount;
            _matchFrames = matchFrames;
            _seed = seed;
            _spawnDistance = spawnDistance;
            _rules = rules;
        }

        public void OnInit(ref Frame frame) {
            var random = new DeterministicRandom(_seed);
            (ulong state0, ulong state1) = random.GetFullState();

            EntityRef matchEntity = frame.CreateEntity();
            int countdownFrames = _rules.PreMatchCountdownFrames;
            frame.Add(matchEntity, new FighterMatchComponent {
                RemainingFrames = _matchFrames,
                MatchState = countdownFrames > 0 ? FighterMatchStates.Countdown : FighterMatchStates.InProgress,
                CountdownFramesRemaining = countdownFrames,
                GoBannerFramesRemaining = countdownFrames > 0 ? 0 : FighterMatchFlowRules.GoBannerFrames,
                MatchMode = _rules.MatchMode,
                WinnerPlayerID = -1,
                WorldSeed = _seed,
                NextEntityID = 2000,
                ItemsEnabled = _rules.ItemsEnabled ? 1 : 0,
                ItemFrequency = _rules.ItemFrequency,
                HazardsEnabled = _rules.HazardsEnabled ? 1 : 0,
                HazardFrequency = _rules.HazardFrequency,
                StageHazardTypeID = _rules.StageHazardTypeID,
                NextOrbSpawnFrames = FighterSpawnIntervals.OrbFrames(_rules.ItemFrequency),
                NextHazardSpawnFrames = FighterSpawnIntervals.HazardFrames(_rules.HazardFrequency),
                RandomState0 = state0,
                RandomState1 = state1
            });

            SpawnFighter(ref frame, 0, in _playerOne, -_spawnDistance);
            SpawnFighter(ref frame, 1, in _playerTwo, _spawnDistance);
        }

        public void Update(ref Frame frame) { }

        private void SpawnFighter(ref Frame frame, int playerID, in FighterLoadout loadout, int spawnX) {
            EntityRef entity = frame.CreateEntity();
            FPVector2 spawn = new(FP64.FromInt(spawnX), FP64.Zero);
            frame.Add(entity, new FighterStateComponent {
                EntityID = 1000 + playerID,
                PlayerID = playerID,
                CharacterID = loadout.CharacterID,
                CurrentHP = loadout.MaxHP,
                MaxHP = loadout.MaxHP,
                Stocks = _stockCount,
                BlockCharges = loadout.MaxBlockCharges,
                FacingRight = playerID == 0 ? 1 : 0,
                IsGrounded = 1,
                RemainingJumps = loadout.MaxJumpCount,
                Weight = loadout.Weight,
                Influence = FP64.Zero,
                Position = spawn,
                Velocity = FPVector2.Zero,
                SpawnPosition = spawn
            });
            frame.Add(entity, new FighterRuntimeComponent {
                StatusIntensity = FP64.One,
                UsesStocks = _rules.MatchMode == (int)FTT.Core.MatchMode.TimeLimit ? 0 : 1
            });
            frame.Add(entity, new FighterTuningComponent {
                BasicDamage = loadout.BasicDamage,
                SpecialOneDamage = loadout.SpecialOneDamage,
                SpecialTwoDamage = loadout.SpecialTwoDamage,
                UltimateDamage = loadout.UltimateDamage,
                MaxBlockCharges = loadout.MaxBlockCharges,
                MaxJumpCount = loadout.MaxJumpCount,
                SpecialOneCooldownFrames = loadout.SpecialOneCooldownFrames,
                SpecialTwoCooldownFrames = loadout.SpecialTwoCooldownFrames,
                SpecialOneStatusType = loadout.SpecialOneStatusType,
                SpecialOneStatusFrames = loadout.SpecialOneStatusFrames,
                SpecialTwoStatusType = loadout.SpecialTwoStatusType,
                SpecialTwoStatusFrames = loadout.SpecialTwoStatusFrames,
                UltimateStatusType = loadout.UltimateStatusType,
                UltimateStatusFrames = loadout.UltimateStatusFrames,
                MoveSpeed = loadout.MoveSpeed,
                JumpSpeed = loadout.JumpSpeed,
                BasicKnockback = loadout.BasicKnockback,
                SpecialOneKnockback = loadout.SpecialOneKnockback,
                SpecialTwoKnockback = loadout.SpecialTwoKnockback,
                UltimateKnockback = loadout.UltimateKnockback,
                SpecialOneStatusIntensity = loadout.SpecialOneStatusIntensity,
                SpecialTwoStatusIntensity = loadout.SpecialTwoStatusIntensity,
                UltimateStatusIntensity = loadout.UltimateStatusIntensity
            });
            FighterAbilityLoadout modes = loadout.AbilityModes;
            frame.Add(entity, new FighterAbilityModeComponent {
                SpecialOneExecutionType = modes.SpecialOneExecutionType,
                SpecialTwoExecutionType = modes.SpecialTwoExecutionType,
                SpecialOneProjectileLifetimeFrames = modes.SpecialOneProjectileLifetimeFrames,
                SpecialTwoProjectileLifetimeFrames = modes.SpecialTwoProjectileLifetimeFrames,
                SpecialOnePersistentTypeID = modes.SpecialOnePersistentTypeID,
                SpecialOneMaxActiveObjects = modes.SpecialOneMaxActiveObjects,
                SpecialOnePersistentLifetimeFrames = modes.SpecialOnePersistentLifetimeFrames,
                SpecialTwoPersistentTypeID = modes.SpecialTwoPersistentTypeID,
                SpecialTwoMaxActiveObjects = modes.SpecialTwoMaxActiveObjects,
                SpecialTwoPersistentLifetimeFrames = modes.SpecialTwoPersistentLifetimeFrames,
                SpecialOneTickIntervalFrames = modes.SpecialOneTickIntervalFrames,
                SpecialTwoTickIntervalFrames = modes.SpecialTwoTickIntervalFrames,
                MovementType = modes.MovementType,
                MovementCooldownFrames = modes.MovementCooldownFrames,
                MovementDurationFrames = modes.MovementDurationFrames,
                MovementResetsJump = modes.MovementResetsJump,
                MovementGrantsHyperArmor = modes.MovementGrantsHyperArmor,
                MovementPersistentTypeID = modes.MovementPersistentTypeID,
                MovementMaxActiveObjects = modes.MovementMaxActiveObjects,
                MovementPersistentLifetimeFrames = modes.MovementPersistentLifetimeFrames,
                SpecialOneProjectileSpeed = modes.SpecialOneProjectileSpeed,
                SpecialTwoProjectileSpeed = modes.SpecialTwoProjectileSpeed,
                MovementDistance = modes.MovementDistance,
                MovementSpeed = modes.MovementSpeed
            });
        }
    }

    public sealed class FighterInputSystem : ISystem, ICommandSystem {
        public void OnCommand(ref Frame frame, ICommand command) {
            if (command is not FighterInputCommand input) return;

            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                if (fighter.PlayerID != input.PlayerId) continue;
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                runtime.MoveX = input.MoveX;
                runtime.MoveY = input.MoveY;
                runtime.HeldButtons = input.HeldButtons;
                runtime.PressedButtons = input.PressedButtons;
                runtime.ReleasedButtons = input.ReleasedButtons;
                return;
            }
        }

        public void Update(ref Frame frame) { }
    }

    /// <summary>
    /// Deterministic pre-match countdown. While <see cref="FighterMatchComponent.MatchState"/>
    /// is <see cref="FighterMatchStates.Countdown"/> the simulation still ticks (so
    /// snapshots, hashes, and rollback stay uniform) but every fighter's sampled
    /// gameplay input is discarded, so nothing can act before "GO!". Runs in
    /// PreUpdate immediately after <see cref="FighterInputSystem"/> and before any
    /// Update/PostUpdate system reads the runtime input fields.
    /// </summary>
    public sealed class FighterCountdownSystem : ISystem {
        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState == FighterMatchStates.Countdown) {
                ClearGameplayInput(ref frame);
                if (match.CountdownFramesRemaining > 0) match.CountdownFramesRemaining--;
                if (match.CountdownFramesRemaining <= 0) {
                    match.MatchState = FighterMatchStates.InProgress;
                    match.GoBannerFramesRemaining = FighterMatchFlowRules.GoBannerFrames;
                }
                return;
            }
            if (match.GoBannerFramesRemaining > 0) match.GoBannerFramesRemaining--;
        }

        private static void ClearGameplayInput(ref Frame frame) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                runtime.MoveX = 0;
                runtime.MoveY = 0;
                runtime.HeldButtons = 0;
                runtime.PressedButtons = 0;
                runtime.ReleasedButtons = 0;
            }
        }
    }

    public sealed class FighterMovementSystem : ISystem {
        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;
        private const int BasicButton = 1 << 2;
        private const int BlockButton = 1 << 6;
        private const int RollButton = 1 << 10;
        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(60);
        private static readonly FP64 Gravity = FP64.FromInt(-30);
        private static readonly FP64 RollSpeedMultiplier = FP64.FromDouble(UniversalMovementRules.RollSpeedMultiplier);

        private readonly FighterStageGeometry _geometry;

        public FighterMovementSystem(FighterStageGeometry geometry = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);
                TickCounters(ref fighter, ref runtime, in tuning);

                // The Chronal Respawn Platform owns the fighter completely: it is
                // frozen, invulnerable, and consumes no input until it drops.
                if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) {
                    ProcessRespawnPlatform(ref fighter, ref runtime);
                    continue;
                }

                if (fighter.InvulnerabilityFrames > 0) fighter.InvulnerabilityFrames--;
                if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) {
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    fighter.Velocity.y += Gravity * FixedDelta;
                } else {
                    bool rooted = runtime.StatusType == (int)FTT.Core.StatusType.Root;
                    FP64 statusMoveMultiplier = runtime.StatusType == (int)FTT.Core.StatusType.TimeDilation
                        ? FP64.Clamp(
                            FP64.One - runtime.StatusIntensity / FP64.FromInt(2),
                            FP64.FromDouble(0.1),
                            FP64.One)
                        : FP64.One;
                    FP64 speedBuffMultiplier = runtime.SpeedBuffFrames > 0
                        ? FP64.FromDouble(1.4)
                        : FP64.One;
                    // Standing inside a zone the fighter owns (Einstein's Relativity
                    // Rift) grants the design's +25% movement speed in both modes.
                    if (runtime.ZoneSpeedBonusFrames > 0) {
                        speedBuffMultiplier *= FP64.FromDouble(1.25);
                    }
                    FP64 jumpBuffMultiplier = runtime.JumpBuffFrames > 0
                        ? FP64.FromDouble(1.3)
                        : FP64.One;
                    if (rooted) FighterUniversalMovementRules.Cancel(ref runtime);
                    // The basic-combo phase machine advances before movement so its
                    // locks and cancels gate the same tick's movement, mirroring how
                    // Story resolves both inside one _PhysicsProcess.
                    ProcessBasicAttackPhase(ref fighter, ref runtime);
                    bool attacking = runtime.AttackPhase != FighterBasicAttackRules.PhaseNone;
                    bool blockStance = FighterBasicAttackRules.IsBlockStance(in fighter, in runtime);
                    TryStartUniversalMovement(
                        ref fighter,
                        ref runtime,
                        rooted,
                        allowRoll: !attacking);
                    bool movementHandled = ProcessUniversalMovement(
                        ref fighter,
                        ref runtime,
                        in tuning,
                        statusMoveMultiplier * speedBuffMultiplier);
                    if (!movementHandled) {
                        ApplyNormalMovement(
                            ref fighter,
                            ref runtime,
                            in tuning,
                            rooted,
                            statusMoveMultiplier,
                            speedBuffMultiplier,
                            jumpBuffMultiplier,
                            groundIsSolid: _geometry.Platforms.Length > 0,
                            // Swings never lock steering — an attacker keeps
                            // full horizontal control at normal run
                            // acceleration; only the block stance roots.
                            // Facing stays committed for the whole swing so
                            // the hitbox direction cannot flip mid-string.
                            lockHorizontal: blockStance,
                            lockFacing: attacking || blockStance,
                            allowJump: !attacking && !blockStance);
                    }
                    if (fighter.IsGrounded == 0) {
                        // Float glide (post-warp cancel) heavily reduces gravity.
                        FP64 gravityStep = runtime.FloatFrames > 0
                            ? Gravity * FixedDelta / FP64.FromInt(5)
                            : Gravity * FixedDelta;
                        fighter.Velocity.y += gravityStep;
                    }
                }

                FP64 previousY = fighter.Position.y;
                fighter.Position += fighter.Velocity * FixedDelta;
                fighter.Position.x = FP64.Clamp(fighter.Position.x, _geometry.LeftWall, _geometry.RightWall);
                if (fighter.Position.y > _geometry.Ceiling) {
                    fighter.Position.y = _geometry.Ceiling;
                    if (fighter.Velocity.y > FP64.Zero) fighter.Velocity.y = FP64.Zero;
                }

                // Walking off a one-way platform edge removes ground support.
                if (fighter.IsGrounded != 0 && fighter.Position.y > FP64.Zero && !HasPlatformSupport(in fighter)) {
                    fighter.IsGrounded = 0;
                }

                if (fighter.DropThroughFrames <= 0 && fighter.Velocity.y <= FP64.Zero) {
                    TryLandOnPlatform(ref fighter, in tuning, previousY);
                }

                // The blast zone is resolved before the ground snap. With the old
                // order a fighter who had already fallen past the blast zone was
                // teleported back up onto the floor the instant their drop-through
                // window expired, which made the bottom blast zone unreachable on
                // any stage whose base floor spans the full width.
                if (fighter.Position.y < _geometry.BottomBlastZone) {
                    FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, in tuning);
                    continue;
                }

                // Stages with authored platforms have a solid base floor; only the
                // legacy flat arena keeps its historical drop-through ground.
                bool groundIsSolid = _geometry.Platforms.Length > 0;
                if ((groundIsSolid || fighter.DropThroughFrames <= 0) && fighter.Position.y <= FP64.Zero) {
                    fighter.Position.y = FP64.Zero;
                    if (fighter.Velocity.y < FP64.Zero) fighter.Velocity.y = FP64.Zero;
                    fighter.IsGrounded = 1;
                    fighter.RemainingJumps = tuning.MaxJumpCount;
                }
            }
        }

        /// <summary>
        /// Chronal Respawn Platform (design-godot.md ~1565-1571). The fighter
        /// stands frozen and invulnerable for up to five seconds. After a short
        /// grace window any gameplay input drops them; otherwise the platform
        /// dissolves on expiry. Either way the three-second spawn invulnerability
        /// is (re)armed at the drop, never before it.
        /// </summary>
        private static void ProcessRespawnPlatform(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime) {
            int elapsed = FighterMatchFlowRules.RespawnPlatformFrames - fighter.RespawnFramesRemaining;
            bool graceElapsed = elapsed >= FighterMatchFlowRules.RespawnPlatformGraceFrames;
            bool inputRequestedDrop = graceElapsed
                && (runtime.MoveX != 0
                    || runtime.MoveY != 0
                    || runtime.PressedButtons != 0
                    || runtime.HeldButtons != 0);

            // Nothing downstream may read this frame's input: no movement, no
            // attack, no ability, no block while the platform holds the fighter.
            runtime.MoveX = 0;
            runtime.MoveY = 0;
            runtime.HeldButtons = 0;
            runtime.PressedButtons = 0;
            runtime.ReleasedButtons = 0;
            FighterUniversalMovementRules.Cancel(ref runtime);

            fighter.Position = FighterMatchFlowRules.RespawnPlatformPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 1;
            fighter.HitstunFrames = 0;
            fighter.DazeFrames = 0;

            if (fighter.RespawnFramesRemaining > 0) fighter.RespawnFramesRemaining--;
            if (inputRequestedDrop || fighter.RespawnFramesRemaining <= 0) {
                fighter.RespawnFramesRemaining = 0;
                fighter.IsGrounded = 0;
                fighter.InvulnerabilityFrames = FighterMatchFlowRules.RespawnInvulnerabilityFrames;
                return;
            }

            // Invulnerable for the whole dissolve, and the 3 s window still has
            // its full length left the instant the platform releases them.
            fighter.InvulnerabilityFrames =
                fighter.RespawnFramesRemaining + FighterMatchFlowRules.RespawnInvulnerabilityFrames;
        }

        /// <summary>True while the fighter stands on a platform surface span.</summary>
        private bool HasPlatformSupport(in FighterStateComponent fighter) {
            for (int index = 0; index < _geometry.Platforms.Length; index++) {
                ref readonly FighterStagePlatform platform = ref _geometry.Platforms[index];
                if (fighter.Position.y == platform.SurfaceY && platform.Supports(fighter.Position.x)) return true;
            }
            return false;
        }

        /// <summary>One-way landing: only when the fall crossed the surface from above.</summary>
        private void TryLandOnPlatform(
            ref FighterStateComponent fighter,
            in FighterTuningComponent tuning,
            FP64 previousY) {
            for (int index = 0; index < _geometry.Platforms.Length; index++) {
                ref readonly FighterStagePlatform platform = ref _geometry.Platforms[index];
                if (previousY < platform.SurfaceY
                    || fighter.Position.y > platform.SurfaceY
                    || !platform.Supports(fighter.Position.x)) continue;
                fighter.Position.y = platform.SurfaceY;
                if (fighter.Velocity.y < FP64.Zero) fighter.Velocity.y = FP64.Zero;
                fighter.IsGrounded = 1;
                fighter.RemainingJumps = tuning.MaxJumpCount;
                return;
            }
        }

        private static void TryStartUniversalMovement(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            bool rooted,
            bool allowRoll = true) {
            if (rooted
                || fighter.IsGrounded == 0
                || runtime.UniversalMovementState != (int)UniversalMovementPhase.None) return;
            int direction = runtime.MoveX > 0 ? 1 : runtime.MoveX < 0 ? -1 : fighter.FacingRight != 0 ? 1 : -1;
            if (allowRoll && (runtime.PressedButtons & RollButton) != 0) {
                runtime.UniversalMovementState = (int)UniversalMovementPhase.RollStartup;
                runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollStartupFrames;
                runtime.UniversalMovementDirection = direction;
            }
        }

        private static bool ProcessUniversalMovement(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            FP64 speedMultiplier) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            if (phase == UniversalMovementPhase.None) return false;
            FP64 direction = FP64.FromInt(runtime.UniversalMovementDirection == 0 ? 1 : runtime.UniversalMovementDirection);
            FP64 runStep = tuning.MoveSpeed / FP64.FromInt(UniversalMovementRules.RunAccelerationFrames);

            switch (phase) {
                case UniversalMovementPhase.RollStartup:
                    fighter.Velocity.x = MoveToward(fighter.Velocity.x, FP64.Zero, runStep);
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.RollTravel;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollTravelFrames;
                    }
                    return true;

                case UniversalMovementPhase.RollTravel:
                    if (runtime.UniversalMovementFramesRemaining == UniversalMovementRules.RollTravelFrames) {
                        if (fighter.InvulnerabilityFrames < UniversalMovementRules.RollInvulnerabilityFrames) {
                            fighter.InvulnerabilityFrames = UniversalMovementRules.RollInvulnerabilityFrames;
                        }
                    }
                    fighter.Velocity.x = direction * tuning.MoveSpeed * speedMultiplier * RollSpeedMultiplier;
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.RollRecovery;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollRecoveryFrames;
                        fighter.Velocity.x = FP64.Zero;
                    }
                    return true;

                case UniversalMovementPhase.RollRecovery:
                    fighter.Velocity.x = MoveToward(fighter.Velocity.x, FP64.Zero, runStep);
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) FighterUniversalMovementRules.Cancel(ref runtime);
                    return true;

                default:
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    return false;
            }
        }

        private static void ApplyNormalMovement(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            bool rooted,
            FP64 statusMoveMultiplier,
            FP64 speedBuffMultiplier,
            FP64 jumpBuffMultiplier,
            bool groundIsSolid = false,
            bool lockHorizontal = false,
            bool lockFacing = false,
            bool allowJump = true) {
            // Only the block stance decelerates to zero (same ramp Story uses).
            // Swings — grounded or aerial — keep full input steering; facing
            // alone is committed through lockFacing.
            FP64 input = rooted || lockHorizontal ? FP64.Zero : FP64.FromInt(runtime.MoveX) / FP64.FromInt(127);
            FP64 maximumSpeed = tuning.MoveSpeed * statusMoveMultiplier * speedBuffMultiplier;
            FP64 targetSpeed = input * maximumSpeed;
            int accelerationFrames = fighter.IsGrounded != 0
                ? UniversalMovementRules.RunAccelerationFrames
                : 4;
            fighter.Velocity.x = MoveToward(
                fighter.Velocity.x,
                targetSpeed,
                maximumSpeed / FP64.FromInt(accelerationFrames));
            if (!lockFacing) {
                if (runtime.MoveX > 0) fighter.FacingRight = 1;
                else if (runtime.MoveX < 0) fighter.FacingRight = 0;
            }

            bool jumpPressed = (runtime.PressedButtons & JumpButton) != 0;
            bool downHeld = (runtime.HeldButtons & DownButton) != 0;
            bool onSolidBaseFloor = groundIsSolid && fighter.Position.y <= FP64.Zero;
            // Drop-through stays available while attacking or blocking, matching
            // Story's IsDropThroughAllowed states; plain jumps do not.
            if (jumpPressed && downHeld && fighter.IsGrounded != 0 && !rooted && !onSolidBaseFloor) {
                fighter.DropThroughFrames = 30;
                fighter.IsGrounded = 0;
                fighter.Velocity.y = FP64.FromInt(-2);
            } else if (allowJump && jumpPressed && fighter.IsGrounded != 0 && !rooted) {
                fighter.Velocity.y = tuning.JumpSpeed * statusMoveMultiplier * jumpBuffMultiplier;
                fighter.IsGrounded = 0;
                fighter.RemainingJumps = tuning.MaxJumpCount - 1;
            } else if (allowJump && jumpPressed && fighter.IsGrounded == 0 && fighter.RemainingJumps > 0 && !rooted) {
                fighter.Velocity.y = tuning.JumpSpeed * statusMoveMultiplier * jumpBuffMultiplier;
                fighter.RemainingJumps--;
            }
        }

        /// <summary>
        /// Advances the universal three-hit basic string
        /// (<see cref="FTT.Combat.BasicComboRules"/>) one tick: swing start, phase
        /// clock, input buffering, aerial landing cancel, and the recovery /
        /// chain-hold cancels. Held horizontal movement steers the attacker but
        /// never cancels the swing or resets the chain — the authored string
        /// pace is the only pace. Jump/roll/block (and a special or the
        /// ultimate, upstream) remain the cancel set.
        /// </summary>
        private static void ProcessBasicAttackPhase(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime) {
            bool basicPressed = (runtime.PressedButtons & BasicButton) != 0;
            int phase = runtime.AttackPhase;
            if (phase == FighterBasicAttackRules.PhaseNone) {
                if (basicPressed
                    && !FighterUniversalMovementRules.IsCombatLocked(in runtime)
                    && !FighterBasicAttackRules.IsBlockStance(in fighter, in runtime)) {
                    FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, 0);
                }
                return;
            }

            bool aerial = (runtime.AttackFlags & FighterBasicAttackRules.FlagAerial) != 0;
            if (aerial && fighter.IsGrounded != 0) {
                // Landing cancels an aerial string with no landing lag.
                FighterBasicAttackRules.CancelString(ref runtime);
                return;
            }

            if (basicPressed) {
                if (phase == FighterBasicAttackRules.PhaseChainHold) {
                    if (runtime.ComboIndex < FTT.Combat.BasicComboRules.ComboHits - 1) {
                        FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, runtime.ComboIndex + 1);
                    }
                    return;
                }
                if (phase != FighterBasicAttackRules.PhaseActive) {
                    runtime.AttackFlags |= FighterBasicAttackRules.FlagBuffered;
                }
            }

            if (phase is FighterBasicAttackRules.PhaseRecovery or FighterBasicAttackRules.PhaseChainHold) {
                // Jumping, rolling, or blocking cancels the recovery and
                // resets the chain (design 752 / 3080). Held movement does
                // not — it steers the swing without touching the string.
                bool cancels = (runtime.PressedButtons & (JumpButton | RollButton)) != 0
                    || (runtime.HeldButtons & BlockButton) != 0;
                if (cancels) {
                    FighterBasicAttackRules.CancelString(ref runtime);
                    return;
                }
            }

            runtime.AttackPhaseFrames--;
            if (runtime.AttackPhaseFrames > 0) return;
            int step = runtime.ComboIndex < 0 ? 0 : runtime.ComboIndex > 2 ? 2 : runtime.ComboIndex;
            switch (phase) {
                case FighterBasicAttackRules.PhaseStartup:
                    runtime.AttackPhase = FighterBasicAttackRules.PhaseActive;
                    runtime.AttackPhaseFrames = aerial
                        ? FTT.Combat.BasicComboRules.AerialActiveFrames[step]
                        : FTT.Combat.BasicComboRules.GroundActiveFrames[step];
                    break;
                case FighterBasicAttackRules.PhaseActive:
                    runtime.AttackPhase = FighterBasicAttackRules.PhaseRecovery;
                    runtime.AttackPhaseFrames = aerial
                        ? FTT.Combat.BasicComboRules.AerialRecoveryFrames[step]
                        : FTT.Combat.BasicComboRules.GroundRecoveryFrames[step];
                    break;
                case FighterBasicAttackRules.PhaseRecovery:
                    if ((runtime.AttackFlags & FighterBasicAttackRules.FlagBuffered) != 0
                        && step < FTT.Combat.BasicComboRules.ComboHits - 1) {
                        FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, step + 1);
                    } else if (step >= FTT.Combat.BasicComboRules.ComboHits - 1) {
                        // The finisher exits straight out; the chain resets.
                        FighterBasicAttackRules.CancelString(ref runtime);
                    } else {
                        runtime.AttackPhase = FighterBasicAttackRules.PhaseChainHold;
                        runtime.AttackPhaseFrames = FTT.Combat.BasicComboRules.ChainHoldFrames;
                        runtime.AttackFlags &= ~FighterBasicAttackRules.FlagBuffered;
                    }
                    break;
                default:
                    FighterBasicAttackRules.CancelString(ref runtime);
                    break;
            }
        }

        private static FP64 MoveToward(FP64 current, FP64 target, FP64 maximumDelta) {
            if (current < target) return FP64.Min(current + maximumDelta, target);
            if (current > target) return FP64.Max(current - maximumDelta, target);
            return target;
        }

        private static void TickCounters(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning) {
            if (fighter.HitstunFrames > 0) fighter.HitstunFrames--;
            if (fighter.DazeFrames > 0) fighter.DazeFrames--;
            if (fighter.HyperArmorFrames > 0) fighter.HyperArmorFrames--;
            if (fighter.DropThroughFrames > 0) fighter.DropThroughFrames--;
            if (runtime.BasicCooldownFrames > 0) runtime.BasicCooldownFrames--;
            // Block-charge regeneration, mirroring Story's BlockSystem: one charge
            // per interval, timer held at full while the stance is up and re-armed
            // when a charge is spent (FighterDamageRules resets it on block).
            if (fighter.BlockCharges >= tuning.MaxBlockCharges || fighter.Stocks <= 0) {
                runtime.BlockRegenFrames = 0;
            } else if (FighterBasicAttackRules.IsBlockStance(in fighter, in runtime)) {
                runtime.BlockRegenFrames = FTT.Combat.BasicComboRules.BlockChargeRegenFrames;
            } else if (runtime.BlockRegenFrames > 0) {
                runtime.BlockRegenFrames--;
                if (runtime.BlockRegenFrames == 0) fighter.BlockCharges++;
            } else {
                runtime.BlockRegenFrames = FTT.Combat.BasicComboRules.BlockChargeRegenFrames;
            }
            if (runtime.SpecialOneCooldownFrames > 0) runtime.SpecialOneCooldownFrames--;
            if (runtime.SpecialTwoCooldownFrames > 0) runtime.SpecialTwoCooldownFrames--;
            if (runtime.MovementCooldownFrames > 0) runtime.MovementCooldownFrames--;
            if (runtime.SpeedBuffFrames > 0) runtime.SpeedBuffFrames--;
            if (runtime.JumpBuffFrames > 0) runtime.JumpBuffFrames--;
            if (runtime.ZoneSpeedBonusFrames > 0) runtime.ZoneSpeedBonusFrames--;
            if (runtime.FloatFrames > 0) runtime.FloatFrames--;
            if (runtime.StatusFrames > 0) {
                runtime.StatusFrames--;
                if (runtime.StatusTickFrames > 0) runtime.StatusTickFrames--;
                if (runtime.StatusType == (int)FTT.Core.StatusType.Venom && runtime.StatusTickFrames <= 0) {
                    int scaledDamage = ScaleIntegerByFP(2, runtime.StatusIntensity);
                    int damage = scaledDamage > 1 ? scaledDamage : 1;
                    int remainingHP = fighter.CurrentHP - damage;
                    fighter.CurrentHP = remainingHP > 0 ? remainingHP : 0;
                    runtime.StatusTickFrames = 60;
                    if (fighter.CurrentHP <= 0) {
                        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, in tuning);
                        return;
                    }
                }
                if (runtime.StatusFrames == 0) {
                    runtime.StatusType = (int)FTT.Core.StatusType.None;
                    runtime.StatusIntensity = FP64.One;
                    runtime.StatusTickFrames = 0;
                }
            }
        }

        private static int ScaleIntegerByFP(int value, FP64 scale) {
            long numerator = (long)value * scale.RawValue + FP64.One.RawValue / 2;
            return (int)(numerator / FP64.One.RawValue);
        }
    }

    internal static class FighterUniversalMovementRules {
        public static bool IsCombatLocked(in FighterRuntimeComponent runtime) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            return phase is UniversalMovementPhase.RollStartup
                or UniversalMovementPhase.RollTravel
                or UniversalMovementPhase.RollRecovery;
        }

        public static void Cancel(ref FighterRuntimeComponent runtime) {
            runtime.UniversalMovementState = (int)UniversalMovementPhase.None;
            runtime.UniversalMovementFramesRemaining = 0;
            runtime.UniversalMovementDirection = 0;
        }
    }

    /// <summary>
    /// The deterministic half of the universal three-hit basic combo. All
    /// timings come from <see cref="FTT.Combat.BasicComboRules"/> — one shared
    /// rulebook with Story. The phase machine advances in
    /// <see cref="FighterMovementSystem"/> so movement locks and cancels resolve
    /// in the same tick they gate; <see cref="FighterCombatSystem"/> applies the
    /// hit during active frames.
    /// </summary>
    internal static class FighterBasicAttackRules {
        public const int PhaseNone = 0;
        public const int PhaseStartup = 1;
        public const int PhaseActive = 2;
        public const int PhaseRecovery = 3;
        public const int PhaseChainHold = 4;

        public const int FlagAerial = 1;
        public const int FlagHitResolved = 2;
        public const int FlagBuffered = 4;

        private const int BlockButton = 1 << 6;

        public static bool IsSwinging(in FighterRuntimeComponent runtime) =>
            runtime.AttackPhase is PhaseStartup or PhaseActive or PhaseRecovery;

        /// <summary>
        /// The grounded block stance, mirroring Story's Blocking state: grounded,
        /// free of hitstun/daze, not mid roll, not mid swing, holding Block.
        /// The movement lock, the attack/ability gates, and the shield-absorb
        /// rule in FighterDamageRules all key off this one predicate.
        /// </summary>
        public static bool IsBlockStance(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime) =>
            fighter.IsGrounded != 0
            && fighter.Stocks > 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            && fighter.RespawnFramesRemaining <= 0
            && runtime.UniversalMovementState == (int)UniversalMovementPhase.None
            && runtime.AttackPhase == PhaseNone
            && (runtime.HeldButtons & BlockButton) != 0;

        public static void CancelString(ref FighterRuntimeComponent runtime) {
            runtime.AttackPhase = PhaseNone;
            runtime.AttackPhaseFrames = 0;
            runtime.AttackFlags = 0;
            runtime.ComboIndex = 0;
        }

        public static void StartSwing(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            int comboStep) {
            bool aerial = fighter.IsGrounded == 0;
            runtime.ComboIndex = comboStep;
            runtime.AttackPhase = PhaseStartup;
            runtime.AttackFlags = aerial ? FlagAerial : 0;
            runtime.AttackPhaseFrames = aerial
                ? FTT.Combat.BasicComboRules.AerialStartupFrames[comboStep]
                : FTT.Combat.BasicComboRules.GroundStartupFrames[comboStep];
            // Defensive: universal movement is provably None at every current call
            // site (IsCombatLocked gates fresh swings, and no roll can start while
            // a string is active), so this cancel is a no-op today. It stays so a
            // future cancel window cannot leave a phase running under a swing.
            FighterUniversalMovementRules.Cancel(ref runtime);
            // Legacy "busy" mirror for observers (HUD, CPU pacing): the remaining
            // swing length. No gameplay system reads it any more.
            runtime.BasicCooldownFrames = aerial
                ? FTT.Combat.BasicComboRules.AerialStartupFrames[comboStep]
                    + FTT.Combat.BasicComboRules.AerialActiveFrames[comboStep]
                    + FTT.Combat.BasicComboRules.AerialRecoveryFrames[comboStep]
                : FTT.Combat.BasicComboRules.GroundStartupFrames[comboStep]
                    + FTT.Combat.BasicComboRules.GroundActiveFrames[comboStep]
                    + FTT.Combat.BasicComboRules.GroundRecoveryFrames[comboStep];
        }
    }

    /// <summary>
    /// Deterministic horizontal jostling. Roll travel explicitly bypasses the
    /// fighter pushbox.
    /// </summary>
    public sealed class FighterPushboxSystem : ISystem {
        public static readonly FP64 MinimumHorizontalDistance = FP64.FromDouble(0.8);
        private static readonly FP64 MaximumVerticalDistance = FP64.FromDouble(1.6);

        private readonly FP64 LeftWall;
        private readonly FP64 RightWall;

        public FighterPushboxSystem(FighterStageGeometry geometry = null) {
            geometry ??= FighterStageGeometry.Default;
            LeftWall = geometry.LeftWall;
            RightWall = geometry.RightWall;
        }

        public void Update(ref Frame frame) {
            EntityRef firstEntity = default;
            EntityRef secondEntity = default;
            bool foundFirst = false;
            bool foundSecond = false;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == 0) { firstEntity = entity; foundFirst = true; }
                else if (fighter.PlayerID == 1) { secondEntity = entity; foundSecond = true; }
            }
            if (!foundFirst || !foundSecond) return;

            ref FighterStateComponent first = ref frame.Get<FighterStateComponent>(firstEntity);
            ref FighterStateComponent second = ref frame.Get<FighterStateComponent>(secondEntity);
            ref FighterRuntimeComponent firstRuntime = ref frame.Get<FighterRuntimeComponent>(firstEntity);
            ref FighterRuntimeComponent secondRuntime = ref frame.Get<FighterRuntimeComponent>(secondEntity);
            if (first.Stocks <= 0 || second.Stocks <= 0) return;
            // A fighter held by the respawn platform is pinned by the movement
            // system; jostling it would fight that pin for a frame.
            if (FighterMatchFlowRules.IsOnRespawnPlatform(in first)
                || FighterMatchFlowRules.IsOnRespawnPlatform(in second)) return;
            if (IsRollTravel(in firstRuntime) || IsRollTravel(in secondRuntime)) return;
            if (FP64.Abs(first.Position.y - second.Position.y) >= MaximumVerticalDistance) return;

            FP64 delta = second.Position.x - first.Position.x;
            FP64 distance = FP64.Abs(delta);
            if (distance >= MinimumHorizontalDistance) return;
            FP64 overlap = MinimumHorizontalDistance - distance;

            bool firstIsLeft = delta > FP64.Zero || (delta == FP64.Zero && first.PlayerID < second.PlayerID);
            ref FighterStateComponent left = ref (firstIsLeft ? ref first : ref second);
            ref FighterStateComponent right = ref (firstIsLeft ? ref second : ref first);

            FP64 half = overlap / FP64.FromInt(2);
            FP64 leftSpace = left.Position.x - LeftWall;
            FP64 rightSpace = RightWall - right.Position.x;
            FP64 leftMove = FP64.Min(half, FP64.Max(FP64.Zero, leftSpace));
            FP64 rightMove = FP64.Min(half, FP64.Max(FP64.Zero, rightSpace));
            FP64 remaining = overlap - leftMove - rightMove;
            if (remaining > FP64.Zero) {
                FP64 extraRight = FP64.Min(remaining, FP64.Max(FP64.Zero, rightSpace - rightMove));
                rightMove += extraRight;
                remaining -= extraRight;
            }
            if (remaining > FP64.Zero) {
                leftMove += FP64.Min(remaining, FP64.Max(FP64.Zero, leftSpace - leftMove));
            }

            left.Position.x -= leftMove;
            right.Position.x += rightMove;
            if (left.Velocity.x > FP64.Zero) left.Velocity.x = FP64.Zero;
            if (right.Velocity.x < FP64.Zero) right.Velocity.x = FP64.Zero;
        }

        private static bool IsRollTravel(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == (int)UniversalMovementPhase.RollTravel;
    }

    public sealed class FighterCombatSystem : ISystem {
        private const int BasicButton = 1 << 2;
        private const int SpecialOneButton = 1 << 3;
        private const int SpecialTwoButton = 1 << 4;
        private const int UltimateButton = 1 << 7;
        // Joan's Divine Piercing (design Section 5): the rapid thrusts shred
        // exactly 2 block charges on block instead of the special-class full
        // shatter. The multi-hit damage total is baked by FighterLoadoutFactory.
        private const int DivinePiercingBlockChargeCost = 2;
        private static readonly FP64 AttackRange = FP64.FromInt(2);
        private static readonly FP64 AttackVerticalRange = FP64.FromDouble(1.6);
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);
        // The shared per-hit knockback table (BasicComboRules.KnockbackMultipliers),
        // pre-converted once to fixed point. FromDouble of a process-constant is
        // deterministic; no float math runs per tick.
        private static readonly FP64[] BasicKnockbackMultipliers = {
            FP64.FromDouble(FTT.Combat.BasicComboRules.KnockbackMultipliers[0]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.KnockbackMultipliers[1]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.KnockbackMultipliers[2])
        };

        public void Update(ref Frame frame) {
            EntityRef first = default;
            EntityRef second = default;
            bool foundFirst = false;
            bool foundSecond = false;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == 0) { first = entity; foundFirst = true; }
                else if (fighter.PlayerID == 1) { second = entity; foundSecond = true; }
            }
            if (!foundFirst || !foundSecond) return;

            ref FighterStateComponent fighterOne = ref frame.Get<FighterStateComponent>(first);
            ref FighterStateComponent fighterTwo = ref frame.Get<FighterStateComponent>(second);
            ref FighterRuntimeComponent runtimeOne = ref frame.Get<FighterRuntimeComponent>(first);
            ref FighterRuntimeComponent runtimeTwo = ref frame.Get<FighterRuntimeComponent>(second);
            ref readonly FighterTuningComponent tuningOne = ref frame.GetReadOnly<FighterTuningComponent>(first);
            ref readonly FighterTuningComponent tuningTwo = ref frame.GetReadOnly<FighterTuningComponent>(second);
            // Bespoke character ultimates dispatch first (X7): a successful
            // dispatch consumes the meter, so the generic melee ultimate intent
            // below cannot double-fire on the same press.
            TryCharacterUltimate(ref frame, first, second, ref fighterOne, ref runtimeOne, in tuningOne);
            TryCharacterUltimate(ref frame, second, first, ref fighterTwo, ref runtimeTwo, in tuningTwo);

            AttackIntent firstIntent = BuildIntent(in fighterOne, in runtimeOne, in tuningOne, in fighterTwo);
            AttackIntent secondIntent = BuildIntent(in fighterTwo, in runtimeTwo, in tuningTwo, in fighterOne);
            ApplyIntent(ref fighterOne, ref runtimeOne, ref fighterTwo, ref runtimeTwo, in tuningTwo, in firstIntent);
            ApplyIntent(ref fighterTwo, ref runtimeTwo, ref fighterOne, ref runtimeOne, in tuningOne, in secondIntent);
            ApplyBasicSwing(ref fighterOne, ref runtimeOne, in tuningOne, ref fighterTwo, ref runtimeTwo, in tuningTwo);
            ApplyBasicSwing(ref fighterTwo, ref runtimeTwo, in tuningTwo, ref fighterOne, ref runtimeOne, in tuningOne);
        }

        /// <summary>
        /// Applies the basic string's hit during its active window. One attempt
        /// per swing — Story's hitbox also connects at most once per activation —
        /// whether it lands, is blocked, or meets invulnerability.
        /// </summary>
        private static void ApplyBasicSwing(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent attackerTuning,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            in FighterTuningComponent targetTuning) {
            if (attackerRuntime.AttackPhase != FighterBasicAttackRules.PhaseActive) return;
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0) return;
            if (attacker.Stocks <= 0) return;
            if (FP64.Abs(target.Position.x - attacker.Position.x) > AttackRange) return;
            if (FP64.Abs(target.Position.y - attacker.Position.y) > AttackVerticalRange) return;

            int step = attackerRuntime.ComboIndex < 0 ? 0 : attackerRuntime.ComboIndex > 2 ? 2 : attackerRuntime.ComboIndex;
            int damage = step == 0
                ? attackerTuning.BasicDamage * 8 / 10
                : step == 1
                    ? attackerTuning.BasicDamage
                    : attackerTuning.BasicDamage * 15 / 10;
            // Shared knockback table (1.0x / 1.2x / 3.0x): the finisher launches.
            FP64 knockback = attackerTuning.BasicKnockback * BasicKnockbackMultipliers[step];
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            FighterDamageRules.ApplyFighterHit(
                ref attacker,
                ref attackerRuntime,
                ref target,
                ref targetRuntime,
                in targetTuning,
                FighterDamageRules.BasicAttackClass,
                damage,
                knockback,
                FTT.Combat.BasicComboRules.HitstunFrames[step],
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                attacker.Position.x,
                true,
                0);
        }

        private static void TryCharacterUltimate(
            ref Frame frame,
            EntityRef attackerEntity,
            EntityRef targetEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning) {
            if ((attackerRuntime.PressedButtons & UltimateButton) == 0
                || attacker.Influence < MaxInfluence
                || attacker.HitstunFrames > 0
                || attacker.DazeFrames > 0
                || attacker.Stocks <= 0
                || FighterUniversalMovementRules.IsCombatLocked(in attackerRuntime)) return;

            if (FighterUltimateRules.TryExecute(
                    ref frame, attackerEntity, targetEntity, ref attacker, ref attackerRuntime, in tuning)) {
                FighterUniversalMovementRules.Cancel(ref attackerRuntime);
                // The ultimate cancels an in-progress basic and resets the chain.
                FighterBasicAttackRules.CancelString(ref attackerRuntime);
                attacker.Influence = FP64.Zero;
            }
        }

        private static AttackIntent BuildIntent(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning,
            in FighterStateComponent target) {
            if (attacker.HitstunFrames > 0
                || attacker.DazeFrames > 0
                || attacker.Stocks <= 0
                || FighterUniversalMovementRules.IsCombatLocked(in attackerRuntime)) return default;
            // The block stance ignores attack inputs, exactly as Story's Blocking
            // state does.
            if (FighterBasicAttackRules.IsBlockStance(in attacker, in attackerRuntime)) return default;
            if (FP64.Abs(target.Position.x - attacker.Position.x) > AttackRange) return default;

            if ((attackerRuntime.PressedButtons & UltimateButton) != 0 && attacker.Influence >= MaxInfluence) {
                return new AttackIntent(
                    3,
                    tuning.UltimateDamage,
                    tuning.UltimateKnockback,
                    30,
                    tuning.UltimateStatusType,
                    tuning.UltimateStatusFrames,
                    tuning.UltimateStatusIntensity);
            }
            if ((attackerRuntime.PressedButtons & SpecialOneButton) != 0 && attackerRuntime.SpecialOneCooldownFrames <= 0) {
                return new AttackIntent(
                    2,
                    tuning.SpecialOneDamage,
                    tuning.SpecialOneKnockback,
                    18,
                    tuning.SpecialOneStatusType,
                    tuning.SpecialOneStatusFrames,
                    tuning.SpecialOneStatusIntensity,
                    tuning.SpecialOneCooldownFrames);
            }
            if ((attackerRuntime.PressedButtons & SpecialTwoButton) != 0 && attackerRuntime.SpecialTwoCooldownFrames <= 0) {
                return new AttackIntent(
                    4,
                    tuning.SpecialTwoDamage,
                    tuning.SpecialTwoKnockback,
                    18,
                    tuning.SpecialTwoStatusType,
                    tuning.SpecialTwoStatusFrames,
                    tuning.SpecialTwoStatusIntensity,
                    tuning.SpecialTwoCooldownFrames,
                    attacker.CharacterID == (int)FighterCharacterID.Joan
                        ? DivinePiercingBlockChargeCost
                        : 0);
            }
            // Basic attacks no longer resolve here: the phase machine in
            // FighterMovementSystem starts and times the swing, and
            // ApplyBasicSwing lands the hit during its active window.
            return default;
        }

        private static void ApplyIntent(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            in FighterTuningComponent targetTuning,
            in AttackIntent intent) {
            if (intent.Kind == 0) return;

            FighterUniversalMovementRules.Cancel(ref attackerRuntime);
            // A special or the ultimate cancels an in-progress basic at any point
            // and resets the chain (design 1051) — the same rule Story applies.
            FighterBasicAttackRules.CancelString(ref attackerRuntime);

            if (intent.Kind == 2) {
                attackerRuntime.SpecialOneCooldownFrames = intent.CooldownFrames > 1 ? intent.CooldownFrames : 1;
            } else if (intent.Kind == 4) {
                attackerRuntime.SpecialTwoCooldownFrames = intent.CooldownFrames > 1 ? intent.CooldownFrames : 1;
            } else {
                attacker.Influence = FP64.Zero;
            }

            int attackClass = intent.Kind == 1
                ? FighterDamageRules.BasicAttackClass
                : intent.Kind == 3
                    ? FighterDamageRules.UltimateAttackClass
                    : FighterDamageRules.SpecialAttackClass;
            FighterDamageRules.ApplyFighterHit(
                ref attacker,
                ref attackerRuntime,
                ref target,
                ref targetRuntime,
                in targetTuning,
                attackClass,
                intent.Damage,
                intent.Knockback,
                intent.HitstunFrames,
                intent.StatusType,
                intent.StatusFrames,
                intent.StatusIntensity,
                attacker.Position.x,
                true,
                intent.BlockChargeCost);
        }

        private readonly struct AttackIntent {
            public readonly int Kind;
            public readonly int Damage;
            public readonly FP64 Knockback;
            public readonly int HitstunFrames;
            public readonly int StatusType;
            public readonly int StatusFrames;
            public readonly FP64 StatusIntensity;
            public readonly int CooldownFrames;
            public readonly int BlockChargeCost;

            public AttackIntent(
                int kind,
                int damage,
                FP64 knockback,
                int hitstunFrames,
                int statusType = (int)FTT.Core.StatusType.None,
                int statusFrames = 0,
                FP64 statusIntensity = default,
                int cooldownFrames = 600,
                int blockChargeCost = 0) {
                Kind = kind;
                Damage = damage;
                Knockback = knockback;
                HitstunFrames = hitstunFrames;
                StatusType = statusType;
                StatusFrames = statusFrames;
                StatusIntensity = statusIntensity;
                CooldownFrames = cooldownFrames;
                BlockChargeCost = blockChargeCost;
            }
        }
    }

    public sealed class FighterMatchSystem : ISystem {
        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1) return;

            if (match.MatchMode != (int)FTT.Core.MatchMode.Stock) match.RemainingFrames--;
            TrackKnockouts(ref frame, ref match);
            int winner = FindStockWinner(ref frame, out bool allAlive, out bool trueTie);
            bool shouldEnd = match.MatchMode switch {
                (int)FTT.Core.MatchMode.Stock => !allAlive,
                (int)FTT.Core.MatchMode.TimeLimit => match.RemainingFrames <= 0,
                _ => !allAlive || match.RemainingFrames <= 0
            };
            if (shouldEnd) {
                if (match.MatchMode == (int)FTT.Core.MatchMode.TimeLimit) {
                    trueTie = match.PlayerOneKOs == match.PlayerTwoKOs;
                    winner = trueTie ? -1 : match.PlayerOneKOs > match.PlayerTwoKOs ? 0 : 1;
                }
                match.MatchState = 2;
                match.WinnerPlayerID = winner;
                match.IsTrueTie = trueTie ? 1 : 0;
            }
        }

        private static void TrackKnockouts(ref Frame frame, ref FighterMatchComponent match) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                ref readonly FighterRuntimeComponent runtime = ref frame.GetReadOnly<FighterRuntimeComponent>(entity);
                if (fighter.PlayerID == 0) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerOneKnockoutsSuffered;
                    if (gained > 0) match.PlayerTwoKOs += gained;
                    match.LastPlayerOneKnockoutsSuffered = runtime.KnockoutsSuffered;
                } else if (fighter.PlayerID == 1) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerTwoKnockoutsSuffered;
                    if (gained > 0) match.PlayerOneKOs += gained;
                    match.LastPlayerTwoKnockoutsSuffered = runtime.KnockoutsSuffered;
                }
            }
        }

        private static int FindStockWinner(ref Frame frame, out bool allAlive, out bool trueTie) {
            FighterStateComponent first = default;
            FighterStateComponent second = default;
            var filter = frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == 0) first = fighter;
                else if (fighter.PlayerID == 1) second = fighter;
            }

            allAlive = first.Stocks > 0 && second.Stocks > 0;
            if (first.Stocks != second.Stocks) {
                trueTie = false;
                return first.Stocks > second.Stocks ? 0 : 1;
            }

            long firstHP = (long)first.CurrentHP * second.MaxHP;
            long secondHP = (long)second.CurrentHP * first.MaxHP;
            trueTie = firstHP == secondHP;
            return trueTie ? -1 : firstHP > secondHP ? 0 : 1;
        }
    }

    internal static class FighterSimulationRules {
        public static void ApplyStockLoss(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning) {
            if (fighter.Stocks <= 0) return;
            runtime.KnockoutsSuffered++;
            if (runtime.UsesStocks != 0) fighter.Stocks--;
            fighter.Influence = fighter.Influence * FP64.FromInt(3) / FP64.FromInt(4);
            fighter.CurrentHP = fighter.MaxHP;
            fighter.BlockCharges = tuning.MaxBlockCharges;
            fighter.RemainingJumps = tuning.MaxJumpCount;
            // Chronal Respawn Platform, all match modes: the fighter materialises
            // frozen and invulnerable at stage centre +3.0 rather than teleporting
            // straight back into play. The 3 s spawn invulnerability is armed when
            // the platform drops them, not here.
            fighter.Position = FighterMatchFlowRules.RespawnPlatformPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 1;
            if (fighter.Stocks > 0) {
                fighter.RespawnFramesRemaining = FighterMatchFlowRules.RespawnPlatformFrames;
                fighter.InvulnerabilityFrames =
                    FighterMatchFlowRules.RespawnPlatformFrames + FighterMatchFlowRules.RespawnInvulnerabilityFrames;
            } else {
                // Out of stocks: no platform, no respawn. The match resolves this frame.
                fighter.RespawnFramesRemaining = 0;
                fighter.InvulnerabilityFrames = FighterMatchFlowRules.RespawnInvulnerabilityFrames;
            }
            fighter.HitstunFrames = 0;
            fighter.DazeFrames = 0;
            runtime.StatusType = (int)FTT.Core.StatusType.None;
            runtime.StatusFrames = 0;
            runtime.StatusTickFrames = 0;
            runtime.StatusIntensity = FP64.One;
            FighterUniversalMovementRules.Cancel(ref runtime);
            // Stock loss ends any swing and resets the chain and shield regen.
            FighterBasicAttackRules.CancelString(ref runtime);
            runtime.BlockRegenFrames = 0;
        }
    }
}
