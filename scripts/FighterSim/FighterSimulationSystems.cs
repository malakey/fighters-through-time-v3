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
            // M23 (Package 12 W5): the first orb's timing is a seeded draw inside
            // the frequency window, like every later one. Drawn only when orbs are
            // live, so an items-off match's RNG stream is untouched.
            int firstOrbFrames = _rules.ItemsEnabled && _rules.ItemFrequency > 0
                ? FighterOrbSpawnRules.DrawSpawnGap(ref random, _rules.ItemFrequency)
                : 0;
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
                HazardCadenceFrames = _rules.HazardCadenceFrames,
                StageHazardTypeID = _rules.StageHazardTypeID,
                MeterPickupsEnabled = _rules.MeterPickupsEnabled ? 1 : 0,
                NextOrbSpawnFrames = firstOrbFrames,
                NextHazardSpawnFrames = _rules.HazardCadenceFrames,
                RandomState0 = state0,
                RandomState1 = state1
            });

            // F22 phase identity + frozen regulation totals (Package 11 A1c).
            frame.Add(matchEntity, new FighterSuddenDeathComponent());

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
            // Klotho zero-initializes, and 0 is a real player ID, so the grab
            // attachment's "unattached" sentinel has to be written in (F23).
            var verb = new FighterVerbComponent();
            verb.ClearGrabPartner();
            frame.Add(entity, verb);
            // V7.6 Echo Step history (Package 11 A1c): the 31-sample per-tick ring
            // across components 313-317. Storage is filled with the spawn
            // coordinate, but ValidCount is 0 — filled slots are NOT fabricated
            // history, so the fighter must live 30 further ticks before an exact
            // t-30 sample exists (TEMPORAL_STATE_CONTRACT.md).
            frame.Add(entity, new FighterEchoStepRing0Component());
            frame.Add(entity, new FighterEchoStepRing1Component());
            frame.Add(entity, new FighterEchoStepRing2Component());
            frame.Add(entity, new FighterEchoStepRing3Component());
            frame.Add(entity, new FighterEchoStepRing4Component());
            FighterEchoStepHistory.Reset(ref frame, entity, in spawn, lifeEpoch: 0);
            // V7.6 F07 (Package 11 A1): the caster-owned Conductive mark. Not a
            // status — no slot, no action lock, zero stagger budget.
            frame.Add(entity, new FighterConductiveComponent {
                FramesRemaining = 0,
                SourcePlayerID = -1,
                ChainConsumedExecutionID = 0
            });
            // M05 (Package 12 W3b): knockdown and get-up, component 320. All
            // zero is "not down".
            frame.Add(entity, new FighterKnockdownComponent());
            // A02 (Package 13 W6): the Ultimate activation strike and the
            // cinematic hold, component 321. All zero is inactive.
            frame.Add(entity, new FighterUltimateActivationComponent());
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
        /// <summary>C01c logical Echo Step request (direct bind or enabled chord).</summary>
        private const int EchoStepButton = 1 << 12;
        /// <summary>C01c "chords are off on the originating device" flag; see GameplayButtons.DirectOrigin.</summary>
        private const int DirectOriginButton = 1 << 14;
        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(60);
        private static readonly FP64 Gravity = FP64.FromInt(-30);
        /// <summary>Signed floor for fast-fall: -<c>UniversalMovementRules.FastFallSpeed</c>.</summary>
        private static readonly FP64 FastFallSpeed = -FP64.FromDouble(UniversalMovementRules.FastFallSpeed);
        /// <summary>
        /// M01 (D1(a)): signed terminal fall velocity, -<c>UniversalMovementRules.TerminalFallSpeed</c>.
        /// A stateless clamp applied after every gravity integration.
        /// </summary>
        private static readonly FP64 TerminalFallVelocity = -FP64.FromDouble(UniversalMovementRules.TerminalFallSpeed);

        /// <summary>Clamps a Y-up vertical velocity at the terminal fall speed (M01).</summary>
        internal static FP64 ClampToTerminal(FP64 velocityY) =>
            velocityY < TerminalFallVelocity ? TerminalFallVelocity : velocityY;
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

                // V7.6 Echo Step history (Package 11 A1c): exactly one sample per
                // authoritative tick, recorded HERE — above the hitstop
                // short-circuit and above every input and movement read — because
                // the contract's "t - 30" has to mean thirty ticks, not thirty
                // ticks in which the fighter happened not to be frozen. An
                // advancing hitstop tick records the same position, as specified.
                FighterEchoStepHistory.Sample(
                    ref frame, entity, in fighter.Position, runtime.KnockoutsSuffered);

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

                // A02 (Package 13 W6, component 321): a fighter held by an
                // opponent's Ultimate cinematic takes no input and does not move
                // until the combat system releases the hold. Their statuses and
                // cooldowns keep ticking; a lethal tick hands them to the respawn.
                ref FighterUltimateActivationComponent ultimateActivation =
                    ref frame.Get<FighterUltimateActivationComponent>(entity);
                if (FighterUltimateActivationRules.IsCaptured(in ultimateActivation)) {
                    ref FighterDefenseComponent capturedDefense = ref frame.Get<FighterDefenseComponent>(entity);
                    TickCounters(ref fighter, ref runtime, ref verb, ref capturedDefense, in tuning);
                    if (fighter.Stocks > 0 && fighter.RespawnFramesRemaining <= 0) {
                        FighterUltimateActivationRules.HoldCaptured(ref fighter, ref runtime, in ultimateActivation);
                    }
                    continue;
                }

                // V7.6 Echo Step: advance an armed wind-up (the snap fires when it
                // reaches 0, after the destination is rechecked). Sampling already
                // happened above, before the hitstop gate.
                AdvanceEchoStep(ref frame, entity, ref fighter, ref verb);

                // V7.6 D01-D04 (Package 11 A1b): the Venom tick inside
                // TickCounters routes through the defensive layer, so the
                // component travels with it.
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(entity);

                // M05 knockdown and get-up (Package 12 W3b, component 320): a
                // sub-phase of Stunned. The downed fighter is invulnerable (the
                // shared InvulnerabilityFrames grant) and takes no action; the
                // get-up that follows is vulnerable and just as locked. A hit
                // during the get-up (or a stock loss) ends the sub-phase.
                ref FighterKnockdownComponent knockdown = ref frame.Get<FighterKnockdownComponent>(entity);
                FighterKnockdownRules.ClearIfInterrupted(in fighter, ref knockdown);
                // A12 (Package 13 W1): a bounce whose tumble ended another way is
                // dropped. A03: a stock loss refreshes the air dodge (the
                // stock-loss chokepoint has no Frame; the platform is the tell).
                FighterSlamRules.ClearIfStale(in fighter, in verb, ref knockdown);
                if (FighterStockLossRules.IsFallen(in fighter)) FighterAirDodgeRules.Refresh(ref knockdown);
                if (FighterKnockdownRules.IsActive(in knockdown)) {
                    TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning);
                    // A lethal Venom tick inside TickCounters is a stock loss;
                    // the respawn owns the fighter from the next tick.
                    FighterKnockdownRules.ClearIfInterrupted(in fighter, ref knockdown);
                    if (!FighterKnockdownRules.IsActive(in knockdown)) continue;
                    // The action lock: this tick's action buttons are discarded
                    // before the ability, combat and grab systems read them
                    // (the countdown system's rule). The stick survives — it
                    // chooses the get-up.
                    runtime.PressedButtons = 0;
                    runtime.HeldButtons = 0;
                    runtime.ReleasedButtons = 0;
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    FighterKnockdownRules.Advance(ref fighter, runtime.MoveX, in tuning, ref knockdown);
                    fighter.Position += fighter.Velocity * FixedDelta;
                    fighter.Position.x = FP64.Clamp(fighter.Position.x, _geometry.LeftWall, _geometry.RightWall);
                    // A roll get-up can carry the fighter off a platform end or
                    // an Open-stage floor edge: the get-up ends and they fall.
                    if (!HasGroundSupport(in fighter)) {
                        fighter.IsGrounded = 0;
                        FighterKnockdownRules.Clear(ref knockdown);
                    }
                    continue;
                }

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

                // A02: the caster is action-locked through the whole Ultimate —
                // wind-up, active, whiff recovery and cinematic. Discarding the
                // tick's input here, before any verb reads it, is what stops Echo
                // Step (or a block, roll, jump or grab) undoing a whiff.
                if (FighterUltimateActivationRules.IsCasterBusy(in ultimateActivation)) {
                    FighterUltimateActivationRules.DiscardInput(ref runtime);
                }

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
                // M06 (Package 12 W3): tumble is excluded — a launched victim
                // techs on ground contact (TryLandingTech) and never escapes
                // into the stance. The gate is the shared pure rule Story reads.
                if (fighter.HitstunFrames > 0
                    && fighter.DazeFrames <= 0
                    && fighter.Stocks > 0
                    && FTT.Combat.BasicComboRules.CanBlockEscapeHitstun(
                        grounded: fighter.IsGrounded != 0,
                        blockHeld: (runtime.HeldButtons & BlockButton) != 0,
                        tumbling: verb.Tumble != 0,
                        stringHitOneGate: verb.HitstunBlockCancelBlocked != 0,
                        stanceCanRise: fighter.BlockCharges > 0 && verb.BlockLockoutFrames <= 0)) {
                    fighter.HitstunFrames = 0;
                }
                if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) {
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    fighter.Velocity.y = ClampToTerminal(fighter.Velocity.y + Gravity * FixedDelta);
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
                        ref knockdown,
                        rooted,
                        // An Echo Step wind-up owns the Roll press that armed it.
                        allowRoll: !attacking && verb.EchoStepWindupFrames == 0 && !shieldStunned);
                    // A03: an air dodge adds no speed; airborne it leaves normal
                    // air control running (jumps withheld), grounded it decelerates.
                    bool airDodging = FighterAirDodgeRules.IsAirDodge(runtime.UniversalMovementState);
                    // Package 12 W4: Tesla's blink and Pocahontas's Spirit Strike run
                    // as sim-local phases of the same universal-movement slot.
                    bool movementHandled = FighterKitMotion.IsKitPhase(runtime.UniversalMovementState)
                        ? FighterKitMotion.Process(
                            ref fighter,
                            ref runtime,
                            in frame.GetReadOnly<FighterAbilityModeComponent>(entity),
                            tuning.MoveSpeed / FP64.FromInt(UniversalMovementRules.RunDecelerationFrames),
                            // Package 13 W7a: the Warp fold's clearance check.
                            _geometry)
                        : airDodging
                            ? FighterAirDodgeRules.Advance(
                                ref fighter,
                                ref runtime,
                                tuning.MoveSpeed / FP64.FromInt(UniversalMovementRules.RunDecelerationFrames))
                            : ProcessUniversalMovement(
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
                            // M04 (Package 12 W3): Blocking → Airborne on Jump;
                            // only shieldstun keeps a blocker grounded.
                            allowJump: !attacking && !shieldStunned && !airDodging,
                            allowDropThrough: !shieldStunned && !airDodging);
                    }
                    // Package 12 W4: the blink hover/translation and the Spirit Strike
                    // carry fly straight — no gravity, no fast-fall snap.
                    if (fighter.IsGrounded == 0 && !FighterKitMotion.SuspendsGravity(in runtime)) {
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
                        // M01 (D1(a)): fast-fall SNAPS to the terminal speed
                        // (it no longer only floors the descent), and every
                        // airborne fall is clamped at terminal.
                        if (fastFalling) fighter.Velocity.y = FastFallSpeed;
                        fighter.Velocity.y = ClampToTerminal(fighter.Velocity.y);
                    }
                }

                // A02: the wind-up and active frames plant the caster (gravity 0
                // for an airborne start) or drive a melee lunge.
                FighterUltimateActivationRules.ApplyCasterMotion(ref fighter, in ultimateActivation);

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
                // M05 (Package 12 W3b): a tumble that lands WITHOUT the tech is a
                // missed tech — the knockdown begins on the landing tick.
                // A12 (Package 13 W1): a slammed victim's first ground contact
                // is the forced, untechable bounce — neither tech nor knockdown.
                if (wasAirborne && fighter.IsGrounded != 0
                    && !FighterSlamRules.TryBounce(ref fighter, in runtime, ref verb, ref knockdown)
                    && !FighterVerbRules.TryLandingTech(ref fighter, in runtime, ref verb)) {
                    FighterKnockdownRules.TryBeginKnockdown(ref fighter, ref verb, ref knockdown);
                }

                // V7.3 regrab cap: grounding resets the per-airtime ledge budget.
                // A03: landing refreshes the air dodge too.
                if (fighter.IsGrounded != 0) {
                    verb.LedgeGrabsThisAirtime = 0;
                    FighterAirDodgeRules.Refresh(ref knockdown);
                }

                // §2.11 ledge capture, resolved last so landing and the ground snap
                // both win: a fighter who reached a surface is standing on it, not
                // hanging off it. Only fighters still airborne after the whole
                // resolve are candidates. A03: a grab refreshes the air dodge.
                if (TryGrabLedge(ref fighter, ref runtime, ref verb, in tuning)) {
                    FighterAirDodgeRules.Refresh(ref knockdown);
                }
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
        private bool TryGrabLedge(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            in FighterTuningComponent tuning) {
            if (!FighterLedgeRules.CanGrab(in fighter, in runtime, in verb)) return false;
            if (!_geometry.TryFindLedge(in fighter.Position, out int anchor)) return false;
            if (!_geometry.TryGetHangPosition(anchor, out FPVector2 hangPosition)) return false;
            FighterLedgeRules.Grab(ref fighter, ref runtime, ref verb, in tuning, anchor, in hangPosition);
            return true;
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
            ref FighterKnockdownComponent knockdown,
            bool rooted,
            bool allowRoll = true) {
            // A03 (Package 13 W1): Roll pressed while airborne is the air dodge.
            // A05: Root refuses it exactly as it refuses the grounded roll.
            if (fighter.IsGrounded == 0) {
                if (allowRoll && (runtime.PressedButtons & RollButton) != 0) {
                    FighterAirDodgeRules.TryStart(ref fighter, ref runtime, ref knockdown, rooted);
                }
                return;
            }
            if (rooted
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
        /// Advances an armed Echo Step wind-up. At completion the <b>same</b>
        /// destination is rechecked against then-current geometry: if it became
        /// blocked the teleport is cancelled and the committed meter and cooldown
        /// are <b>retained</b> — no refund, no nearby substitute, no extra recovery
        /// penalty (TEMPORAL_STATE_CONTRACT.md step 5). On a successful arrival
        /// velocity is zeroed, facing is preserved, and grounded-versus-airborne
        /// locomotion is chosen from the destination's actual contact.
        ///
        /// <para>An effective interruption — hitstun, daze, death, or entering a
        /// grab on either side — cancels the wind-up outright: the pending teleport
        /// must never fire afterwards. The ghost telegraph was the opponent's read
        /// and they took it, so the cost stands.</para>
        /// </summary>
        private void AdvanceEchoStep(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb) {
            if (verb.EchoStepWindupFrames <= 0) return;
            if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0 || fighter.Stocks <= 0
                || FighterGrabRules.IsBusy(in verb)) {
                CancelArmedEchoStep(ref frame, entity, ref verb);
                return;
            }
            verb.EchoStepWindupFrames--;
            if (verb.EchoStepWindupFrames != 0) return;

            var destination = new FPVector2(verb.EchoStepDestX, verb.EchoStepDestY);
            ref FighterEchoStepRing0Component ring = ref frame.Get<FighterEchoStepRing0Component>(entity);
            ring.Armed = 0;
            // Blocked at completion: cancel the teleport, keep the cost.
            if (!FighterEchoStepRules.IsDestinationClear(_geometry, in destination)) return;
            fighter.Position = destination;
            fighter.Velocity = FPVector2.Zero;
            // Grounded versus airborne comes from the destination's actual contact:
            // the ground snap re-resolves on this tick's integration, and an
            // elevated destination falls.
            if (fighter.Position.y > FP64.Zero) fighter.IsGrounded = 0;
        }

        private static void CancelArmedEchoStep(
            ref Frame frame, EntityRef entity, ref FighterVerbComponent verb) {
            verb.EchoStepWindupFrames = 0;
            ref FighterEchoStepRing0Component ring = ref frame.Get<FighterEchoStepRing0Component>(entity);
            ring.Armed = 0;
        }

        /// <summary>
        /// Echo Step initiation (V7.6). During the recovery frames of the fighter's
        /// own swing the Block+Roll chord — or the direct <c>gameplay_echo_step</c>
        /// bind — snaps to the position held <b>exactly</b> 30 ticks earlier.
        ///
        /// <para><b>Exact or nothing.</b> The destination is resolved before
        /// anything is spent, and a refusal (insufficient history, or a destination
        /// inside terrain / an authored kill region) costs <b>no meter and no
        /// cooldown</b> and does not start the wind-up. An accepted activation
        /// spends the 30 meter, starts the 120-frame cooldown and arms the 8-frame
        /// wind-up atomically, once.</para>
        ///
        /// <para>Never an escape: hitstun, daze, a ledge hang, the respawn platform,
        /// a grab on either side, and hitstop (the movement loop skips frozen
        /// fighters before reaching here) all refuse it.</para>
        /// </summary>
        private void TryStartEchoStep(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb) {
            if (!EchoStepStateAllows(in fighter, in runtime, in verb)) return;
            if (runtime.AttackPhase != FighterBasicAttackRules.PhaseRecovery) return;

            // Resolve the destination FIRST: its availability is part of whether the
            // activation is legal at all, so an illegal activation never reaches the
            // meter.
            bool hasDestination =
                FighterEchoStepHistory.TryGetLookback(ref frame, entity, out FPVector2 destination)
                && FighterEchoStepRules.IsDestinationClear(_geometry, in destination);
            FP64 cost = FP64.FromInt(FTT.Combat.BasicComboRules.EchoStepMeterCost);
            bool echoLegal = hasDestination && fighter.Influence >= cost;

            // V7.6 same-frame chord priority, from the one shared table both modes
            // read. A direct bind requests the same verb once and grants no extra
            // priority, cancel, leniency or resource bypass.
            bool chordAllowed = (runtime.HeldButtons & DirectOriginButton) == 0;
            bool directRequest = (runtime.PressedButtons & EchoStepButton) != 0;
            FTT.Combat.BasicComboRules.RecoveryVerb chosen =
                FTT.Combat.BasicComboRules.SelectRecoveryVerb(
                    blockHeld: chordAllowed && (runtime.HeldButtons & BlockButton) != 0,
                    blockPressed: (runtime.PressedButtons & BlockButton) != 0,
                    rollHeld: (runtime.HeldButtons & RollButton) != 0,
                    rollPressed: (runtime.PressedButtons & RollButton) != 0,
                    basicPressed: (runtime.PressedButtons & BasicButton) != 0,
                    grabLegal: FighterGrabRules.CanStartGrab(in fighter, in runtime, in verb),
                    echoLegal: echoLegal);
            bool requested =
                directRequest || chosen == FTT.Combat.BasicComboRules.RecoveryVerb.EchoStep;
            if (!requested) return;
            // A refused direct action does nothing and spends nothing.
            if (!echoLegal) return;

            fighter.Influence -= cost;
            verb.EchoStepCooldownFrames = FTT.Combat.BasicComboRules.EchoStepCooldownFrames;
            verb.EchoStepWindupFrames = FTT.Combat.BasicComboRules.EchoStepWindupFrames;
            verb.EchoStepDestX = destination.x;
            verb.EchoStepDestY = destination.y;
            ref FighterEchoStepRing0Component ring = ref frame.Get<FighterEchoStepRing0Component>(entity);
            ring.Armed = 1;
            ring.ActivationTick = ring.LatestTick;
        }

        /// <summary>The state gates an Echo Step shares with every other verb.</summary>
        private static bool EchoStepStateAllows(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) {
            if (verb.EchoStepWindupFrames > 0 || verb.EchoStepCooldownFrames > 0) return false;
            if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0 || fighter.Stocks <= 0) return false;
            if (FighterGrabRules.IsBusy(in verb)) return false;
            if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) return false;
            if (FighterLedgeRules.IsHanging(in runtime)) return false;
            return true;
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
                or UniversalMovementPhase.RollRecovery
                // A03 (Package 13 W1): the air dodge is a Rolling sub-phase.
                or UniversalMovementPhase.AirDodgeStartup
                or UniversalMovementPhase.AirDodgeInvulnerable
                or UniversalMovementPhase.AirDodgeRecovery
                // Package 12 W4: a blink or Spirit Strike in flight is an action.
                || FighterKitMotion.IsKitPhase(runtime.UniversalMovementState);
        }

        public static void Cancel(ref FighterRuntimeComponent runtime) {
            runtime.UniversalMovementState = (int)UniversalMovementPhase.None;
            runtime.UniversalMovementFramesRemaining = 0;
            runtime.UniversalMovementDirection = 0;
        }
    }

    /// <summary>
    /// A03 (Package 13 W1): the deterministic air dodge — Roll pressed while
    /// airborne. It runs in the existing universal-movement slot of component
    /// 305 as phases 5–7 (<see cref="UniversalMovementPhase.AirDodgeStartup"/> …
    /// <see cref="UniversalMovementPhase.AirDodgeRecovery"/>), so hits cancel it
    /// and it snapshots for free; the only new state is the once-per-airtime
    /// latch <c>AirDodgeUsed</c> on component 320 (plan D7).
    ///
    /// <para>No speed is added: gravity, momentum and normal air control keep
    /// running (the movement system still integrates them), and a held
    /// direction contributes only a positional shift of
    /// <see cref="UniversalMovementRules.AirDodgeShiftUnits"/> spread evenly over
    /// the eight invulnerable frames. The invulnerable frames also switch the
    /// pushbox off. Landing does not cancel the dodge — the remaining recovery
    /// plays out grounded, decelerating like a roll recovery.</para>
    ///
    /// <para><see cref="FighterRuntimeComponent.UniversalMovementDirection"/>
    /// carries the held direction as <c>(dx + 1) * 3 + (dy + 1)</c>, dx and dy in
    /// {-1, 0, 1} (4 = neutral).</para>
    /// </summary>
    internal static class FighterAirDodgeRules {
        private const int NeutralDirectionCode = 4;
        private static readonly FP64 AxisShiftPerFrame = FP64.FromDouble(
            UniversalMovementRules.AirDodgeShiftUnits / UniversalMovementRules.AirDodgeInvulnerableFrames);
        private static readonly FP64 DiagonalShiftPerFrame = FP64.FromDouble(
            UniversalMovementRules.AirDodgeShiftUnits / UniversalMovementRules.AirDodgeInvulnerableFrames
                * 0.70710678118654752);

        public static bool IsAirDodge(int universalMovementState) =>
            universalMovementState is (int)UniversalMovementPhase.AirDodgeStartup
                or (int)UniversalMovementPhase.AirDodgeInvulnerable
                or (int)UniversalMovementPhase.AirDodgeRecovery;

        /// <summary>The invulnerable, pushbox-free window.</summary>
        public static bool IsInvulnerablePhase(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == (int)UniversalMovementPhase.AirDodgeInvulnerable;

        /// <summary>
        /// Starts an air dodge if one is legal: airborne, not already dodged
        /// this airtime, not rooted (A05), no other universal movement running.
        /// The caller has already excluded hitstun, daze, hitstop, grab states,
        /// the ledge hang and any live swing (an aerial string's recovery or
        /// chain window is cancelled by the same Roll press just before this).
        /// </summary>
        public static bool TryStart(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterKnockdownComponent knockdown,
            bool rooted) {
            if (rooted
                || fighter.IsGrounded != 0
                || knockdown.AirDodgeUsed != 0
                || runtime.UniversalMovementState != (int)UniversalMovementPhase.None) return false;
            int dx = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
            // World Y is up; a negative MoveY stick value is "up held".
            int dy = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
            knockdown.AirDodgeUsed = 1;
            runtime.UniversalMovementState = (int)UniversalMovementPhase.AirDodgeStartup;
            runtime.UniversalMovementFramesRemaining = UniversalMovementRules.AirDodgeStartupFrames;
            runtime.UniversalMovementDirection = (dx + 1) * 3 + (dy + 1);
            return true;
        }

        /// <summary>
        /// Advances one dodge tick. Returns true when the dodge owns this tick's
        /// horizontal motion (a grounded recovery, decelerating); false while
        /// airborne, so normal air control still runs — with jumps withheld.
        /// </summary>
        public static bool Advance(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            FP64 groundDecelStep) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            if (phase == UniversalMovementPhase.AirDodgeInvulnerable) {
                if (runtime.UniversalMovementFramesRemaining == UniversalMovementRules.AirDodgeInvulnerableFrames
                    && fighter.InvulnerabilityFrames < UniversalMovementRules.AirDodgeInvulnerableFrames) {
                    fighter.InvulnerabilityFrames = UniversalMovementRules.AirDodgeInvulnerableFrames;
                }
                ApplyShift(ref fighter, runtime.UniversalMovementDirection);
            }

            runtime.UniversalMovementFramesRemaining--;
            if (runtime.UniversalMovementFramesRemaining <= 0) {
                switch (phase) {
                    case UniversalMovementPhase.AirDodgeStartup:
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.AirDodgeInvulnerable;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.AirDodgeInvulnerableFrames;
                        break;
                    case UniversalMovementPhase.AirDodgeInvulnerable:
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.AirDodgeRecovery;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.AirDodgeRecoveryFrames;
                        break;
                    default:
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        break;
                }
            }

            if (fighter.IsGrounded == 0) return false;
            // Landed mid-dodge: the rest plays out grounded, bleeding speed off.
            if (fighter.Velocity.x > FP64.Zero) {
                fighter.Velocity.x = FP64.Max(fighter.Velocity.x - groundDecelStep, FP64.Zero);
            } else if (fighter.Velocity.x < FP64.Zero) {
                fighter.Velocity.x = FP64.Min(fighter.Velocity.x + groundDecelStep, FP64.Zero);
            }
            return true;
        }

        private static void ApplyShift(ref FighterStateComponent fighter, int directionCode) {
            if (directionCode == NeutralDirectionCode) return;
            int dx = directionCode / 3 - 1;
            int dy = directionCode % 3 - 1;
            FP64 step = dx != 0 && dy != 0 ? DiagonalShiftPerFrame : AxisShiftPerFrame;
            fighter.Position.x += step * FP64.FromInt(dx);
            fighter.Position.y += step * FP64.FromInt(dy);
        }

        /// <summary>Landing, a ledge grab or a stock loss refreshes the once-per-airtime dodge.</summary>
        public static void Refresh(ref FighterKnockdownComponent knockdown) => knockdown.AirDodgeUsed = 0;
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
        /// <summary>
        /// A13 (Package 13 W1): the universal 0.6-unit lower-torso pushbox —
        /// two fighters at contact stand this far apart. Was 0.8, wider than
        /// the grab could reach from contact.
        /// </summary>
        public static readonly FP64 MinimumHorizontalDistance =
            FP64.FromDouble(FTT.Combat.BasicComboRules.PushboxWidthUnits);
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
            // A03 (Package 13 W1): the air dodge's invulnerable frames drop the pushbox.
            if (FighterAirDodgeRules.IsInvulnerablePhase(in firstRuntime)
                || FighterAirDodgeRules.IsInvulnerablePhase(in secondRuntime)) return;
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
        /// <summary>
        /// M08/M05 (Package 12 W3b): the two fighters' authored hit contracts —
        /// static match configuration, like a system's stage geometry, never
        /// snapshot state. The melee Special/Ultimate intents read their hitstun
        /// and launch flag here instead of the retired fixed 18/30 frames.
        /// </summary>
        private readonly FighterHitContractTable _contracts;

        public FighterCombatSystem(FighterHitContractTable contracts = null) {
            _contracts = contracts ?? FighterHitContractTable.Default;
        }

        /// <summary>
        /// The generic melee intent's legacy contract, kept only for a slot whose
        /// loadout carries no authored contract (a hand-built test loadout that
        /// left <see cref="FighterAbilityLoadout"/> at its zero default).
        /// </summary>
        internal const int LegacySpecialHitstunFrames = 18;
        // A02 (Package 13 W6): the generic melee Ultimate intent is gone, and its
        // 30-frame legacy hitstun with it; the finale reads
        // UltimateActivationRules.FinaleHitstunFrames.

        /// <summary>
        /// Resolves the hitstun and launch flag a melee intent applies: the
        /// authored contract when one is projected, the legacy 18/30 frames and
        /// "launches" otherwise.
        /// </summary>
        internal static void ResolveIntentContract(
            in FighterAbilityHitData contract, int legacyHitstunFrames,
            out int hitstunFrames, out bool launches) {
            if (contract.HitstunFrames > 0) {
                hitstunFrames = contract.HitstunFrames;
                launches = contract.Launches;
            } else {
                hitstunFrames = legacyHitstunFrames;
                launches = true;
            }
        }

        private const int BasicButton = 1 << 2;
        private const int SpecialOneButton = 1 << 3;
        private const int SpecialTwoButton = 1 << 4;
        private const int UltimateButton = 1 << 7;
        // A01 (Package 13 W1): an ordinary blocked Special spends min(2,
        // charges); the three authored Shield-Breakers (Divine Piercing, The
        // Emancipator, Splitting Strike) spend every charge — 1, 2 or 3 go to 0
        // with the normal shatter response. The block class rides the
        // projected FighterAbilityHitData, never a blockChargeCost override.
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

            // A02 (Package 13 W6): live activations advance first — the phase
            // clock, the strike's contact test, the cinematic hold and the D15
            // finale — then a fresh press is accepted. Acceptance spends the
            // meter, so the ultimate can never also reach a melee intent below.
            AdvanceUltimate(ref frame, first, second);
            AdvanceUltimate(ref frame, second, first);
            if (oneActing) TryCharacterUltimate(ref frame, first, ref fighterOne, ref runtimeOne);
            if (twoActing) TryCharacterUltimate(ref frame, second, ref fighterTwo, ref runtimeTwo);

            AttackIntent firstIntent = !oneActing ? default : BuildIntent(in fighterOne, in runtimeOne, in verbOne, in tuningOne, in fighterTwo, _contracts);
            AttackIntent secondIntent = !twoActing ? default : BuildIntent(in fighterTwo, in runtimeTwo, in verbTwo, in tuningTwo, in fighterOne, _contracts);
            ApplyIntent(ref fighterOne, ref runtimeOne, ref verbOne, ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, in firstIntent);
            ApplyIntent(ref fighterTwo, ref runtimeTwo, ref verbTwo, ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, in secondIntent);
            // F07 marks are written onto the VICTIM's component 318.
            ref FighterConductiveComponent conductiveOne = ref frame.Get<FighterConductiveComponent>(first);
            ref FighterConductiveComponent conductiveTwo = ref frame.Get<FighterConductiveComponent>(second);
            // M09 (Package 12 W3): a stock loss clears the victim's mark. The
            // stock-loss chokepoint has no Frame, so the respawn platform (and
            // an out-of-stocks fighter) is the observable state read here — the
            // mark can never survive onto the next life.
            ClearMarkIfRespawning(in fighterOne, ref conductiveOne);
            ClearMarkIfRespawning(in fighterTwo, ref conductiveTwo);
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
            // A12 (Package 13 W1): a Down-Air slam owes its victim one forced
            // ground bounce, recorded on the victim's component 320.
            ref FighterKnockdownComponent knockdownOne = ref frame.Get<FighterKnockdownComponent>(first);
            ref FighterKnockdownComponent knockdownTwo = ref frame.Get<FighterKnockdownComponent>(second);
            if (oneActing) {
                ApplyBasicSwing(ref fighterOne, ref runtimeOne, ref verbOne, in tuningOne, ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, ref conductiveTwo, ref knockdownTwo);
                ApplyConstructSwing(ref frame, ref fighterOne, ref runtimeOne, in tuningOne);
            }
            if (twoActing) {
                ApplyBasicSwing(ref fighterTwo, ref runtimeTwo, ref verbTwo, in tuningTwo, ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, ref conductiveOne, ref knockdownOne);
                ApplyConstructSwing(ref frame, ref fighterTwo, ref runtimeTwo, in tuningTwo);
            }
        }

        /// <summary>
        /// M09: the Conductive mark does not survive a stock loss. A fighter on
        /// the Chronal Respawn Platform (or out of stocks) is by definition past
        /// a stock loss, so its mark is dropped.
        /// </summary>
        internal static void ClearMarkIfRespawning(
            in FighterStateComponent fighter, ref FighterConductiveComponent mark) {
            if (mark.FramesRemaining <= 0 && mark.SourcePlayerID < 0) return;
            if (fighter.RespawnFramesRemaining > 0 || fighter.Stocks <= 0) {
                FighterConductiveRules.Clear(ref mark);
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
            // C01c: the direct bind and the preset chord request the same verb,
            // once (FighterGrabRules.Requested deduplicates them).
            if (!oneFrozen && FighterGrabRules.Requested(in runtimeOne)
                && FighterGrabRules.CanStartGrab(in one, in runtimeOne, in verbOne)) {
                verbOne.GrabPhase = FighterGrabRules.PhaseStartup;
                verbOne.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
                verbOne.ClearGrabPartner();
            }
            if (!twoFrozen && FighterGrabRules.Requested(in runtimeTwo)
                && FighterGrabRules.CanStartGrab(in two, in runtimeTwo, in verbTwo)) {
                verbTwo.GrabPhase = FighterGrabRules.PhaseStartup;
                verbTwo.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
                verbTwo.ClearGrabPartner();
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
                opponentVerb.ClearGrabPartner();
            }
            verb.GrabPhase = FighterGrabRules.PhaseNone;
            verb.GrabPhaseFrames = 0;
            verb.ClearGrabPartner();
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
            // F23 (Package 11 A1c): the attachment is explicit snapshot state now.
            // The two-fighter systems used to resolve the partner positionally,
            // which no restored snapshot could describe, and the throw had no
            // once-only damage guard beyond the phase machine.
            grabberVerb.GrabPartnerPlayerID = victim.PlayerID;
            grabberVerb.ThrowDamageApplied = false;
            victimVerb.GrabPartnerPlayerID = grabber.PlayerID;
            victimVerb.ThrowDamageApplied = false;
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
                    grabberVerb.ClearGrabPartner();
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
                    grabberVerb.ClearGrabPartner();
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
            // F23 once-only guard: a duplicate resolve (a restored snapshot that
            // re-enters the same throw frame) must not deal the damage twice.
            if (grabberVerb.ThrowDamageApplied) {
                grabberVerb.GrabPhase = FighterGrabRules.PhaseNone;
                grabberVerb.GrabPhaseFrames = 0;
                grabberVerb.ClearGrabPartner();
                victimVerb.ClearGrabPartner();
                return;
            }
            grabberVerb.ThrowDamageApplied = true;
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
                bypassesFiniteShields: true,
                // M05: all three throws launch.
                launches: FTT.Combat.BasicComboRules.ThrowLaunches);
            // Trajectories are fixed — no DI on throws (the throw IS the
            // decision), so the stashed launch never resolves through DI.
            victimVerb.PendingLaunchActive = 0;
            victimVerb.ThrowImmunityFrames = FTT.Combat.BasicComboRules.ThrowImmunityFrames;
            grabberVerb.GrabPhase = FighterGrabRules.PhaseNone;
            grabberVerb.GrabPhaseFrames = 0;
            grabberVerb.ClearGrabPartner();
            victimVerb.ClearGrabPartner();
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
            ref FighterConductiveComponent targetConductive,
            ref FighterKnockdownComponent targetKnockdown) {
            if (attackerRuntime.AttackPhase != FighterBasicAttackRules.PhaseActive) return;
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0) return;
            if (attacker.Stocks <= 0) return;

            // Directional attacks (§2.8) own their own boxes: the Up-Attack
            // launches upward, the Down-Air slams (A12).
            if (FighterBasicAttackRules.IsVariantSwing(in attackerRuntime)) {
                ApplyDirectionalSwing(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetKnockdown);
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
                blockCancelableHitstun: step >= 1,
                // M05/M07 (Package 12 W3b): the finisher launches, hits 1-2 are
                // grounded knockback — except Lincoln's launching hit 2.
                launches: FTT.Combat.BasicComboRules.StringHitLaunchesFor(profile, step));
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
            in FighterTuningComponent targetTuning,
            ref FighterKnockdownComponent targetKnockdown) {
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
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            if (!upAttack) {
                ApplyDownAirSlam(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetKnockdown, damage);
                return;
            }
            // ApplyFighterHit derives both impulse axes from one magnitude, so the
            // small horizontal factor is folded into the magnitude and the
            // vertical scale carries the ratio back up to the authored 2.5x.
            FP64 knockback = attackerTuning.BasicKnockback * DirectionalHorizontalKnockback;
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
                DirectionalVerticalKnockbackScale,
                // M05: the Up-Attack and the Down-Air are authored launchers.
                launches: FTT.Combat.BasicComboRules.DirectionalAttackLaunches);
        }

        /// <summary>A12: the slam's downward magnitude factor — the Up-Attack's vertical profile.</summary>
        private static readonly FP64 DownAirSlamVerticalKnockback =
            FP64.FromDouble(FTT.Combat.BasicComboRules.DirectionalAttackVerticalKnockback);

        /// <summary>
        /// A12 (Package 13 W1): the Down-Air is a Smash-style slam. It spikes the
        /// victim STRAIGHT DOWN — a signed vector (0, −2.5 × base) through D10's
        /// vector path, so weight and low-HP scaling apply and DI still bends it
        /// ±15° at hitstop end. A connecting slam that leaves the victim tumbling
        /// owes it one forced ground bounce (<see cref="FighterSlamRules"/>),
        /// consumed by the movement system on the first floor or one-way surface
        /// contact. Frame data and damage are unchanged.
        /// </summary>
        private static void ApplyDownAirSlam(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            in FighterTuningComponent attackerTuning,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            ref FighterKnockdownComponent targetKnockdown,
            int damage) {
            bool landed = FighterDamageRules.ApplyFighterHit(
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
                FP64.Zero,
                FTT.Combat.BasicComboRules.DirectionalAttackHitstunFrames,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                attacker.Position.x,
                launches: FTT.Combat.BasicComboRules.DirectionalAttackLaunches,
                hasKnockbackVector: true,
                knockbackVertical: -(attackerTuning.BasicKnockback * DownAirSlamVerticalKnockback));
            if (landed) FighterSlamRules.TryArm(in target, in targetVerb, ref targetKnockdown);
        }

        private static int ScaleDamage(int value, FP64 scale) {
            long numerator = (long)value * scale.RawValue + FP64.One.RawValue / 2;
            return (int)(numerator / FP64.One.RawValue);
        }

        /// <summary>
        /// A02 acceptance: a pressed Ultimate with a full meter, from a state
        /// that could act, starts the activation wind-up. Nothing is dealt yet;
        /// see <see cref="AdvanceUltimate"/>.
        /// </summary>
        private void TryCharacterUltimate(
            ref Frame frame,
            EntityRef attackerEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime) {
            if ((attackerRuntime.PressedButtons & UltimateButton) == 0
                || attacker.Influence < MaxInfluence
                || attacker.HitstunFrames > 0
                || attacker.DazeFrames > 0
                || attacker.Stocks <= 0
                || attacker.RespawnFramesRemaining > 0
                || FighterLedgeRules.IsHanging(in attackerRuntime)
                || FighterUniversalMovementRules.IsCombatLocked(in attackerRuntime)) return;
            ref FighterUltimateActivationComponent activation =
                ref frame.Get<FighterUltimateActivationComponent>(attackerEntity);
            if (FighterUltimateActivationRules.IsCasterBusy(in activation)
                || FighterUltimateActivationRules.IsCaptured(in activation)) return;
            FighterUltimateActivationRules.Accept(
                ref attacker, ref attackerRuntime, ref activation, _contracts.UltimateFor(attacker.PlayerID));
        }

        /// <summary>
        /// A02: one tick of <paramref name="casterEntity"/>'s Ultimate. The
        /// wind-up, active and whiff clocks pause under hitstop (a frozen
        /// fighter takes no action); the cinematic hold never does. A grab, a
        /// stock loss or — during whiff recovery — hitstun or daze ends the
        /// Ultimate with the meter still spent.
        /// </summary>
        private void AdvanceUltimate(ref Frame frame, EntityRef casterEntity, EntityRef targetEntity) {
            ref FighterUltimateActivationComponent activation =
                ref frame.Get<FighterUltimateActivationComponent>(casterEntity);
            if (activation.Phase == FighterUltimateActivationRules.PhaseNone) return;

            ref FighterStateComponent caster = ref frame.Get<FighterStateComponent>(casterEntity);
            ref FighterVerbComponent casterVerb = ref frame.Get<FighterVerbComponent>(casterEntity);
            ref FighterUltimateActivationComponent targetActivation =
                ref frame.Get<FighterUltimateActivationComponent>(targetEntity);
            int captorSlot = caster.PlayerID + 1;

            if (caster.Stocks <= 0 || caster.RespawnFramesRemaining > 0 || casterVerb.BeingHeld != 0) {
                if (targetActivation.CaptorSlot == captorSlot) {
                    FighterUltimateActivationRules.ReleaseCapture(ref targetActivation);
                }
                FighterUltimateActivationRules.ClearCaster(ref caster, ref activation);
                return;
            }

            FighterUltimateData data = _contracts.UltimateFor(caster.PlayerID);
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);

            if (activation.Phase == FighterUltimateActivationRules.PhaseCinematic) {
                // The victim is gone (KO, Defy's protected recovery, or someone
                // else's hold): the cinematic ends with no finale.
                if (target.Stocks <= 0
                    || target.RespawnFramesRemaining > 0
                    || FighterDefenseRules.IsDefyProtected(in targetDefense)
                    || targetActivation.CaptorSlot != captorSlot) {
                    if (targetActivation.CaptorSlot == captorSlot) {
                        FighterUltimateActivationRules.ReleaseCapture(ref targetActivation);
                    }
                    FighterUltimateActivationRules.ClearCaster(ref caster, ref activation);
                    return;
                }
                activation.PhaseFrames--;
                targetActivation.CapturedFrames = activation.PhaseFrames;
                if (activation.PhaseFrames > 0) return;
                bool facingRight = activation.FacingRight != 0;
                FighterUltimateActivationRules.ReleaseCapture(ref targetActivation);
                FighterUltimateActivationRules.ClearCaster(ref caster, ref activation);
                if (data.HasFinale) {
                    DeliverUltimateFinale(ref frame, casterEntity, targetEntity, in data, facingRight);
                }
                return;
            }

            if (casterVerb.HitstopFrames > 0) return;

            if (activation.Phase == FighterUltimateActivationRules.PhaseWhiffRecovery) {
                // Whiff recovery is the punish window: a hit that stuns or dazes
                // the caster ends it (the punish owns the fighter now).
                if (caster.HitstunFrames > 0 || caster.DazeFrames > 0 || --activation.PhaseFrames <= 0) {
                    FighterUltimateActivationRules.ClearCaster(ref caster, ref activation);
                }
                return;
            }

            if (activation.Phase == FighterUltimateActivationRules.PhaseWindup) {
                if (--activation.PhaseFrames > 0) return;
                activation.Phase = FighterUltimateActivationRules.PhaseActive;
                activation.PhaseFrames = data.ActiveFrames > 0 ? data.ActiveFrames : 1;
                activation.Origin = caster.Position;
            }

            int activeFrames = data.ActiveFrames > 0 ? data.ActiveFrames : 1;
            int activeIndex = activeFrames - activation.PhaseFrames;
            if (FighterUltimateActivationRules.StrikeConnects(
                    in caster, in activation, in data, activeIndex,
                    in target, in targetDefense, in targetActivation)) {
                StartUltimateCinematic(ref frame, casterEntity, targetEntity, in data);
                return;
            }
            if (--activation.PhaseFrames > 0) return;
            // The whiff: nothing is dealt, the meter stays spent, and the caster
            // sits in whiff recovery (armor off).
            caster.HyperArmorFrames = 0;
            if (data.WhiffRecoveryFrames > 0) {
                activation.Phase = FighterUltimateActivationRules.PhaseWhiffRecovery;
                activation.PhaseFrames = data.WhiffRecoveryFrames;
            } else {
                FighterUltimateActivationRules.ClearCaster(ref caster, ref activation);
            }
        }

        /// <summary>
        /// A02 contact: the victim is held at the contact point for the whole
        /// cinematic (their own Ultimate, grab, Echo Step wind-up, swing and
        /// movement are cancelled) and the character's existing Ultimate starts
        /// on them at its full authored damage. Aegis- or barrier-absorbed
        /// contact is contact: those layers resolve against the cinematic's hits.
        /// </summary>
        private static void StartUltimateCinematic(
            ref Frame frame, EntityRef casterEntity, EntityRef targetEntity, in FighterUltimateData data) {
            ref FighterStateComponent caster = ref frame.Get<FighterStateComponent>(casterEntity);
            ref FighterRuntimeComponent casterRuntime = ref frame.Get<FighterRuntimeComponent>(casterEntity);
            ref readonly FighterTuningComponent casterTuning = ref frame.GetReadOnly<FighterTuningComponent>(casterEntity);
            ref FighterUltimateActivationComponent activation =
                ref frame.Get<FighterUltimateActivationComponent>(casterEntity);
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            ref FighterUltimateActivationComponent targetActivation =
                ref frame.Get<FighterUltimateActivationComponent>(targetEntity);

            caster.HyperArmorFrames = 0;
            activation.Phase = FighterUltimateActivationRules.PhaseCinematic;
            activation.PhaseFrames = data.CinematicFrames;

            FPVector2 anchor = target.Position;
            if (FighterUltimateActivationRules.IsCasterBusy(in targetActivation)) {
                FighterUltimateActivationRules.ClearCaster(ref target, ref targetActivation);
            }
            targetActivation.CaptorSlot = caster.PlayerID + 1;
            targetActivation.CapturedFrames = activation.PhaseFrames;
            targetActivation.CaptureAnchor = anchor;
            target.Velocity = FPVector2.Zero;
            FighterUniversalMovementRules.Cancel(ref targetRuntime);
            FighterBasicAttackRules.CancelString(ref targetRuntime);
            if (FighterLedgeRules.IsHanging(in targetRuntime)) FighterLedgeRules.ClearHang(ref targetRuntime);
            if (targetVerb.GrabPhase != FighterGrabRules.PhaseNone) {
                targetVerb.GrabPhase = FighterGrabRules.PhaseNone;
                targetVerb.GrabPhaseFrames = 0;
                targetVerb.ClearGrabPartner();
            }
            // An armed Echo Step wind-up cannot snap the victim out of the hold;
            // its committed cost stays spent, as on any interruption.
            targetVerb.EchoStepWindupFrames = 0;
            targetVerb.PendingLaunchActive = 0;

            if (FighterUltimateRules.TryExecute(
                    ref frame, casterEntity, targetEntity, ref caster, ref casterRuntime,
                    in casterTuning, in data, in anchor)) return;

            // A character with no bespoke cinematic lands its whole sequence as
            // one Ultimate hit and releases at once.
            FighterUltimateActivationRules.ReleaseCapture(ref targetActivation);
            bool facingRight = activation.FacingRight != 0;
            FighterUltimateActivationRules.ClearCaster(ref caster, ref activation);
            ref FighterVerbComponent casterVerb = ref frame.Get<FighterVerbComponent>(casterEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            int total = casterTuning.UltimateDamage * (data.HitCount > 0 ? data.HitCount : 1) + data.FinaleDamage;
            FighterDamageRules.ApplyFighterHit(
                ref caster, ref casterRuntime, ref casterVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                FighterDamageRules.UltimateAttackClass, total, casterTuning.UltimateKnockback,
                FTT.Combat.UltimateActivationRules.FinaleHitstunFrames,
                casterTuning.UltimateStatusType, casterTuning.UltimateStatusFrames, casterTuning.UltimateStatusIntensity,
                FinaleOriginX(in target, facingRight),
                creditInfluence: false);
        }

        /// <summary>
        /// D15: the finale that closes the cinematic — one Ultimate-class direct
        /// hit of the authored <c>FinaleDamage</c> with the authored Ultimate
        /// knockback, carrying a damage-over-time status (C04's Venom) but never
        /// a control hold, launching when <c>FinaleLaunches</c>, and
        /// driving the victim in the caster's facing direction, toward the
        /// blast zone. D03h: zero damage-dealt meter; D03g: a direct impact
        /// keeps its Rally reclaim.
        /// </summary>
        private static void DeliverUltimateFinale(
            ref Frame frame, EntityRef casterEntity, EntityRef targetEntity,
            in FighterUltimateData data, bool facingRight) {
            ref FighterStateComponent caster = ref frame.Get<FighterStateComponent>(casterEntity);
            ref FighterRuntimeComponent casterRuntime = ref frame.Get<FighterRuntimeComponent>(casterEntity);
            ref FighterVerbComponent casterVerb = ref frame.Get<FighterVerbComponent>(casterEntity);
            ref readonly FighterTuningComponent casterTuning = ref frame.GetReadOnly<FighterTuningComponent>(casterEntity);
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            bool carriesStatus = FTT.Combat.UltimateActivationRules.StatusRidesFinale(
                (FTT.Core.StatusType)casterTuning.UltimateStatusType, data.FinaleDamage);
            FighterDamageRules.ApplyFighterHit(
                ref caster, ref casterRuntime, ref casterVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                FighterDamageRules.UltimateAttackClass, data.FinaleDamage, casterTuning.UltimateKnockback,
                FTT.Combat.UltimateActivationRules.FinaleHitstunFrames,
                carriesStatus ? casterTuning.UltimateStatusType : (int)FTT.Core.StatusType.None,
                carriesStatus ? casterTuning.UltimateStatusFrames : 0,
                casterTuning.UltimateStatusIntensity,
                FinaleOriginX(in target, facingRight),
                creditInfluence: false,
                launches: data.FinaleLaunches);
        }

        /// <summary>A hit origin one unit behind the victim, so the knockback points along the caster's facing.</summary>
        private static FP64 FinaleOriginX(in FighterStateComponent target, bool casterFacingRight) =>
            casterFacingRight ? target.Position.x - FP64.One : target.Position.x + FP64.One;

        private static AttackIntent BuildIntent(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterVerbComponent attackerVerb,
            in FighterTuningComponent tuning,
            in FighterStateComponent target,
            FighterHitContractTable contracts) {
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

            // M08/M05 (Package 12 W3b): hitstun and launch come from the
            // attacker's authored contract (DEFER-SIM-ABILITY-HITSTUN, generic
            // half), not the old fixed 18/30 frames.
            // A02 (Package 13 W6): there is no melee-range Ultimate intent any
            // more — every Ultimate is accepted in TryCharacterUltimate, which
            // spends the meter before this runs, and lands only through its
            // activation strike (AdvanceUltimate).
            // A01/D10 (Package 13 W1): a melee Special intent reads the
            // contract's block class (Shield-Breaker) and signed knockback.
            if ((attackerRuntime.PressedButtons & SpecialOneButton) != 0 && attackerRuntime.SpecialOneCooldownFrames <= 0) {
                FighterAbilityHitData contract = contracts.For(attacker.PlayerID, FighterHitContractTable.SlotSpecialOne);
                ResolveIntentContract(contract, LegacySpecialHitstunFrames, out int hitstun, out bool launches);
                return AttackIntent.Special(
                    2,
                    tuning.SpecialOneDamage,
                    tuning.SpecialOneKnockback,
                    hitstun,
                    tuning.SpecialOneStatusType,
                    tuning.SpecialOneStatusFrames,
                    tuning.SpecialOneStatusIntensity,
                    tuning.SpecialOneCooldownFrames,
                    launches,
                    in contract);
            }
            if ((attackerRuntime.PressedButtons & SpecialTwoButton) != 0 && attackerRuntime.SpecialTwoCooldownFrames <= 0) {
                FighterAbilityHitData contract = contracts.For(attacker.PlayerID, FighterHitContractTable.SlotSpecialTwo);
                ResolveIntentContract(contract, LegacySpecialHitstunFrames, out int hitstun, out bool launches);
                return AttackIntent.Special(
                    4,
                    tuning.SpecialTwoDamage,
                    tuning.SpecialTwoKnockback,
                    hitstun,
                    tuning.SpecialTwoStatusType,
                    tuning.SpecialTwoStatusFrames,
                    tuning.SpecialTwoStatusIntensity,
                    tuning.SpecialTwoCooldownFrames,
                    launches,
                    in contract);
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
                blockChargeCost: intent.BlockChargeCost,
                launches: intent.Launches,
                shieldBreaker: intent.ShieldBreaker,
                hasKnockbackVector: intent.HasKnockbackVector,
                knockbackVertical: intent.KnockbackVertical);
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
            /// <summary>M05: the authored launch flag of the ability this intent executes.</summary>
            public readonly bool Launches;
            /// <summary>A01: the ability is an authored Shield-Breaker.</summary>
            public readonly bool ShieldBreaker;
            /// <summary>D10: the knockback is the authored signed vector (Knockback = |x|).</summary>
            public readonly bool HasKnockbackVector;
            /// <summary>D10: the authored Y-up vertical; negative is a spike.</summary>
            public readonly FP64 KnockbackVertical;

            public AttackIntent(
                int kind,
                int damage,
                FP64 knockback,
                int hitstunFrames,
                int statusType = (int)FTT.Core.StatusType.None,
                int statusFrames = 0,
                FP64 statusIntensity = default,
                int cooldownFrames = 600,
                int blockChargeCost = 0,
                bool launches = true,
                bool shieldBreaker = false,
                bool hasKnockbackVector = false,
                FP64 knockbackVertical = default) {
                Kind = kind;
                Damage = damage;
                Knockback = knockback;
                HitstunFrames = hitstunFrames;
                StatusType = statusType;
                StatusFrames = statusFrames;
                StatusIntensity = statusIntensity;
                CooldownFrames = cooldownFrames;
                BlockChargeCost = blockChargeCost;
                Launches = launches;
                ShieldBreaker = shieldBreaker;
                HasKnockbackVector = hasKnockbackVector;
                KnockbackVertical = knockbackVertical;
            }

            /// <summary>
            /// D10: the intent's knockback — the contract's signed vector when
            /// one is projected, otherwise the historical tuning scalar.
            /// </summary>
            public static AttackIntent Special(
                int kind, int damage, FP64 tuningKnockback, int hitstunFrames,
                int statusType, int statusFrames, FP64 statusIntensity, int cooldownFrames,
                bool launches, in FighterAbilityHitData contract) =>
                new(kind, damage,
                    contract.HasKnockbackVector ? contract.KnockbackX : tuningKnockback,
                    hitstunFrames, statusType, statusFrames, statusIntensity, cooldownFrames,
                    launches: launches,
                    shieldBreaker: contract.ShieldBreaker,
                    hasKnockbackVector: contract.HasKnockbackVector,
                    knockbackVertical: contract.KnockbackY);
        }
    }

    public sealed class FighterMatchSystem : ISystem {
        /// <summary>V7.1 Overtime: the final 60 seconds of any timed match.</summary>
        public const int OvertimeFrames = 3600;

        private readonly FighterStageGeometry _geometry;

        public FighterMatchSystem(FighterStageGeometry geometry = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1) return;

            // V7: the clock ticks in every mode when enabled — Stock defaults to
            // the 8:00 timer as well (configurable, including Off); an infinite
            // stock match with two turtling players needs a horizon.
            if (match.TimerEnabled == 1 && match.RemainingFrames > 0) match.RemainingFrames--;
            bool timerExpired = match.TimerEnabled == 1 && match.RemainingFrames <= 0;
            UpdateOvertimeFlags(ref frame, in match);
            TrackStockLosses(ref frame, ref match, out int playerOneFell, out int playerTwoFell);
            // A04 (Package 13 W1): a stock lost anywhere in this tick — the
            // blast zone included, which resolves after the object systems ran
            // — despawns the fallen fighter's objects and zones on this tick.
            if (playerOneFell > 0 || playerTwoFell > 0) {
                FighterStockLossRules.DespawnOwnedBy(ref frame, playerOneFell > 0, playerTwoFell > 0);
            }

            // F22: the next ACTUAL death ends it — one dead fighter loses, two in
            // the same tick is the existing Draw. Nothing else can end Sudden Death
            // (there is no timer and no stock pool).
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
            // F21: the silent `_ =>` default arm is gone. Hybrid is retired, and an
            // unrecognized mode falls back to Stock explicitly rather than
            // inheriting whatever the last arm happened to be.
            bool shouldEnd = match.MatchMode == (int)FTT.Core.MatchMode.TimeLimit
                ? timerExpired
                : !allAlive || timerExpired;
            if (!shouldEnd) return;

            if (match.MatchMode == (int)FTT.Core.MatchMode.TimeLimit) {
                // F21 Time mode: FEWEST STOCKS LOST, no HP tiebreak and no
                // attacker credit. The fields this reads used to count KOs SCORED
                // and the comparison picked the larger — the opposite bookkeeping.
                // Every KO in this regulation tick has already been settled by
                // TrackStockLosses above, so both totals are final here.
                trueTie = match.PlayerOneStocksLost == match.PlayerTwoStocksLost;
                winner = trueTie ? -1 : match.PlayerOneStocksLost < match.PlayerTwoStocksLost ? 0 : 1;
            }
            // A true tie — including Time's 0-0 — enters Sudden Death rather than
            // recording a draw.
            if (trueTie) {
                EnterSuddenDeath(ref frame, ref match);
                return;
            }
            match.MatchState = 2;
            match.WinnerPlayerID = winner;
            match.IsTrueTie = 0;
        }

        /// <summary>
        /// F22 Sudden Death entry (Package 11 A1c) — a shared <b>round reset</b>
        /// that keeps what the contract says to keep and clears everything else.
        /// The retired V7.1 version clamped both fighters to 1 HP, forced hazards
        /// on over the house rules, and marked Defy used; all three are gone.
        ///
        /// <para>Per <c>FIGHTER_MATCH_RULES.md</c> "Transition state":</para>
        /// <list type="bullet">
        /// <item>a <b>living</b> fighter keeps its exact current positive HP,
        /// including unequal HP in a Time tie — no heal, no normalization, no clamp
        /// to 1;</item>
        /// <item>a fighter already dead or mid-respawn at the end of regulation
        /// receives its <b>normal Fighter respawn HP</b> exactly once, resolved
        /// from the committed end-of-regulation life state and recorded on
        /// component 319 so a duplicate transition cannot grant it twice. No extra
        /// stock loss, no change to the frozen totals, no new spawn protection;</item>
        /// <item>hazards <b>respect the match toggle</b> — Off stays Off. If On, the
        /// first full warning starts after play resumes at twice-normal cadence,
        /// and the regulation Overtime multiplier does not carry in;</item>
        /// <item>meter goes to 0, every cooldown is ready, block refills, both
        /// status slots, the Rally pool and the Echo Step history are cleared, and
        /// every regulation projectile, zone, construct and orb is removed;</item>
        /// <item>Defy is disabled for the phase by <em>derivation</em> from
        /// <c>SuddenDeathActive</c> — its spent flag is preserved untouched, so no
        /// new use is consumed merely on entry;</item>
        /// <item>the regulation stocks-lost totals are frozen for the results
        /// screen, and the transition is committed together with the phase
        /// generation so a late regulation event is discarded by identity.</item>
        /// </list>
        /// </summary>
        private void EnterSuddenDeath(ref Frame frame, ref FighterMatchComponent match) {
            ref FighterSuddenDeathComponent phase = ref frame.GetSingleton<FighterSuddenDeathComponent>();
            // Committed together with the phase flip: identity first, so anything
            // reading the generation sees the new phase and the frozen totals as
            // one state.
            phase.PhaseGeneration++;
            phase.FrozenPlayerOneStocksLost = match.PlayerOneStocksLost;
            phase.FrozenPlayerTwoStocksLost = match.PlayerTwoStocksLost;
            match.SuddenDeathActive = 1;
            match.TimerEnabled = 0;
            match.RemainingFrames = 0;

            // Remove every regulation orb and disable spawning/collection for the
            // rest of the match (V7.3 ruling #9; FighterOrbSystem early-returns).
            var orbFilter = frame.Filter<FighterOrbComponent>();
            while (orbFilter.Next(out EntityRef orbEntity)) frame.DestroyEntity(orbEntity);
            // "Remove every regulation projectile, attack zone, summon, construct
            // and temporary ability platform, including objects whose owner died.
            // Clear pending damage/events associated with them."
            var projectileFilter = frame.Filter<FighterProjectileComponent>();
            while (projectileFilter.Next(out EntityRef projectile)) frame.DestroyEntity(projectile);
            var zoneFilter = frame.Filter<FighterZoneComponent>();
            while (zoneFilter.Next(out EntityRef zone)) frame.DestroyEntity(zone);
            var constructFilter = frame.Filter<FighterPersistentObjectComponent>();
            while (constructFilter.Next(out EntityRef construct)) frame.DestroyEntity(construct);
            // The arena returns to its authored round-start state: live hazards are
            // cleared and the first warning is scheduled fresh below.
            var hazardFilter = frame.Filter<FighterHazardComponent>();
            while (hazardFilter.Next(out EntityRef hazard)) frame.DestroyEntity(hazard);

            // "Respect the match's hazard toggle. If Off, no hazard warnings or
            // damage." The retired version forced HazardsEnabled = 1 here, which
            // overrode the house rules outright.
            if (match.HazardsEnabled == 1 && match.HazardCadenceFrames > 0) {
                // Twice-normal cadence by halving idle and recovery while warning
                // and active durations are retained, and never multiplied again by
                // regulation Overtime (every OvertimeActive flag is cleared below).
                int interval = match.HazardCadenceFrames / 2;
                match.NextHazardSpawnFrames = interval > 0 ? interval : 1;
            } else {
                match.NextHazardSpawnFrames = 0;
            }
            match.NextOrbSpawnFrames = 0;

            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterVerbComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(entity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);

                // Dead-entry resolution, Option A. "Already dead or in the normal
                // respawn sequence" is exactly HP <= 0, out of stocks, or still on
                // the respawn platform; anything else is a living fighter whose
                // current positive HP is preserved untouched.
                bool deadOrRespawning = fighter.CurrentHP <= 0
                    || fighter.Stocks <= 0
                    || fighter.RespawnFramesRemaining > 0;
                if (deadOrRespawning && !RespawnHPAlreadyGranted(in phase, fighter.PlayerID)) {
                    fighter.CurrentHP = fighter.MaxHP;
                    MarkRespawnHPGranted(ref phase, fighter.PlayerID);
                }
                // One decisive life each: no stock pool, no further respawns. This
                // is not a stock LOSS — the frozen regulation totals are untouched.
                fighter.Stocks = 1;

                fighter.Position = fighter.SpawnPosition;
                fighter.Velocity = FPVector2.Zero;
                fighter.IsGrounded = 1;
                fighter.HitstunFrames = 0;
                fighter.DazeFrames = 0;
                fighter.HyperArmorFrames = 0;
                fighter.DropThroughFrames = 0;
                fighter.RespawnFramesRemaining = 0;
                // "Clear prior respawn platforms and invulnerability, plus old
                // attack/roll invulnerability... once play resumes, no new
                // spawn-invulnerability period is granted."
                fighter.InvulnerabilityFrames = 0;
                fighter.BlockCharges = tuning.MaxBlockCharges;
                fighter.RemainingJumps = tuning.MaxJumpCount;
                // Meter 0; no pending Ultimate.
                fighter.Influence = FP64.Zero;

                verb.EchoPool = FP64.Zero;
                verb.EchoDrainPerFrame = FP64.Zero;
                verb.HitstopFrames = 0;
                verb.Tumble = 0;
                verb.PendingLaunchActive = 0;
                verb.TechLockoutFrames = 0;
                verb.EchoStepWindupFrames = 0;
                verb.EchoStepCooldownFrames = 0;
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
                verb.GrabPhase = FighterGrabRules.PhaseNone;
                verb.GrabPhaseFrames = 0;
                verb.BeingHeld = 0;
                verb.ThrowImmunityFrames = 0;
                verb.ClearGrabPartner();
                verb.MomentumRefundsSlotOne = 0;
                verb.MomentumRefundsSlotTwo = 0;
                // No Overtime multiplier inside the phase.
                verb.OvertimeActive = 0;
                // "Preserve its spent flag": DefyHistoryUsed is deliberately NOT
                // written here. The phase disable is derived from
                // match.SuddenDeathActive at the read site, so entry consumes no
                // new use and restoring a snapshot cannot resurrect a stale copy.

                runtime.SpecialOneCooldownFrames = 0;
                runtime.SpecialTwoCooldownFrames = 0;
                runtime.MovementCooldownFrames = 0;
                runtime.BasicCooldownFrames = 0;
                runtime.BlockRegenFrames = 0;
                runtime.SpeedBuffFrames = 0;
                runtime.JumpBuffFrames = 0;
                runtime.ZoneSpeedBonusFrames = 0;
                runtime.FloatFrames = 0;
                runtime.AegisHits = 0;
                // Both status slots, plus marks and tethers.
                runtime.StatusType = (int)FTT.Core.StatusType.None;
                runtime.StatusFrames = 0;
                runtime.StatusIntensity = FP64.One;
                runtime.DamageStatusType = (int)FTT.Core.StatusType.None;
                runtime.DamageStatusFrames = 0;
                runtime.DamageStatusIntensity = FP64.One;
                runtime.StatusTickFrames = 0;
                // Empty input buffers: no queued regulation attack fires after the
                // restart.
                runtime.MoveX = 0;
                runtime.MoveY = 0;
                runtime.HeldButtons = 0;
                runtime.PressedButtons = 0;
                runtime.ReleasedButtons = 0;
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
                runtime.LedgeRegrabLockoutFrames = 0;
                if (frame.Has<FighterConductiveComponent>(entity)) {
                    ref FighterConductiveComponent conductive =
                        ref frame.Get<FighterConductiveComponent>(entity);
                    conductive.FramesRemaining = 0;
                    conductive.SourcePlayerID = -1;
                    conductive.ChainConsumedExecutionID = 0;
                }
                // M05 (Package 12 W3b): nobody starts Sudden Death on the floor.
                if (frame.Has<FighterKnockdownComponent>(entity)) {
                    ref FighterKnockdownComponent knockdown =
                        ref frame.Get<FighterKnockdownComponent>(entity);
                    FighterKnockdownRules.Clear(ref knockdown);
                }
                // A02 (Package 13 W6): no activation, whiff recovery, cinematic
                // or hold survives into Sudden Death ("no pending Ultimate").
                if (frame.Has<FighterUltimateActivationComponent>(entity)) {
                    ref FighterUltimateActivationComponent ultimateActivation =
                        ref frame.Get<FighterUltimateActivationComponent>(entity);
                    FighterUltimateActivationRules.ClearCaster(ref fighter, ref ultimateActivation);
                    FighterUltimateActivationRules.ReleaseCapture(ref ultimateActivation);
                }
                // "Initialize a new Echo Step history generation at the spawn...
                // Require 30 subsequent simulation ticks before use; never teleport
                // into regulation history."
                FighterEchoStepHistory.Reset(
                    ref frame, entity, in fighter.SpawnPosition, runtime.KnockoutsSuffered);
            }

            // The common match-start ready countdown: controls and simulation
            // clocks are frozen together for both players and resume together.
            match.CountdownFramesRemaining = FighterMatchFlowRules.CountdownFrames;
            match.MatchState = FighterMatchStates.Countdown;
            match.GoBannerFramesRemaining = 0;
        }

        private static bool RespawnHPAlreadyGranted(in FighterSuddenDeathComponent phase, int playerID) =>
            playerID == 0 ? phase.PlayerOneRespawnHPGranted != 0 : phase.PlayerTwoRespawnHPGranted != 0;

        private static void MarkRespawnHPGranted(ref FighterSuddenDeathComponent phase, int playerID) {
            if (playerID == 0) phase.PlayerOneRespawnHPGranted = 1;
            else phase.PlayerTwoRespawnHPGranted = 1;
        }

        /// <summary>
        /// V7.1 Overtime: the final 3,600 frames of a timed match set every
        /// fighter's OvertimeActive flag (echo fraction x1.5 capped 0.60 in the
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

        /// <summary>
        /// F21: mirrors each fighter's own <c>KnockoutsSuffered</c> — the victim-side
        /// count the single chokepoint <c>FighterSimulationRules.ApplyStockLoss</c>
        /// already raises for every cause — onto the match totals.
        ///
        /// <para>Because the source is that one chokepoint, the increment is
        /// atomic with the living-to-KO transition and before respawn, every cause
        /// (opponent damage, pit, hazard, self-KO, DoT, a surviving projectile or
        /// construct) counts identically with no attacker credit, a hit prevented
        /// by Defy or invulnerability never reaches it, co-occurring lethal damage
        /// and a blast-zone crossing produce one death and one increment, and
        /// duplicate callbacks cannot repeat it. The totals are rollback snapshot
        /// and hash state, so resimulation replaces them rather than adding to an
        /// external counter.</para>
        /// </summary>
        private static void TrackStockLosses(
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
                    if (gained > 0 && match.SuddenDeathActive == 0) match.PlayerOneStocksLost += gained;
                    playerOneFell = gained;
                    match.LastPlayerOneKnockoutsSuffered = runtime.KnockoutsSuffered;
                } else if (fighter.PlayerID == 1) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerTwoKnockoutsSuffered;
                    if (gained > 0 && match.SuddenDeathActive == 0) match.PlayerTwoStocksLost += gained;
                    playerTwoFell = gained;
                    match.LastPlayerTwoKnockoutsSuffered = runtime.KnockoutsSuffered;
                }
            }
        }

        /// <summary>
        /// Stock-mode timeout: remaining stocks, then remaining HP <b>as a fraction
        /// of that fighter's own maximum</b> (V7.3 ruling #8), which is why the
        /// comparison cross-multiplies rather than comparing HP directly.
        ///
        /// <para>F21's "unreclaimed Rally echo is excluded from HP comparisons" needs
        /// no arithmetic here: the Echo Pool is <em>reclaimable</em> HP held beside
        /// <c>CurrentHP</c>, and only an actual reclaim adds it
        /// (<c>FighterDamageRules</c>). Reading <c>CurrentHP</c> therefore excludes it
        /// by construction — subtracting the pool as well would penalize the victim
        /// twice.</para>
        /// </summary>
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
            verb.ClearGrabPartner();
            // F21: the single victim-side increment, atomic with the living-to-KO
            // transition and before the respawn placement below. Every cause
            // reaches this one chokepoint, so nothing needs attacker credit and no
            // duplicate callback can repeat it. It is also the Echo Step history's
            // life epoch — the movement sampler opens a new generation the moment
            // this value moves, which is how a chokepoint with no Frame in hand
            // still resets the ring (Package 11 A1c).
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
            // M09 (Package 12 W3): the respawning fighter keeps no temporary
            // owner buff from the last life — the zone speed bonus and the Warp
            // float window. Cooldowns and the Defy flag are deliberately kept.
            // The victim's Conductive mark (component 318) is cleared by the
            // combat system, which holds the Frame this chokepoint does not.
            runtime.ZoneSpeedBonusFrames = 0;
            runtime.FloatFrames = 0;
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
