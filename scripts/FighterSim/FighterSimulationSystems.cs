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
                TimerEnabled = _matchFrames > 0 ? 1 : 0,
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
                DamageStatusIntensity = FP64.One,
                UsesStocks = _rules.MatchMode == (int)FTT.Core.MatchMode.TimeLimit ? 0 : 1,
                // Klotho zero-initializes components, and 0 is a valid anchor
                // (platform 0's left edge), so "not hanging" must be written in.
                LedgeAnchor = FighterLedgeRules.NoAnchor
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
                UltimateStatusIntensity = loadout.UltimateStatusIntensity,
                AirControl = loadout.AirControl > FP64.Zero ? loadout.AirControl : FP64.One
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
            // V7/V7.1/V7.2 verb-layer state (hitstop, DI, tech, Rally, Defy,
            // Echo Step, Momentum, grabs) — the new snapshot components the
            // design mandates (ID 310+).
            frame.Add(entity, new FighterVerbComponent());
            frame.Add(entity, new FighterEchoRingComponent {
                Sample0X = spawn.x, Sample0Y = spawn.y,
                Sample1X = spawn.x, Sample1Y = spawn.y,
                Sample2X = spawn.x, Sample2Y = spawn.y,
                Sample3X = spawn.x, Sample3Y = spawn.y,
                Sample4X = spawn.x, Sample4Y = spawn.y,
                SampleCountdown = 6
            });
            // V7.6 F07 (Package 11 A1): the caster-owned Conductive mark. Not a
            // status — no slot, no action lock, zero stagger budget.
            frame.Add(entity, new FighterConductiveComponent {
                FramesRemaining = 0,
                SourcePlayerID = -1,
                ChainConsumedExecutionID = 0
            });
            // V7.6 D01-D04 (Package 11 A1b): the defensive layer - Defy
            // protected recovery, the Temporal Aegis flag and the D02b HP
            // barrier. Snapshot and hash state like every other component.
            frame.Add(entity, new FighterDefenseComponent {
                DefyProtectionAwaitControl = 0,
                DefyProtectionFrames = 0,
                DefyProcIdentity = 0,
                AegisActive = 0,
                BarrierPoints = 0,
                BarrierCapacity = 0,
                BarrierRemainingFrames = 0,
                BarrierEffectID = 0
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
        /// <summary>Signed floor for fast-fall: -<c>UniversalMovementRules.FastFallSpeed</c>.</summary>
        private static readonly FP64 FastFallSpeed = -FP64.FromDouble(UniversalMovementRules.FastFallSpeed);
        private static readonly FP64 RollSpeedMultiplier = FP64.FromDouble(UniversalMovementRules.RollSpeedMultiplier);

        /// <summary>
        /// The tick length and free-fall acceleration, exposed read-only so an
        /// input-side consumer — the CPU's pit-risk trajectory probe (Package 11
        /// A9) — integrates exactly the way the simulation does instead of keeping
        /// a second copy of either number.
        /// </summary>
        internal static FP64 FixedDeltaSeconds => FixedDelta;

        /// <inheritdoc cref="FixedDeltaSeconds"/>
        internal static FP64 GravityPerSecondSquared => Gravity;

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
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(entity);

                // V7 universal hitstop: both parties of a hit are fully suspended
                // — velocity, position, phases, and every per-fighter timer. The
                // world (projectiles, constructs, hazards, the match clock) keeps
                // running. DI resolves on the frame the freeze ends.
                if (verb.HitstopFrames > 0) {
                    verb.HitstopFrames--;
                    if (verb.HitstopFrames == 0) {
                        FighterVerbRules.ResolvePendingLaunch(ref fighter, in runtime, ref verb);
                    }
                    continue;
                }
                if (fighter.HitstunFrames <= 0) {
                    verb.Tumble = 0;
                    // Hitstun over: the hit-2 block-cancel gate re-opens and any
                    // stashed launch is dead — it must never replay under a
                    // later hit's hitstop (V7.3).
                    verb.HitstunBlockCancelBlocked = 0;
                    verb.PendingLaunchActive = 0;
                }

                // V7.1 Echo Step bookkeeping: sample the position ring and
                // advance an armed wind-up (the snap fires when it reaches 0).
                AdvanceEchoStep(ref frame, entity, ref fighter, ref verb);

                // V7.6 D01-D04 (Package 11 A1b): the Venom tick inside
                // TickCounters routes through the defensive layer, so the
                // component travels with it.
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(entity);

                // Landing-tech recovery: invulnerable, in place, no actions.
                if (verb.TechLockoutFrames > 0) {
                    verb.TechLockoutFrames--;
                    fighter.Velocity = FPVector2.Zero;
                    TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning);
                    continue;
                }

                // V7.2 grabs: a grabbing or held fighter is rooted — no input
                // processing, no movement. Cooldowns and statuses keep ticking
                // (no pausing on stun applies here too); the combat system owns
                // the grab phases and the victim's pinning.
                if (FighterGrabRules.IsBusy(in verb)) {
                    fighter.Velocity = FPVector2.Zero;
                    TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning);
                    continue;
                }

                TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning);

                // The Chronal Respawn Platform owns the fighter completely: it is
                // frozen, invulnerable, and consumes no input until it drops.
                if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) {
                    ProcessRespawnPlatform(
                        ref fighter, ref runtime, _geometry.RespawnPlatformPosition);
                    continue;
                }

                if (fighter.InvulnerabilityFrames > 0) fighter.InvulnerabilityFrames--;

                // Gameplay-feel plan §2.11 — a hanging fighter is short-circuited
                // exactly the way hitstun short-circuits input: no gravity, no
                // movement, no attack phase, no pushbox. Jump climbs, Down drops,
                // and the hang auto-releases at five seconds; whichever ends it,
                // the resulting velocity is integrated on the next tick.
                if (FighterLedgeRules.IsHanging(in runtime)) {
                    if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) {
                        // Struck off the ledge. The hang ends without zeroing
                        // velocity — the combat system already wrote the knockback
                        // — and the lockout stops an instant regrab at the anchor
                        // the fighter is still standing in.
                        FighterLedgeRules.ClearHang(ref runtime);
                        runtime.LedgeRegrabLockoutFrames = FighterLedgeRules.RegrabLockoutFrames;
                    } else if (_geometry.TryGetHangPosition(runtime.LedgeAnchor, out FPVector2 hangPosition)) {
                        FighterLedgeRules.Process(ref fighter, ref runtime, in tuning, in hangPosition);
                        continue;
                    } else {
                        // Defensive: an anchor that no longer resolves (a stage
                        // swap under a restored snapshot) drops the hang rather
                        // than pinning the fighter to a platform that is not there.
                        FighterLedgeRules.ClearHang(ref runtime);
                        continue;
                    }
                }

                // Gameplay-feel plan §2.4 — Block cancels hitstun. A *grounded*
                // victim holding Block leaves hitstun immediately and flows into
                // the normal stance through IsBlockStance on this same tick.
                // Daze (the guard-break punish window) is never cancelable, and
                // an airborne victim cannot block at all, so neither escapes.
                // V7.3 hit-2 gate: string hit 1 arms HitstunBlockCancelBlocked —
                // only after hit two connects may Block escape — and a stance
                // the fighter could not raise (no charges, shatter lockout)
                // cannot be escaped into either.
                if (fighter.HitstunFrames > 0
                    && fighter.DazeFrames <= 0
                    && fighter.IsGrounded != 0
                    && fighter.Stocks > 0
                    && verb.HitstunBlockCancelBlocked == 0
                    && fighter.BlockCharges > 0
                    && verb.BlockLockoutFrames <= 0
                    && (runtime.HeldButtons & BlockButton) != 0) {
                    fighter.HitstunFrames = 0;
                }
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
                    // V7.1 Echo Step: checked BEFORE the phase machine, while
                    // the swing is still in its recovery frames — the same
                    // Block+Roll chord would otherwise be eaten by the string's
                    // block/roll cancel one line later.
                    TryStartEchoStep(ref frame, entity, ref fighter, ref runtime, ref verb);
                    // The basic-combo phase machine advances before movement so its
                    // locks and cancels gate the same tick's movement, mirroring how
                    // Story resolves both inside one _PhysicsProcess.
                    ProcessBasicAttackPhase(ref fighter, ref runtime, in verb);
                    bool attacking = runtime.AttackPhase != FighterBasicAttackRules.PhaseNone;
                    bool blockStance = FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);
                    // V7.3 shieldstun: the stunned blocker is locked into the
                    // stance — no roll, no jump, no drop-through out of it.
                    bool shieldStunned = verb.ShieldStunFrames > 0;
                    TryStartUniversalMovement(
                        ref fighter,
                        ref runtime,
                        rooted,
                        // An Echo Step wind-up owns the Roll press that armed it.
                        allowRoll: !attacking && verb.EchoStepWindupFrames == 0 && !shieldStunned);
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
                            // "Drop inputs are ignored on the main floor" needs a
                            // per-x answer now that the floor can have holes: the
                            // rule applies where the stage HAS a solid floor and
                            // the fighter is standing on it. Over a pit there is
                            // nothing to drop through, so the branch must not fire
                            // either way. The legacy flat arena authors neither
                            // platforms nor segments and keeps its historical
                            // drop-through ground.
                            groundIsSolid: StageHasSolidFloorRule
                                && _geometry.HasFloorSupport(fighter.Position.x),
                            // Swings never lock steering — an attacker keeps
                            // full horizontal control at normal run
                            // acceleration; only the block stance roots.
                            // Facing follows the held direction through a swing
                            // too (gameplay-feel plan §2.12, superseding the
                            // 2026-08-09 "committed for the whole string"
                            // decision); hitbox placement still reads facing at
                            // active-start, so there is no mid-active migration.
                            lockHorizontal: blockStance,
                            lockFacing: blockStance,
                            allowJump: !attacking && !blockStance,
                            allowDropThrough: !shieldStunned);
                    }
                    if (fighter.IsGrounded == 0) {
                        // Fast-fall (§2.9, 2026-08-10): a stateless rule derived
                        // from held input every tick — no snapshot field. Holding
                        // Down in the air cancels the warp float window and pins
                        // vertical velocity to at least FastFallSpeed downward.
                        // Hitstun and daze are handled by the branch above, which
                        // never reaches here; the guard is kept explicit so the
                        // rule survives a future restructure of the gate.
                        bool fastFalling = (runtime.HeldButtons & DownButton) != 0
                            && fighter.HitstunFrames <= 0
                            && fighter.DazeFrames <= 0;
                        if (fastFalling) runtime.FloatFrames = 0;
                        // Float glide (post-warp cancel) heavily reduces gravity.
                        FP64 gravityStep = runtime.FloatFrames > 0
                            ? Gravity * FixedDelta / FP64.FromInt(5)
                            : Gravity * FixedDelta;
                        fighter.Velocity.y += gravityStep;
                        if (fastFalling && fighter.Velocity.y > FastFallSpeed) {
                            fighter.Velocity.y = FastFallSpeed;
                        }
                    }
                }

                FP64 previousY = fighter.Position.y;
                bool wasAirborne = fighter.IsGrounded == 0;
                fighter.Position += fighter.Velocity * FixedDelta;
                fighter.Position.x = FP64.Clamp(fighter.Position.x, _geometry.LeftWall, _geometry.RightWall);
                if (fighter.Position.y > _geometry.Ceiling) {
                    fighter.Position.y = _geometry.Ceiling;
                    if (fighter.Velocity.y > FP64.Zero) fighter.Velocity.y = FP64.Zero;
                }

                // Walking off an edge removes ground support — a one-way platform
                // end above the floor plane, or (Package 11 A9) the end of a main
                // floor segment on an Open stage. An unbroken floor supports every
                // x, so the Sealed stages never take the second branch.
                if (fighter.IsGrounded != 0 && !HasGroundSupport(in fighter)) {
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
                    ref FighterDefenseComponent fallDefense = ref frame.Get<FighterDefenseComponent>(entity);
                    FighterSimulationRules.ApplyStockLoss(
                        ref fighter, ref runtime, ref verb, ref fallDefense, in tuning, _geometry);

                    continue;
                }

                // Stages with authored platforms have a solid base floor; only the
                // legacy flat arena keeps its historical drop-through ground.
                // Package 11 A9: the snap is per-x now. Over an authored pit there
                // is no floor to snap to, so the fighter keeps falling toward the
                // blast zone — which the branch above already resolved this tick,
                // so the ordering is right.
                bool hasFloorHere = _geometry.HasFloorSupport(fighter.Position.x);
                bool groundIsSolid = hasFloorHere && StageHasSolidFloorRule;
                if (hasFloorHere
                    && (groundIsSolid || fighter.DropThroughFrames <= 0)
                    && fighter.Position.y <= FP64.Zero) {
                    fighter.Position.y = FP64.Zero;
                    if (fighter.Velocity.y < FP64.Zero) fighter.Velocity.y = FP64.Zero;
                    fighter.IsGrounded = 1;
                    fighter.RemainingJumps = tuning.MaxJumpCount;
                }

                // Landing tech (V7 pillar #4): a launched victim who holds Block
                // on the frame they touch down techs — hitstun ends and a short
                // invulnerable in-place recovery replaces the knockdown ride-out.
                if (wasAirborne && fighter.IsGrounded != 0) {
                    FighterVerbRules.TryLandingTech(ref fighter, in runtime, ref verb);
                }

                // V7.3 regrab cap: grounding resets the per-airtime ledge budget.
                if (fighter.IsGrounded != 0) {
                    verb.LedgeGrabsThisAirtime = 0;
                }

                // §2.11 ledge capture, resolved last so landing and the ground snap
                // both win: a fighter who reached a surface is standing on it, not
                // hanging off it. Only fighters still airborne after the whole
                // resolve are candidates.
                TryGrabLedge(ref fighter, ref runtime, ref verb, in tuning);
            }

            ResolveLedgeTrump(ref frame);
        }

        /// <summary>
        /// V7.3 ledge trump, resolved once per tick after every fighter has
        /// moved and grabbed: when both fighters hold the same anchor, the
        /// EARLIER hanger (larger LedgeStateFrames) is forced off through
        /// <see cref="FighterLedgeRules.Release"/>, which arms the 30-frame
        /// regrab lockout. A same-frame tie (both grabbed this tick, both at
        /// zero LedgeStateFrames) keeps the lower PlayerID — deterministic and
        /// independent of iteration order.
        /// </summary>
        private static void ResolveLedgeTrump(ref Frame frame) {
            EntityRef firstEntity = default;
            EntityRef secondEntity = default;
            bool foundFirst = false;
            bool foundSecond = false;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterRuntimeComponent runtime = ref frame.GetReadOnly<FighterRuntimeComponent>(entity);
                if (!FighterLedgeRules.IsHanging(in runtime)) continue;
                if (!foundFirst) { firstEntity = entity; foundFirst = true; }
                else { secondEntity = entity; foundSecond = true; }
            }
            if (!foundSecond) return;

            ref FighterRuntimeComponent firstRuntime = ref frame.Get<FighterRuntimeComponent>(firstEntity);
            ref FighterRuntimeComponent secondRuntime = ref frame.Get<FighterRuntimeComponent>(secondEntity);
            if (firstRuntime.LedgeAnchor != secondRuntime.LedgeAnchor) return;

            ref FighterStateComponent first = ref frame.Get<FighterStateComponent>(firstEntity);
            ref FighterStateComponent second = ref frame.Get<FighterStateComponent>(secondEntity);
            bool releaseFirst = firstRuntime.LedgeStateFrames != secondRuntime.LedgeStateFrames
                ? firstRuntime.LedgeStateFrames > secondRuntime.LedgeStateFrames
                : first.PlayerID > second.PlayerID;
            if (releaseFirst) {
                FighterLedgeRules.Release(ref first, ref firstRuntime);
            } else {
                FighterLedgeRules.Release(ref second, ref secondRuntime);
            }
        }

        /// <summary>
        /// Catches a platform end when the fighter's state and position both allow
        /// it (gameplay-feel plan §2.11). The legacy flat arena has no platforms and
        /// therefore no ledges, which <see cref="FighterStageGeometry.TryFindLedge"/>
        /// handles by finding nothing.
        /// </summary>
        private void TryGrabLedge(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            in FighterTuningComponent tuning) {
            if (!FighterLedgeRules.CanGrab(in fighter, in runtime, in verb)) return;
            if (!_geometry.TryFindLedge(in fighter.Position, out int anchor)) return;
            if (!_geometry.TryGetHangPosition(anchor, out FPVector2 hangPosition)) return;
            FighterLedgeRules.Grab(ref fighter, ref runtime, ref verb, in tuning, anchor, in hangPosition);
        }

        /// <summary>
        /// Chronal Respawn Platform (design-godot.md ~1565-1571). The fighter
        /// stands frozen and invulnerable for up to five seconds. After a short
        /// grace window any gameplay input drops them; otherwise the platform
        /// dissolves on expiry. Either way the three-second spawn invulnerability
        /// is (re)armed at the drop, never before it.
        ///
        /// <para>Package 11 A9: the platform sits at the <i>stage's</i> authored
        /// anchor, not the global stage-centre constant — on Paris centre is a pit
        /// now. Because the position is re-pinned on every tick the platform holds
        /// the fighter, this call is also the authority: a stock loss raised by the
        /// hit pipeline (which has no geometry in hand) places the fighter at the
        /// shared default for one tick and is corrected here before the drop.</para>
        /// </summary>
        private static void ProcessRespawnPlatform(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FPVector2 respawnPosition) {
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

            fighter.Position = respawnPosition;
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

        /// <summary>
        /// True when this stage's base floor is solid at all — authored platforms
        /// or authored floor segments. Only the legacy flat arena is false, and it
        /// keeps its historical drop-through ground.
        /// </summary>
        private bool StageHasSolidFloorRule =>
            _geometry.Platforms.Length > 0 || _geometry.IsOpenStage;

        /// <summary>
        /// True while a grounded fighter still has something under them: a one-way
        /// platform surface above the floor plane, or the main floor at the floor
        /// plane itself. The floor branch is what makes a pit edge a real edge —
        /// on an unbroken floor it always answers true.
        /// </summary>
        private bool HasGroundSupport(in FighterStateComponent fighter) =>
            fighter.Position.y > FP64.Zero
                ? HasPlatformSupport(in fighter)
                : _geometry.HasFloorSupport(fighter.Position.x);

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
            // Roll startup and recovery only ever bleed speed toward zero, so they
            // take the grounded decel ramp with every other stop site (§2.1),
            // not the accel ramp they historically borrowed.
            FP64 runStep = tuning.MoveSpeed / FP64.FromInt(UniversalMovementRules.RunDecelerationFrames);

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
            bool allowJump = true,
            bool allowDropThrough = true) {
            // Only the block stance decelerates to zero (same ramp Story uses).
            // Swings — grounded or aerial — keep full input steering; facing
            // alone is committed through lockFacing.
            FP64 input = rooted || lockHorizontal ? FP64.Zero : FP64.FromInt(runtime.MoveX) / FP64.FromInt(127);
            FP64 maximumSpeed = tuning.MoveSpeed * statusMoveMultiplier * speedBuffMultiplier;
            FP64 targetSpeed = input * maximumSpeed;
            // Grounded movement runs two ramps (2026-08-10 feel batch §2.1):
            // accelerating toward a same-signed target uses the 14-frame run
            // ramp, while bleeding speed off — a released stick, a rooted or
            // block-locked zero target, or a reversal against current
            // velocity — uses the 12-frame decel ramp. Air control keeps its
            // single 4-frame constant in both modes.
            int accelerationFrames;
            if (fighter.IsGrounded == 0) {
                accelerationFrames = 4;
            } else {
                bool decelerating = targetSpeed == FP64.Zero
                    || (targetSpeed > FP64.Zero && fighter.Velocity.x < FP64.Zero)
                    || (targetSpeed < FP64.Zero && fighter.Velocity.x > FP64.Zero);
                accelerationFrames = decelerating
                    ? UniversalMovementRules.RunDecelerationFrames
                    : UniversalMovementRules.RunAccelerationFrames;
            }
            FP64 rampStep = maximumSpeed / FP64.FromInt(accelerationFrames);
            if (fighter.IsGrounded == 0) {
                // V7.2 wiring: per-character aerial input responsiveness
                // (CharacterData.AirControlMultiplier — 0.4 Lincoln .. 0.75
                // Pocahontas). Zero from hand-built tunings reads as 1.0.
                FP64 airControl = tuning.AirControl > FP64.Zero ? tuning.AirControl : FP64.One;
                rampStep *= airControl;
            }
            fighter.Velocity.x = MoveToward(
                fighter.Velocity.x,
                targetSpeed,
                rampStep);
            if (!lockFacing) {
                if (runtime.MoveX > 0) fighter.FacingRight = 1;
                else if (runtime.MoveX < 0) fighter.FacingRight = 0;
            }

            bool jumpPressed = (runtime.PressedButtons & JumpButton) != 0;
            bool downHeld = (runtime.HeldButtons & DownButton) != 0;
            bool onSolidBaseFloor = groundIsSolid && fighter.Position.y <= FP64.Zero;
            // Drop-through stays available while attacking or blocking, matching
            // Story's IsDropThroughAllowed states; plain jumps do not.
            // Shieldstun (V7.3) locks it with everything else.
            if (allowDropThrough && jumpPressed && downHeld && fighter.IsGrounded != 0 && !rooted && !onSolidBaseFloor) {
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
            ref FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) {
            bool basicPressed = (runtime.PressedButtons & BasicButton) != 0;
            int phase = runtime.AttackPhase;
            if (phase == FighterBasicAttackRules.PhaseNone) {
                if (basicPressed
                    && !FighterUniversalMovementRules.IsCombatLocked(in runtime)
                    && !FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb)) {
                    FighterBasicAttackRules.StartSwing(
                        ref fighter,
                        ref runtime,
                        0,
                        FighterBasicAttackRules.SelectVariant(in fighter, in runtime));
                }
                return;
            }

            bool aerial = (runtime.AttackFlags & FighterBasicAttackRules.FlagAerial) != 0;
            bool variantSwing = FighterBasicAttackRules.IsVariantSwing(in runtime);
            if (aerial && fighter.IsGrounded != 0) {
                // Landing cancels an aerial string with no landing lag.
                FighterBasicAttackRules.CancelString(ref runtime);
                return;
            }

            // Directional attacks stand outside the chain: they neither buffer
            // into the string nor continue it.
            if (basicPressed && !variantSwing) {
                if (phase == FighterBasicAttackRules.PhaseChainHold) {
                    // A fresh direction re-selects at this swing start, so
                    // Up + BasicAttack out of the hold window starts an
                    // up-attack (chain reset) rather than continuing the string.
                    int variant = FighterBasicAttackRules.SelectVariant(in fighter, in runtime);
                    if (variant != FTT.Combat.BasicComboRules.VariantChain) {
                        FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, 0, variant);
                    } else if (runtime.ComboIndex < FTT.Combat.BasicComboRules.ComboHits - 1) {
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
                    runtime.AttackPhaseFrames =
                        FighterBasicAttackRules.ActiveFramesFor(runtime.AttackFlags, step);
                    break;
                case FighterBasicAttackRules.PhaseActive:
                    runtime.AttackPhase = FighterBasicAttackRules.PhaseRecovery;
                    runtime.AttackPhaseFrames =
                        FighterBasicAttackRules.RecoveryFramesFor(runtime.AttackFlags, step);
                    break;
                case FighterBasicAttackRules.PhaseRecovery:
                    // A directional attack exits straight out — no chain hold,
                    // no buffered continuation, combo index already zero.
                    if (variantSwing) {
                        FighterBasicAttackRules.CancelString(ref runtime);
                    } else if ((runtime.AttackFlags & FighterBasicAttackRules.FlagBuffered) != 0
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

        /// <summary>
        /// Echo Step ring sampling (5 entries, one every 6 frames — the oldest
        /// approximates "30 frames ago") plus wind-up advancement. The snap
        /// fires when the wind-up reaches zero: position only, velocity zeroed,
        /// facing preserved, actionable immediately. Being struck during the
        /// wind-up cancels it with no refund — the ghost telegraph was the
        /// opponent's read and they took it.
        /// </summary>
        private const int EchoRingSampleIntervalFrames = 6;
        private const int EchoRingSampleCount = 5;

        private static void AdvanceEchoStep(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb) {
            ref FighterEchoRingComponent ring = ref frame.Get<FighterEchoRingComponent>(entity);
            ring.SampleCountdown--;
            if (ring.SampleCountdown <= 0) {
                ring.SampleCountdown = EchoRingSampleIntervalFrames;
                WriteRingSample(ref ring, in fighter.Position);
            }

            if (verb.EchoStepWindupFrames <= 0) return;
            if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0 || fighter.Stocks <= 0
                // V7.3: entering a grab — as grabber or victim — cancels an
                // armed wind-up outright: no snap, no refund, cooldown stands.
                // This runs BEFORE the movement loop's IsBusy short-circuit,
                // so it is the one place the cancel can happen.
                || FighterGrabRules.IsBusy(in verb)) {
                verb.EchoStepWindupFrames = 0;
                return;
            }
            verb.EchoStepWindupFrames--;
            if (verb.EchoStepWindupFrames == 0) {
                fighter.Position = new FPVector2(verb.EchoStepDestX, verb.EchoStepDestY);
                fighter.Velocity = FPVector2.Zero;
                // The ground snap re-resolves against the destination's terrain
                // on this tick's integration; an elevated destination falls.
                if (fighter.Position.y > FP64.Zero) fighter.IsGrounded = 0;
            }
        }

        private static void WriteRingSample(ref FighterEchoRingComponent ring, in FPVector2 position) {
            switch (ring.RingIndex) {
                case 0: ring.Sample0X = position.x; ring.Sample0Y = position.y; break;
                case 1: ring.Sample1X = position.x; ring.Sample1Y = position.y; break;
                case 2: ring.Sample2X = position.x; ring.Sample2Y = position.y; break;
                case 3: ring.Sample3X = position.x; ring.Sample3Y = position.y; break;
                default: ring.Sample4X = position.x; ring.Sample4Y = position.y; break;
            }
            ring.RingIndex = (ring.RingIndex + 1) % EchoRingSampleCount;
        }

        /// <summary>RingIndex points at the next slot to overwrite — the oldest sample.</summary>
        private static FPVector2 OldestRingSample(in FighterEchoRingComponent ring) => ring.RingIndex switch {
            0 => new FPVector2(ring.Sample0X, ring.Sample0Y),
            1 => new FPVector2(ring.Sample1X, ring.Sample1Y),
            2 => new FPVector2(ring.Sample2X, ring.Sample2Y),
            3 => new FPVector2(ring.Sample3X, ring.Sample3Y),
            _ => new FPVector2(ring.Sample4X, ring.Sample4Y)
        };

        /// <summary>
        /// Echo Step initiation (V7.1): the Block+Roll chord during the
        /// recovery frames of the fighter's own swing spends 30 meter, arms the
        /// 8-frame wind-up toward the position ~30 frames back, and starts the
        /// 120-frame internal cooldown. Never an escape: hitstun, daze, ledge
        /// hang, the respawn platform, and hitstop (the movement loop skips
        /// frozen fighters entirely) all refuse it.
        /// </summary>
        private static void TryStartEchoStep(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb) {
            if (verb.EchoStepWindupFrames > 0 || verb.EchoStepCooldownFrames > 0) return;
            if (runtime.AttackPhase != FighterBasicAttackRules.PhaseRecovery) return;
            if ((runtime.HeldButtons & BlockButton) == 0 || (runtime.HeldButtons & RollButton) == 0) return;
            if ((runtime.PressedButtons & (BlockButton | RollButton)) == 0) return;
            if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0 || fighter.Stocks <= 0) return;
            // V7.3: never from inside a grab — grabbing, whiff recovery, being
            // held, or the throw animation all refuse the step.
            if (FighterGrabRules.IsBusy(in verb)) return;
            if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) return;
            if (FighterLedgeRules.IsHanging(in runtime)) return;
            FP64 cost = FP64.FromInt(FTT.Combat.BasicComboRules.EchoStepMeterCost);
            if (fighter.Influence < cost) return;

            fighter.Influence -= cost;
            verb.EchoStepCooldownFrames = FTT.Combat.BasicComboRules.EchoStepCooldownFrames;
            verb.EchoStepWindupFrames = FTT.Combat.BasicComboRules.EchoStepWindupFrames;
            ref readonly FighterEchoRingComponent ring = ref frame.GetReadOnly<FighterEchoRingComponent>(entity);
            FPVector2 destination = OldestRingSample(in ring);
            verb.EchoStepDestX = destination.x;
            verb.EchoStepDestY = destination.y;
        }

        private static void TickCounters(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            in FighterTuningComponent tuning) {
            // Rally drain (V7.1): the Echo Pool bleeds to zero over 150 frames
            // from the last accrual. Echo that finishes draining is permanently
            // lost — and only then does the victim's meter-from-damage accrue
            // on it (the no-double-earning guardrail).
            if (verb.EchoPool > FP64.Zero) {
                FP64 drained = FP64.Min(verb.EchoPool, verb.EchoDrainPerFrame);
                verb.EchoPool -= drained;
                fighter.Influence = FP64.Min(
                    FP64.FromInt(100),
                    fighter.Influence + drained / FP64.FromInt(4));
                if (verb.EchoPool <= FP64.Zero || verb.EchoDrainPerFrame <= FP64.Zero) {
                    verb.EchoPool = FP64.Zero;
                    verb.EchoDrainPerFrame = FP64.Zero;
                }
            }
            if (verb.ThrowImmunityFrames > 0) verb.ThrowImmunityFrames--;
            if (verb.EchoStepCooldownFrames > 0) verb.EchoStepCooldownFrames--;
            if (verb.ShieldStunFrames > 0) verb.ShieldStunFrames--;

            if (fighter.HitstunFrames > 0) fighter.HitstunFrames--;
            if (fighter.DazeFrames > 0) fighter.DazeFrames--;
            if (fighter.HyperArmorFrames > 0) fighter.HyperArmorFrames--;
            if (fighter.DropThroughFrames > 0) fighter.DropThroughFrames--;
            // §2.11 regrab lockout. It only runs down while off a ledge, which is
            // the whole point: releasing pins the fighter inside the capture box
            // they just left, and the lockout is what lets them fall out of it.
            if (runtime.LedgeRegrabLockoutFrames > 0) runtime.LedgeRegrabLockoutFrames--;
            if (runtime.BasicCooldownFrames > 0) runtime.BasicCooldownFrames--;
            // Block-charge regeneration, mirroring Story's BlockSystem: one charge
            // per interval, timer held at full while the stance is up and re-armed
            // when a charge is spent (FighterDamageRules resets it on block).
            // V7.3 shatter lockout: while it runs, regen is held with the interval
            // re-armed, so charge #1 lands exactly one interval after the lockout
            // expires (shatter + 480f).
            if (verb.BlockLockoutFrames > 0) {
                verb.BlockLockoutFrames--;
                runtime.BlockRegenFrames = FTT.Combat.BasicComboRules.BlockChargeRegenFrames;
            } else if (fighter.BlockCharges >= tuning.MaxBlockCharges || fighter.Stocks <= 0) {
                runtime.BlockRegenFrames = 0;
            } else if (FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb)) {
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
                if (runtime.StatusFrames == 0) {
                    runtime.StatusType = (int)FTT.Core.StatusType.None;
                    runtime.StatusIntensity = FP64.One;
                }
            }
            if (runtime.DamageStatusFrames > 0) {
                runtime.DamageStatusFrames--;
                if (runtime.StatusTickFrames > 0) runtime.StatusTickFrames--;
                if (runtime.DamageStatusType == (int)FTT.Core.StatusType.Venom && runtime.StatusTickFrames <= 0) {
                    int scaledDamage = ScaleIntegerByFP(2, runtime.DamageStatusIntensity);
                    int damage = scaledDamage > 1 ? scaledDamage : 1;
                    runtime.StatusTickFrames = 60;
                    // V7.3: venom routes through the unattributed-damage
                    // chokepoint — Defy History, Rally echo, and victim meter
                    // all apply to DoT exactly as to a hit (no attacker
                    // credit, no hitstop). A KO'd tick ends this pass.
                    int knockoutsBefore = runtime.KnockoutsSuffered;
                    FighterDamageRules.ApplyUnattributedDamage(
                        ref fighter, ref runtime, ref verb, ref defense, in tuning, damage);
                    if (runtime.KnockoutsSuffered != knockoutsBefore) return;
                }
                if (runtime.DamageStatusFrames == 0) {
                    runtime.DamageStatusType = (int)FTT.Core.StatusType.None;
                    runtime.DamageStatusIntensity = FP64.One;
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
    /// Deterministic ledge grab (gameplay-feel plan §2.11), the sim's first slice
    /// of audit M-16. Only one-way platform ends are grabbable: a stage's base
    /// floor spans wall to wall and its side walls are solid, so there is no other
    /// edge to catch. The whole feature is three snapshotted ints in
    /// <see cref="FighterRuntimeComponent"/> — the hang position is re-derived from
    /// the stage geometry every tick, since geometry is constant for the match and
    /// never enters a snapshot.
    /// </summary>
    internal static class FighterLedgeRules {
        /// <summary><see cref="FighterRuntimeComponent.LedgeAnchor"/> for "not hanging".</summary>
        public const int NoAnchor = -1;
        /// <summary>Hang ends on its own after five seconds, with the regrab lockout.</summary>
        public const int AutoReleaseFrames = 300;
        /// <summary>Half a second of no regrab after a Down release or an auto-release.</summary>
        public const int RegrabLockoutFrames = 30;

        /// <summary>Capture box half-width around the platform end.</summary>
        public static readonly FP64 CaptureHalfWidth = FP64.FromDouble(0.5);
        /// <summary>Capture box depth below the platform surface.</summary>
        public static readonly FP64 CaptureDepth = FP64.FromDouble(1.2);
        /// <summary>How far outside the platform the hang position sits.</summary>
        public static readonly FP64 HangOutwardOffset = FP64.FromDouble(0.25);
        /// <summary>How far below the surface the hang position sits.</summary>
        public static readonly FP64 HangDepth = FP64.One;
        /// <summary>
        /// Vertical-speed ceiling for a grab. Positive so a fighter still rising
        /// slowly near the apex catches the ledge they jumped up to; anything
        /// faster than this is a jump that clears the edge, not a grab.
        /// </summary>
        public static readonly FP64 MaxGrabVerticalSpeed = FP64.FromInt(2);
        /// <summary>The climb jump is nine-tenths of a ground jump.</summary>
        public static readonly FP64 ClimbJumpMultiplier = FP64.FromDouble(0.9);

        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;

        public static bool IsHanging(in FighterRuntimeComponent runtime) => runtime.LedgeAnchor >= 0;

        public static void ClearHang(ref FighterRuntimeComponent runtime) {
            runtime.LedgeAnchor = NoAnchor;
            runtime.LedgeStateFrames = 0;
        }

        /// <summary>
        /// The fighter-state half of the grab test. Geometry is the caller's job
        /// (<see cref="FighterStageGeometry.TryFindLedge"/>) so the two halves stay
        /// independently testable. V7.3 regrab cap: only
        /// <see cref="FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime"/> grabs
        /// per airtime — the next is refused until grounding (or a stock loss)
        /// resets the budget.
        /// </summary>
        public static bool CanGrab(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) =>
            fighter.Stocks > 0
            && fighter.IsGrounded == 0
            && fighter.RespawnFramesRemaining <= 0
            && fighter.DropThroughFrames == 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            && runtime.LedgeRegrabLockoutFrames <= 0
            && verb.LedgeGrabsThisAirtime < FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime
            && !IsHanging(in runtime)
            && fighter.Velocity.y <= MaxGrabVerticalSpeed;

        /// <summary>
        /// Latches the hang: pinned position, zeroed velocity, refilled jumps, and
        /// a cancelled swing/roll. Gravity is skipped for as long as the anchor is
        /// set, so nothing else has to know about the state.
        /// </summary>
        public static void Grab(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            in FighterTuningComponent tuning,
            int anchor,
            in FPVector2 hangPosition) {
            // V7.3 regrab budget: every latched grab spends one of the
            // per-airtime allowance CanGrab enforces.
            verb.LedgeGrabsThisAirtime++;
            runtime.LedgeAnchor = anchor;
            runtime.LedgeStateFrames = 0;
            fighter.Position = hangPosition;
            fighter.Velocity = FPVector2.Zero;
            // Jumps refill on the grab, which is what makes the climb jump the
            // ground-jump equivalent rather than an air jump.
            fighter.RemainingJumps = tuning.MaxJumpCount;
            // The fighter faces the stage they are about to climb onto: side 0 is
            // the platform's left edge, so the platform is to their right.
            fighter.FacingRight = anchor % 2 == 0 ? 1 : 0;
            FighterUniversalMovementRules.Cancel(ref runtime);
            FighterBasicAttackRules.CancelString(ref runtime);
            runtime.FloatFrames = 0;
        }

        /// <summary>
        /// Advances one hang tick. Returns true while the fighter is still hanging,
        /// which is the movement system's signal to skip gravity, movement, and the
        /// attack phase machine entirely — the same short-circuit hitstun gets.
        /// </summary>
        public static bool Process(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            in FPVector2 hangPosition) {
            fighter.Position = hangPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 0;

            // Jump climbs. It does not spend a jump: the grab refilled them and the
            // climb is the ground-jump equivalent, so the fighter leaves with the
            // same budget a jump off the floor would have left.
            if ((runtime.PressedButtons & JumpButton) != 0) {
                ClearHang(ref runtime);
                fighter.Velocity.y = tuning.JumpSpeed * ClimbJumpMultiplier;
                fighter.RemainingJumps = tuning.MaxJumpCount - 1;
                return false;
            }

            // Down drops off. Fast-fall (§2.9) never fights this: release wins, and
            // because Down is still held the fighter fast-falls immediately after,
            // which is the intended "drop fast off the ledge" input.
            if ((runtime.HeldButtons & DownButton) != 0) {
                Release(ref fighter, ref runtime);
                return false;
            }

            runtime.LedgeStateFrames++;
            if (runtime.LedgeStateFrames >= AutoReleaseFrames) {
                Release(ref fighter, ref runtime);
                return false;
            }
            return true;
        }

        /// <summary>Drops off into a fall and arms the regrab lockout.</summary>
        public static void Release(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime) {
            ClearHang(ref runtime);
            runtime.LedgeRegrabLockoutFrames = RegrabLockoutFrames;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 0;
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
        /// <summary>Directional-attack variant latch (§2.8): up-attack.</summary>
        public const int FlagUpAttack = 8;
        /// <summary>Directional-attack variant latch (§2.8): down-air.</summary>
        public const int FlagDownAir = 16;
        /// <summary>Either variant bit — a swing outside the three-hit chain.</summary>
        public const int VariantMask = FlagUpAttack | FlagDownAir;
        /// <summary>One construct sweep per swing (2026-08-11: basics damage enemy constructs).</summary>
        public const int FlagConstructHitResolved = 32;

        private const int BlockButton = 1 << 6;

        public static bool IsSwinging(in FighterRuntimeComponent runtime) =>
            runtime.AttackPhase is PhaseStartup or PhaseActive or PhaseRecovery;

        /// <summary>True while the running swing is an up-attack or a down-air.</summary>
        public static bool IsVariantSwing(in FighterRuntimeComponent runtime) =>
            (runtime.AttackFlags & VariantMask) != 0;

        /// <summary>
        /// Reads the swing variant out of the held input, through the shared
        /// selection rule. Quantized MoveY &lt; -30 is the sim's "Up held"
        /// (Story uses the float equivalent, -0.25); Down is the button bit,
        /// as everywhere else in the simulation.
        /// </summary>
        public static int SelectVariant(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime) =>
            FTT.Combat.BasicComboRules.SelectAttackVariant(
                upHeld: runtime.MoveY < -30,
                downHeld: (runtime.HeldButtons & (1 << 1)) != 0,
                airborne: fighter.IsGrounded == 0);

        // V7.1 per-character string profiles, indexed by FighterCharacterID
        // (Einstein 0 .. Pocahontas 8). Resolved once from the shared rulebook —
        // the same profiles Story consumes — so the modes cannot drift; unknown
        // IDs fall back to the template.
        private static readonly FTT.Combat.BasicStringProfile[] StringProfiles = {
            FTT.Combat.BasicComboRules.StringProfileFor("einstein"),
            FTT.Combat.BasicComboRules.StringProfileFor("joan"),
            FTT.Combat.BasicComboRules.StringProfileFor("leonardo"),
            FTT.Combat.BasicComboRules.StringProfileFor("lincoln"),
            FTT.Combat.BasicComboRules.StringProfileFor("cleopatra"),
            FTT.Combat.BasicComboRules.StringProfileFor("tesla"),
            FTT.Combat.BasicComboRules.StringProfileFor("shakespeare"),
            FTT.Combat.BasicComboRules.StringProfileFor("mozart"),
            FTT.Combat.BasicComboRules.StringProfileFor("pocahontas")
        };

        public static FTT.Combat.BasicStringProfile StringProfileFor(int characterID) =>
            characterID >= 0 && characterID < StringProfiles.Length
                ? StringProfiles[characterID]
                : FTT.Combat.BasicComboRules.TemplateStringProfile;

        public static int StartupFramesFor(int characterID, int attackFlags, int comboStep) {
            if ((attackFlags & FlagUpAttack) != 0) return FTT.Combat.BasicComboRules.UpAttackStartupFrames;
            if ((attackFlags & FlagDownAir) != 0) return FTT.Combat.BasicComboRules.DownAirStartupFrames;
            // Chain opener/finisher startups are the authored per-character axis
            // (V7.1); active and recovery frames below stay universal.
            return (attackFlags & FlagAerial) != 0
                ? StringProfileFor(characterID).AerialStartupFrames[comboStep]
                : StringProfileFor(characterID).GroundStartupFrames[comboStep];
        }

        public static int ActiveFramesFor(int attackFlags, int comboStep) {
            if ((attackFlags & FlagUpAttack) != 0) return FTT.Combat.BasicComboRules.UpAttackActiveFrames;
            if ((attackFlags & FlagDownAir) != 0) return FTT.Combat.BasicComboRules.DownAirActiveFrames;
            return (attackFlags & FlagAerial) != 0
                ? FTT.Combat.BasicComboRules.AerialActiveFrames[comboStep]
                : FTT.Combat.BasicComboRules.GroundActiveFrames[comboStep];
        }

        public static int RecoveryFramesFor(int attackFlags, int comboStep) {
            if ((attackFlags & FlagUpAttack) != 0) return FTT.Combat.BasicComboRules.UpAttackRecoveryFrames;
            if ((attackFlags & FlagDownAir) != 0) return FTT.Combat.BasicComboRules.DownAirRecoveryFrames;
            return (attackFlags & FlagAerial) != 0
                ? FTT.Combat.BasicComboRules.AerialRecoveryFrames[comboStep]
                : FTT.Combat.BasicComboRules.GroundRecoveryFrames[comboStep];
        }

        /// <summary>
        /// The grounded block stance, mirroring Story's Blocking state: grounded,
        /// free of hitstun/daze, not mid roll, not mid swing, holding Block with
        /// at least one charge and no shatter lockout (V7.3). Shieldstun locks
        /// the blocker INTO the stance regardless of held buttons. The movement
        /// lock, the attack/ability gates, and the shield-absorb rule in
        /// FighterDamageRules all key off this one predicate.
        /// </summary>
        public static bool IsBlockStance(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) {
            if (fighter.Stocks <= 0) return false;
            // V7.3 shieldstun: a blocked hit locks the stance up — the blocker
            // cannot drop block, attack, grab, roll, or jump until it expires.
            if (verb.ShieldStunFrames > 0) return true;
            return fighter.IsGrounded != 0
                && fighter.HitstunFrames <= 0
                && fighter.DazeFrames <= 0
                && fighter.RespawnFramesRemaining <= 0
                // V7.3: an empty shield never raises the stance, and neither
                // does the five-second shatter lockout.
                && fighter.BlockCharges > 0
                && verb.BlockLockoutFrames <= 0
                // V7.3 grab triangle: a grabbing fighter (startup, active, the
                // 24f whiff recovery) or a held victim has NO functioning
                // shield — attack beats grab.
                && verb.GrabPhase == FighterGrabRules.PhaseNone
                && verb.BeingHeld == 0
                && runtime.UniversalMovementState == (int)UniversalMovementPhase.None
                && runtime.AttackPhase == PhaseNone
                && (runtime.HeldButtons & BlockButton) != 0;
        }

        public static void CancelString(ref FighterRuntimeComponent runtime) {
            runtime.AttackPhase = PhaseNone;
            runtime.AttackPhaseFrames = 0;
            runtime.AttackFlags = 0;
            runtime.ComboIndex = 0;
        }

        public static void StartSwing(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            int comboStep,
            int variant = FTT.Combat.BasicComboRules.VariantChain) {
            bool aerial = fighter.IsGrounded == 0;
            // A directional attack is a single strike outside the chain: the
            // combo index resets to zero and never advances from here.
            runtime.ComboIndex = variant == FTT.Combat.BasicComboRules.VariantChain ? comboStep : 0;
            runtime.AttackPhase = PhaseStartup;
            int flags = aerial ? FlagAerial : 0;
            if (variant == FTT.Combat.BasicComboRules.VariantUpAttack) flags |= FlagUpAttack;
            else if (variant == FTT.Combat.BasicComboRules.VariantDownAir) flags |= FlagDownAir;
            runtime.AttackFlags = flags;
            runtime.AttackPhaseFrames = StartupFramesFor(fighter.CharacterID, flags, runtime.ComboIndex);
            // Defensive: universal movement is provably None at every current call
            // site (IsCombatLocked gates fresh swings, and no roll can start while
            // a string is active), so this cancel is a no-op today. It stays so a
            // future cancel window cannot leave a phase running under a swing.
            FighterUniversalMovementRules.Cancel(ref runtime);
            // Legacy "busy" mirror for observers (HUD, CPU pacing): the remaining
            // swing length. No gameplay system reads it any more.
            runtime.BasicCooldownFrames = StartupFramesFor(fighter.CharacterID, flags, runtime.ComboIndex)
                + ActiveFramesFor(flags, runtime.ComboIndex)
                + RecoveryFramesFor(flags, runtime.ComboIndex);
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
            // A hanging fighter is pinned to its ledge anchor by the movement
            // system; jostling it would fight that pin for a frame. Co-hangs on
            // the same anchor never persist past the tick they occur — the V7.3
            // ledge-trump pass at the end of FighterMovementSystem.Update
            // releases the earlier hanger — so this guard only covers the
            // legitimate one-hanger (or two-different-anchors) cases.
            if (FighterLedgeRules.IsHanging(in firstRuntime)
                || FighterLedgeRules.IsHanging(in secondRuntime)) return;
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
        // V7.6 F15 (Package 11 A1b): the two-charge "shield-stutter" exception
        // is RETIRED. Joan's Divine Piercing and Lincoln's Emancipator are
        // ordinary Special-class FULL shatters against a charge-based shield in
        // both modes - 1, 2 or 3 charges all go to 0 with the normal Special
        // shatter response (shatter-freeze, daze, 5 s lockout). Lincoln's S1
        // already passed 0 here, so the two modes disagreed until now.
        private static readonly FP64 AttackRange = FP64.FromInt(2);
        private static readonly FP64 AttackVerticalRange = FP64.FromDouble(1.6);
        // V7 normative string hitboxes (design "Hitbox & Hurtbox Geometry"):
        // three escalating boxes offset IN FRONT of the attacker only — a swing
        // never hits behind the attacker's back. Sizes/offsets are the authored
        // template (1.2×1.0 @ 1.0 / 1.4×1.2 @ (1.0, 0.2) / 1.8×1.4 @ 1.2),
        // stored as half-extents; the V7.1 profile percentages scale width
        // (size and offset together) and height. The old single symmetric
        // 2.0×1.6 both-sides box was a recorded defect against this spec.
        private static readonly FP64[] StringBoxHalfWidths = {
            FP64.FromDouble(0.6), FP64.FromDouble(0.7), FP64.FromDouble(0.9)
        };
        private static readonly FP64[] StringBoxHalfHeights = {
            FP64.FromDouble(0.5), FP64.FromDouble(0.6), FP64.FromDouble(0.7)
        };
        private static readonly FP64[] StringBoxOffsetsX = {
            FP64.One, FP64.One, FP64.FromDouble(1.2)
        };
        private static readonly FP64[] StringBoxOffsetsY = {
            FP64.Zero, FP64.FromDouble(0.2), FP64.Zero
        };
        /// <summary>The universal fighter hurtbox half-extents the sim uses.</summary>
        private static readonly FPVector2 VictimHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        /// <summary>
        /// The active string box for a combo step, in world space: offset in
        /// front of the attacker's current facing, scaled by the profile's
        /// reach percentages.
        /// </summary>
        private static void ResolveStringBox(
            in FighterStateComponent attacker,
            in FTT.Combat.BasicStringProfile profile,
            int step,
            out FPVector2 center,
            out FPVector2 halfExtents) {
            FP64 widthScale = FP64.FromInt(profile.ReachWidthPercent) / FP64.FromInt(100);
            FP64 heightScale = FP64.FromInt(profile.ReachHeightPercent) / FP64.FromInt(100);
            FP64 facing = attacker.FacingRight != 0 ? FP64.One : -FP64.One;
            center = new FPVector2(
                attacker.Position.x + facing * StringBoxOffsetsX[step] * widthScale,
                attacker.Position.y + StringBoxOffsetsY[step]);
            halfExtents = new FPVector2(
                StringBoxHalfWidths[step] * widthScale,
                StringBoxHalfHeights[step] * heightScale);
        }
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);
        // The shared per-hit knockback table (BasicComboRules.KnockbackMultipliers),
        // pre-converted once to fixed point. FromDouble of a process-constant is
        // deterministic; no float math runs per tick.
        private static readonly FP64[] BasicKnockbackMultipliers = {
            FP64.FromDouble(FTT.Combat.BasicComboRules.KnockbackMultipliers[0]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.KnockbackMultipliers[1]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.KnockbackMultipliers[2])
        };
        // Directional attacks (§2.8). Boxes are authored in world units around the
        // attacker's origin; the Story pixel hitboxes mirror these proportions.
        private static readonly FP64 UpAttackHorizontalReach = FP64.FromDouble(1.2);
        private static readonly FP64 UpAttackVerticalReach = FP64.FromDouble(2.4);
        private static readonly FP64 DownAirHorizontalReach = FP64.One;
        private static readonly FP64 DownAirVerticalReach = FP64.FromInt(2);
        private static readonly FP64 DirectionalDamageMultiplier =
            FP64.FromDouble(FTT.Combat.BasicComboRules.DirectionalAttackDamageMultiplier);
        private static readonly FP64 DirectionalHorizontalKnockback =
            FP64.FromDouble(FTT.Combat.BasicComboRules.DirectionalAttackHorizontalKnockback);
        private static readonly FP64 DirectionalVerticalKnockbackScale = FP64.FromDouble(
            FTT.Combat.BasicComboRules.DirectionalAttackVerticalKnockback
                / FTT.Combat.BasicComboRules.DirectionalAttackHorizontalKnockback);

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
            ref FighterVerbComponent verbOne = ref frame.Get<FighterVerbComponent>(first);
            ref FighterVerbComponent verbTwo = ref frame.Get<FighterVerbComponent>(second);
            ref readonly FighterTuningComponent tuningOne = ref frame.GetReadOnly<FighterTuningComponent>(first);
            ref readonly FighterTuningComponent tuningTwo = ref frame.GetReadOnly<FighterTuningComponent>(second);
            // V7.1 hitstop: a frozen fighter takes no offensive action this tick
            // (their pressed buttons are simply not consumed; the freeze is at
            // most 8 frames). Hits CAN still land on a frozen target — hitstop
            // then extends via max-assign, never shortens.
            bool oneFrozen = verbOne.HitstopFrames > 0;
            bool twoFrozen = verbTwo.HitstopFrames > 0;
            // V7.6 D01-D04 (Package 11 A1b): the defensive layer travels with
            // every hit path, because the Defy protection gate and the D02b
            // Aegis/barrier order live at the top of ApplyFighterHit.
            ref FighterDefenseComponent defenseOne = ref frame.Get<FighterDefenseComponent>(first);
            ref FighterDefenseComponent defenseTwo = ref frame.Get<FighterDefenseComponent>(second);

            // V7.2 grabs & throws advance before every other action: a fighter
            // mid-grab or held takes no other offensive action this tick.
            AdvanceGrabs(
                ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne,
                ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo,
                oneFrozen, twoFrozen);
            bool oneActing = !oneFrozen && !FighterGrabRules.IsBusy(in verbOne);
            bool twoActing = !twoFrozen && !FighterGrabRules.IsBusy(in verbTwo);

            // Bespoke character ultimates dispatch first (X7): a successful
            // dispatch consumes the meter, so the generic melee ultimate intent
            // below cannot double-fire on the same press.
            if (oneActing) TryCharacterUltimate(ref frame, first, second, ref fighterOne, ref runtimeOne, in tuningOne);
            if (twoActing) TryCharacterUltimate(ref frame, second, first, ref fighterTwo, ref runtimeTwo, in tuningTwo);

            AttackIntent firstIntent = !oneActing ? default : BuildIntent(in fighterOne, in runtimeOne, in verbOne, in tuningOne, in fighterTwo);
            AttackIntent secondIntent = !twoActing ? default : BuildIntent(in fighterTwo, in runtimeTwo, in verbTwo, in tuningTwo, in fighterOne);
            ApplyIntent(ref fighterOne, ref runtimeOne, ref verbOne, ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, in firstIntent);
            ApplyIntent(ref fighterTwo, ref runtimeTwo, ref verbTwo, ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, in secondIntent);
            // F07 marks are written onto the VICTIM's component 318.
            ref FighterConductiveComponent conductiveOne = ref frame.Get<FighterConductiveComponent>(first);
            ref FighterConductiveComponent conductiveTwo = ref frame.Get<FighterConductiveComponent>(second);
            FighterConductiveRules.Tick(ref conductiveOne, verbOne.HitstopFrames > 0);
            FighterConductiveRules.Tick(ref conductiveTwo, verbTwo.HitstopFrames > 0);
            // V7.6 D04 (Package 11 A1b): the protected second does not start
            // until the survivor's first resumed NORMAL-CONTROL tick, and its
            // countdown pauses during global hitstop and suspended-combat
            // presentations. Attacking neither cancels nor refreshes it.
            FighterDefenseRules.Tick(
                ref defenseOne, verbOne.HitstopFrames > 0, IsDefyActionable(in fighterOne, in verbOne));
            FighterDefenseRules.Tick(
                ref defenseTwo, verbTwo.HitstopFrames > 0, IsDefyActionable(in fighterTwo, in verbTwo));
            if (oneActing) {
                ApplyBasicSwing(ref fighterOne, ref runtimeOne, ref verbOne, in tuningOne, ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, ref conductiveTwo);
                ApplyConstructSwing(ref frame, ref fighterOne, ref runtimeOne, in tuningOne);
            }
            if (twoActing) {
                ApplyBasicSwing(ref fighterTwo, ref runtimeTwo, ref verbTwo, in tuningTwo, ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, ref conductiveOne);
                ApplyConstructSwing(ref frame, ref fighterTwo, ref runtimeTwo, in tuningTwo);
            }
        }

        /// <summary>
        /// The D04 "normal player control has resumed" test: alive, out of the
        /// Defy presentation freeze, free of hitstun/daze/respawn, and not held.
        /// Deliberately NOT an input test - the contract says the window opens
        /// at the first resumed control tick, "not on the player's first input".
        /// </summary>
        private static bool IsDefyActionable(
            in FighterStateComponent fighter, in FighterVerbComponent verb) =>
            fighter.Stocks > 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            && fighter.RespawnFramesRemaining <= 0
            && verb.HitstopFrames <= 0
            && verb.BeingHeld == 0;

        /// <summary>
        /// V7.2 grabs &amp; throws, both fighters per tick: initiation from the
        /// Block+BasicAttack chord, phase advancement, the simultaneous-grab
        /// clash, the 30-frame hold, and throw resolution through the ordinary
        /// damage chokepoint. Frozen (hitstop) fighters are fully suspended.
        /// </summary>
        private static void AdvanceGrabs(
            ref FighterStateComponent one, ref FighterRuntimeComponent runtimeOne,
            ref FighterVerbComponent verbOne, ref FighterDefenseComponent defenseOne,
            in FighterTuningComponent tuningOne,
            ref FighterStateComponent two, ref FighterRuntimeComponent runtimeTwo,
            ref FighterVerbComponent verbTwo, ref FighterDefenseComponent defenseTwo,
            in FighterTuningComponent tuningTwo,
            bool oneFrozen, bool twoFrozen) {
            // Initiation. Both may start the same frame; the clash resolves it.
            if (!oneFrozen && FighterGrabRules.ChordPressed(in runtimeOne)
                && FighterGrabRules.CanStartGrab(in one, in runtimeOne, in verbOne)) {
                verbOne.GrabPhase = FighterGrabRules.PhaseStartup;
                verbOne.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
            }
            if (!twoFrozen && FighterGrabRules.ChordPressed(in runtimeTwo)
                && FighterGrabRules.CanStartGrab(in two, in runtimeTwo, in verbTwo)) {
                verbTwo.GrabPhase = FighterGrabRules.PhaseStartup;
                verbTwo.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
            }

            // A hit breaks a grab in every pre-throw phase (attack beats grab);
            // the 12-frame throw animation is invulnerable and cannot break.
            BreakGrabIfStruck(ref one, ref verbOne, ref verbTwo);
            BreakGrabIfStruck(ref two, ref verbTwo, ref verbOne);

            // The simultaneous clash: both active windows seize the same frame.
            bool oneSeizes = !oneFrozen
                && verbOne.GrabPhase == FighterGrabRules.PhaseActive
                && FighterGrabRules.ActiveWindowSeizes(in one, in two, in runtimeTwo, in verbTwo);
            bool twoSeizes = !twoFrozen
                && verbTwo.GrabPhase == FighterGrabRules.PhaseActive
                && FighterGrabRules.ActiveWindowSeizes(in two, in one, in runtimeOne, in verbOne);
            if (oneSeizes && twoSeizes) {
                FighterGrabRules.ApplyClash(ref one, ref verbOne);
                FighterGrabRules.ApplyClash(ref two, ref verbTwo);
            } else if (oneSeizes) {
                ConnectGrab(ref one, ref verbOne, ref two, ref runtimeTwo, ref verbTwo);
            } else if (twoSeizes) {
                ConnectGrab(ref two, ref verbTwo, ref one, ref runtimeOne, ref verbOne);
            }

            AdvanceGrabPhase(
                ref one, ref runtimeOne, ref verbOne, in tuningOne,
                ref two, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, oneFrozen);
            AdvanceGrabPhase(
                ref two, ref runtimeTwo, ref verbTwo, in tuningTwo,
                ref one, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, twoFrozen);
        }

        private static void BreakGrabIfStruck(
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb,
            ref FighterVerbComponent opponentVerb) {
            if (verb.GrabPhase is FighterGrabRules.PhaseNone or FighterGrabRules.PhaseThrowAnimation) return;
            if (fighter.HitstunFrames <= 0 && fighter.DazeFrames <= 0 && fighter.Stocks > 0) return;
            if (verb.GrabPhase == FighterGrabRules.PhaseHolding) {
                FighterGrabRules.ReleaseHeldVictim(ref opponentVerb);
            }
            verb.GrabPhase = FighterGrabRules.PhaseNone;
            verb.GrabPhaseFrames = 0;
        }

        private static void ConnectGrab(
            ref FighterStateComponent grabber,
            ref FighterVerbComponent grabberVerb,
            ref FighterStateComponent victim,
            ref FighterRuntimeComponent victimRuntime,
            ref FighterVerbComponent victimVerb) {
            grabberVerb.GrabPhase = FighterGrabRules.PhaseHolding;
            grabberVerb.GrabPhaseFrames = FTT.Combat.BasicComboRules.ThrowDecisionFrames;
            victimVerb.BeingHeld = 1;
            // Being seized ends the victim's own grab attempt outright — two
            // simultaneous holders would otherwise pin each other into a
            // position feedback loop.
            victimVerb.GrabPhase = FighterGrabRules.PhaseNone;
            victimVerb.GrabPhaseFrames = 0;
            victim.Velocity = FPVector2.Zero;
            FighterUniversalMovementRules.Cancel(ref victimRuntime);
            FighterBasicAttackRules.CancelString(ref victimRuntime);
            FighterLedgeRules.ClearHang(ref victimRuntime);
            PinHeldVictim(in grabber, ref victim, ref victimRuntime);
        }

        /// <summary>
        /// The held victim cannot act: pinned to the grabber's front, velocity
        /// zeroed, and their sampled input cleared — which also keeps
        /// IsBlockStance false so the throw is unblockable by construction.
        /// </summary>
        private static void PinHeldVictim(
            in FighterStateComponent grabber,
            ref FighterStateComponent victim,
            ref FighterRuntimeComponent victimRuntime) {
            victim.Position = FighterGrabRules.HeldPosition(in grabber);
            victim.Velocity = FPVector2.Zero;
            victim.IsGrounded = 1;
            victimRuntime.HeldButtons = 0;
            victimRuntime.PressedButtons = 0;
        }

        private static void AdvanceGrabPhase(
            ref FighterStateComponent grabber,
            ref FighterRuntimeComponent grabberRuntime,
            ref FighterVerbComponent grabberVerb,
            in FighterTuningComponent grabberTuning,
            ref FighterStateComponent victim,
            ref FighterRuntimeComponent victimRuntime,
            ref FighterVerbComponent victimVerb,
            ref FighterDefenseComponent victimDefense,
            in FighterTuningComponent victimTuning,
            bool frozen) {
            if (frozen || grabberVerb.GrabPhase == FighterGrabRules.PhaseNone) return;

            // A fighter who got seized cannot keep an own grab running.
            if (grabberVerb.BeingHeld != 0) {
                grabberVerb.GrabPhase = FighterGrabRules.PhaseNone;
                grabberVerb.GrabPhaseFrames = 0;
                return;
            }

            if (grabberVerb.GrabPhase is FighterGrabRules.PhaseHolding or FighterGrabRules.PhaseThrowAnimation) {
                // A victim who stopped being held mid-hold (a hazard KO'd them
                // and stock loss cleared the flag) ends the grab outright.
                if (victimVerb.BeingHeld == 0) {
                    grabberVerb.GrabPhase = FighterGrabRules.PhaseNone;
                    grabberVerb.GrabPhaseFrames = 0;
                    return;
                }
                PinHeldVictim(in grabber, ref victim, ref victimRuntime);
            }

            grabberVerb.GrabPhaseFrames--;
            if (grabberVerb.GrabPhaseFrames > 0) return;

            switch (grabberVerb.GrabPhase) {
                case FighterGrabRules.PhaseStartup:
                    grabberVerb.GrabPhase = FighterGrabRules.PhaseActive;
                    grabberVerb.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabActiveFrames;
                    break;
                case FighterGrabRules.PhaseActive:
                    // The active window ended without seizing: the whiff — the
                    // most punishable committal in the kit.
                    grabberVerb.GrabPhase = FighterGrabRules.PhaseRecovery;
                    grabberVerb.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabWhiffRecoveryFrames;
                    break;
                case FighterGrabRules.PhaseRecovery:
                    grabberVerb.GrabPhase = FighterGrabRules.PhaseNone;
                    break;
                case FighterGrabRules.PhaseHolding:
                    // The decision window closed: the held direction picks the
                    // throw, then both fighters are invulnerable for the
                    // 12-frame throw animation.
                    grabberVerb.ThrowDirection =
                        FighterGrabRules.ResolveThrowDirection(in grabber, in grabberRuntime);
                    grabberVerb.GrabPhase = FighterGrabRules.PhaseThrowAnimation;
                    grabberVerb.GrabPhaseFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
                    if (grabber.InvulnerabilityFrames < FTT.Combat.BasicComboRules.ThrowAnimationFrames) {
                        grabber.InvulnerabilityFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
                    }
                    if (victim.InvulnerabilityFrames < FTT.Combat.BasicComboRules.ThrowAnimationFrames) {
                        victim.InvulnerabilityFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
                    }
                    break;
                case FighterGrabRules.PhaseThrowAnimation:
                    ResolveThrow(
                        ref grabber, ref grabberRuntime, ref grabberVerb, in grabberTuning,
                        ref victim, ref victimRuntime, ref victimVerb, ref victimDefense, in victimTuning);
                    break;
            }
        }

        private static void ResolveThrow(
            ref FighterStateComponent grabber,
            ref FighterRuntimeComponent grabberRuntime,
            ref FighterVerbComponent grabberVerb,
            in FighterTuningComponent grabberTuning,
            ref FighterStateComponent victim,
            ref FighterRuntimeComponent victimRuntime,
            ref FighterVerbComponent victimVerb,
            ref FighterDefenseComponent victimDefense,
            in FighterTuningComponent victimTuning) {
            int direction = grabberVerb.ThrowDirection;
            // The back throw is a positional reversal: the victim swings to the
            // grabber's other side and both fighters turn around before launch.
            if (direction == FighterGrabRules.ThrowBack) {
                grabber.FacingRight = grabber.FacingRight != 0 ? 0 : 1;
                victim.FacingRight = victim.FacingRight != 0 ? 0 : 1;
                victim.Position = FighterGrabRules.HeldPosition(in grabber);
            }

            FP64 knockback;
            FP64 verticalScale;
            if (direction == FighterGrabRules.ThrowUp) {
                // 2.5x vertical: a small horizontal carry with the vertical
                // component brought up to the authored 2.5x.
                knockback = grabberTuning.BasicKnockback * FP64.FromDouble(0.8);
                verticalScale = FP64.FromDouble(
                    FTT.Combat.BasicComboRules.UpThrowKnockbackMultiplier / 0.8);
            } else {
                // Forward and back throws share the horizontal shape; the back
                // throw reads its own authored multiplier (V7.3 — it silently
                // borrowed the forward value before).
                knockback = grabberTuning.BasicKnockback * FP64.FromDouble(
                    direction == FighterGrabRules.ThrowBack
                        ? FTT.Combat.BasicComboRules.BackThrowKnockbackMultiplier
                        : FTT.Combat.BasicComboRules.ForwardThrowKnockbackMultiplier);
                verticalScale = FP64.FromDouble(0.2);
            }

            // The throw's invulnerability was for third parties; the throw
            // itself must connect, so the animation grant is cleared first.
            victim.InvulnerabilityFrames = 0;
            victimVerb.BeingHeld = 0;
            // V7.6 D03d (Package 11 A1b): a legal PRIMARY throw bypasses the
            // finite shields. It deals normal damage and applies its normal
            // launch WITHOUT consuming Temporal Aegis or HP-barrier capacity -
            // no decrement, no absorption/break, no block perk, and the skipped
            // protection is not treated as partial reduction. Before this an
            // active Aegis absorbed the throw entirely, which is exactly the
            // case the contract forbids. Defy still applies normally.
            FighterDamageRules.ApplyFighterHit(
                ref grabber, ref grabberRuntime, ref grabberVerb,
                ref victim, ref victimRuntime, ref victimVerb, ref victimDefense, in victimTuning,
                FighterDamageRules.BasicAttackClass,
                grabberTuning.BasicDamage,
                knockback,
                FighterGrabRules.ThrowHitstunFrames,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                grabber.Position.x,
                verticalKnockbackScale: verticalScale,
                bypassesFiniteShields: true);
            // Trajectories are fixed — no DI on throws (the throw IS the
            // decision), so the stashed launch never resolves through DI.
            victimVerb.PendingLaunchActive = 0;
            victimVerb.ThrowImmunityFrames = FTT.Combat.BasicComboRules.ThrowImmunityFrames;
            grabberVerb.GrabPhase = FighterGrabRules.PhaseNone;
            grabberVerb.GrabPhaseFrames = 0;
        }

        /// <summary>
        /// A basic swing also damages the opponent's deployed constructs in
        /// reach — mirroring Story, where the active hitbox overlaps construct
        /// hurtboxes on the PersistentObject layer (2026-08-11: without this,
        /// nothing in the simulation could ever damage a construct). One sweep
        /// per swing; constructs have no block or armor, so damage applies
        /// directly, and the sweep is independent of the fighter-target hit.
        /// </summary>
        private static void ApplyConstructSwing(
            ref Frame frame,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent attackerTuning) {
            if (attackerRuntime.AttackPhase != FighterBasicAttackRules.PhaseActive) return;
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagConstructHitResolved) != 0) return;
            if (attacker.Stocks <= 0) return;

            int step = attackerRuntime.ComboIndex < 0 ? 0 : attackerRuntime.ComboIndex > 2 ? 2 : attackerRuntime.ComboIndex;
            // Same V7.1 profile axes the fighter-target swing applies.
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            int damage = FighterBasicAttackRules.IsVariantSwing(in attackerRuntime)
                ? attackerTuning.BasicDamage
                : attackerTuning.BasicDamage * profile.DamageTenths[step] / 10;
            if (damage <= 0) return;

            // V7 normative boxes: the construct sweep uses the same escalating
            // front-only box as the fighter-target swing.
            ResolveStringBox(in attacker, in profile, step, out FPVector2 boxCenter, out FPVector2 boxHalf);
            bool hitAny = false;
            var constructs = frame.Filter<FighterPersistentObjectComponent>();
            while (constructs.Next(out EntityRef entity)) {
                ref FighterPersistentObjectComponent persistent =
                    ref frame.Get<FighterPersistentObjectComponent>(entity);
                if (persistent.OwnerPlayerID == attacker.PlayerID || persistent.CurrentHP <= 0) continue;
                if (!FighterEntityQueries.Overlaps(
                        in boxCenter, in boxHalf,
                        in persistent.Position, in persistent.HalfExtents)) continue;
                int remaining = persistent.CurrentHP - damage;
                persistent.CurrentHP = remaining > 0 ? remaining : 0;
                hitAny = true;
            }
            if (hitAny) attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagConstructHitResolved;
        }

        /// <summary>
        /// Applies the basic string's hit during its active window. One attempt
        /// per swing — Story's hitbox also connects at most once per activation —
        /// whether it lands, is blocked, or meets invulnerability.
        /// </summary>
        private static void ApplyBasicSwing(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            in FighterTuningComponent attackerTuning,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            ref FighterConductiveComponent targetConductive) {
            if (attackerRuntime.AttackPhase != FighterBasicAttackRules.PhaseActive) return;
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0) return;
            if (attacker.Stocks <= 0) return;

            // Directional attacks (§2.8) own their own boxes and launch upward.
            if (FighterBasicAttackRules.IsVariantSwing(in attackerRuntime)) {
                ApplyDirectionalSwing(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning);
                return;
            }

            // V7.1 string profile: reach and damage shape are the attacker's
            // authored axes (BasicComboRules.StringProfileFor); the tenths sum
            // is pinned at 33 so a full string stays 3.3x BasicDamage.
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            int step = attackerRuntime.ComboIndex < 0 ? 0 : attackerRuntime.ComboIndex > 2 ? 2 : attackerRuntime.ComboIndex;
            // V7 normative boxes: the step's escalating box, in front of the
            // attacker's current facing only — a swing never hits behind.
            ResolveStringBox(in attacker, in profile, step, out FPVector2 boxCenter, out FPVector2 boxHalf);
            if (!FighterEntityQueries.Overlaps(
                    in boxCenter, in boxHalf, in target.Position, in VictimHalfExtents)) return;

            int damage = attackerTuning.BasicDamage * profile.DamageTenths[step] / 10;
            bool finisher = step == 2;
            // Shared knockback table (BasicComboRules.KnockbackMultipliers,
            // 1.0x / 1.2x / 4.5x); the finisher launches with the profile's
            // authored tenths (V7.1 rider: Mozart's 5.5x spacing shove).
            FP64 knockback = attackerTuning.BasicKnockback * (finisher
                ? FP64.FromInt(profile.FinisherKnockbackTenths) / FP64.FromInt(10)
                : BasicKnockbackMultipliers[step]);
            // V7.1 riders through the ordinary payload: the finisher's authored
            // status (Tesla's priming Static Charge, Cleopatra's venom mark)
            // and hit 2's vertical launch scale (Lincoln's heavy upward swing).
            int riderStatusType = finisher ? profile.FinisherStatusType : (int)FTT.Core.StatusType.None;
            int riderStatusFrames = finisher ? profile.FinisherStatusFrames : 0;
            FP64 riderIntensity = riderStatusType != (int)FTT.Core.StatusType.None
                ? FP64.FromInt(profile.FinisherStatusIntensityMilli) / FP64.FromInt(1000)
                : FP64.One;
            FP64 hit2VerticalScale = step == 1
                ? FP64.FromInt(profile.Hit2VerticalLaunchTenths) / FP64.FromInt(10)
                : FP64.Zero;
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            bool connected = FighterDamageRules.ApplyFighterHit(
                ref attacker,
                ref attackerRuntime,
                ref attackerVerb,
                ref target,
                ref targetRuntime,
                ref targetVerb,
                ref targetDefense,
                in targetTuning,
                FighterDamageRules.BasicAttackClass,
                damage,
                knockback,
                FTT.Combat.BasicComboRules.HitstunFrames[step],
                riderStatusType,
                riderStatusFrames,
                riderIntensity,
                attacker.Position.x,
                true,
                0,
                hit2VerticalScale,
                // V7.3 hit-2 cancel gate: only string hit 1 arms the block-cancel
                // block — hits two and three leave hitstun escapable.
                blockCancelableHitstun: step >= 1);
            // V7.1 Resonance Momentum: a CONNECTING finisher (never hits 1-2,
            // a directional strike, or a blocked hit) refunds 60 frames on both
            // special cooldowns, capped at two refunds per cooldown cycle per
            // slot. Cooldown is earned by completing strings.
            if (connected && finisher) {
                ApplyMomentumRefund(ref attackerRuntime, ref attackerVerb);
            }
            // V7.6 F07: the finisher's caster-owned MARK, applied alongside (never
            // instead of) its status. Fighter Mode always uses the BASELINE
            // duration — the tesla_conductive_hold extension is a Story-only
            // Resonance node and must never reach the deterministic sim.
            if (connected && finisher && profile.FinisherMarkType == (int)FTT.Combat.ComboMarkType.Conductive) {
                FighterConductiveRules.ApplyMark(
                    ref targetConductive, attacker.PlayerID, profile.FinisherMarkFrames);
            }
        }

        /// <summary>
        /// Resonance Momentum's refund (V7.1): 60 frames off each RUNNING
        /// special cooldown, at most twice per cooldown cycle per slot (the
        /// counters reset when a slot's cooldown is armed).
        /// </summary>
        internal static void ApplyMomentumRefund(
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb) {
            const int refundFrames = FTT.Combat.BasicComboRules.MomentumRefundFrames;
            if (runtime.SpecialOneCooldownFrames > 0
                && verb.MomentumRefundsSlotOne < FTT.Combat.BasicComboRules.MomentumRefundCapPerCycle) {
                runtime.SpecialOneCooldownFrames =
                    runtime.SpecialOneCooldownFrames > refundFrames
                        ? runtime.SpecialOneCooldownFrames - refundFrames
                        : 0;
                verb.MomentumRefundsSlotOne++;
            }
            if (runtime.SpecialTwoCooldownFrames > 0
                && verb.MomentumRefundsSlotTwo < FTT.Combat.BasicComboRules.MomentumRefundCapPerCycle) {
                runtime.SpecialTwoCooldownFrames =
                    runtime.SpecialTwoCooldownFrames > refundFrames
                        ? runtime.SpecialTwoCooldownFrames - refundFrames
                        : 0;
                verb.MomentumRefundsSlotTwo++;
            }
        }

        /// <summary>
        /// The two directional strikes (§2.8). Up-attack reaches above the
        /// attacker's origin, down-air below it; both deal a flat 1.0x basic and
        /// launch the victim upward through <c>verticalKnockbackScale</c>. Same
        /// one-attempt-per-swing rule, same meter/status/block flow as the chain.
        /// </summary>
        private static void ApplyDirectionalSwing(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            in FighterTuningComponent attackerTuning,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning) {
            bool upAttack = (attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagUpAttack) != 0;
            FP64 horizontalReach = upAttack ? UpAttackHorizontalReach : DownAirHorizontalReach;
            if (FP64.Abs(target.Position.x - attacker.Position.x) > horizontalReach) return;

            // World Y is up: the up-attack box sits above the attacker's origin,
            // the down-air box below it. Neither reaches past the origin plane.
            FP64 verticalOffset = upAttack
                ? target.Position.y - attacker.Position.y
                : attacker.Position.y - target.Position.y;
            FP64 verticalReach = upAttack ? UpAttackVerticalReach : DownAirVerticalReach;
            if (verticalOffset < FP64.Zero || verticalOffset > verticalReach) return;

            int damage = ScaleDamage(attackerTuning.BasicDamage, DirectionalDamageMultiplier);
            // ApplyFighterHit derives both impulse axes from one magnitude, so the
            // small horizontal factor is folded into the magnitude and the
            // vertical scale carries the ratio back up to the authored 2.5x.
            FP64 knockback = attackerTuning.BasicKnockback * DirectionalHorizontalKnockback;
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            FighterDamageRules.ApplyFighterHit(
                ref attacker,
                ref attackerRuntime,
                ref attackerVerb,
                ref target,
                ref targetRuntime,
                ref targetVerb,
                ref targetDefense,
                in targetTuning,
                FighterDamageRules.BasicAttackClass,
                damage,
                knockback,
                FTT.Combat.BasicComboRules.DirectionalAttackHitstunFrames,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                attacker.Position.x,
                true,
                0,
                DirectionalVerticalKnockbackScale);
        }

        private static int ScaleDamage(int value, FP64 scale) {
            long numerator = (long)value * scale.RawValue + FP64.One.RawValue / 2;
            return (int)(numerator / FP64.One.RawValue);
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
                || FighterLedgeRules.IsHanging(in attackerRuntime)
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
            in FighterVerbComponent attackerVerb,
            in FighterTuningComponent tuning,
            in FighterStateComponent target) {
            if (attacker.HitstunFrames > 0
                || attacker.DazeFrames > 0
                || attacker.Stocks <= 0
                // Hanging suppresses the intent the way hitstun does (§2.11):
                // attacks, specials and the ultimate are all unavailable off a ledge.
                || FighterLedgeRules.IsHanging(in attackerRuntime)
                || FighterUniversalMovementRules.IsCombatLocked(in attackerRuntime)) return default;
            // The block stance ignores attack inputs, exactly as Story's Blocking
            // state does.
            if (FighterBasicAttackRules.IsBlockStance(in attacker, in attackerRuntime, in attackerVerb)) return default;
            if (FP64.Abs(target.Position.x - attacker.Position.x) > AttackRange) return default;
            // V7.3: melee-execution specials and the generic melee ultimate are
            // front-only, matching ResolveStringBox's convention — nothing in
            // the kit hits behind the attacker's back.
            bool targetInFront = attacker.FacingRight != 0
                ? target.Position.x >= attacker.Position.x
                : target.Position.x <= attacker.Position.x;
            if (!targetInFront) return default;

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
                    tuning.SpecialTwoCooldownFrames);
            }
            // Basic attacks no longer resolve here: the phase machine in
            // FighterMovementSystem starts and times the swing, and
            // ApplyBasicSwing lands the hit during its active window.
            return default;
        }

        private static void ApplyIntent(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            in AttackIntent intent) {
            if (intent.Kind == 0) return;

            FighterUniversalMovementRules.Cancel(ref attackerRuntime);
            // A special or the ultimate cancels an in-progress basic at any point
            // and resets the chain (design 1051) — the same rule Story applies.
            FighterBasicAttackRules.CancelString(ref attackerRuntime);

            if (intent.Kind == 2) {
                attackerRuntime.SpecialOneCooldownFrames = intent.CooldownFrames > 1 ? intent.CooldownFrames : 1;
                // A fresh cooldown cycle re-arms its Resonance Momentum refunds.
                attackerVerb.MomentumRefundsSlotOne = 0;
            } else if (intent.Kind == 4) {
                attackerRuntime.SpecialTwoCooldownFrames = intent.CooldownFrames > 1 ? intent.CooldownFrames : 1;
                attackerVerb.MomentumRefundsSlotTwo = 0;
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
                ref attackerVerb,
                ref target,
                ref targetRuntime,
                ref targetVerb,
                ref targetDefense,
                in targetTuning,
                attackClass,
                intent.Damage,
                intent.Knockback,
                intent.HitstunFrames,
                intent.StatusType,
                intent.StatusFrames,
                intent.StatusIntensity,
                attacker.Position.x,
                // V7.6 D03h (Package 11 A1b): a direct Ultimate impact awards
                // its caster ZERO damage-dealt meter. The Ultimate still costs
                // 100 on accepted activation; this only removes the refund.
                // Direct-hit Rally reclaim from an Ultimate impact is retained
                // (D03g), which is why collectsEcho is untouched.
                creditInfluence: attackClass != FighterDamageRules.UltimateAttackClass,
                blockChargeCost: intent.BlockChargeCost);
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
        /// <summary>V7.1 Overtime: the final 60 seconds of any timed match.</summary>
        public const int OvertimeFrames = 3600;

        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1) return;

            // V7: the clock ticks in every mode when enabled — Stock defaults to
            // the 8:00 timer as well (configurable, including Off); an infinite
            // stock match with two turtling players needs a horizon.
            if (match.TimerEnabled == 1 && match.RemainingFrames > 0) match.RemainingFrames--;
            bool timerExpired = match.TimerEnabled == 1 && match.RemainingFrames <= 0;
            UpdateOvertimeFlags(ref frame, in match);
            TrackKnockouts(ref frame, ref match, out int playerOneFell, out int playerTwoFell);

            // V7.1 Sudden Death: the first KO ends it — a simultaneous
            // double-KO is the one recorded Draw. Nothing else can end SD
            // (there is no timer and stocks are phantom).
            if (match.SuddenDeathActive == 1) {
                if (playerOneFell > 0 || playerTwoFell > 0) {
                    bool doubleKO = playerOneFell > 0 && playerTwoFell > 0;
                    match.MatchState = 2;
                    match.WinnerPlayerID = doubleKO ? -1 : playerOneFell > 0 ? 1 : 0;
                    match.IsTrueTie = doubleKO ? 1 : 0;
                }
                return;
            }

            int winner = FindStockWinner(ref frame, out bool allAlive, out bool trueTie);
            bool shouldEnd = match.MatchMode switch {
                (int)FTT.Core.MatchMode.Stock => !allAlive || timerExpired,
                (int)FTT.Core.MatchMode.TimeLimit => timerExpired,
                _ => !allAlive || timerExpired
            };
            if (shouldEnd) {
                if (match.MatchMode == (int)FTT.Core.MatchMode.TimeLimit) {
                    trueTie = match.PlayerOneKOs == match.PlayerTwoKOs;
                    winner = trueTie ? -1 : match.PlayerOneKOs > match.PlayerTwoKOs ? 0 : 1;
                }
                // V7.1: a true tie no longer records an immediate draw — the
                // match enters Sudden Death and the first hit decides it.
                if (trueTie) {
                    EnterSuddenDeath(ref frame, ref match);
                    return;
                }
                match.MatchState = 2;
                match.WinnerPlayerID = winner;
                match.IsTrueTie = 0;
            }
        }

        /// <summary>
        /// Sudden Death (V7.1, replaces the immediate draw): both fighters
        /// respawn at their spawn points with 1 HP, the timer is off, hazards
        /// are forced on at the accelerated cadence, Defy History is disabled
        /// (pre-marked used — the first hit must end it), and Rally is moot at
        /// 1 HP (the pool is cleared and nothing survivable accrues).
        /// </summary>
        private static void EnterSuddenDeath(ref Frame frame, ref FighterMatchComponent match) {
            match.SuddenDeathActive = 1;
            match.TimerEnabled = 0;
            match.RemainingFrames = 0;
            // V7.3 ruling #9: Chronal Orbs are OFF in Sudden Death. Live orbs
            // are cleared here and FighterOrbSystem early-returns for the rest
            // of the match, so nothing spawns, lingers, or awards.
            var orbFilter = frame.Filter<FighterOrbComponent>();
            while (orbFilter.Next(out EntityRef orbEntity)) {
                frame.DestroyEntity(orbEntity);
            }
            // Hazards are forced on at the accelerated cadence even when the
            // house rules had them off (a disabled match carries frequency 0,
            // which would leave the forced flag inert).
            match.HazardsEnabled = 1;
            if (match.HazardFrequency <= 0) match.HazardFrequency = 3;
            if (match.NextHazardSpawnFrames <= 0) {
                match.NextHazardSpawnFrames = FighterSpawnIntervals.HazardFrames(match.HazardFrequency);
            }
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterVerbComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(entity);
                // A double-elimination tie arrives with 0 stocks: grant one
                // phantom stock so the KO machinery can record the deciding hit.
                if (fighter.Stocks < 1) fighter.Stocks = 1;
                fighter.CurrentHP = 1;
                fighter.Position = fighter.SpawnPosition;
                fighter.Velocity = FPVector2.Zero;
                fighter.IsGrounded = 1;
                fighter.HitstunFrames = 0;
                fighter.DazeFrames = 0;
                fighter.RespawnFramesRemaining = 0;
                fighter.InvulnerabilityFrames = 0;
                verb.EchoPool = FP64.Zero;
                verb.EchoDrainPerFrame = FP64.Zero;
                verb.HitstopFrames = 0;
                verb.Tumble = 0;
                verb.PendingLaunchActive = 0;
                verb.EchoStepWindupFrames = 0;
                verb.ShieldStunFrames = 0;
                verb.BlockLockoutFrames = 0;
                verb.HitstunBlockCancelBlocked = 0;
                verb.LedgeGrabsThisAirtime = 0;
                // V7.6 F22/F13 (Package 11 A1b): Sudden Death BARS Defy without
                // marking it SPENT. The seal read-model must distinguish
                // "unavailable in this context" from "already used", and
                // pre-marking DefyHistoryUsed = 1 conflated them. The bar is
                // derived from match.SuddenDeathActive at the read site
                // (FighterDefenseRules / FighterDefySeal), so the underlying
                // spent flag is left exactly as the match left it.
                verb.OvertimeActive = 0;
                FighterUniversalMovementRules.Cancel(ref runtime);
                FighterBasicAttackRules.CancelString(ref runtime);
                FighterLedgeRules.ClearHang(ref runtime);
                // D04/D02e: Sudden Death cleanup clears the transient Defy
                // window, the Aegis bubble and any barrier.
                if (frame.Has<FighterDefenseComponent>(entity)) {
                    ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(entity);
                    FighterDefenseRules.Clear(ref defense);
                    runtime.AegisHits = 0;
                    // F22: Defy is BARRED for the rest of the match, and the bar
                    // survives the stock losses Sudden Death produces.
                    defense.DefyBarred = 1;
                }
            }
        }

        /// <summary>
        /// V7.1 Overtime: the final 3,600 frames of a timed match set every
        /// fighter's OvertimeActive flag (echo fraction ×1.5 capped 0.60 in the
        /// damage chokepoint; hazard cadence doubles in the hazard system).
        /// Not applied in Sudden Death or untimed matches.
        /// </summary>
        private static void UpdateOvertimeFlags(ref Frame frame, in FighterMatchComponent match) {
            int active = match.SuddenDeathActive == 0
                && match.TimerEnabled == 1
                && match.RemainingFrames > 0
                && match.RemainingFrames <= OvertimeFrames ? 1 : 0;
            var filter = frame.Filter<FighterVerbComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(entity);
                verb.OvertimeActive = active;
            }
        }

        private static void TrackKnockouts(
            ref Frame frame,
            ref FighterMatchComponent match,
            out int playerOneFell,
            out int playerTwoFell) {
            playerOneFell = 0;
            playerTwoFell = 0;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                ref readonly FighterRuntimeComponent runtime = ref frame.GetReadOnly<FighterRuntimeComponent>(entity);
                if (fighter.PlayerID == 0) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerOneKnockoutsSuffered;
                    if (gained > 0) match.PlayerTwoKOs += gained;
                    playerOneFell = gained;
                    match.LastPlayerOneKnockoutsSuffered = runtime.KnockoutsSuffered;
                } else if (fighter.PlayerID == 1) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerTwoKnockoutsSuffered;
                    if (gained > 0) match.PlayerOneKOs += gained;
                    playerTwoFell = gained;
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
        /// <summary>
        /// Resolves one stock loss: verb-layer wipe, stock decrement, meter
        /// retention, and the Chronal Respawn Platform placement.
        /// </summary>
        /// <param name="geometry">
        /// The match's stage geometry, when the caller holds one. It supplies the
        /// stage's authored respawn anchor (Package 11 A9): on an Open stage whose
        /// centre is a pit, the global stage-centre constant would drop the
        /// respawning fighter straight back into the hole. Callers without geometry
        /// in hand — the hit pipeline's static damage chokepoints — pass null and
        /// get the shared default; <c>ProcessRespawnPlatform</c> re-pins the
        /// fighter to the stage anchor on the next movement tick, before the
        /// platform ever drops them.
        /// </param>
        public static void ApplyStockLoss(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            in FighterTuningComponent tuning,
            FighterStageGeometry geometry = null) {
            if (fighter.Stocks <= 0) return;
            // V7.6 D04/D02e (Package 11 A1b): death and stock loss clear the
            // transient Defy protection window, the Aegis bubble and any HP
            // barrier. The SPENT Defy flag itself is deliberately untouched -
            // Defy is once per match, not once per stock.
            FighterDefenseRules.Clear(ref defense);
            runtime.AegisHits = 0;
            // V7.1: KO / stock loss clears the Echo Pool and every transient
            // verb-layer state; it never transfers across stocks.
            verb.EchoPool = FP64.Zero;
            verb.EchoDrainPerFrame = FP64.Zero;
            verb.Tumble = 0;
            verb.PendingLaunchActive = 0;
            verb.HitstopFrames = 0;
            verb.TechLockoutFrames = 0;
            verb.GrabPhase = 0;
            verb.GrabPhaseFrames = 0;
            verb.BeingHeld = 0;
            verb.ThrowImmunityFrames = 0;
            verb.EchoStepWindupFrames = 0;
            verb.ShieldStunFrames = 0;
            verb.BlockLockoutFrames = 0;
            verb.HitstunBlockCancelBlocked = 0;
            verb.LedgeGrabsThisAirtime = 0;
            runtime.KnockoutsSuffered++;
            if (runtime.UsesStocks != 0) fighter.Stocks--;
            fighter.Influence = fighter.Influence * FP64.FromInt(3) / FP64.FromInt(4);
            fighter.CurrentHP = fighter.MaxHP;
            fighter.BlockCharges = tuning.MaxBlockCharges;
            fighter.RemainingJumps = tuning.MaxJumpCount;
            // Chronal Respawn Platform, all match modes: the fighter materialises
            // frozen and invulnerable at the stage's respawn anchor (centre +3.0
            // unless the stage overrides it) rather than teleporting straight back
            // into play. The 3 s spawn invulnerability is armed when the platform
            // drops them, not here.
            fighter.Position =
                geometry?.RespawnPlatformPosition ?? FighterMatchFlowRules.RespawnPlatformPosition;
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
            runtime.DamageStatusType = (int)FTT.Core.StatusType.None;
            runtime.DamageStatusFrames = 0;
            runtime.StatusTickFrames = 0;
            runtime.StatusIntensity = FP64.One;
            runtime.DamageStatusIntensity = FP64.One;
            FighterUniversalMovementRules.Cancel(ref runtime);
            // Stock loss ends any swing and resets the chain and shield regen.
            FighterBasicAttackRules.CancelString(ref runtime);
            runtime.BlockRegenFrames = 0;
            // A fighter knocked off a ledge is no longer on it, and the respawn
            // platform must not inherit a regrab lockout from the last life.
            FighterLedgeRules.ClearHang(ref runtime);
            runtime.LedgeRegrabLockoutFrames = 0;
        }
    }
}
