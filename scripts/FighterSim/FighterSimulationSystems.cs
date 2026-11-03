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
            // Momentum, grabs) — the new snapshot components the
            // design mandates (ID 310+).
            // Klotho zero-initializes, and 0 is a real player ID, so the grab
            // attachment's "unattached" sentinel has to be written in (F23).
            var verb = new FighterVerbComponent();
            verb.ClearGrabPartner();
            // Meter and Defy update 2026-10-31 (G3.1): every match starts at 2 pips.
            verb.MomentumUnits = FighterMomentumRules.StartUnits;
            frame.Add(entity, verb);
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
            // Package 13 W5: Tubman's Foresight counter stance, component 322.
            // All zero is inactive; every fighter carries it.
            frame.Add(entity, new FighterCounterComponent());
            // Combat and movement update 2026-10-16 (plan §1.5): the action
            // state, component 323 — the Fighter Special cast, the D3 buffer, the
            // perfect-block clock, the Down tap and the action flags. All zero is
            // idle; every fighter carries it.
            frame.Add(entity, new FighterActionStateComponent());
            // V7.6 D02 (Package 11 A1b): the defensive layer - the Temporal Aegis
            // flag and the D02b HP barrier. Snapshot and hash state like every other component.
            frame.Add(entity, new FighterDefenseComponent {
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

    public sealed partial class FighterMovementSystem : ISystem {
        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;
        private const int BasicButton = 1 << 2;
        private const int BlockButton = 1 << 6;
        private const int RollButton = 1 << 10;
        /// <summary>
        /// The tick length. Gravity, the fall caps and the per-tick air step
        /// live in <see cref="FighterAirPhysics"/>, which the CPU's trajectory
        /// probe and recovery estimates integrate with too.
        /// </summary>
        private static readonly FP64 FixedDelta = FighterAirPhysics.FixedDelta;
        private static readonly FP64 RollSpeedMultiplier = FP64.FromDouble(UniversalMovementRules.RollSpeedMultiplier);

        private readonly FighterStageGeometry _geometry;
        /// <summary>
        /// Decision 1.2 (combat and movement update 2026-10-16): the fighters'
        /// projected hit contracts, which carry each Special's authored startup,
        /// active and recovery frames for the cast clock. Static match
        /// configuration, never snapshot state.
        /// </summary>
        private readonly FighterHitContractTable _contracts;

        public FighterMovementSystem(FighterStageGeometry geometry = null, FighterHitContractTable contracts = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
            _contracts = contracts ?? FighterHitContractTable.Default;
        }

        public void Update(ref Frame frame) {
            // Meter and Defy update 2026-10-31 (P2): the Momentum gauge regenerates
            // only while the match is live, so every match and every Sudden Death
            // starts at exactly two pips at GO. A Defy press needs a live match too.
            bool matchLive = frame.GetSingleton<FighterMatchComponent>().MatchState
                == FighterMatchStates.InProgress;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(entity);
                // Combat and movement update (plan §1.5): component 323.
                ref FighterActionStateComponent action = ref frame.Get<FighterActionStateComponent>(entity);
                // A02 (Package 13 W6, component 321) and the M05 knockdown
                // (Package 12 W3b, component 320), read above the hitstop gate:
                // its frozen ticks hold both action locks too (fix round A).
                ref FighterUltimateActivationComponent ultimateActivation =
                    ref frame.Get<FighterUltimateActivationComponent>(entity);
                ref FighterKnockdownComponent knockdown = ref frame.Get<FighterKnockdownComponent>(entity);

                // CM-MOVE (5.1): the short hop's Jump latch, from this tick's raw edges.
                UpdateJumpLatch(ref action, in runtime);

                // Meter and Defy update 2026-10-31 (plan section 2.4, O9 to O11): the Defy
                // press is read here, above the hitstop gate that every early-continue
                // branch below sits behind, so one site covers every state it is legal
                // from. A press on an unfrozen tick begins at once and claims the tick; one
                // on a frozen tick is held one deep and begins on the thaw tick (P12).
                bool defyBeganThisTick = ReadDefyPress(
                    ref frame, entity, ref fighter, ref runtime, ref verb, ref action,
                    in ultimateActivation, ref knockdown, matchLive);

                // V7 universal hitstop: both parties of a hit are fully suspended
                // — velocity, position, phases, and every per-fighter timer. The
                // world (projectiles, constructs, hazards, the match clock) keeps
                // running. DI resolves on the frame the freeze ends.
                if (verb.HitstopFrames > 0) {
                    // F3 (2026-10-04 feel pass): a BasicAttack press made while
                    // frozen is buffered for the next chain hit instead of being
                    // dropped — the freeze is the impact moment players mash into.
                    FighterBasicAttackRules.BufferChainPress(ref runtime);
                    // 2026-10-11 (addendum): a Special press inside the freeze of
                    // this fighter's own landed swing is held for its thaw tick.
                    FighterAbilityLockoutRules.BufferFreezePress(ref runtime);
                    // Plan §2.1.6, the cast lock in the hitstop branch: a landed
                    // Special freezes its caster, and this branch continues before
                    // the cast region below, so on every frozen tick — the thaw
                    // tick included, whose presses the ability and combat systems
                    // read — a busy caster's input is discarded here. An
                    // interrupted cast (a hit put the caster in hitstun) is
                    // cleared first, so the victim keeps its DI read.
                    FighterSpecialCastRules.ClearIfInterrupted(
                        ref action, in fighter, in verb,
                        FighterUltimateActivationRules.IsCaptured(in ultimateActivation),
                        FighterKnockdownRules.IsActive(in knockdown));
                    // D3 (CM-CANCEL, plan §2.11.1): a cancel press made in the
                    // caster's own freeze (its landed hit's) is buffered here,
                    // before the discard below zeroes it, and never aged. After
                    // the interrupt check, so an interrupted cast's buffer is
                    // emptied at once rather than fed.
                    FighterSpecialCancelRules.Feed(ref action, in fighter, in runtime, hitstopTick: true);
                    if (FighterSpecialCastRules.IsBusy(in action)) {
                        FighterUltimateActivationRules.DiscardInput(ref runtime);
                    }
                    // A running Defy is interrupted by a hit exactly as a cast is (the
                    // blast's own freeze is on a fighter with no hitstun, so it survives).
                    FighterDefyRules.ClearIfInterrupted(
                        ref action, in fighter, in verb,
                        FighterUltimateActivationRules.IsCaptured(in ultimateActivation),
                        FighterKnockdownRules.IsActive(in knockdown));
                    verb.HitstopFrames--;
                    if (verb.HitstopFrames == 0 && BeginBufferedDefy(
                            ref frame, entity, ref fighter, ref runtime, ref verb, ref action,
                            in ultimateActivation, ref knockdown, matchLive)) {
                        // The thaw tick: the buffered Defy overrides the hit's pending
                        // launch, hitstun and knockdown (BeginBufferedDefy cleared them).
                    } else if (verb.HitstopFrames == 0) {
                        FighterVerbRules.ResolvePendingLaunch(ref fighter, in runtime, ref verb);
                        // The thaw tick: the ability and combat systems read this
                        // tick's presses (HitstopFrames is 0 for them), so the held
                        // press joins them here and the ordinary gates decide it.
                        FighterAbilityLockoutRules.ReleaseFreezeBuffer(ref runtime);
                    }
                    // Fix round A (CM-FIXA-SIM, rulings (a) and (c)): the M05
                    // knockdown and its get-up, and the fighter's own Ultimate
                    // (A02: wind-up, active, whiff recovery, cinematic), are
                    // action-locked on every frozen tick too — the thaw tick
                    // included, whose presses the ability and combat systems read
                    // and whose held Block would raise the stance there. A downed
                    // fighter's own landed shot, or an Ultimate caster's, freezes
                    // it with no hit on it at all. Their branches below never run
                    // on a frozen tick, so the lock is held here: buttons only,
                    // after the DI read and the freeze-buffer release. The stick
                    // survives — a hit during the get-up or the whiff recovery
                    // freezes its victim with the knockdown or the activation
                    // still marked, and that freeze's DI is read from the stick.
                    // CanReadAbilityPress, CanStartGrab and UltimatePressPayable
                    // are the second gates.
                    if (FighterKnockdownRules.IsActive(in knockdown)
                        || FighterUltimateActivationRules.IsCasterBusy(in ultimateActivation)
                        || FighterDefyRules.IsDefying(in action)) {
                        FighterUltimateActivationRules.DiscardButtons(ref runtime);
                    }
                    continue;
                }
                // CM-HIT (combat and movement update 2026-10-16): the tumble
                // (3.2: it outlives hitstun while airborne, value 2), the air
                // dodge refreshed by any applied hitstun (5.3), the respawn-
                // protection record (4.2) and the throw animation's grabber grant.
                // FighterMovementSystem.HitReaction.cs.
                ApplyHitReaction(ref fighter, ref verb, ref knockdown, ref action);
                if (fighter.HitstunFrames <= 0) {
                    // Hitstun over: the hit-2 block-cancel gate re-opens and any
                    // stashed launch is dead — it must never replay under a
                    // later hit's hitstop (V7.3).
                    verb.HitstunBlockCancelBlocked = 0;
                    verb.PendingLaunchActive = 0;
                }
                // Plan §1.5: the stock-loss reset of component 323. The stock-loss
                // chokepoint has no Frame, so the platform is the tell: every field
                // is held at zero while the respawn platform holds the fighter
                // (respawn protection is set at the drop, after this hold).
                if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) action = default;
                // CM-MOVE (5.1 Q5, 5.2, 6.3): the own-jump rise, the ledge stick
                // re-arm and the double-tap Down window.
                TickMovementLatches(ref action, in fighter, in runtime, in verb, in ultimateActivation);

                // A02 (Package 13 W6, component 321): a fighter held by an
                // opponent's Ultimate cinematic takes no input and does not move
                // until the combat system releases the hold. Their statuses and
                // cooldowns keep ticking; a lethal tick hands them to the respawn.
                // Package 13 W5 (component 322): Tubman's Foresight stance clock,
                // after the hitstop gate. A hit, grab, capture or stock loss ends it.
                ref FighterCounterComponent counter = ref frame.Get<FighterCounterComponent>(entity);
                FighterForesightRules.Advance(
                    ref counter, in fighter, in verb,
                    FighterUltimateActivationRules.IsCaptured(in ultimateActivation));
                // Decision 1.2 (plan §2.1.6): the Special cast clock, after the
                // hitstop gate so a cast pauses in its own hitstop. The interrupt
                // set ends it first (the cost stays spent; an unfired effect never
                // fires). A Delegated cast (Foresight) ends with its 322 stance.
                AdvanceSpecialCast(ref frame, entity, ref fighter, ref runtime, in verb, ref action,
                    in counter, FighterUltimateActivationRules.IsCaptured(in ultimateActivation),
                    FighterKnockdownRules.IsActive(in knockdown));
                // CM-BLOCK (decision 3.4): the perfect-block clock's once-per-unfrozen-tick step.
                FighterBlockTimingRules.BeginTick(ref action, in fighter);
                if (FighterUltimateActivationRules.IsCaptured(in ultimateActivation)) {
                    FighterDefyRules.Clear(ref action);
                    ref FighterDefenseComponent capturedDefense = ref frame.Get<FighterDefenseComponent>(entity);
                    TickCounters(ref fighter, ref runtime, ref verb, ref capturedDefense, in tuning, matchLive);
                    if (fighter.Stocks > 0 && fighter.RespawnFramesRemaining <= 0) {
                        FighterUltimateActivationRules.HoldCaptured(ref fighter, ref runtime, in ultimateActivation);
                    }
                    continue;
                }


                // V7.6 D01-D04 (Package 11 A1b): the Venom tick inside
                // TickCounters routes through the defensive layer, so the
                // component travels with it.
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(entity);

                // M05 knockdown and get-up (Package 12 W3b, component 320): a
                // sub-phase of Stunned. The downed fighter is invulnerable (the
                // shared InvulnerabilityFrames grant) and takes no action; the
                // get-up that follows is vulnerable and just as locked. A hit
                // during the get-up (or a stock loss) ends the sub-phase.
                FighterKnockdownRules.ClearIfInterrupted(in fighter, ref knockdown);
                // A12 (Package 13 W1): a bounce whose tumble ended another way is
                // dropped. A03: a stock loss refreshes the air dodge (the
                // stock-loss chokepoint has no Frame; the platform is the tell).
                FighterSlamRules.ClearIfStale(in fighter, in verb, ref knockdown);
                if (FighterStockLossRules.IsFallen(in fighter)) {
                    FighterAirDodgeRules.Refresh(ref knockdown);
                    // Package 13 W7b (M04): a stock loss re-arms the staff refund too.
                    knockdown.StaffRefundUsed = 0;
                }
                // Meter and Defy update 2026-10-31: the Defy's clock. Frames 1 to 6 hold the
                // fighter in place (invulnerable, no gravity) and finish the tick here;
                // the recovery (7 to 30) discards the fighter's input and falls through to
                // the ordinary movement below so gravity and landing run as usual.
                if (ProcessDefy(
                        ref fighter, ref runtime, ref verb, ref defense, ref action,
                        in tuning, in ultimateActivation, in knockdown, defyBeganThisTick, matchLive)) continue;

                if (FighterKnockdownRules.IsActive(in knockdown)) {
                    TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning, matchLive);
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
                    if (!HasGroundSupport(in fighter) && !FighterReachKitRules.StandsOnStaff(ref frame, in fighter)) {
                        fighter.IsGrounded = 0;
                        FighterKnockdownRules.Clear(ref knockdown);
                    }
                    continue;
                }

                // Landing-tech recovery: invulnerable, in place, no actions.
                // 1.1(3) (plan §2.1.3): "no actions" now holds for every verb —
                // this tick's input is discarded before the ability, combat and
                // grab systems read it (the knockdown branch's rule), so no
                // Special, movement ability, paid Ultimate or grab starts during
                // the tech, and the D10 classifier sees no press.
                // Fix round A (CM-FIXA-SIM, ruling (b)): the invulnerability
                // counts down WITH the lock (the knockdown branch's pattern) — the
                // normal path's decrement below never runs during the tech, so the
                // grant used to sit frozen through the lock and then outlive it by
                // 11 actionable ticks. TryLandingTech grants the lock + 1 (the
                // landing tick's own combat pass), so the two end together:
                // invulnerable on L..L+12, hittable and free on L+13.
                if (verb.TechLockoutFrames > 0) {
                    verb.TechLockoutFrames--;
                    if (fighter.InvulnerabilityFrames > 0) fighter.InvulnerabilityFrames--;
                    fighter.Velocity = FPVector2.Zero;
                    FighterUltimateActivationRules.DiscardInput(ref runtime);
                    TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning, matchLive);
                    continue;
                }

                // V7.2 grabs: a grabbing or held fighter is rooted — no input
                // processing, no movement. Cooldowns and statuses keep ticking
                // (no pausing on stun applies here too); the combat system owns
                // the grab phases and the victim's pinning.
                if (FighterGrabRules.IsBusy(in verb)) {
                    fighter.Velocity = FPVector2.Zero;
                    TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning, matchLive);
                    continue;
                }

                // Plan §2.1.6, the cast region: a busy caster's input is
                // discarded on every tick before any verb (or the block-regen
                // stance read in TickCounters) sees it. The one exit is the
                // grounded recovery Block cancel (Story's Package 12 W3 rule),
                // which clears the cast and keeps this tick's held input so the
                // stance rises now. Fix round A (CM-FIXA-SIM, ruling (d)): that
                // tick raises the stance and nothing else — its presses are masked
                // to the Block bit (the held buttons and the stick stay), so a
                // roll, jump, drop-through, swing, grab, movement
                // ability, Special or Ultimate needs a fresh press from the next
                // tick, as Story's Block cancel enters Blocking and returns.
                // D3 (CM-CANCEL, plan §2.11.1): the region's body lives in
                // ResolveSpecialCastRegion (FighterMovementSystem.SpecialCancel.cs):
                // the buffer feed, then the Block cancel (now also out of
                // Foresight's whiff recovery and sidestep), then — never on a tick
                // the Block cancel won, and on the live input, before any mask —
                // a landed cast's D3 cancel, else the discard.
                bool specialCancelFired =
                    ResolveSpecialCastRegion(ref fighter, ref runtime, in verb, ref action, ref counter, in knockdown);

                TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning, matchLive);

                // A02: the caster is action-locked through the whole Ultimate —
                // wind-up, active, whiff recovery and cinematic. Discarding the
                // tick's input here, before any verb reads it, is what stops Echo
                // Step (or a block, roll, jump or grab) undoing a whiff.
                if (FighterUltimateActivationRules.IsCasterBusy(in ultimateActivation)) {
                    FighterUltimateActivationRules.DiscardInput(ref runtime);
                }
                // Package 13 W5: the Foresight stance (startup, window, whiff
                // recovery, sidestep) is action-locked the same way — nothing
                // cancels a whiffed read.
                if (FighterForesightRules.IsBusy(in counter)) {
                    FighterUltimateActivationRules.DiscardInput(ref runtime);
                }

                // The Chronal Respawn Platform owns the fighter completely: it is
                // frozen, invulnerable, and consumes no input until it drops.
                if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) {
                    ProcessRespawnPlatform(
                        ref fighter, ref runtime, ref action, _geometry.RespawnPlatformPosition);
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
                        // CM-MOVE (5.2): the hang now also reads the get-ups.
                        ProcessHang(ref fighter, ref runtime, ref verb, ref defense, ref action, in tuning, in hangPosition);
                        continue;
                    } else {
                        // Defensive: an anchor that no longer resolves (a stage
                        // swap under a restored snapshot) drops the hang rather
                        // than pinning the fighter to a platform that is not there.
                        FighterLedgeRules.ClearHang(ref runtime);
                        continue;
                    }
                }
                // CM-MOVE (5.2): a ledge climb (universal-movement values 8–10) is
                // pinned like the hang until it hands the tick back.
                if (FighterLedgeRules.IsGetUpClimb(in runtime)
                    && AdvanceLedgeGetUp(ref fighter, ref runtime, in tuning)) continue;

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
                    // Launch gravity: flat, clamped at the launch terminal —
                    // untouched by the 2026-10-08 air model, so launch arcs, DI
                    // and the tech window keep their timing.
                    fighter.Velocity.y = FighterAirPhysics.StepLaunch(fighter.Velocity.y);
                    // FU-KNOCK R3 (2026-10-25): plain grounded hitstun is a short
                    // shove, not a constant-speed glide. The hit site scaled the
                    // speed; strong friction stops it within ~15 ticks and the
                    // victim is planted for the rest of the stun. The guard-break
                    // daze keeps its frictionless push and launches their flight.
                    if (fighter.IsGrounded != 0
                        && fighter.DazeFrames <= 0
                        && verb.Tumble == FighterVerbRules.TumbleNone) {
                        fighter.Velocity.x = FighterKnockbackFeelRules.StepShove(fighter.Velocity.x);
                    }
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
                    // Package 13 W7b (A08): Attack during the Wing-Dive ends the
                    // dive so this same tick's phase machine starts the air
                    // strike (GU-A; it was the aerial string) — the dive is an
                    // approach, not a hover.
                    FighterKitMotion.TryWingDiveAttackCancel(ref runtime);
                    // GR-RUN (9b): a fresh press made running is the running attack,
                    // never a D3 cancel's press.
                    bool running = !specialCancelFired && FighterBasicAttackRules.IsRunningForAttack(
                        in fighter, in runtime, tuning.MoveSpeed * statusMoveMultiplier * speedBuffMultiplier);
                    if (ProcessBasicAttackPhase(ref fighter, ref runtime, in verb, running)) {
                        FighterActionRules.OnActionAccepted(
                            ref fighter, ref verb, ref defense, ref action, FighterActionKind.Swing);
                    }
                    bool attacking = runtime.AttackPhase != FighterBasicAttackRules.PhaseNone;
                    bool blockStance = FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);
                    // GR-RUN: past its startup the running attack stops steering.
                    bool runAttackCommitted = FighterBasicAttackRules.RunAttackCommitted(in runtime);
                    // CM-BLOCK (decision 3.4): a fresh raise with a Block press arms the perfect block.
                    FighterBlockTimingRules.Observe(ref action, blockStance, (runtime.PressedButtons & BlockButton) != 0);
                    // V7.3 shieldstun: the stunned blocker is locked into the
                    // stance — no roll, no jump, no drop-through out of it.
                    bool shieldStunned = verb.ShieldStunFrames > 0;
                    TryStartUniversalMovement(
                        ref fighter,
                        ref runtime,
                        ref knockdown,
                        ref verb,
                        ref defense,
                        ref action,
                        rooted,
                        allowRoll: !attacking && !shieldStunned);
                    // A03: an air dodge adds no speed; airborne it leaves normal
                    // air control running (jumps withheld), grounded it decelerates.
                    bool airDodging = FighterAirDodgeRules.IsAirDodge(runtime.UniversalMovementState);
                    // The grounded stop ramp's step, rounded up so it ends on
                    // exactly RunDecelerationFrames (FighterAirPhysics.RampStep).
                    FP64 groundDecelStep = FighterAirPhysics.RampStep(
                        tuning.MoveSpeed, UniversalMovementRules.RunDecelerationFrames);
                    // Package 12 W4 / 13 W5: Tesla's blink and Tubman's North Star Leap run
                    // as sim-local phases of the same universal-movement slot.
                    bool movementHandled = FighterKitMotion.IsKitPhase(runtime.UniversalMovementState)
                        ? FighterKitMotion.Process(
                            ref fighter,
                            ref runtime,
                            in frame.GetReadOnly<FighterAbilityModeComponent>(entity),
                            groundDecelStep,
                            // Package 13 W7a: the Warp fold's clearance check.
                            _geometry)
                        : airDodging
                            ? FighterAirDodgeRules.Advance(
                                ref fighter,
                                ref runtime,
                                groundDecelStep)
                            : ProcessUniversalMovement(
                                ref fighter,
                                ref runtime,
                                in tuning,
                                statusMoveMultiplier * speedBuffMultiplier);
                    // Q1 (2026-10-16): an airborne Special cast keeps its drift
                    // and skips air control; a grounded one brakes to a stop. A
                    // kit that writes its own velocity overrides that: Tubman's
                    // Foresight plants its stance (X 0) on every tick its 322
                    // stance is busy, grounded and airborne, as Story's
                    // TubmanForesight does on every executing tick (fix round A).
                    if (!movementHandled && FighterForesightRules.IsBusy(in counter)) {
                        fighter.Velocity.x = FP64.Zero;
                    } else if (!movementHandled && !CastKeepsAirDrift(in action, in fighter)) {
                        ApplyNormalMovement(
                            ref fighter,
                            ref runtime,
                            ref verb,
                            ref defense,
                            ref action,
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
                            lockHorizontal: blockStance || (runAttackCommitted && fighter.IsGrounded != 0),
                            lockFacing: blockStance || runAttackCommitted,
                            // M04 (Package 12 W3): Blocking → Airborne on Jump;
                            // only shieldstun keeps a blocker grounded.
                            allowJump: !attacking && !shieldStunned && !airDodging,
                            allowDropThrough: !shieldStunned && !airDodging);
                        // GR-RUN: the active frames lunge along the latched facing.
                        ApplyRunAttackLunge(ref fighter, in runtime, rooted, statusMoveMultiplier);
                    }
                    // Package 12 W4 / 13 W5: the blink hover/translation and the leap
                    // carry fly straight — no gravity, no fast-fall.
                    if (fighter.IsGrounded == 0 && !FighterKitMotion.SuspendsGravity(in runtime)) {
                        // The 2026-10-08 air model (FighterAirPhysics.StepMovement,
                        // the rule Story's ProcessAirborne applies too): one
                        // gravity multiplier per tick — fast-fall, the float
                        // glide, the apex hang after a jump, the fall multiplier
                        // — then the fall cap, eased into from above.
                        // Fast-fall (§2.9) stays a stateless rule derived from
                        // held input every tick — no snapshot field. Holding Down
                        // in the air, rising or falling, cancels the warp float
                        // window and ramps the descent in under heavier gravity
                        // toward the lower fast-fall cap; it no longer snaps.
                        // Hitstun and daze are handled by the branch above, which
                        // never reaches here; the guard is kept explicit so the
                        // rule survives a future restructure of the gate.
                        bool fastFalling = (runtime.HeldButtons & DownButton) != 0
                            && fighter.HitstunFrames <= 0
                            && fighter.DazeFrames <= 0;
                        if (fastFalling) runtime.FloatFrames = 0;
                        bool jumpedThisAirtime = FighterAirPhysics.HasJumpedThisAirtime(
                            fighter.RemainingJumps, tuning.MaxJumpCount);
                        // Decision 1.2: a cast that started airborne falls at
                        // half gravity through its startup and active frames
                        // (Foresight's through its startup and window).
                        fighter.Velocity.y = CastHalvesGravity(in action, in counter)
                            ? FighterAirPhysics.StepMovement(
                                fighter.Velocity.y,
                                fastFalling,
                                floating: runtime.FloatFrames > 0,
                                jumpedThisAirtime,
                                FighterAirPhysics.AerialSpecialGravityScale)
                            // CM-MOVE (5.1, 6.1): the short hop on a released
                            // rise from the fighter's own jump, and the hang
                            // only while Jump is held.
                            : FighterAirPhysics.StepMovement(
                                fighter.Velocity.y,
                                fastFalling,
                                floating: runtime.FloatFrames > 0,
                                jumpedThisAirtime: jumpedThisAirtime,
                                jumpHeld: JumpHeldForGravity(in action),
                                riseFromOwnJump: RiseFromOwnJump(in action));
                    }
                }

                // A02: the wind-up and active frames plant the caster (gravity 0
                // for an airborne start) or drive a melee lunge.
                FighterUltimateActivationRules.ApplyCasterMotion(ref fighter, in ultimateActivation);

                FP64 previousY = fighter.Position.y;
                bool wasAirborne = fighter.IsGrounded == 0;
                fighter.Position += fighter.Velocity * FixedDelta;
                FP64 unclampedX = fighter.Position.x;
                fighter.Position.x = FP64.Clamp(fighter.Position.x, _geometry.LeftWall, _geometry.RightWall);
                // FU-KNOCK R2 (2026-10-25): a launched fighter driven into a side
                // wall rebounds once per launch.
                FighterKnockbackFeelRules.TryWallBounce(
                    ref fighter, ref verb, ref action, unclampedX, _geometry.LeftWall, _geometry.RightWall);
                if (fighter.Position.y > _geometry.Ceiling) {
                    fighter.Position.y = _geometry.Ceiling;
                    if (fighter.Velocity.y > FP64.Zero) fighter.Velocity.y = FP64.Zero;
                }

                // Walking off an edge removes ground support — a one-way platform
                // end above the floor plane, or (Package 11 A9) the end of a main
                // floor segment on an Open stage. An unbroken floor supports every
                // x, so the Sealed stages never take the second branch.
                // Package 13 W7b (M04): a Sonata staff platform is a walkable
                // one-way surface for either fighter while it lives.
                if (fighter.IsGrounded != 0 && !HasGroundSupport(in fighter)
                    && !FighterReachKitRules.StandsOnStaff(ref frame, in fighter)) {
                    fighter.IsGrounded = 0;
                }

                bool landedOnOwnStaff = false;
                if (fighter.DropThroughFrames <= 0 && fighter.Velocity.y <= FP64.Zero) {
                    TryLandOnPlatform(ref fighter, in tuning, previousY);
                    if (fighter.IsGrounded == 0) {
                        int staffOwner = FighterReachKitRules.TryLandOnStaff(ref frame, ref fighter, in tuning, previousY);
                        landedOnOwnStaff = wasAirborne && staffOwner == fighter.PlayerID;
                    }
                    // CM-MOVE (6.2, Q6): the one-way lip pop, before the ledge capture.
                    TryLipPop(ref fighter, in runtime, in action, in ultimateActivation, in tuning, previousY);
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
                // 1.1(3) (plan §2.1.3): the tech's own landing tick L takes no
                // action either — its presses are discarded here, before the
                // ability, combat and grab systems read them.
                // Fix round A (CM-FIXA-SIM, ruling (a)): the missed tech's own
                // landing tick L takes no action either — its buttons are
                // discarded here (the knockdown branch's rule; the stick, which
                // chooses the get-up, survives), and CanReadAbilityPress,
                // CanStartGrab and UltimatePressPayable refuse while the
                // knockdown is active.
                if (wasAirborne && fighter.IsGrounded != 0
                    && !FighterSlamRules.TryBounce(ref fighter, in runtime, ref verb, ref knockdown)) {
                    if (FighterVerbRules.TryLandingTech(ref fighter, in runtime, ref verb)) {
                        FighterUltimateActivationRules.DiscardInput(ref runtime);
                    } else if (FighterKnockdownRules.TryBeginKnockdown(ref fighter, ref verb, ref knockdown)) {
                        FighterUltimateActivationRules.DiscardButtons(ref runtime);
                    }
                }

                // V7.3 regrab cap: grounding resets the per-airtime ledge budget.
                // A03: landing refreshes the air dodge too.
                if (fighter.IsGrounded != 0) {
                    verb.LedgeGrabsThisAirtime = 0;
                    FighterAirDodgeRules.Refresh(ref knockdown);
                }
                // M04 (Package 13 W7b, both modes per D14): landing on his OWN
                // staff refunds half of Sonata Drift's remaining cooldown, at most
                // once per airtime; real ground (not a staff) re-arms it.
                if (landedOnOwnStaff) {
                    FighterReachKitRules.ApplyStaffRefund(ref runtime, ref knockdown);
                } else if (fighter.IsGrounded != 0 && !FighterReachKitRules.StandsOnStaff(ref frame, in fighter)) {
                    knockdown.StaffRefundUsed = 0;
                }

                // §2.11 ledge capture, resolved last so landing and the ground snap
                // both win: a fighter who reached a surface is standing on it, not
                // hanging off it. Only fighters still airborne after the whole
                // resolve are candidates. A03: a grab refreshes the air dodge.
                // Decision 1.2: no ledge capture while casting (Story grabs only from Airborne);
                // a Defy's recovery is action-locked the same way.
                if (!FighterSpecialCastRules.IsBusy(in action)
                    && !FighterDefyRules.IsDefying(in action)
                    && TryGrabLedge(ref fighter, ref runtime, ref verb, in tuning)) {
                    FighterAirDodgeRules.Refresh(ref knockdown);
                    knockdown.StaffRefundUsed = 0;
                    FighterActionRules.OnActionAccepted(
                        ref fighter, ref verb, ref defense, ref action, FighterActionKind.LedgeGrab);
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
            // CM-FIXB-SIM (B8): the trumped hang ends, so does its first-grab
            // intangibility. The hanger's tick has already counted this tick's
            // hang frame, so LedgeStateFrames is the elapsed hang ticks.
            if (releaseFirst) {
                FighterLedgeRules.EndFirstGrabIntangibility(
                    ref first, in frame.GetReadOnly<FighterVerbComponent>(firstEntity),
                    ref frame.Get<FighterActionStateComponent>(firstEntity), firstRuntime.LedgeStateFrames);
                FighterLedgeRules.Release(ref first, ref firstRuntime);
            } else {
                FighterLedgeRules.EndFirstGrabIntangibility(
                    ref second, in frame.GetReadOnly<FighterVerbComponent>(secondEntity),
                    ref frame.Get<FighterActionStateComponent>(secondEntity), secondRuntime.LedgeStateFrames);
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
            // Package 13 W5: North Star Leap snaps to ledges from 0.5 units
            // farther than the normal capture box, while it flies and on its
            // settle frame (FighterKitMotion.HasLedgeSnapBonus).
            FP64 snapBonus = FighterKitMotion.HasLedgeSnapBonus(in runtime)
                ? FighterKitMotion.LeapLedgeSnapBonus
                : FP64.Zero;
            if (!_geometry.TryFindLedge(in fighter.Position, snapBonus, out int anchor)) return false;
            if (!_geometry.TryGetHangPosition(anchor, out FPVector2 hangPosition)) return false;
            FighterLedgeRules.Grab(ref fighter, ref runtime, ref verb, in tuning, anchor, in hangPosition);
            return true;
        }

        /// <summary>
        /// Chronal Respawn Platform (design-godot.md ~1565-1571). The fighter
        /// stands frozen and invulnerable for up to five seconds. After a short
        /// grace window any gameplay input drops them; otherwise the platform
        /// dissolves on expiry. Either way the spawn protection is armed at the
        /// drop, never before it: CM-HIT (decision 4.2, Fighter only) made it 120
        /// frames shrinking to 60 the longer the fighter waited
        /// (<see cref="FighterMatchFlowRules.RespawnInvulnerabilityFor"/>),
        /// recorded on component 323 so the first offensive action can end it.
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
            ref FighterActionStateComponent action,
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
                // CM-HIT (4.2): the wait is read AFTER this tick's decrement — 31
                // at the first legal input drop (120 frames), 300 at the
                // auto-drop (exactly 60) — and the grant is recorded on 323 bit
                // 0 (the platform hold zeroed 323 earlier this tick).
                int waited = FighterMatchFlowRules.RespawnPlatformFrames - fighter.RespawnFramesRemaining;
                fighter.RespawnFramesRemaining = 0;
                fighter.IsGrounded = 0;
                FighterMatchFlowRules.GrantRespawnProtection(ref fighter, ref action, waited);
                return;
            }

            // Invulnerable for the whole dissolve, with at least the longest
            // drop grant still left the instant the platform releases them.
            fighter.InvulnerabilityFrames =
                fighter.RespawnFramesRemaining + FighterMatchFlowRules.RespawnInvulnerabilityMaxFrames;
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
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
            bool rooted,
            bool allowRoll = true) {
            // A03 (Package 13 W1): Roll pressed while airborne is the air dodge.
            // A05: Root refuses it exactly as it refuses the grounded roll.
            if (fighter.IsGrounded == 0) {
                if (allowRoll && (runtime.PressedButtons & RollButton) != 0
                    && FighterAirDodgeRules.TryStart(ref fighter, ref runtime, ref knockdown, rooted)) {
                    FighterActionRules.OnActionAccepted(
                        ref fighter, ref verb, ref defense, ref action, FighterActionKind.AirDodge);
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
                FighterActionRules.OnActionAccepted(
                    ref fighter, ref verb, ref defense, ref action, FighterActionKind.Roll);
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
            FP64 runStep = FighterAirPhysics.RampStep(tuning.MoveSpeed, UniversalMovementRules.RunDecelerationFrames);

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
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
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
            // Grounded movement runs two ramps (2026-08-10 feel batch §2.1,
            // cut to 6 and 4 frames by user direction 2026-10-08): accelerating
            // toward a same-signed target uses the RunAccelerationFrames run
            // ramp, while bleeding speed off — a released stick, a rooted or
            // block-locked zero target, or a reversal against current
            // velocity — uses the RunDecelerationFrames stop ramp. Both steps
            // round up (FighterAirPhysics.RampStep) so each ramp ends on its
            // exact frame. Air control keeps its single 4-frame constant in
            // both modes.
            FP64 rampStep;
            if (fighter.IsGrounded == 0) {
                rampStep = maximumSpeed / FP64.FromInt(4);
                // V7.2 wiring: per-character aerial input responsiveness
                // (CharacterData.AirControlMultiplier — 0.40 Lincoln .. 0.70
                // Leonardo/Cleopatra). Zero from hand-built tunings reads as 1.0.
                FP64 airControl = tuning.AirControl > FP64.Zero ? tuning.AirControl : FP64.One;
                rampStep *= airControl;
            } else {
                bool decelerating = targetSpeed == FP64.Zero
                    || (targetSpeed > FP64.Zero && fighter.Velocity.x < FP64.Zero)
                    || (targetSpeed < FP64.Zero && fighter.Velocity.x > FP64.Zero);
                rampStep = FighterAirPhysics.RampStep(
                    maximumSpeed,
                    decelerating
                        ? UniversalMovementRules.RunDecelerationFrames
                        : UniversalMovementRules.RunAccelerationFrames);
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
            // CM-MOVE (6.3): the double tap of Down drops too (both inputs, both
            // modes); TryDoubleTapDropThrough also opens the window on a first tap.
            if (allowDropThrough && fighter.IsGrounded != 0 && !rooted
                && TryDoubleTapDropThrough(ref fighter, ref action, in runtime, onSolidBaseFloor)) {
                // Dropped by the double tap.
            } else if (allowDropThrough && jumpPressed && downHeld && fighter.IsGrounded != 0 && !rooted && !onSolidBaseFloor) {
                BeginDropThrough(ref fighter);
            } else if (allowJump && jumpPressed && fighter.IsGrounded != 0 && !rooted) {
                fighter.Velocity.y = tuning.JumpSpeed * statusMoveMultiplier * jumpBuffMultiplier;
                fighter.IsGrounded = 0;
                fighter.RemainingJumps = tuning.MaxJumpCount - 1;
                // CM-MOVE (Q5): the fighter's own jump — a released Jump short-hops it.
                MarkOwnJump(ref action);
                FighterActionRules.OnActionAccepted(ref fighter, ref verb, ref defense, ref action, FighterActionKind.Jump);
            } else if (allowJump && jumpPressed && fighter.IsGrounded == 0 && fighter.RemainingJumps > 0 && !rooted) {
                fighter.Velocity.y = tuning.JumpSpeed * statusMoveMultiplier * jumpBuffMultiplier;
                fighter.RemainingJumps--;
                MarkOwnJump(ref action);
                FighterActionRules.OnActionAccepted(ref fighter, ref verb, ref defense, ref action, FighterActionKind.AirJump);
            }
        }

        /// <summary>
        /// Advances the universal three-hit basic string
        /// (<see cref="FTT.Combat.BasicComboRules"/>) one tick: swing start, phase
        /// clock, input buffering, aerial landing cancel, and the recovery /
        /// chain-hold cancels. Held horizontal movement steers the attacker but
        /// never cancels the swing or resets the chain — the authored string
        /// pace is the only pace. Jump/roll/block (and a special or the
        /// ultimate, upstream) remain the cancel set. GU-A (2026-10-12): an air
        /// press is the single air strike, and no chain hit ever starts in the
        /// air — a continuation that would begin airborne ends the string.
        /// Internal so the GU-A suites can drive it on a hand-built fighter.
        /// Returns true when a swing started this tick (a fresh swing or a chain
        /// continuation), the caller's cue for the action chokepoint
        /// (<see cref="FighterActionRules.OnActionAccepted"/>, plan §1.5).
        /// </summary>
        internal static bool ProcessBasicAttackPhase(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            bool running = false) {
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
                        FighterBasicAttackRules.SelectVariant(in fighter, in runtime, running));
                    return true;
                }
                return false;
            }

            bool aerial = (runtime.AttackFlags & FighterBasicAttackRules.FlagAerial) != 0;
            bool variantSwing = FighterBasicAttackRules.IsVariantSwing(in runtime);
            bool airborne = fighter.IsGrounded == 0;
            if (aerial && !airborne) {
                // Landing cancels a swing started in the air (the air strike, an
                // airborne up-attack, the down-air) with no landing lag.
                FighterBasicAttackRules.CancelString(ref runtime);
                return false;
            }

            // The air strike and the directional attacks stand outside the
            // chain: they neither buffer into the string nor continue it.
            if (basicPressed && !variantSwing) {
                if (phase == FighterBasicAttackRules.PhaseChainHold) {
                    // A fresh direction re-selects at this swing start, so
                    // Up + BasicAttack out of the hold window starts an
                    // up-attack (chain reset) rather than continuing the string,
                    // and (GU-A) any press made airborne — a grounded string that
                    // walked off a ledge — starts the air strike, never hit 2.
                    // GR-RUN: never the running attack (held movement never breaks
                    // the chain), so this re-selection states no "running".
                    int variant = FighterBasicAttackRules.SelectVariant(in fighter, in runtime);
                    if (variant != FTT.Combat.BasicComboRules.VariantChain) {
                        FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, 0, variant);
                        return true;
                    }
                    if (runtime.ComboIndex < FTT.Combat.BasicComboRules.ComboHits - 1) {
                        FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, runtime.ComboIndex + 1);
                        return true;
                    }
                    return false;
                }
                // F3 (2026-10-04 feel pass): startup, ACTIVE or recovery — the
                // old active-frame exclusion is gone (it is not in the design);
                // a second press while one is buffered queues for the hit after.
                FighterBasicAttackRules.BufferChainPress(ref runtime);
            }

            if (phase is FighterBasicAttackRules.PhaseRecovery or FighterBasicAttackRules.PhaseChainHold) {
                // Jumping, rolling, or blocking cancels the recovery and
                // resets the chain (design 752 / 3080). Held movement does
                // not — it steers the swing without touching the string.
                bool cancels = (runtime.PressedButtons & (JumpButton | RollButton)) != 0
                    || (runtime.HeldButtons & BlockButton) != 0;
                if (cancels) {
                    FighterBasicAttackRules.CancelString(ref runtime);
                    return false;
                }
            }

            // F3 (design-godot.md: "Holding the attack button through the window
            // also continues the chain") — after the cancel set, so a held
            // button never swallows a jump, roll or block. Story's
            // ProcessRecoveryHold applies the same order.
            if (phase == FighterBasicAttackRules.PhaseChainHold
                && !variantSwing
                && (runtime.HeldButtons & BasicButton) != 0
                && runtime.ComboIndex < FTT.Combat.BasicComboRules.ComboHits - 1) {
                // GU-A: no chain hit starts in the air — an airborne fighter's
                // held continuation ends the string instead.
                if (airborne) {
                    FighterBasicAttackRules.CancelString(ref runtime);
                    return false;
                }
                FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, runtime.ComboIndex + 1);
                return true;
            }

            runtime.AttackPhaseFrames--;
            if (runtime.AttackPhaseFrames > 0) return false;
            int step = runtime.ComboIndex < 0 ? 0 : runtime.ComboIndex > 2 ? 2 : runtime.ComboIndex;
            bool started = false;
            switch (phase) {
                case FighterBasicAttackRules.PhaseStartup:
                    runtime.AttackPhase = FighterBasicAttackRules.PhaseActive;
                    runtime.AttackPhaseFrames =
                        FighterBasicAttackRules.ActiveFramesFor(runtime.AttackFlags, step);
                    // 10.1 (plan §2.10.1): the box facing is latched here, at
                    // active start, before this tick's steering can turn it.
                    if (fighter.FacingRight != 0) {
                        runtime.AttackFlags |= FighterBasicAttackRules.FlagActiveFacingRight;
                    } else {
                        runtime.AttackFlags &= ~FighterBasicAttackRules.FlagActiveFacingRight;
                    }
                    break;
                case FighterBasicAttackRules.PhaseActive:
                    runtime.AttackPhase = FighterBasicAttackRules.PhaseRecovery;
                    runtime.AttackPhaseFrames =
                        FighterBasicAttackRules.RecoveryFramesFor(runtime.AttackFlags, step);
                    break;
                case FighterBasicAttackRules.PhaseRecovery:
                    // A single strike (the air strike or a directional attack)
                    // exits straight out — no chain hold, no buffered
                    // continuation, combo index already zero.
                    if (variantSwing) {
                        FighterBasicAttackRules.CancelString(ref runtime);
                    } else if (FTT.Combat.BasicComboRules.ChainContinues(
                        step,
                        buffered: (runtime.AttackFlags & FighterBasicAttackRules.FlagBuffered) != 0,
                        attackHeld: (runtime.HeldButtons & BasicButton) != 0)) {
                        if (airborne) {
                            // GU-A: a continuation that would start in the air
                            // (the string dropped through a one-way platform
                            // mid-swing; a walk-off is caught by the ledge grab
                            // first) ends the string instead.
                            FighterBasicAttackRules.CancelString(ref runtime);
                            break;
                        }
                        // F3: a buffered press, or BasicAttack still held,
                        // continues the chain; a queued second press carries
                        // into the hit that starts here.
                        bool queued = (runtime.AttackFlags & FighterBasicAttackRules.FlagQueuedBuffered) != 0;
                        FighterBasicAttackRules.StartSwing(ref fighter, ref runtime, step + 1);
                        if (queued) runtime.AttackFlags |= FighterBasicAttackRules.FlagBuffered;
                        started = true;
                    } else if (step >= FTT.Combat.BasicComboRules.ComboHits - 1) {
                        // The finisher exits straight out; the chain resets.
                        FighterBasicAttackRules.CancelString(ref runtime);
                    } else {
                        runtime.AttackPhase = FighterBasicAttackRules.PhaseChainHold;
                        runtime.AttackPhaseFrames = FTT.Combat.BasicComboRules.ChainHoldFrames;
                        runtime.AttackFlags &= ~(FighterBasicAttackRules.FlagBuffered
                            | FighterBasicAttackRules.FlagQueuedBuffered);
                    }
                    break;
                default:
                    FighterBasicAttackRules.CancelString(ref runtime);
                    break;
            }
            return started;
        }

        private static readonly FP64 RunAttackLungeSpeed =
            FP64.FromDouble(FTT.Combat.BasicComboRules.RunAttackLungeSpeedUnits);

        /// <summary>
        /// GR-RUN: on the running attack's active ticks, on the ground, the speed is
        /// the lunge's, so they travel <see cref="FTT.Combat.BasicComboRules.RunAttackLungeUnits"/>
        /// (less under a slow, none rooted) whatever the stick or the carried run
        /// speed says. Hitstop never reaches here, so a landing pauses the lunge.
        /// </summary>
        private static void ApplyRunAttackLunge(
            ref FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            bool rooted,
            FP64 statusMoveMultiplier) {
            if (rooted
                || fighter.IsGrounded == 0
                || !FighterBasicAttackRules.IsRunAttack(in runtime)
                || runtime.AttackPhase != FighterBasicAttackRules.PhaseActive) {
                return;
            }
            FP64 speed = RunAttackLungeSpeed * statusMoveMultiplier;
            fighter.Velocity.x = fighter.FacingRight != 0 ? speed : -speed;
        }

        private static FP64 MoveToward(FP64 current, FP64 target, FP64 maximumDelta) {
            if (current < target) return FP64.Min(current + maximumDelta, target);
            if (current > target) return FP64.Max(current - maximumDelta, target);
            return target;
        }

        private static void TickCounters(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            in FighterTuningComponent tuning,
            bool regenMomentum) {
            // Rally drain (V7.1): the Echo Pool bleeds to zero over 150 frames
            // from the last accrual. Echo that finishes draining is permanently
            // lost — and only then does the victim's meter-from-damage accrue
            // on it (the no-double-earning guardrail).
            if (verb.EchoPool > FP64.Zero) {
                FP64 drained = FP64.Min(verb.EchoPool, verb.EchoDrainPerFrame);
                verb.EchoPool -= drained;
                fighter.Influence = FP64.Min(
                    FighterMeterRules.Cap,
                    fighter.Influence + FighterMeterRules.FromDamageTaken(drained));
                if (verb.EchoPool <= FP64.Zero || verb.EchoDrainPerFrame <= FP64.Zero) {
                    verb.EchoPool = FP64.Zero;
                    verb.EchoDrainPerFrame = FP64.Zero;
                }
            }
            if (verb.ThrowImmunityFrames > 0) verb.ThrowImmunityFrames--;
            if (verb.ShieldStunFrames > 0) verb.ShieldStunFrames--;

            if (fighter.HitstunFrames > 0) fighter.HitstunFrames--;
            if (fighter.DazeFrames > 0) fighter.DazeFrames--;
            if (fighter.HyperArmorFrames > 0) fighter.HyperArmorFrames--;
            if (fighter.DropThroughFrames > 0) fighter.DropThroughFrames--;
            // §2.11 regrab lockout. It only runs down while off a ledge, which is
            // the whole point: releasing pins the fighter inside the capture box
            // they just left, and the lockout is what lets them fall out of it.
            if (runtime.LedgeRegrabLockoutFrames > 0) runtime.LedgeRegrabLockoutFrames--;
            // The ability lockout A and the D1 Special-sourced lockout S tick together.
            FighterAbilityLockoutRules.Tick(ref runtime);
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
            if (regenMomentum) verb.MomentumUnits = FighterMomentumRules.Regen(verb.MomentumUnits);
            if (runtime.SpecialOneReuseFrames > 0) runtime.SpecialOneReuseFrames--;
            if (runtime.SpecialTwoReuseFrames > 0) runtime.SpecialTwoReuseFrames--;
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
                    int scaledDamage = ScaleIntegerByFP(FighterLingeringKitRules.VenomDamagePerSecondPerIntensity, runtime.DamageStatusIntensity);
                    int damage = scaledDamage > 1 ? scaledDamage : 1;
                    runtime.StatusTickFrames = FighterLingeringKitRules.VenomTickFrames;
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
            // C03 (Package 13 W7b): Desert Mirage's sand rush passes through opponents.
            if (FighterKitMotion.PassesThroughFighters(in firstRuntime)
                || FighterKitMotion.PassesThroughFighters(in secondRuntime)) return;
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

    public sealed partial class FighterCombatSystem : ISystem {
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
        // template, read from the one cross-mode table
        // (BasicComboRules.StringBoxHalfWidthUnits / ...HalfHeightUnits /
        // ...OffsetXUnits / ...OffsetYUnits, 2026-10-11) and converted once here
        // with FP64.FromDouble, exactly as the literals were, so no hash moved.
        // The V7.1 profile percentages scale width (size and offset together)
        // and height. The old single symmetric 2.0×1.6 both-sides box was a
        // recorded defect against this spec.
        private static readonly FP64[] StringBoxHalfWidths = {
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits[0]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits[1]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits[2])
        };
        private static readonly FP64[] StringBoxHalfHeights = {
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxHalfHeightUnits[0]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxHalfHeightUnits[1]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxHalfHeightUnits[2])
        };
        private static readonly FP64[] StringBoxOffsetsX = {
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxOffsetXUnits[0]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxOffsetXUnits[1]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxOffsetXUnits[2])
        };
        private static readonly FP64[] StringBoxOffsetsY = {
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxOffsetYUnits[0]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxOffsetYUnits[1]),
            FP64.FromDouble(FTT.Combat.BasicComboRules.StringBoxOffsetYUnits[2])
        };

        /// <summary>
        /// The horizontal half-width of the hurtbox every basic swing is tested
        /// against (<see cref="FighterHurtbox.HalfExtents"/>, plan §2.8.3). Exposed so the CPU's
        /// neutral melee band reads the live value rather than a copy
        /// (2026-10-11; ledger <c>VERIFY-SIM-HURTBOX-WIDTH</c> may change it).
        /// </summary>
        internal static FP64 VictimHurtboxHalfWidth => FighterHurtbox.HalfExtents.x;

        /// <summary>
        /// How far in front of the attacker's pivot a profile's grounded string
        /// box for <paramref name="step"/> reaches, in world units: the box
        /// centre's forward offset plus its half-width, both scaled by
        /// <paramref name="reachWidthPercent"/> exactly as
        /// <see cref="ResolveStringBox"/> scales them. The sim's "pivot reach"
        /// against a fighter is this plus <see cref="VictimHurtboxHalfWidth"/>.
        /// </summary>
        internal static FP64 StringBoxFarEdge(int reachWidthPercent, int step) {
            int clamped = step < 0 ? 0 : step > 2 ? 2 : step;
            FP64 widthScale = FP64.FromInt(reachWidthPercent) / FP64.FromInt(100);
            return StringBoxOffsetsX[clamped] * widthScale + StringBoxHalfWidths[clamped] * widthScale;
        }

        /// <summary>
        /// The active string box for a combo step, in world space: offset in
        /// front of the attacker's current facing, scaled by the profile's
        /// reach percentages. Internal so StringHitboxGeometryTests can hold
        /// the Story box to it.
        /// </summary>
        internal static void ResolveStringBox(
            in FighterStateComponent attacker,
            in FTT.Combat.BasicStringProfile profile,
            int step,
            out FPVector2 center,
            out FPVector2 halfExtents) =>
            ResolveStringBox(in attacker, in profile, step, attacker.FacingRight != 0, out center, out halfExtents);

        /// <summary>
        /// The active string box for an explicit facing — the swing's facing
        /// latched at active start (<see cref="FighterBasicAttackRules.FlagActiveFacingRight"/>,
        /// decision 10.1). The three sim callers read the latch; the overload
        /// above (the attacker's current facing) stays for the geometry suites.
        /// </summary>
        internal static void ResolveStringBox(
            in FighterStateComponent attacker,
            in FTT.Combat.BasicStringProfile profile,
            int step,
            bool facingRight,
            out FPVector2 center,
            out FPVector2 halfExtents) {
            FP64 widthScale = FP64.FromInt(profile.ReachWidthPercent) / FP64.FromInt(100);
            FP64 heightScale = FP64.FromInt(profile.ReachHeightPercent) / FP64.FromInt(100);
            FP64 facing = facingRight ? FP64.One : -FP64.One;
            center = new FPVector2(
                attacker.Position.x + facing * StringBoxOffsetsX[step] * widthScale,
                attacker.Position.y + StringBoxOffsetsY[step]);
            halfExtents = new FPVector2(
                StringBoxHalfWidths[step] * widthScale,
                StringBoxHalfHeights[step] * heightScale);
        }
        /// <summary>The swing's box facing, latched at active start (decision 10.1).</summary>
        private static bool LatchedFacingRight(in FighterRuntimeComponent runtime) =>
            (runtime.AttackFlags & FighterBasicAttackRules.FlagActiveFacingRight) != 0;

        private static readonly FP64 MaxInfluence = FighterMeterRules.Cap;
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
        // GU-A: the air strike's damage scale, converted once like the others.
        private static readonly FP64 AirAttackDamageMultiplier =
            FP64.FromDouble(FTT.Combat.BasicComboRules.AirAttackDamageMultiplier);
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

            // Meter and Defy update 2026-10-31: a Defy blast resolves first, so what it
            // staggers is cancelled before any grab, strike or intent acts (P4, P6).
            ResolveDefyBlasts(ref frame, first, second);

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
            // Plan §1.5: component 323, the action state (the Special cast).
            ref FighterActionStateComponent actionOne = ref frame.Get<FighterActionStateComponent>(first);
            ref FighterActionStateComponent actionTwo = ref frame.Get<FighterActionStateComponent>(second);

            // V7.2 grabs & throws advance before every other action: a fighter
            // mid-grab or held takes no other offensive action this tick. Fix
            // round A: the grab start reads the knockdown (320) and the
            // fighter's own Ultimate (321), both action locks.
            AdvanceGrabs(
                ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, ref actionOne, in tuningOne,
                ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, ref actionTwo, in tuningTwo,
                oneFrozen, twoFrozen,
                in frame.GetReadOnly<FighterKnockdownComponent>(first),
                in frame.GetReadOnly<FighterUltimateActivationComponent>(first),
                in frame.GetReadOnly<FighterKnockdownComponent>(second),
                in frame.GetReadOnly<FighterUltimateActivationComponent>(second),
                (int)frame.Tick);
            bool oneActing = !oneFrozen && !FighterGrabRules.IsBusy(in verbOne);
            bool twoActing = !twoFrozen && !FighterGrabRules.IsBusy(in verbTwo);

            // A02 (Package 13 W6): live activations advance first — the phase
            // clock, the strike's contact test, the cinematic hold and the D15
            // finale — then a fresh press is accepted. Acceptance spends the
            // meter, so the ultimate can never also reach a melee intent below.
            AdvanceUltimate(ref frame, first, second);
            AdvanceUltimate(ref frame, second, first);
            // A fighter the opponent's activation strike captured just now takes
            // no further action this tick (the movement system discards a held
            // victim's input from the next tick on). Without this, a special the
            // ability system held back for a same-tick Ultimate press
            // (UltimatePressAccepted, read before the capture) would reach
            // BuildIntent's generic melee branch as a phantom melee hit.
            oneActing = oneActing && !FighterUltimateActivationRules.IsCaptured(
                in frame.GetReadOnly<FighterUltimateActivationComponent>(first));
            twoActing = twoActing && !FighterUltimateActivationRules.IsCaptured(
                in frame.GetReadOnly<FighterUltimateActivationComponent>(second));
            if (oneActing) TryCharacterUltimate(ref frame, first, ref fighterOne, ref runtimeOne);
            if (twoActing) TryCharacterUltimate(ref frame, second, ref fighterTwo, ref runtimeTwo);

            // Decision 1.2 (plan §2.1.6): a Melee-execution Special was accepted
            // (and paid for) on its press in FighterAbilityEntitySystem; here it
            // is tested for contact on every active tick of its cast until it
            // resolves one (MeleeResolved). The §1.7 tick claim of a held-back
            // Ultimate is settled at that acceptance, so it needs no read here.
            AttackIntent firstIntent = !oneActing ? default : BuildIntent(
                in fighterOne, in runtimeOne, in actionOne,
                in frame.GetReadOnly<FighterAbilityModeComponent>(first), in tuningOne, in fighterTwo, _contracts);
            AttackIntent secondIntent = !twoActing ? default : BuildIntent(
                in fighterTwo, in runtimeTwo, in actionTwo,
                in frame.GetReadOnly<FighterAbilityModeComponent>(second), in tuningTwo, in fighterOne, _contracts);
            if (firstIntent.Kind != 0) actionOne.SpecialFlags |= FighterSpecialCastRules.FlagMeleeResolved;
            if (secondIntent.Kind != 0) actionTwo.SpecialFlags |= FighterSpecialCastRules.FlagMeleeResolved;
            // Package 13 W5 (component 322): Tubman's Foresight catches a melee
            // Special intent or a basic swing on the victim's side.
            ref FighterCounterComponent counterOne = ref frame.Get<FighterCounterComponent>(first);
            ref FighterCounterComponent counterTwo = ref frame.Get<FighterCounterComponent>(second);
            ApplyIntent(ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, ref actionOne, ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, in firstIntent, ref counterTwo, ref actionTwo, _contracts);
            ApplyIntent(ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, ref actionTwo, ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, in secondIntent, ref counterOne, ref actionOne, _contracts);
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
            // D02c: the HP barrier's lifetime counts down once per tick and
            // pauses in the holder's own hitstop.
            FighterDefenseRules.TickBarrier(ref defenseOne, verbOne.HitstopFrames > 0);
            FighterDefenseRules.TickBarrier(ref defenseTwo, verbTwo.HitstopFrames > 0);
            // A12 (Package 13 W1): a Down-Air slam owes its victim one forced
            // ground bounce, recorded on the victim's component 320.
            ref FighterKnockdownComponent knockdownOne = ref frame.Get<FighterKnockdownComponent>(first);
            ref FighterKnockdownComponent knockdownTwo = ref frame.Get<FighterKnockdownComponent>(second);
            if (oneActing) {
                ApplyBasicSwing(ref fighterOne, ref runtimeOne, ref verbOne, in tuningOne, ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, ref conductiveTwo, ref knockdownTwo, ref counterTwo, ref actionTwo);
                ApplyConstructSwing(ref frame, ref fighterOne, ref runtimeOne, in tuningOne);
            }
            if (twoActing) {
                ApplyBasicSwing(ref fighterTwo, ref runtimeTwo, ref verbTwo, in tuningTwo, ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, ref conductiveOne, ref knockdownOne, ref counterOne, ref actionOne);
                ApplyConstructSwing(ref frame, ref fighterTwo, ref runtimeTwo, in tuningTwo);
            }
            // Package 13 W5: a Foresight trigger owes its counter strike. Both
            // answers resolve after every ordinary hit of the tick (a caught
            // projectile's answer lands on the next tick, because projectiles
            // resolve after this system).
            ResolveForesightAnswer(
                ref fighterOne, ref runtimeOne, ref verbOne, ref counterOne, ref actionOne, in tuningOne,
                ref fighterTwo, ref runtimeTwo, ref verbTwo, ref defenseTwo, in tuningTwo, ref actionTwo);
            ResolveForesightAnswer(
                ref fighterTwo, ref runtimeTwo, ref verbTwo, ref counterTwo, ref actionTwo, in tuningTwo,
                ref fighterOne, ref runtimeOne, ref verbOne, ref defenseOne, in tuningOne, ref actionOne);
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
        /// V7.2 grabs &amp; throws, both fighters per tick: initiation from the
        /// Block+BasicAttack chord, phase advancement, the simultaneous-grab
        /// clash, the 30-frame hold, and throw resolution through the ordinary
        /// damage chokepoint. Frozen (hitstop) fighters are fully suspended.
        /// </summary>
        private static void AdvanceGrabs(
            ref FighterStateComponent one, ref FighterRuntimeComponent runtimeOne,
            ref FighterVerbComponent verbOne, ref FighterDefenseComponent defenseOne,
            ref FighterActionStateComponent actionOne,
            in FighterTuningComponent tuningOne,
            ref FighterStateComponent two, ref FighterRuntimeComponent runtimeTwo,
            ref FighterVerbComponent verbTwo, ref FighterDefenseComponent defenseTwo,
            ref FighterActionStateComponent actionTwo,
            in FighterTuningComponent tuningTwo,
            bool oneFrozen, bool twoFrozen,
            in FighterKnockdownComponent knockdownOne,
            in FighterUltimateActivationComponent activationOne,
            in FighterKnockdownComponent knockdownTwo,
            in FighterUltimateActivationComponent activationTwo,
            int tick) {
            // Initiation. Both may start the same frame; the clash resolves it.
            // C01c: the direct bind and the preset chord request the same verb,
            // once (FighterGrabRules.Requested deduplicates them).
            if (!oneFrozen && FighterGrabRules.Requested(in runtimeOne)
                && FighterGrabRules.CanStartGrab(in one, in runtimeOne, in verbOne, in actionOne, in knockdownOne, in activationOne)) {
                verbOne.GrabPhase = FighterGrabRules.PhaseStartup;
                verbOne.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
                verbOne.ClearGrabPartner();
                FighterActionRules.OnActionAccepted(ref one, ref verbOne, ref defenseOne, ref actionOne, FighterActionKind.Grab);
            }
            if (!twoFrozen && FighterGrabRules.Requested(in runtimeTwo)
                && FighterGrabRules.CanStartGrab(in two, in runtimeTwo, in verbTwo, in actionTwo, in knockdownTwo, in activationTwo)) {
                verbTwo.GrabPhase = FighterGrabRules.PhaseStartup;
                verbTwo.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
                verbTwo.ClearGrabPartner();
                FighterActionRules.OnActionAccepted(ref two, ref verbTwo, ref defenseTwo, ref actionTwo, FighterActionKind.Grab);
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
                ConnectGrab(ref one, ref verbOne, ref two, ref runtimeTwo, ref verbTwo, tick);
            } else if (twoSeizes) {
                ConnectGrab(ref two, ref verbTwo, ref one, ref runtimeOne, ref verbOne, tick);
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
            ref FighterVerbComponent victimVerb,
            int tick) {
            grabberVerb.GrabPhase = FighterGrabRules.PhaseHolding;
            // Catch identity for presentation (310 GrabConnectFrame): the sim tick of
            // the connect, +1 so tick 0 is distinguishable from "no catch" (0).
            grabberVerb.GrabConnectFrame = tick + 1;
            // +1: this connect tick's own phase decrement (AdvanceGrabPhase runs
            // right after) is not a hold tick, so the first thaw tick sees the full
            // window and the auto throw lands after exactly ThrowDecisionFrames
            // unfrozen hold ticks.
            grabberVerb.GrabPhaseFrames = FTT.Combat.BasicComboRules.ThrowDecisionFrames + 1;
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
            victimVerb.GrabConnectFrame = 0;
            victim.Velocity = FPVector2.Zero;
            FighterUniversalMovementRules.Cancel(ref victimRuntime);
            FighterBasicAttackRules.CancelString(ref victimRuntime);
            FighterLedgeRules.ClearHang(ref victimRuntime);
            PinHeldVictim(in grabber, ref victim, ref victimRuntime);
            // Grab pass 2026-10-31: the catch freeze. Both fighters hitstop on the
            // connect tick; the hold does not advance while its grabber is frozen
            // (AdvanceGrabPhase), so the freeze never eats the hold.
            FighterVerbRules.ApplyHitstop(
                ref grabberVerb, ref victimVerb, FTT.Combat.BasicComboRules.GrabCatchHitstopFrames);
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
                grabberVerb.GrabConnectFrame = 0;
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

            if (grabberVerb.GrabPhase == FighterGrabRules.PhaseHolding) {
                // Grab pass 2026-10-31: the early throw. Hold ticks already spent =
                // decision window - frames left; only this tick's Attack press
                // edge counts (the chord's own press was made before the catch).
                int elapsed = FTT.Combat.BasicComboRules.ThrowDecisionFrames - grabberVerb.GrabPhaseFrames;
                bool attackEdge = (grabberRuntime.PressedButtons & (1 << 2)) != 0;
                if (FTT.Combat.GrabThrowRules.ThrowTriggerThisTick(
                        elapsed, attackEdge,
                        FighterGrabRules.ThrowIntent(in grabber, in grabberRuntime),
                        out FTT.Combat.GrabThrowDirection early)) {
                    BeginThrowAnimation(ref grabber, ref grabberVerb, ref victim, (int)early);
                    return;
                }
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
                    // throw (neutral = forward), then both fighters are
                    // invulnerable for the 12-frame throw animation.
                    BeginThrowAnimation(
                        ref grabber, ref grabberVerb, ref victim,
                        FighterGrabRules.ResolveThrowDirection(in grabber, in grabberRuntime));
                    break;
                case FighterGrabRules.PhaseThrowAnimation:
                    ResolveThrow(
                        ref grabber, ref grabberRuntime, ref grabberVerb, in grabberTuning,
                        ref victim, ref victimRuntime, ref victimVerb, ref victimDefense, in victimTuning);
                    break;
            }
        }

        private static void BeginThrowAnimation(
            ref FighterStateComponent grabber,
            ref FighterVerbComponent grabberVerb,
            ref FighterStateComponent victim,
            int direction) {
            grabberVerb.ThrowDirection = direction;
            grabberVerb.GrabPhase = FighterGrabRules.PhaseThrowAnimation;
            grabberVerb.GrabPhaseFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
            if (grabber.InvulnerabilityFrames < FTT.Combat.BasicComboRules.ThrowAnimationFrames) {
                grabber.InvulnerabilityFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
            }
            if (victim.InvulnerabilityFrames < FTT.Combat.BasicComboRules.ThrowAnimationFrames) {
                victim.InvulnerabilityFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
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
            // Same V7.1 profile axes the fighter-target swing applies. The three
            // single strikes (the air strike and the two directional strikes,
            // all a flat 1.0x) deal one basic, through hit 1's box at step 0.
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            bool runAttack = FighterBasicAttackRules.IsRunAttack(in attackerRuntime);
            int damage = runAttack
                ? ScaleDamage(attackerTuning.BasicDamage, RunAttackDamageMultiplier)
                : FighterBasicAttackRules.IsVariantSwing(in attackerRuntime)
                    ? attackerTuning.BasicDamage
                    : attackerTuning.BasicDamage * profile.DamageTenths[step] / 10;
            if (damage <= 0) return;

            // V7 normative boxes: the construct sweep uses the same escalating
            // front-only box as the fighter-target swing (GR-RUN: the same stretched
            // box for the running attack).
            FPVector2 boxCenter;
            FPVector2 boxHalf;
            if (runAttack) {
                ResolveRunAttackBox(
                    in attacker, in profile, LatchedFacingRight(in attackerRuntime), out boxCenter, out boxHalf);
            } else {
                ResolveStringBox(
                    in attacker, in profile, step, LatchedFacingRight(in attackerRuntime),
                    out boxCenter, out boxHalf);
            }
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
            ref FighterKnockdownComponent targetKnockdown,
            ref FighterCounterComponent targetCounter,
            ref FighterActionStateComponent targetAction) {
            if (attackerRuntime.AttackPhase != FighterBasicAttackRules.PhaseActive) return;
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0) return;
            if (attacker.Stocks <= 0) return;

            // CM-MOVE (decision 5.2): the ledge attack's strike, a single strike
            // with the air strike's hit model (FighterCombatSystem.LedgeAttack.cs).
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagLedgeAttack) != 0) {
                FighterHitOutcome ledgeOutcome = ApplyLedgeAttackSwing(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetCounter, ref targetAction, out int ledgeDamage);
                OnSwingResolved(
                    ref attackerRuntime, ref attackerVerb, ledgeOutcome, ledgeDamage,
                    FTT.Combat.BasicComboRules.MomentumGainUnitsFor(FTT.Combat.BasicComboRules.VariantChain, 0));
                return;
            }

            // GU-A (2026-10-12): the single air strike — string hit 1's box and
            // knockback at one basic, never a launch. Routed before the
            // directional strikes, which own every other variant bit.
            if ((attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagAirAttack) != 0) {
                FighterHitOutcome airOutcome = ApplyAirAttackSwing(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetCounter, ref targetAction, out int airDamage);
                OnSwingResolved(
                    ref attackerRuntime, ref attackerVerb, airOutcome, airDamage,
                    FTT.Combat.BasicComboRules.MomentumGainUnitsFor(
                        FTT.Combat.BasicComboRules.VariantAirAttack, 0));
                return;
            }

            // GR-RUN (9b): the running attack, another single strike on hit 1's
            // chassis (FighterCombatSystem.RunAttack.cs).
            if (FighterBasicAttackRules.IsRunAttack(in attackerRuntime)) {
                FighterHitOutcome runOutcome = ApplyRunAttackSwing(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetCounter, ref targetAction, out int runDamage);
                OnSwingResolved(
                    ref attackerRuntime, ref attackerVerb, runOutcome, runDamage,
                    FTT.Combat.BasicComboRules.MomentumGainUnitsFor(
                        FTT.Combat.BasicComboRules.VariantRunAttack, 0));
                return;
            }

            // Directional attacks (§2.8) own their own boxes: the Up-Attack
            // launches upward, the Down-Air slams (A12).
            if (FighterBasicAttackRules.IsVariantSwing(in attackerRuntime)) {
                FighterHitOutcome variantOutcome = ApplyDirectionalSwing(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetKnockdown, ref targetCounter, ref targetAction, out int variantDamage);
                OnSwingResolved(
                    ref attackerRuntime, ref attackerVerb, variantOutcome, variantDamage,
                    FTT.Combat.BasicComboRules.MomentumGainUnitsFor(
                        (attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagUpAttack) != 0
                            ? FTT.Combat.BasicComboRules.VariantUpAttack
                            : FTT.Combat.BasicComboRules.VariantDownAir,
                        0));
                return;
            }

            // V7.1 string profile: reach and damage shape are the attacker's
            // authored axes (BasicComboRules.StringProfileFor); the tenths sum
            // is pinned at 33 so a full string stays 3.3x BasicDamage.
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            int step = attackerRuntime.ComboIndex < 0 ? 0 : attackerRuntime.ComboIndex > 2 ? 2 : attackerRuntime.ComboIndex;
            // V7 normative boxes: the step's escalating box, in front of the
            // facing latched at active start only — a swing never hits behind,
            // and steering mid-active never flips the box (decision 10.1).
            ResolveStringBox(
                in attacker, in profile, step, LatchedFacingRight(in attackerRuntime),
                out FPVector2 boxCenter, out FPVector2 boxHalf);
            if (!FighterEntityQueries.Overlaps(
                    in boxCenter, in boxHalf, in target.Position, in FighterHurtbox.HalfExtents)) return;

            int damage = attackerTuning.BasicDamage * profile.DamageTenths[step] / 10;
            bool finisher = step == 2;
            // Shared knockback table (BasicComboRules.KnockbackMultipliers,
            // 1.0x / 1.2x / 4.5x); the finisher launches with the profile's
            // authored tenths (V7.1 rider: Mozart's 5.5x spacing shove).
            FP64 knockback = attackerTuning.BasicKnockback * (finisher
                ? FP64.FromInt(profile.FinisherKnockbackTenths) / FP64.FromInt(10)
                : BasicKnockbackMultipliers[step]);
            // FU-KNOCK R1 (2026-10-25): the Fighter finisher flies twice as far.
            // Horizontal speed doubles and the vertical stays the force (scale
            // 1/2 on the doubled impulse), so the arc height and airtime are
            // unchanged and the travel doubles. Story's finisher never reads this.
            if (finisher) knockback *= FighterKnockbackFeelRules.FinisherHorizontalScale;
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
                : finisher ? FighterKnockbackFeelRules.FinisherVerticalScale : FP64.Zero;
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            // Package 13 W5: a string swing is a strike Foresight can catch.
            if (FighterForesightRules.TryCounter(ref targetCounter, ref target)) return;
            FighterHitOutcome outcome = FighterDamageRules.ResolveFighterHit(
                ref targetAction,
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
            bool connected = outcome == FighterHitOutcome.Landed;
            // D1 (2026-10-11) and the meter update (2026-10-31): a landed or absorbed
            // swing opens the hit-confirm cancel and pays its Momentum-gauge gain once.
            OnSwingResolved(
                ref attackerRuntime, ref attackerVerb, outcome, damage,
                FTT.Combat.BasicComboRules.MomentumGainUnitsFor(FTT.Combat.BasicComboRules.VariantChain, step));
            // V7.6 F07: the finisher's caster-owned MARK, applied alongside (never
            // instead of) its status. Fighter Mode always uses the BASELINE
            // duration — the tesla_conductive_hold extension is a Story-only
            // Resonance node and must never reach the deterministic sim.
            // Lingering-kit pass: a landed string finisher is the Rift's second collapse trigger (read by the persistent-object system).
            if (connected && finisher) FighterLingeringKitRules.NoteOwedFinisher(ref attackerVerb);
            if (connected && finisher && profile.FinisherMarkType == (int)FTT.Combat.ComboMarkType.Conductive) {
                FighterConductiveRules.ApplyMark(
                    ref targetConductive, attacker.PlayerID, profile.FinisherMarkFrames);
            }
        }

        /// <summary>
        /// D1 (2026-10-11) and the Momentum gauge (2026-10-31): one resolved basic
        /// swing. A Landed or Absorbed outcome carrying authored damage (D1a; a
        /// zero-damage payload never lands) sets the swing-landed latch on its 0 → 1
        /// edge and adds that swing's gauge gain once (360 / 360 / 1440 units); a landing that froze the attacker arms
        /// the freeze-only Special buffer. A Refused or Blocked outcome (a box
        /// miss and a Foresight counter included) does neither.
        /// </summary>
        internal static void OnSwingResolved(
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            FighterHitOutcome outcome,
            int damage,
            int momentumGainUnits) {
            if (damage <= 0) return;
            if (outcome != FighterHitOutcome.Landed && outcome != FighterHitOutcome.Absorbed) return;
            // Lingering-kit pass: one owed synced strike per landed basic hit (FighterPersistentObjectSystem reads it).
            FighterLingeringKitRules.NoteOwedStrike(ref attackerVerb);
            if (FighterAbilityLockoutRules.NoteSwingLanded(ref attackerRuntime)) {
                attackerVerb.MomentumUnits = FighterMomentumRules.Gain(attackerVerb.MomentumUnits, momentumGainUnits);
            }
            if (attackerVerb.HitstopFrames > 0) FighterAbilityLockoutRules.ArmFreezeBuffer(ref attackerRuntime);
        }

        /// <summary>
        /// GU-A (2026-10-12): the single air strike. It swings string hit 1's
        /// box (<see cref="ResolveStringBox"/> at
        /// <see cref="FTT.Combat.BasicComboRules.AirAttackStringStep"/>, the box
        /// the aerial string's opener always used) for a flat
        /// <see cref="FTT.Combat.BasicComboRules.AirAttackDamageMultiplier"/>
        /// basic, hit 1's knockback multiplier on the scalar path, the 30-frame
        /// <see cref="FTT.Combat.BasicComboRules.AirAttackHitstunFrames"/>, no
        /// rider and no launch. It is not string hit 1, so its hitstun stays
        /// block-cancelable like the directional strikes'. Same one-attempt-per-
        /// swing rule as the chain: a box miss is Refused and tries again next
        /// tick; a Foresight counter is Refused.
        /// </summary>
        private static FighterHitOutcome ApplyAirAttackSwing(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            in FighterTuningComponent attackerTuning,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            ref FighterCounterComponent targetCounter,
            ref FighterActionStateComponent targetAction,
            out int damage) {
            damage = 0;
            const int step = FTT.Combat.BasicComboRules.AirAttackStringStep;
            FTT.Combat.BasicStringProfile profile =
                FighterBasicAttackRules.StringProfileFor(attacker.CharacterID);
            ResolveStringBox(
                in attacker, in profile, step, LatchedFacingRight(in attackerRuntime),
                out FPVector2 boxCenter, out FPVector2 boxHalf);
            if (!FighterEntityQueries.Overlaps(
                    in boxCenter, in boxHalf, in target.Position, in FighterHurtbox.HalfExtents)) {
                return FighterHitOutcome.Refused;
            }

            damage = ScaleDamage(attackerTuning.BasicDamage, AirAttackDamageMultiplier);
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            if (FighterForesightRules.TryCounter(ref targetCounter, ref target)) return FighterHitOutcome.Refused;
            return FighterDamageRules.ResolveFighterHit(
                ref targetAction,
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
                attackerTuning.BasicKnockback * BasicKnockbackMultipliers[step],
                FTT.Combat.BasicComboRules.AirAttackHitstunFrames,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                attacker.Position.x,
                true,
                0,
                FP64.Zero,
                blockCancelableHitstun: true,
                launches: FTT.Combat.BasicComboRules.AirAttackLaunches);
        }

        /// <summary>
        /// The two directional strikes (§2.8). Up-attack reaches above the
        /// attacker's origin, down-air below it; both deal a flat 1.0x basic and
        /// launch the victim upward through <c>verticalKnockbackScale</c>. Same
        /// one-attempt-per-swing rule, same meter/status/block flow as the chain.
        /// Only the up-attack and the down-air reach here (the air strike has
        /// <see cref="ApplyAirAttackSwing"/>), so "not the up-attack" is the
        /// down-air.
        /// </summary>
        private static FighterHitOutcome ApplyDirectionalSwing(
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
            ref FighterCounterComponent targetCounter,
            ref FighterActionStateComponent targetAction,
            out int damage) {
            // 2026-10-11 (D1a): the outcome feeds the hit-confirm cancel and
            // per-hit Momentum; a box miss and a Foresight counter are Refused.
            damage = 0;
            bool upAttack = (attackerRuntime.AttackFlags & FighterBasicAttackRules.FlagUpAttack) != 0;
            FP64 horizontalReach = upAttack ? UpAttackHorizontalReach : DownAirHorizontalReach;
            if (FP64.Abs(target.Position.x - attacker.Position.x) > horizontalReach) return FighterHitOutcome.Refused;

            // World Y is up: the up-attack box sits above the attacker's origin,
            // the down-air box below it. Neither reaches past the origin plane.
            FP64 verticalOffset = upAttack
                ? target.Position.y - attacker.Position.y
                : attacker.Position.y - target.Position.y;
            FP64 verticalReach = upAttack ? UpAttackVerticalReach : DownAirVerticalReach;
            if (verticalOffset < FP64.Zero || verticalOffset > verticalReach) return FighterHitOutcome.Refused;

            damage = ScaleDamage(attackerTuning.BasicDamage, DirectionalDamageMultiplier);
            attackerRuntime.AttackFlags |= FighterBasicAttackRules.FlagHitResolved;
            // Package 13 W5: the Up-Attack and the Down-Air are strikes too.
            if (FighterForesightRules.TryCounter(ref targetCounter, ref target)) return FighterHitOutcome.Refused;
            if (!upAttack) {
                return ApplyDownAirSlam(
                    ref attacker, ref attackerRuntime, ref attackerVerb, in attackerTuning,
                    ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    ref targetKnockdown, ref targetAction, damage);
            }
            // ApplyFighterHit derives both impulse axes from one magnitude, so the
            // small horizontal factor is folded into the magnitude and the
            // vertical scale carries the ratio back up to the authored 2.5x.
            FP64 knockback = attackerTuning.BasicKnockback * DirectionalHorizontalKnockback;
            return FighterDamageRules.ResolveFighterHit(
                ref targetAction,
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
        private static FighterHitOutcome ApplyDownAirSlam(
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
            ref FighterActionStateComponent targetAction,
            int damage) {
            FighterHitOutcome outcome = FighterDamageRules.ResolveFighterHit(
                ref targetAction,
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
            if (outcome == FighterHitOutcome.Landed) FighterSlamRules.TryArm(in target, in targetVerb, ref targetKnockdown);
            return outcome;
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
            ref FighterUltimateActivationComponent activation =
                ref frame.Get<FighterUltimateActivationComponent>(attackerEntity);
            if (!UltimatePressAccepted(
                    in attacker, in attackerRuntime,
                    in frame.GetReadOnly<FighterVerbComponent>(attackerEntity),
                    in frame.GetReadOnly<FighterActionStateComponent>(attackerEntity),
                    in activation,
                    in frame.GetReadOnly<FighterKnockdownComponent>(attackerEntity))) return;
            FighterUltimateActivationRules.Accept(
                ref attacker, ref attackerRuntime, ref activation, _contracts.UltimateFor(attacker.PlayerID));
            FighterAbilityLockoutRules.ArmAbility(ref attackerRuntime);
            FighterActionRules.OnActionAccepted(
                ref attacker,
                ref frame.Get<FighterVerbComponent>(attackerEntity),
                ref frame.Get<FighterDefenseComponent>(attackerEntity),
                ref frame.Get<FighterActionStateComponent>(attackerEntity),
                FighterActionKind.Ultimate);
        }

        /// <summary>
        /// The A02 acceptance gate for this tick's Ultimate press: pressed, a
        /// full meter, a state that could act, the ability lockout clear (a
        /// swing started this tick already holds it) and not already casting
        /// or captured. A refusal leaves the meter untouched.
        /// <see cref="FighterAbilityEntitySystem"/> reads the same gate earlier
        /// in the tick so an accepted Ultimate claims the tick (no special or
        /// movement ability beside it), the order Story polls in.
        /// </summary>
        internal static bool UltimatePressAccepted(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterVerbComponent attackerVerb,
            in FighterActionStateComponent attackerAction,
            in FighterUltimateActivationComponent activation,
            in FighterKnockdownComponent knockdown) =>
            UltimatePressPayable(in attacker, in attackerRuntime, in attackerVerb, in attackerAction, in activation, in knockdown)
            && FighterAbilityLockoutRules.AllowsUltimate(in attackerRuntime);

        /// <summary>
        /// 2026-10-11 (D1, PROVISIONAL-HIT-CONFIRM-RULES (4)): a meter-paid
        /// Ultimate press the ability lockout ALONE refuses. It still claims the
        /// tick from Special 1 and Special 2 (never from the movement ability):
        /// D1 opens the Specials while the Ultimate stays locked, and the default
        /// gamepad Ultimate is the Movement + Special 2 chord, which must not cast
        /// its Special 2 inside a hit-confirm. Story's CheckUltimateInput sets the
        /// same claim.
        /// </summary>
        internal static bool UltimatePressHeldBackByLockout(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterVerbComponent attackerVerb,
            in FighterActionStateComponent attackerAction,
            in FighterUltimateActivationComponent activation,
            in FighterKnockdownComponent knockdown) =>
            UltimatePressPayable(in attacker, in attackerRuntime, in attackerVerb, in attackerAction, in activation, in knockdown)
            && !FighterAbilityLockoutRules.AllowsUltimate(in attackerRuntime);

        /// <summary>
        /// Every term of <see cref="UltimatePressAccepted"/> except the ability
        /// lockout: pressed, a full meter, a state that could act, and not
        /// already casting or captured. Combat and movement update 2026-10-16:
        /// never from the block stance or shieldstun (decision 2.3,
        /// <c>FighterBasicAttackRules.IsBlockStance</c> covers both — Story's
        /// Blocking state never polls the Ultimate either), never during the
        /// landing tech's lock (1.1(3)), and never during the fighter's own
        /// Special cast (1.2). Every caller reads this one predicate — the
        /// combat Update, <see cref="FighterAbilityEntitySystem"/>'s tick claim
        /// and, through it, the refusal classifier — so a refused stance
        /// Ultimate claims nothing either. Fix round A (CM-FIXA-SIM): never
        /// during the M05 knockdown or its get-up, from its landing tick on
        /// (component 320), whose discard a hitstop thaw tick does not reach.
        /// </summary>
        internal static bool UltimatePressPayable(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterVerbComponent attackerVerb,
            in FighterActionStateComponent attackerAction,
            in FighterUltimateActivationComponent activation,
            in FighterKnockdownComponent knockdown) =>
            (attackerRuntime.PressedButtons & UltimateButton) != 0
            && attacker.Influence >= MaxInfluence
            && attacker.HitstunFrames <= 0
            && attacker.DazeFrames <= 0
            && attacker.Stocks > 0
            && attacker.RespawnFramesRemaining <= 0
            && attackerVerb.TechLockoutFrames <= 0
            && !FighterSpecialCastRules.IsBusy(in attackerAction)
            && !FighterKnockdownRules.IsActive(in knockdown)
            && !FighterBasicAttackRules.IsBlockStance(in attacker, in attackerRuntime, in attackerVerb)
            && !FighterLedgeRules.IsHanging(in attackerRuntime)
            && !FighterUniversalMovementRules.IsCombatLocked(in attackerRuntime)
            && !FighterUltimateActivationRules.IsCasterBusy(in activation)
            && !FighterUltimateActivationRules.IsCaptured(in activation);

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
                // The victim is gone (KO, respawn, or someone else's hold): the cinematic ends with no finale.
                if (target.Stocks <= 0
                    || target.RespawnFramesRemaining > 0
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
        /// cinematic (their own Ultimate, grab, swing and
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
                (FTT.Core.StatusType)casterTuning.UltimateStatusType, data.HasFinale);
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

        /// <summary>
        /// Package 13 W5 — Foresight's counter strike. On the tick after a
        /// trigger (<see cref="FighterCounterComponent.AnswerPending"/>) Tubman
        /// answers the opponent if they are within
        /// <see cref="FTT.Combat.TubmanKitRules.ForesightAnswerRangeUnits"/>: one
        /// Special-class direct hit of her Special 2 damage (20), her Special 2
        /// contract's hitstun, signed knockback and launch flag, pushed away
        /// from her. It never asks the opponent's own Foresight (a counter
        /// strike cannot trigger another), and an attacker out of range is
        /// simply not answered. The pending flag clears either way.
        /// </summary>
        private void ResolveForesightAnswer(
            ref FighterStateComponent tubman,
            ref FighterRuntimeComponent tubmanRuntime,
            ref FighterVerbComponent tubmanVerb,
            ref FighterCounterComponent counter,
            ref FighterActionStateComponent tubmanAction,
            in FighterTuningComponent tubmanTuning,
            ref FighterStateComponent opponent,
            ref FighterRuntimeComponent opponentRuntime,
            ref FighterVerbComponent opponentVerb,
            ref FighterDefenseComponent opponentDefense,
            in FighterTuningComponent opponentTuning,
            ref FighterActionStateComponent opponentAction) {
            if (counter.AnswerPending == 0) return;
            counter.AnswerPending = 0;
            if (tubman.Stocks <= 0 || !FighterForesightRules.AnswerReaches(in tubman, in opponent)) return;
            FighterAbilityHitData contract = _contracts.For(tubman.PlayerID, FighterHitContractTable.SlotSpecialTwo);
            ResolveIntentContract(contract, LegacySpecialHitstunFrames, out int hitstun, out bool launches);
            // The answer faces the attacker it read.
            tubman.FacingRight = opponent.Position.x >= tubman.Position.x ? 1 : 0;
            // D3 (CM-CANCEL, plan §2.11.1): a landed answer arms the recovery
            // cancel out of the sidestep (Foresight's Delegated cast, slot 2).
            FighterHitOutcome outcome = FighterDamageRules.ResolveFighterHit(
                ref opponentAction,
                ref tubman,
                ref tubmanRuntime,
                ref tubmanVerb,
                ref opponent,
                ref opponentRuntime,
                ref opponentVerb,
                ref opponentDefense,
                in opponentTuning,
                FighterDamageRules.SpecialAttackClass,
                tubmanTuning.SpecialTwoDamage,
                contract.HasKnockbackVector ? contract.KnockbackX : tubmanTuning.SpecialTwoKnockback,
                hitstun,
                (int)FTT.Core.StatusType.None,
                0,
                FP64.One,
                tubman.Position.x,
                launches: launches,
                shieldBreaker: contract.ShieldBreaker,
                hasKnockbackVector: contract.HasKnockbackVector,
                knockbackVertical: contract.KnockbackY);
            FighterSpecialCancelRules.NoteLanded(
                ref tubmanAction, FighterHitContractTable.SlotSpecialTwo, outcome, tubmanTuning.SpecialTwoDamage, in contract);
        }

        /// <summary>
        /// Decision 1.2 (plan §2.1.6): a Melee-execution Special's contact test.
        /// The press was accepted — cooldown, lockout and cast begun — in
        /// <see cref="FighterAbilityEntitySystem"/>; here the slot is tested on
        /// EVERY active tick of its cast until it resolves one contact (the
        /// caller sets <c>FlagMeleeResolved</c> once an intent is built). It never
        /// arms a cooldown or the lockout. Divine Piercing (the lunge's thrusts)
        /// and Foresight (the 322 stance) resolve elsewhere. The generic branch
        /// applies the tuning component's folded total, which equals the
        /// authored damage only for single-hit slots — in the production roster
        /// only Splitting Strike reaches it (ledger
        /// <c>VERIFY-TUNING-SPECIAL-TOTAL-FOLD</c>).
        /// </summary>
        private static AttackIntent BuildIntent(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterActionStateComponent attackerAction,
            in FighterAbilityModeComponent attackerModes,
            in FighterTuningComponent tuning,
            in FighterStateComponent target,
            FighterHitContractTable contracts) {
            if (!FighterSpecialCastRules.IsActive(in attackerAction)
                || FighterSpecialCastRules.HasFlag(in attackerAction, FighterSpecialCastRules.FlagMeleeResolved)) {
                return default;
            }
            if (attacker.HitstunFrames > 0
                || attacker.DazeFrames > 0
                || attacker.Stocks <= 0
                // Fix round A (CM-FIXA-SIM): a caster KO'd earlier this tick (the
                // blast zone, a lethal Venom tick) stands on the respawn platform
                // with its cast still marked until the next movement pass clears
                // it; it resolves no contact from the platform's anchor.
                || FighterMatchFlowRules.IsOnRespawnPlatform(in attacker)
                // Hanging suppresses the intent the way hitstun does (§2.11).
                || FighterLedgeRules.IsHanging(in attackerRuntime)) return default;
            int slot = attackerAction.SpecialSlot;
            int executionType = slot == FighterHitContractTable.SlotSpecialOne
                ? attackerModes.SpecialOneExecutionType
                : attackerModes.SpecialTwoExecutionType;
            if (executionType != FighterSpecialCastRules.MeleeExecutionType
                || FighterSpecialCastRules.IsPiercingLunge(in attacker, in attackerModes, slot)
                || FighterSpecialCastRules.IsForesight(in attacker, slot)) return default;

            // Package 13 W7b (LN03): Splitting Strike is a 2.2-unit overhead arc
            // that catches grounded and airborne targets alike and spikes the
            // airborne ones — tested against its own box, not the generic 2-unit
            // melee gate.
            if (attacker.CharacterID == (int)FighterCharacterID.Lincoln && slot == FighterHitContractTable.SlotSpecialTwo) {
                if (!FighterReachKitRules.SplittingArcReaches(in attacker, in target)) return default;
                FighterAbilityHitData splitting = contracts.For(attacker.PlayerID, FighterHitContractTable.SlotSpecialTwo);
                ResolveIntentContract(splitting, LegacySpecialHitstunFrames, out int splitHitstun, out bool splitLaunches);
                return new AttackIntent(
                    4,
                    tuning.SpecialTwoDamage,
                    splitting.HasKnockbackVector ? splitting.KnockbackX : tuning.SpecialTwoKnockback,
                    splitHitstun,
                    tuning.SpecialTwoStatusType,
                    tuning.SpecialTwoStatusFrames,
                    tuning.SpecialTwoStatusIntensity,
                    launches: splitLaunches && FighterReachKitRules.SplittingLaunches(in splitting, in target),
                    shieldBreaker: splitting.ShieldBreaker,
                    hasKnockbackVector: splitting.HasKnockbackVector,
                    knockbackVertical: FighterReachKitRules.SplittingVertical(in splitting, in target));
            }
            if (FP64.Abs(target.Position.x - attacker.Position.x) > AttackRange) return default;
            // V7.3: melee-execution specials are front-only, matching
            // ResolveStringBox's convention — nothing in the kit hits behind
            // the attacker's back.
            bool targetInFront = attacker.FacingRight != 0
                ? target.Position.x >= attacker.Position.x
                : target.Position.x <= attacker.Position.x;
            if (!targetInFront) return default;

            // M08/M05 (Package 12 W3b): hitstun and launch come from the
            // attacker's authored contract (DEFER-SIM-ABILITY-HITSTUN, generic
            // half), not the old fixed 18/30 frames.
            // A01/D10 (Package 13 W1): a melee Special intent reads the
            // contract's block class (Shield-Breaker) and signed knockback.
            if (slot == FighterHitContractTable.SlotSpecialOne) {
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
                    launches,
                    in contract);
            }
            {
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
                    launches,
                    in contract);
            }
        }

        private static void ApplyIntent(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            ref FighterDefenseComponent attackerDefense,
            ref FighterActionStateComponent attackerAction,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            in AttackIntent intent,
            ref FighterCounterComponent targetCounter,
            ref FighterActionStateComponent targetAction,
            FighterHitContractTable contracts) {
            if (intent.Kind == 0) return;

            // Decision 1.2 (plan §2.1.6): the Special was accepted on its press —
            // its cooldown, the ability lockout, the Momentum reset, the string
            // cancel and the action chokepoint all ran in
            // FighterAbilityEntitySystem — so the contact arms nothing here.
            // (Kind 3, the retired melee Ultimate intent, has no producer since
            // A02; every Ultimate lands through its activation strike.)

            // Package 13 W5: a melee Special intent is a strike Foresight can
            // catch. The attacker's cooldown is already spent; nothing lands.
            if (intent.Kind != 3 && FighterForesightRules.TryCounter(ref targetCounter, ref target)) return;

            int attackClass = intent.Kind == 1
                ? FighterDamageRules.BasicAttackClass
                : intent.Kind == 3
                    ? FighterDamageRules.UltimateAttackClass
                    : FighterDamageRules.SpecialAttackClass;
            FighterHitOutcome outcome = FighterDamageRules.ResolveFighterHit(
                ref targetAction,
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
            // D3 (CM-CANCEL, plan §2.11.1): a melee Special intent that landed
            // (D1a) arms its cast's recovery cancel. Kind 2 is Special 1, 4 is
            // Special 2.
            int slot = intent.Kind == 2
                ? FighterHitContractTable.SlotSpecialOne
                : intent.Kind == 4 ? FighterHitContractTable.SlotSpecialTwo : 0;
            if (slot != 0) {
                FighterSpecialCancelRules.NoteLanded(
                    ref attackerAction, slot, outcome, intent.Damage, contracts.For(attacker.PlayerID, slot));
                FighterLingeringKitRules.NoteOwnerSpecialHit(
                    ref attackerVerb, outcome, intent.Damage, contracts.For(attacker.PlayerID, slot));
            }
        }

        private readonly struct AttackIntent {
            public readonly int Kind;
            public readonly int Damage;
            public readonly FP64 Knockback;
            public readonly int HitstunFrames;
            public readonly int StatusType;
            public readonly int StatusFrames;
            public readonly FP64 StatusIntensity;
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
                int statusType, int statusFrames, FP64 statusIntensity,
                bool launches, in FighterAbilityHitData contract) =>
                new(kind, damage,
                    contract.HasKnockbackVector ? contract.KnockbackX : tuningKnockback,
                    hitstunFrames, statusType, statusFrames, statusIntensity,
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
        /// status slots, the Rally pool are cleared, and
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
                verb.ShieldStunFrames = 0;
                verb.BlockLockoutFrames = 0;
                verb.HitstunBlockCancelBlocked = 0;
                verb.LedgeGrabsThisAirtime = 0;
                verb.GrabPhase = FighterGrabRules.PhaseNone;
                verb.GrabPhaseFrames = 0;
                verb.BeingHeld = 0;
                verb.ThrowImmunityFrames = 0;
                verb.ClearGrabPartner();
                // Meter and Defy update 2026-10-31 (5.2, O22): the gauge restarts at
                // the match-start fill with both reuse delays clear.
                verb.MomentumUnits = FighterMomentumRules.SuddenDeathUnits;
                // No Overtime multiplier inside the phase.
                verb.OvertimeActive = 0;
                runtime.SpecialOneReuseFrames = 0;
                runtime.SpecialTwoReuseFrames = 0;
                runtime.MovementCooldownFrames = 0;
                // The ability lockout and its D1 hit-confirm state (S, C, the
                // freeze-only Special buffer).
                FighterAbilityLockoutRules.Clear(ref runtime);
                runtime.BlockRegenFrames = 0;
                runtime.SpeedBuffFrames = 0;
                runtime.JumpBuffFrames = 0;
                runtime.ZoneSpeedBonusFrames = 0;
                runtime.FloatFrames = 0;
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
                // D02e: Sudden Death cleanup clears the Aegis bubble and any
                // barrier. Defy is NOT barred any more (meter and Defy update,
                // 5.2): the Influence restart at 0 is the only limit, so a Defy
                // is legal once 50 is earned again.
                if (frame.Has<FighterDefenseComponent>(entity)) {
                    FighterDefenseRules.Clear(ref frame.Get<FighterDefenseComponent>(entity));
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
                // Combat and movement update (plan §1.5): no Special cast, D3
                // buffer, block clock, Down tap or action flag survives into
                // Sudden Death — component 323 returns to all zero.
                if (frame.Has<FighterActionStateComponent>(entity)) {
                    frame.Get<FighterActionStateComponent>(entity) = default;
                }
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
            // 2026-10-11: the stock loss zeroes the hitstop below, so a held
            // freeze-only Special press dies with it. The D1 lockouts S and C are
            // left to expire on the respawn platform (TickCounters runs there).
            runtime.FreezeBufferArmed = 0;
            runtime.FreezeBufferedSpecials = 0;
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
            verb.ShieldStunFrames = 0;
            verb.BlockLockoutFrames = 0;
            verb.HitstunBlockCancelBlocked = 0;
            verb.LedgeGrabsThisAirtime = 0;
            verb.ClearGrabPartner();
            // F21: the single victim-side increment, atomic with the living-to-KO
            // transition and before the respawn placement below. Every cause
            // reaches this one chokepoint, so nothing needs attacker credit and no
            // duplicate callback can repeat it.
            runtime.KnockoutsSuffered++;
            if (runtime.UsesStocks != 0) fighter.Stocks--;
            fighter.Influence = FighterMeterRules.RetainOnStockLoss(fighter.Influence);
            fighter.CurrentHP = fighter.MaxHP;
            fighter.BlockCharges = tuning.MaxBlockCharges;
            fighter.RemainingJumps = tuning.MaxJumpCount;
            // Chronal Respawn Platform, all match modes: the fighter materialises
            // frozen and invulnerable at the stage's respawn anchor (centre +3.0
            // unless the stage overrides it) rather than teleporting straight back
            // into play. The spawn protection (CM-HIT 4.2: 120 frames shrinking
            // to 60 with the wait) is armed when the platform drops them, not here.
            fighter.Position =
                geometry?.RespawnPlatformPosition ?? FighterMatchFlowRules.RespawnPlatformPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 1;
            if (fighter.Stocks > 0) {
                fighter.RespawnFramesRemaining = FighterMatchFlowRules.RespawnPlatformFrames;
                fighter.InvulnerabilityFrames =
                    FighterMatchFlowRules.RespawnPlatformFrames + FighterMatchFlowRules.RespawnInvulnerabilityMaxFrames;
            } else {
                // Out of stocks: no platform, no respawn. The match resolves this frame.
                fighter.RespawnFramesRemaining = 0;
                fighter.InvulnerabilityFrames = FighterMatchFlowRules.RespawnInvulnerabilityMaxFrames;
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
