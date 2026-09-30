using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Deterministic CPU input generator and the single owner of the utility
    /// decision table. Decisions are evaluated every three simulation ticks and
    /// delivered after the difficulty-specific reaction delay, so generated frames
    /// can be recorded and replayed like human/network input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The decision table reads a mode-neutral <see cref="CpuDecisionObservation"/>,
    /// so the Story-side Level 13 Mirror Paradox drives this exact engine through
    /// the observation overload instead of maintaining a parallel port. Nothing here
    /// may take a Godot or Story dependency.
    /// </para>
    /// <para>
    /// The controller is an <i>input source</i>, exactly like a human pad: its RNG,
    /// schedule ring, and button-edge state are deliberately outside the rollback
    /// snapshot (Package 6 plan §2.5). Determinism comes from the seed plus the
    /// observation sequence, not from being simulation state.
    /// </para>
    /// <para>
    /// Band behaviour follows <c>design-godot.md</c> §10 "CPU Fighter AI
    /// Specifications" difficulty matrices; every rate lives in
    /// <see cref="CpuBandTuning"/> rather than inline in the table.
    /// </para>
    /// </remarks>
    public sealed class FighterCpuController {
        private const int DecisionIntervalTicks = 3;
        private const int ScheduleCapacity = 128;

        /// <summary>Frames an edge button stays held before the forced release.</summary>
        private const int EdgeHoldFrames = 2;
        /// <summary>Frames the edge bits stay clear so the next press is a real edge.</summary>
        private const int EdgeReleaseGapFrames = 1;

        /// <summary>
        /// Buttons the simulation reads from the <i>pressed edge</i>. They are pulsed
        /// (held, released, gap) instead of latched, otherwise a CPU that keeps
        /// choosing the same action produces exactly one edge and can never double
        /// jump, re-attack, or chain a recovery.
        /// </summary>
        private const GameplayButtons EdgeButtons =
            GameplayButtons.Jump | GameplayButtons.BasicAttack | GameplayButtons.Special1
            | GameplayButtons.Special2 | GameplayButtons.Ultimate | GameplayButtons.MovementAbility
            | GameplayButtons.Roll;

        /// <summary>design Section 6/10: the Mirror Paradox reaction window.</summary>
        public const int HardReactionDelayMinFrames = 4;
        public const int HardReactionDelayMaxFrames = 8;

        private static readonly FP64 CloseRange = FP64.FromInt(2);
        private static readonly FP64 FarRange = FP64.FromInt(5);
        private static readonly FP64 OrbAwarenessRange = FP64.FromInt(8);
        private static readonly FP64 OrbClimbHeight = FP64.One;
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);
        private static readonly FP64 CorneredMargin = FP64.FromDouble(1.5);
        /// <summary>Incoming shots beyond this horizontal gap are not yet a threat.</summary>
        private static readonly FP64 ProjectileAwarenessRange = FP64.FromInt(7);
        /// <summary>Shots this far above/below the fighter will pass clean over or under.</summary>
        private static readonly FP64 ProjectileVerticalBand = FP64.FromInt(2);
        /// <summary>
        /// Below this much total walkable room outside a hazard's danger band there
        /// is nowhere to flee to, so flee-evasion is pointless (M-9, Globe heckle).
        /// </summary>
        private static readonly FP64 MinimumHazardEscapeRoom = FP64.One;
        /// <summary>Canonical grab reach, from the shared rules — never an AI constant.</summary>
        private static readonly FP64 GrabReach = FP64.FromDouble(FTT.Combat.BasicComboRules.GrabReachUnits);
        /// <summary>Canonical Echo Step meter cost, from the shared rules.</summary>
        private static readonly FP64 EchoStepMeterCost =
            FP64.FromInt(FTT.Combat.BasicComboRules.EchoStepMeterCost);
        /// <summary>Vertical slack that still counts as standing on an authored platform.</summary>
        private static readonly FP64 PlatformLandingTolerance = FP64.FromDouble(0.35);
        /// <summary><c>FighterGrabRules</c> phase 4: the grab is holding its victim.</summary>
        private const int GrabPhaseHolding = 4;

        /// <summary>
        /// The verb a scheduled decision intends to emit, so delivery can revalidate
        /// self-legality before the chord reaches the simulation.
        /// </summary>
        private enum CpuVerbAction {
            None = 0,
            Grab = 1,
            EchoStep = 2
        }

        private readonly CpuDifficulty _difficulty;
        private readonly CpuBandTuning _tuning;
        private readonly FighterStageGeometry _geometry;
        private readonly ICpuWorldObserver _world;
        private readonly CpuRecoveryProfile _profile;
        private readonly ScheduledDecision[] _schedule = new ScheduledDecision[ScheduleCapacity];
        private uint _randomState;
        private GameplayButtons _sustainedHeld;
        private GameplayButtons _activeEdges;
        private GameplayButtons _pendingEdges;
        private int _edgeFramesRemaining;
        private int _edgeGapFramesRemaining;

        // === V7.4 hitstun defense (input-side only; no combat-rules change) ===
        /// <summary>
        /// Frames the escape stance lingers after hitstun ends grounded, so the
        /// blocked-into stance survives the rest of the attacker's string (the
        /// hit-2 → hit-3 connect gap is bounded by hit 2's 40-frame hitstun).
        /// Input-side pacing only — the sim rules are untouched.
        /// </summary>
        public const int EscapeStanceLingerFrames = 45;

        /// <summary>
        /// Upper bound on the pit-risk trajectory probe (four seconds). A launch
        /// that has not reached a landing surface inside it is already past the
        /// blast zone or pinned on the ceiling; either way the probe's answer no
        /// longer matters.
        /// </summary>
        private const int TrajectoryProbeTicks = 240;

        /// <summary>
        /// Frames an offstage episode may run before Hard drops every optional
        /// hesitation and commits its highest-progress route. The contract's
        /// "no indefinite offstage cooldown/ledge stalls" rule needs a bound, and
        /// four seconds is longer than any authored recovery route.
        /// </summary>
        private const int OffstageStallLimitFrames = 240;

        private int _lastObservedHitstunFrames;
        private int _escapeStanceLingerRemaining;
        private bool _hitstunHoldBlockActive;
        private bool _hitstunDiActive;

        // === F19 recovery episode (Package 11 A9b; input-side latch, not sim state) ===
        /// <summary>
        /// An offstage <b>episode</b> per <c>CPU_RECOVERY.md</c>: it opens when
        /// the fighter first becomes unsupported over a stage gap or past a wall,
        /// and ends only on a stable landing on authored stage floor/platform, a
        /// legal ledge capture, or a KO. Easy and Medium may activate the movement
        /// ability <b>once</b> per episode; a jump refund or a temporary
        /// staff-platform landing does not reset that limit, which is exactly why
        /// the latch is an episode rather than a per-airtime counter.
        /// </summary>
        private bool _episodeActive;
        private int _episodeMovementActivations;
        /// <summary>
        /// Delivery tick of a movement activation that has been planned but not
        /// yet emitted, or −1. The per-episode limit is spent when the input
        /// actually reaches the simulation, not when the plan is formed — and the
        /// schedule ring can overwrite a pending slot, so a marker whose tick has
        /// passed unconsumed is released rather than charged.
        /// </summary>
        private int _movementActivationDeliveryTick = -1;
        private int _episodeMobilitySpecialActivations;
        private int _episodeFrames;
        private int _episodeStocksAtStart;

        // === F19 verb policy: grabs and Echo Step ===
        /// <summary>
        /// Admission is rolled <b>once per opportunity</b>, never once per
        /// three-frame evaluation, and a failed roll never reopens the same
        /// continuing opportunity (<c>CPU_COMBAT_POLICY.md</c>). Opportunity
        /// identity is what makes that enforceable: a grab opportunity is the run
        /// of decisions over which the blocking/grounded/range conditions hold
        /// continuously, and an Echo Step opportunity is keyed to the CPU's own
        /// attack execution ID.
        /// </summary>
        private int _grabOpportunityID;
        private int _grabOpportunityRolledID;
        private bool _grabOpportunityAdmitted;
        private bool _grabOpportunityOpen;
        private int _attackExecutionID;
        private int _lastObservedAttackPhase;
        /// <summary>−1, not 0, so the very first execution is still an unrolled one.</summary>
        private int _echoOpportunityRolledExecutionID = -1;
        private bool _echoOpportunityAdmitted;
        /// <summary>Verb the decision currently being built intends to emit.</summary>
        private CpuVerbAction _pendingVerb;
        /// <summary>Whether the decision being built spends a movement activation.</summary>
        private bool _pendingMovementActivation;

        public FighterCpuController(CpuDifficulty difficulty, int seed)
            : this(difficulty, seed, null, null) {
        }

        /// <summary>
        /// Full construction. <paramref name="geometry"/> supplies the stage bounds
        /// and platform summary the observation carries; <paramref name="world"/>
        /// supplies live orbs/hazards, the verb layer and the match-live gate. Both
        /// are optional — omitting them yields the Story-equivalent "absent"
        /// sentinels. <paramref name="tuningOverride"/> replaces the difficulty
        /// band's rates — a test seam for pinning roll gates at 0%/100%; production
        /// callers omit it. <paramref name="recoveryProfile"/> is the F19
        /// per-character planning profile built from the same normalized
        /// <see cref="FighterLoadout"/> the fighter itself was built from; omitting
        /// it leaves the planner on universal movement only.
        /// </summary>
        public FighterCpuController(
            CpuDifficulty difficulty,
            int seed,
            FighterStageGeometry geometry,
            ICpuWorldObserver world,
            CpuBandTuning? tuningOverride = null,
            CpuRecoveryProfile? recoveryProfile = null) {
            _difficulty = difficulty;
            _tuning = tuningOverride ?? CpuBandTuning.For(difficulty);
            _geometry = geometry;
            _world = world;
            _profile = recoveryProfile ?? default;
            _randomState = unchecked((uint)seed) ^ 0xA511E9B3u;
            if (_randomState == 0) _randomState = 0x6D2B79F5u;
        }

        public CpuDifficulty Difficulty => _difficulty;

        /// <summary>Per-band rates in force, exposed so tests can pin the matrices.</summary>
        public CpuBandTuning Tuning => _tuning;

        /// <summary>The F19 recovery profile in force; <c>HasKit</c> is false when none was supplied.</summary>
        public CpuRecoveryProfile RecoveryProfile => _profile;

        /// <summary>
        /// Movement-ability activations spent in the current offstage episode.
        /// Exposed for the F19 drills — the once-per-episode limit is a policy
        /// claim that has to be observable to be pinned.
        /// </summary>
        public int EpisodeMovementActivations => _episodeMovementActivations;

        /// <summary>
        /// Identity of the grab opportunity whose admission has been rolled, and
        /// the CPU's attack execution whose Echo Step admission has been rolled.
        /// <c>DEFER-CPU-SNAPSHOT</c>: <c>CPU_COMBAT_POLICY.md</c> asks for these
        /// to be snapshotted for resimulation, but the controller is an input
        /// source outside the rollback snapshot by design (Package 6 §2.5) — the
        /// produced frames are recorded and replayed instead. Exposed here so the
        /// once-per-opportunity rule is testable without re-architecting that.
        /// </summary>
        public int GrabOpportunityID => _grabOpportunityID;

        /// <inheritdoc cref="GrabOpportunityID"/>
        public int AttackExecutionID => _attackExecutionID;

        public PlayerInputFrame Sample(
            uint tick,
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            in PlayerInputFrame previousFrame) {
            CpuDecisionObservation observation =
                Observe(in self, in selfRuntime, in target, in targetRuntime, _geometry, _world);
            return Sample(tick, in observation, in previousFrame);
        }

        /// <summary>
        /// Mode-neutral entry point: same schedule ring, same decision table, same
        /// RNG stream as the deterministic path. Story adapters call this one.
        /// </summary>
        public PlayerInputFrame Sample(
            uint tick,
            in CpuDecisionObservation observation,
            in PlayerInputFrame previousFrame) {
            TrackEpisodeAndExecution(in observation);
            if (_movementActivationDeliveryTick >= 0 && _movementActivationDeliveryTick < (int)tick) {
                // Its slot was overwritten by a later decision, so the activation
                // never reached the simulation and must not be charged.
                _movementActivationDeliveryTick = -1;
            }
            if (tick % DecisionIntervalTicks == 0) {
                _pendingVerb = CpuVerbAction.None;
                _pendingMovementActivation = false;
                GameplayButtons decision = Decide(in observation, out sbyte moveX, out sbyte moveY);
                int deliveryTick = checked((int)tick + NextReactionDelay());
                int slot = deliveryTick % ScheduleCapacity;
                _schedule[slot] = new ScheduledDecision {
                    Tick = deliveryTick,
                    MoveX = moveX,
                    MoveY = moveY,
                    Held = decision,
                    Verb = _pendingVerb,
                    SpendsMovementActivation = _pendingMovementActivation
                };
                if (_pendingMovementActivation) _movementActivationDeliveryTick = deliveryTick;
            }

            sbyte outputMoveX = previousFrame.MoveX;
            sbyte outputMoveY = previousFrame.MoveY;
            int outputSlot = (int)(tick % ScheduleCapacity);
            if (_schedule[outputSlot].Tick == (int)tick) {
                ScheduledDecision delivery = _schedule[outputSlot];
                _schedule[outputSlot].Tick = -1;
                // "Revalidate legality immediately before each planned action"
                // (CPU_RECOVERY.md) / "Use current self-state for legality"
                // (CPU_COMBAT_POLICY.md). Opponent reads stay delayed — an
                // opponent who stops blocking is allowed to make a planned grab
                // whiff — but a verb the CPU itself can no longer legally start
                // is dropped rather than mashed, and a missed window produces no
                // retroactive cancel, reroll or refund.
                if (delivery.Verb == CpuVerbAction.None
                    || VerbStillLegalForSelf(delivery.Verb, in observation)) {
                    if (delivery.SpendsMovementActivation) {
                        _episodeMovementActivations++;
                        _movementActivationDeliveryTick = -1;
                    }
                    outputMoveX = delivery.MoveX;
                    outputMoveY = delivery.MoveY;
                    _sustainedHeld = delivery.Held & ~EdgeButtons;
                    GameplayButtons edges = delivery.Held & EdgeButtons;
                    if (edges != GameplayButtons.None) _pendingEdges = edges;
                }
            }

            AdvanceEdgePulse();
            GameplayButtons held = ApplyHitstunDefense(
                in observation, _sustainedHeld | _activeEdges, ref outputMoveX);
            return PlayerInputFrame.Create(
                tick, outputMoveX / 127f, outputMoveY / 127f,
                held, previousFrame.Held);
        }

        /// <summary>
        /// V7.4 hitstun defense (design §10 difficulty matrices, the "Hitstun
        /// Defense" rows). Rolled <b>once per hitstun instance</b> — a new
        /// instance is <c>HitstunFrames</c> rising from zero or above the
        /// previous frame's value (a mid-stun refresh is a new hit and re-rolls).
        /// A successful defense roll holds Block for the remainder of the
        /// hitstun, through the airborne tumble and ground contact, and for a
        /// short grounded linger — buying the hit-2 escape, the blocked rest of
        /// the string, and the landing tech purely through the ordinary sim
        /// rules (nothing in the simulation is special-cased). A successful DI
        /// roll additionally holds the stick toward stage centre while hitstun
        /// runs, which is exactly what <c>FighterVerbRules.ResolvePendingLaunch</c>
        /// reads when the launch hitstop ends.
        ///
        /// <para><b>Pit-aware DI shipped with Package 11 A9b.</b> The V7.4 deferral
        /// ("no cheap launch-trajectory-vs-pit test, and the authored stages run
        /// solid floors") expired with A9's three Open stages, and
        /// <see cref="ResolveDiHold"/> now reads that floor topology: while the
        /// launch trajectory ends over a gap the hold aims at the nearest
        /// pit-facing floor edge instead of stage centre, which on Paris is the
        /// hole itself. Sealed stages and any trajectory already ending over floor
        /// keep the original toward-centre hold exactly.</para>
        /// <para>
        /// This reflex deliberately bypasses the decision schedule, and
        /// <c>CPU_RECOVERY.md</c> now says so in as many words: "Geometry and the
        /// CPU's own current state may be checked directly for input legality;
        /// opponent information still passes through the reaction buffer."
        /// Hitstun is self-state.
        /// </para>
        /// The RNG draws live in the same recorded-input path as every other
        /// roll (BlockPercent and friends): the produced frame is recorded and
        /// replayed like human input, so rollback never re-samples them. The
        /// daze branch stays inert — dazed players genuinely cannot act.
        /// </summary>
        private GameplayButtons ApplyHitstunDefense(
            in CpuDecisionObservation observation, GameplayButtons held, ref sbyte moveX) {
            int hitstun = observation.HitstunFrames;
            bool newInstance = hitstun > 0
                && (_lastObservedHitstunFrames <= 0 || hitstun > _lastObservedHitstunFrames);
            if (newInstance) {
                _hitstunHoldBlockActive = NextPercent() < _tuning.HitstunDefensePercent;
                _hitstunDiActive = NextPercent() < _tuning.DiPercent;
                _escapeStanceLingerRemaining = EscapeStanceLingerFrames;
            }
            if (_hitstunHoldBlockActive && hitstun <= 0 && observation.IsGrounded != 0) {
                // Grounded with hitstun over (escaped, teched, or ridden out):
                // the stance lingers briefly so the rest of the string meets a
                // shield, then the hold releases.
                if (_escapeStanceLingerRemaining > 0) _escapeStanceLingerRemaining--;
                if (_escapeStanceLingerRemaining <= 0) {
                    _hitstunHoldBlockActive = false;
                    _hitstunDiActive = false;
                }
            }
            _lastObservedHitstunFrames = hitstun;

            if (!_hitstunHoldBlockActive
                || observation.SuppressGameplayInput != 0
                || observation.DazeFrames > 0
                || observation.Stocks <= 0) {
                return held;
            }
            // The hold is EXCLUSIVE, not additive: a scheduled BasicAttack edge
            // landing while Block is held would read as the grab chord, and a
            // grabbing fighter has no functioning shield (V7.3) — the committed
            // escape stance must never convert itself into a grab attempt.
            held = GameplayButtons.Block;
            // M05 (Package 12 W3b): DI only matters on a LAUNCH. A non-launching
            // hit leaves a grounded victim on the floor (grounded knockback), so
            // an airborne body in hitstun is the input-side read of "launched" —
            // the grounded hitstun of hits 1-2 gets the Block hold (the hit-2
            // escape) and no stick. Input-side only; the sim is untouched.
            if (_hitstunDiActive && hitstun > 0 && observation.IsGrounded == 0
                && observation.HasStageBounds != 0) {
                moveX = ResolveDiHold(in observation);
            }
            return held;
        }

        /// <summary>
        /// Pit-aware DI (Package 11 A9b; the V7.4 deferral expired with A9's Open
        /// stages). While the current launch trajectory ends over a gap, the
        /// useful hold is toward the nearest pit-facing floor edge — the direction
        /// that puts solid ground under the fighter — not toward stage centre,
        /// which on Paris <i>is</i> the hole. Everywhere else (Sealed stages, the
        /// legacy arena, any trajectory that already ends over floor) the answer
        /// is the unchanged toward-centre fallback, so no Closed-stage behaviour
        /// moves. Pure fixed-point reads off A9's observation block; no new
        /// geometry query and no float.
        /// </summary>
        private static sbyte ResolveDiHold(in CpuDecisionObservation observation) {
            long centerRaw = (observation.LeftWallRaw + observation.RightWallRaw) / 2;
            sbyte towardCentre = observation.SelfPositionXRaw < centerRaw ? (sbyte)127 : (sbyte)-127;
            if (observation.HasFloorSegments == 0) return towardCentre;
            bool overGap = observation.HasFloorSupportUnderSelf == 0;
            if (!overGap && observation.LaunchTrajectoryCrossesGap == 0) return towardCentre;

            bool hasLeft = observation.HasFloorEdgeLeft != 0;
            bool hasRight = observation.HasFloorEdgeRight != 0;
            if (!hasLeft && !hasRight) return towardCentre;
            if (!hasLeft) return 127;
            if (!hasRight) return -127;
            long leftGap = observation.SelfPositionXRaw - observation.NearestFloorEdgeLeftXRaw;
            long rightGap = observation.NearestFloorEdgeRightXRaw - observation.SelfPositionXRaw;
            // Ties resolve rightward, deterministically, on every peer.
            return Absolute(leftGap) < Absolute(rightGap) ? (sbyte)-127 : (sbyte)127;
        }

        /// <summary>
        /// Runs the press → hold → release → gap cycle for edge buttons. A pending
        /// decision waits for the gap to clear, so every scheduled action reaches the
        /// simulation as its own <c>Pressed</c> edge.
        /// </summary>
        private void AdvanceEdgePulse() {
            if (_edgeFramesRemaining > 0) {
                _edgeFramesRemaining--;
                if (_edgeFramesRemaining <= 0) {
                    _activeEdges = GameplayButtons.None;
                    _edgeGapFramesRemaining = EdgeReleaseGapFrames;
                }
                return;
            }
            if (_edgeGapFramesRemaining > 0) {
                _edgeGapFramesRemaining--;
                return;
            }
            if (_pendingEdges == GameplayButtons.None) return;
            _activeEdges = _pendingEdges;
            _pendingEdges = GameplayButtons.None;
            _edgeFramesRemaining = EdgeHoldFrames;
        }

        /// <summary>
        /// Projects deterministic simulation components onto the shared observation.
        /// Stage bounds, orbs, and hazards stay at their "absent" sentinels.
        /// </summary>
        public static CpuDecisionObservation Observe(
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime) =>
            Observe(in self, in selfRuntime, in target, in targetRuntime, null, null);

        /// <summary>
        /// Full deterministic projection: fighter components plus the stage geometry
        /// handed to the controller at construction plus the live world entities.
        /// </summary>
        public static CpuDecisionObservation Observe(
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            FighterStageGeometry geometry,
            ICpuWorldObserver world) {
            var observation = new CpuDecisionObservation {
                SelfPositionXRaw = self.Position.x.RawValue,
                SelfPositionYRaw = self.Position.y.RawValue,
                SelfVelocityXRaw = self.Velocity.x.RawValue,
                SelfVelocityYRaw = self.Velocity.y.RawValue,
                TargetPositionXRaw = target.Position.x.RawValue,
                TargetPositionYRaw = target.Position.y.RawValue,
                Stocks = self.Stocks,
                HitstunFrames = self.HitstunFrames,
                DazeFrames = self.DazeFrames,
                IsGrounded = self.IsGrounded,
                IsLedgeHanging = FighterLedgeRules.IsHanging(in selfRuntime) ? 1 : 0,
                RemainingJumps = self.RemainingJumps,
                SelfCurrentHP = self.CurrentHP,
                SelfMaxHP = self.MaxHP,
                InfluenceRaw = self.Influence.RawValue,
                SpecialOneCooldownFrames = selfRuntime.SpecialOneCooldownFrames,
                SpecialTwoCooldownFrames = selfRuntime.SpecialTwoCooldownFrames,
                MovementCooldownFrames = selfRuntime.MovementCooldownFrames,
                TargetCurrentHP = target.CurrentHP,
                TargetMaxHP = target.MaxHP,
                TargetHitstunFrames = target.HitstunFrames,
                TargetPressedButtons = targetRuntime.PressedButtons,
                TargetInfluenceRaw = target.Influence.RawValue
            };

            if (geometry != null) {
                observation.HasStageBounds = 1;
                observation.LeftWallRaw = geometry.LeftWall.RawValue;
                observation.RightWallRaw = geometry.RightWall.RawValue;
                observation.CeilingRaw = geometry.Ceiling.RawValue;
                observation.BottomBlastZoneRaw = geometry.BottomBlastZone.RawValue;
                observation.PlatformCount = geometry.Platforms.Length;
                if (geometry.Platforms.Length > 0) {
                    int nearest = 0;
                    FP64 best = FP64.Abs(geometry.Platforms[0].CenterX - self.Position.x);
                    for (int index = 1; index < geometry.Platforms.Length; index++) {
                        FP64 distance = FP64.Abs(geometry.Platforms[index].CenterX - self.Position.x);
                        if (distance >= best) continue;
                        best = distance;
                        nearest = index;
                    }
                    observation.NearestPlatformCenterXRaw = geometry.Platforms[nearest].CenterX.RawValue;
                    observation.NearestPlatformSurfaceYRaw = geometry.Platforms[nearest].SurfaceY.RawValue;
                    observation.NearestPlatformHalfWidthRaw = geometry.Platforms[nearest].HalfWidth.RawValue;
                }
                FillFloorTopology(ref observation, in self, geometry);
            }

            if (world != null) {
                observation.SuppressGameplayInput = world.IsMatchLive ? 0 : 1;
                if (world.TryGetNearestOrb(in self.Position, out FighterOrbComponent orb)) {
                    observation.HasOrb = 1;
                    observation.OrbEffectType = orb.EffectType;
                    observation.OrbPositionXRaw = orb.Position.x.RawValue;
                    observation.OrbPositionYRaw = orb.Position.y.RawValue;
                }
                if (world.TryGetRelevantHazard(in self.Position, out FighterHazardComponent hazard)) {
                    observation.HasHazard = 1;
                    observation.HazardPhase = hazard.Phase;
                    observation.HazardPositionXRaw = hazard.Position.x.RawValue;
                    observation.HazardPositionYRaw = hazard.Position.y.RawValue;
                    observation.HazardHalfWidthRaw = hazard.HalfExtents.x.RawValue;
                }
                if (world.TryGetNearestHostileProjectile(
                        self.PlayerID, in self.Position, out FighterProjectileComponent projectile)) {
                    observation.HasHostileProjectile = 1;
                    observation.ProjectileRelativeXRaw = (projectile.Position.x - self.Position.x).RawValue;
                    observation.ProjectileRelativeYRaw = (projectile.Position.y - self.Position.y).RawValue;
                    observation.ProjectileVelocityXRaw = projectile.Velocity.x.RawValue;
                    observation.ProjectileVelocityYRaw = projectile.Velocity.y.RawValue;
                }
                FillVerbLayer(ref observation, in self, in selfRuntime, in target, in targetRuntime, world);
            }

            return observation;
        }

        /// <summary>
        /// Fills the F19 verb block (Package 11 A9b). Every field is gated behind
        /// an observer that can actually resolve a <c>FighterVerbComponent</c>, so
        /// a Story adapter — which has none — leaves <c>HasVerbState</c> and
        /// <c>HasTargetVerbState</c> at zero and the grab / Echo Step branches are
        /// unreachable there by construction, exactly like the stage, orb and
        /// floor-topology blocks.
        /// </summary>
        private static void FillVerbLayer(
            ref CpuDecisionObservation observation,
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            ICpuWorldObserver world) {
            if (world.TryGetVerbState(self.PlayerID, out CpuVerbState selfVerb)) {
                observation.HasVerbState = 1;
                observation.SelfFacingRight = self.FacingRight != 0 ? 1 : 0;
                observation.SelfAttackPhase = selfRuntime.AttackPhase;
                observation.SelfAttackMadeContact =
                    (selfRuntime.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0 ? 1 : 0;
                observation.SelfRolling =
                    selfRuntime.UniversalMovementState != (int)UniversalMovementPhase.None ? 1 : 0;
                observation.SelfGrabPhase = selfVerb.GrabPhase;
                observation.SelfBeingHeld = selfVerb.BeingHeld;
                observation.SelfShieldStunFrames = selfVerb.ShieldStunFrames;
                observation.SelfEchoStepCooldownFrames = selfVerb.EchoStepCooldownFrames;
                observation.SelfEchoStepWindupFrames = selfVerb.EchoStepWindupFrames;
                observation.SelfDefyAvailable = selfVerb.DefyHistoryUsed == 0 ? 1 : 0;
                if (world.TryGetEchoStepDestination(self.PlayerID, out FPVector2 destination)) {
                    observation.HasEchoStepDestination = 1;
                    observation.EchoStepDestinationXRaw = destination.x.RawValue;
                    observation.EchoStepDestinationYRaw = destination.y.RawValue;
                }
            }
            if (!world.TryGetVerbState(target.PlayerID, out CpuVerbState targetVerb)) return;
            observation.HasTargetVerbState = 1;
            observation.TargetIsGrounded = target.IsGrounded != 0 ? 1 : 0;
            observation.TargetBlockStance = targetVerb.BlockStance;
            observation.TargetShieldStunFrames = targetVerb.ShieldStunFrames;
            observation.TargetInvulnerabilityFrames = target.InvulnerabilityFrames;
            observation.TargetDazeFrames = target.DazeFrames;
            observation.TargetRolling =
                targetRuntime.UniversalMovementState != (int)UniversalMovementPhase.None ? 1 : 0;
            observation.TargetThrowImmune =
                targetVerb.ThrowImmunityFrames > 0 || targetVerb.BeingHeld != 0 ? 1 : 0;
        }

        /// <summary>
        /// Fills the main-floor topology block (Package 11 A9). On a Sealed stage
        /// the floor is unbroken, so <c>HasFloorSegments</c> stays 0 and only
        /// <c>HasFloorSupportUnderSelf</c> is written — always 1 — which is what
        /// keeps "closed stages never trigger recovery" true by construction.
        /// </summary>
        private static void FillFloorTopology(
            ref CpuDecisionObservation observation,
            in FighterStateComponent self,
            FighterStageGeometry geometry) {
            observation.HasFloorSupportUnderSelf = geometry.HasFloorSupport(self.Position.x) ? 1 : 0;
            if (!geometry.IsOpenStage) return;
            observation.HasFloorSegments = 1;

            bool hasLeft = geometry.TryGetNearestFloorEdge(self.Position.x, -1, out FP64 leftEdge);
            bool hasRight = geometry.TryGetNearestFloorEdge(self.Position.x, 1, out FP64 rightEdge);
            if (hasLeft) {
                observation.HasFloorEdgeLeft = 1;
                observation.NearestFloorEdgeLeftXRaw = leftEdge.RawValue;
            }
            if (hasRight) {
                observation.HasFloorEdgeRight = 1;
                observation.NearestFloorEdgeRightXRaw = rightEdge.RawValue;
            }
            if (observation.HasFloorSupportUnderSelf == 0 && hasLeft && hasRight) {
                observation.CurrentGapWidthRaw = (rightEdge - leftEdge).RawValue;
            }
            observation.LaunchTrajectoryCrossesGap = TrajectoryEndsOverGap(in self, geometry) ? 1 : 0;
        }

        /// <summary>
        /// Ballistic pit-risk probe: integrates the fighter's current position and
        /// velocity forward with the simulation's own tick length and gravity until
        /// they reach a landing surface, and reports whether that surface is a gap.
        /// Horizontal input and air control are deliberately ignored — the question
        /// is where the CURRENT trajectory ends, which is exactly what DI and
        /// recovery planning need to know. Pure fixed point, no allocation, and
        /// bounded at <see cref="TrajectoryProbeTicks"/>.
        /// </summary>
        private static bool TrajectoryEndsOverGap(
            in FighterStateComponent self, FighterStageGeometry geometry) {
            if (self.IsGrounded != 0) return false;
            FP64 delta = FighterMovementSystem.FixedDeltaSeconds;
            FP64 gravityStep = FighterMovementSystem.GravityPerSecondSquared * delta;
            FP64 x = self.Position.x;
            FP64 y = self.Position.y;
            FP64 velocityX = self.Velocity.x;
            FP64 velocityY = self.Velocity.y;

            for (int tick = 0; tick < TrajectoryProbeTicks; tick++) {
                FP64 previousY = y;
                // M01: the probe integrates exactly like the sim, terminal clamp included.
                velocityY = FighterMovementSystem.ClampToTerminal(velocityY + gravityStep);
                x += velocityX * delta;
                y += velocityY * delta;
                if (x < geometry.LeftWall) x = geometry.LeftWall;
                else if (x > geometry.RightWall) x = geometry.RightWall;

                // A one-way platform caught on the way down is a landing, not a pit.
                for (int index = 0; index < geometry.Platforms.Length; index++) {
                    FighterStagePlatform platform = geometry.Platforms[index];
                    if (previousY < platform.SurfaceY
                        || y > platform.SurfaceY
                        || !platform.Supports(x)) continue;
                    return false;
                }
                if (y > FP64.Zero) continue;
                return !geometry.HasFloorSupport(x);
            }
            return false;
        }

        public int GetReactionDelayBounds(out int maximum) {
            int minimum = _difficulty switch {
                CpuDifficulty.Easy => 30,
                CpuDifficulty.Normal => 15,
                CpuDifficulty.Hard => HardReactionDelayMinFrames,
                _ => 15
            };
            maximum = _difficulty switch {
                CpuDifficulty.Easy => 45,
                CpuDifficulty.Normal => 20,
                CpuDifficulty.Hard => HardReactionDelayMaxFrames,
                _ => 20
            };
            return minimum;
        }

        /// <summary>Legacy two-axis-free entry point retained for existing callers.</summary>
        public GameplayButtons Decide(in CpuDecisionObservation observation, out sbyte moveX) =>
            Decide(in observation, out moveX, out _);

        /// <summary>The utility decision table. One implementation, both modes.</summary>
        public GameplayButtons Decide(in CpuDecisionObservation observation, out sbyte moveX, out sbyte moveY) {
            moveX = 0;
            moveY = 0;
            // The decision table stays quiet in hitstun/daze — a stunned
            // fighter has no utility actions to schedule. The V7.4 hitstun
            // defense (hold Block through the stun, DI the launch) is NOT a
            // scheduled decision: it rides the reflex layer in
            // ApplyHitstunDefense, applied to the output frame directly,
            // because a 30-frame hitstun is over before Easy's 30–45-frame
            // reaction delay could deliver anything. Daze stays fully inert.
            if (observation.SuppressGameplayInput != 0
                || observation.Stocks <= 0
                || observation.HitstunFrames > 0
                || observation.DazeFrames > 0) {
                return GameplayButtons.None;
            }

            // Hanging off a ledge (§2.11) suppresses every attack and ability, so
            // the only meaningful action is the climb jump. It is checked ahead of
            // off-stage recovery because a hanging fighter is by definition already
            // holding the stage edge — recovery's drift-and-jump would just spend
            // the jump budget the grab refilled.
            if (observation.IsLedgeHanging != 0) return GameplayButtons.Jump;

            // A grab already holding the opponent: the only meaningful "decision"
            // left is the throw direction, which the shared rules read off held
            // input when the hold's decision window closes.
            if (observation.HasVerbState != 0 && observation.SelfGrabPhase == GrabPhaseHolding) {
                moveX = ResolveThrowHold(in observation);
                return GameplayButtons.None;
            }

            // "Recovery planning takes priority over optional offense and orb
            // pursuit while return is at risk" (CPU_RECOVERY.md) — which is why
            // this stays ahead of every other branch.
            if (IsOffStage(in observation)) return DecideRecovery(in observation, out moveX, out moveY);
            // Echo Step sits here, above hazard and orb behaviour: it is the
            // defensive cancel out of the CPU's own whiffed swing, and during
            // those recovery frames no other branch has a legal answer anyway.
            if (TryDecideEchoStep(in observation, out GameplayButtons echoStep, out moveX)) return echoStep;
            if (TryDecideHazardEvasion(in observation, out GameplayButtons evasion, out moveX)) return evasion;
            if (TryDecideProjectileDefense(in observation, out GameplayButtons defense, out moveX)) return defense;
            if (TryDecideOrbPursuit(in observation, out GameplayButtons pursuit, out moveX)) return pursuit;
            return DecideCombat(in observation, out moveX);
        }

        // === Off-stage recovery ===

        /// <summary>
        /// F19 recovery detection (Package 11 A9b). Recovery means "returning from
        /// an unsupported position over a <b>stage gap</b> to a reachable platform
        /// or legal ledge before the bottom blast zone", detected from authored
        /// floor segments, position and velocity — never from camera bounds or a
        /// universal main-platform Y threshold.
        ///
        /// <para>Two branches, both requiring authored bounds (which is what keeps
        /// a Story adapter, with no stage concept and an arbitrary world origin,
        /// out of here entirely):</para>
        /// <list type="bullet">
        /// <item>past a solid side wall — unchanged;</item>
        /// <item><b>unsupported over a gap</b> — either already below the floor
        /// plane with no floor under this X, or airborne on a trajectory that ends
        /// over a pit (A9's <c>LaunchTrajectoryCrossesGap</c>).</item>
        /// </list>
        /// <para>On a <b>Sealed</b> stage <c>HasFloorSegments</c> is zero, so the
        /// gap branch cannot fire at all and ordinary supported traversal — a
        /// routine jump above solid floor — never triggers an emergency cast.
        /// That is the contract's explicit Closed-stage requirement, satisfied by
        /// construction rather than by a threshold. The legacy pre-A9 test (below
        /// the floor plane and airborne, on <i>any</i> stage) is kept only for
        /// stages with no authored segments <b>and</b> a solid base floor, where
        /// being under the floor plane really does mean falling past it.</para>
        /// </summary>
        private static bool IsOffStage(in CpuDecisionObservation observation) {
            if (observation.HasStageBounds == 0) return false;
            if (observation.SelfPositionXRaw < observation.LeftWallRaw
                || observation.SelfPositionXRaw > observation.RightWallRaw) return true;
            if (observation.HasFloorSegments != 0) {
                if (observation.IsGrounded != 0) return false;
                return observation.HasFloorSupportUnderSelf == 0
                    || observation.LaunchTrajectoryCrossesGap != 0;
            }
            return observation.SelfPositionYRaw < 0 && observation.IsGrounded == 0;
        }

        /// <summary>
        /// True once the fighter is back on authored stage geometry: solid main
        /// floor under this X, or standing on one of the stage's own one-way
        /// platforms. A temporary support — Mozart's three-second staff platform,
        /// a construct — is deliberately <b>not</b> a stable landing, because the
        /// contract says such a landing must not reset Easy/Medium's
        /// one-activation planning limit.
        /// </summary>
        private static bool IsStableStageLanding(in CpuDecisionObservation observation) {
            if (observation.IsGrounded == 0) return false;
            if (observation.HasStageBounds == 0) return true;
            if (observation.HasFloorSegments == 0) return true;
            if (observation.HasFloorSupportUnderSelf != 0) return true;
            if (observation.PlatformCount <= 0) return false;
            long surfaceDelta = observation.SelfPositionYRaw - observation.NearestPlatformSurfaceYRaw;
            long centreDelta = observation.SelfPositionXRaw - observation.NearestPlatformCenterXRaw;
            return Absolute(surfaceDelta) <= PlatformLandingTolerance.RawValue
                && Absolute(centreDelta) <= observation.NearestPlatformHalfWidthRaw;
        }

        /// <summary>
        /// Maintains the offstage episode latch and the attack-execution counter.
        /// Runs every tick, before the decision cadence, because both are
        /// self-state facts rather than decisions.
        /// </summary>
        private void TrackEpisodeAndExecution(in CpuDecisionObservation observation) {
            // A swing entering startup — from neutral or chained straight out of
            // the previous hit's recovery — is a new attack execution, which is
            // what Echo Step admission is keyed to.
            if (observation.SelfAttackPhase == FighterBasicAttackRules.PhaseStartup
                && _lastObservedAttackPhase != FighterBasicAttackRules.PhaseStartup) {
                _attackExecutionID++;
            }
            _lastObservedAttackPhase = observation.SelfAttackPhase;

            bool offStage = IsOffStage(in observation);
            if (!_episodeActive) {
                if (!offStage) return;
                _episodeActive = true;
                _episodeMovementActivations = 0;
                _episodeMobilitySpecialActivations = 0;
                _movementActivationDeliveryTick = -1;
                _episodeFrames = 0;
                _episodeStocksAtStart = observation.Stocks;
                return;
            }
            _episodeFrames++;
            // The three endings the contract allows, and only those: a stable
            // landing on authored stage geometry, a legal ledge capture, or a KO.
            if (IsStableStageLanding(in observation)
                || observation.IsLedgeHanging != 0
                || observation.Stocks < _episodeStocksAtStart
                || observation.Stocks <= 0) {
                _episodeActive = false;
                _episodeMovementActivations = 0;
                _episodeMobilitySpecialActivations = 0;
                _movementActivationDeliveryTick = -1;
                _episodeFrames = 0;
            }
        }

        /// <summary>
        /// The F19 tiered recovery planner, replacing the inverted
        /// jump → movement → Special 2 percentage ladder.
        ///
        /// <para><b>Easy</b> steers at the nearest plausible legal landing, spends
        /// remaining jumps, and may activate its movement ability <b>at most once
        /// per offstage episode</b>. Specials stay disabled — that is the whole
        /// correction: the shipped band used Special 2 at 55% and the movement
        /// ability never.</para>
        /// <para><b>Medium</b> estimates whether the return is reachable with the
        /// jumps it still has, and otherwise spends <b>one</b> suitable recovery
        /// ability — the movement ability by default, or a validated mobility
        /// Special where the profile approves one (none do today). No multi-ability
        /// chains, no platform/refund planning.</para>
        /// <para><b>Hard</b> compares routes: it holds a jump back when the
        /// movement ability alone already covers the gap and the ability is the
        /// better opener, chains jump → movement → refunded jump where the kit
        /// actually refunds (Breeze Glide), re-evaluates every decision, and past
        /// <see cref="OffstageStallLimitFrames"/> drops every optional hesitation so
        /// it cannot stall offstage on cooldown cycles.</para>
        /// <para>Every distance, duration and trajectory comes from
        /// <see cref="CpuRecoveryProfile"/>, which is built from the same
        /// normalized <see cref="FighterLoadout"/> the human's fighter is built
        /// from. There are no CPU-only jump resets, cooldown refunds,
        /// invulnerability or teleport reach anywhere in this method.</para>
        /// </summary>
        private GameplayButtons DecideRecovery(
            in CpuDecisionObservation observation, out sbyte moveX, out sbyte moveY) {
            moveY = 0;
            moveX = ResolveReturnDirection(in observation);
            GameplayButtons held = GameplayButtons.None;
            bool falling = observation.SelfVelocityYRaw <= 0;
            bool desperate = _episodeFrames >= OffstageStallLimitFrames;

            bool movementReady = observation.MovementCooldownFrames <= 0
                && MovementActivationsAllowed();
            bool jumpsLeft = observation.RemainingJumps > 0;
            // Route comparison, Hard only: when the jumps in hand cannot cover the
            // remaining horizontal distance but the movement ability can, opening
            // with the ability is the feasible route rather than the fallback.
            bool preferAbilityFirst = _tuning.PlansMultiActionRecovery
                && movementReady
                && !JumpsAloneCanReturn(in observation);

            if (jumpsLeft && falling && !preferAbilityFirst
                && (desperate || NextPercent() < _tuning.RecoveryJumpPercent)) {
                return held | GameplayButtons.Jump;
            }
            if (movementReady && (desperate || NextPercent() < _tuning.RecoveryMovementPercent)) {
                _pendingMovementActivation = true;
                AimMovementAbility(in observation, ref moveX, ref moveY);
                return held | GameplayButtons.MovementAbility;
            }
            // A jump the ability-first branch deferred is still worth taking once
            // the ability has fired or turned out to be unavailable.
            if (jumpsLeft && falling
                && (desperate || NextPercent() < _tuning.RecoveryJumpPercent)) {
                return held | GameplayButtons.Jump;
            }
            // The validated optional mobility Special. CPU_RECOVERY.md forbids
            // casting a Special merely because it occupies a slot, so only a
            // CpuRecoveryProfile approval reaches this branch — since Package 12
            // W4 that is Pocahontas's Spirit Strike alone (GAP-10b).
            if (_profile.MobilitySpecial != CpuMobilitySpecialSlot.None
                && _tuning.RecoveryMobilitySpecialPercent > 0
                && MobilitySpecialActivationsAllowed()
                && MobilitySpecialReady(in observation)
                && NextPercent() < _tuning.RecoveryMobilitySpecialPercent) {
                _episodeMobilitySpecialActivations++;
                return held | (_profile.MobilitySpecial == CpuMobilitySpecialSlot.SpecialOne
                    ? GameplayButtons.Special1
                    : GameplayButtons.Special2);
            }
            return held;
        }

        /// <summary>
        /// Easy and Medium plan <b>one</b> movement-ability activation per offstage
        /// episode; Hard is limited only by the ability's own cooldown.
        /// </summary>
        private bool MovementActivationsAllowed() =>
            _tuning.RecoveryMovementActivationsPerEpisode <= 0
            || _episodeMovementActivations + (_movementActivationDeliveryTick >= 0 ? 1 : 0)
                < _tuning.RecoveryMovementActivationsPerEpisode;

        private bool MobilitySpecialActivationsAllowed() =>
            _tuning.RecoveryMovementActivationsPerEpisode <= 0
            || _episodeMobilitySpecialActivations + _episodeMovementActivations
                < _tuning.RecoveryMovementActivationsPerEpisode;

        private bool MobilitySpecialReady(in CpuDecisionObservation observation) =>
            _profile.MobilitySpecial == CpuMobilitySpecialSlot.SpecialOne
                ? observation.SpecialOneCooldownFrames <= 0
                : observation.SpecialTwoCooldownFrames <= 0;

        /// <summary>
        /// Which way the return lies. Over a pit with A9's floor topology in hand
        /// that is the nearer pit-facing floor edge; otherwise it is the old
        /// toward-centre steer, which is still correct for a fighter pushed past a
        /// side wall on a Sealed stage.
        /// </summary>
        private static sbyte ResolveReturnDirection(in CpuDecisionObservation observation) {
            if (observation.SelfPositionXRaw < observation.LeftWallRaw) return 127;
            if (observation.SelfPositionXRaw > observation.RightWallRaw) return -127;
            return ResolveDiHold(in observation);
        }

        /// <summary>
        /// Estimates whether the jumps still in hand cover the remaining
        /// horizontal distance to the return target, using the profile's
        /// normalized per-jump reach. Deliberately optimistic on Hard's side of
        /// the comparison: the question is only which action opens the route.
        /// </summary>
        private bool JumpsAloneCanReturn(in CpuDecisionObservation observation) {
            if (observation.RemainingJumps <= 0) return false;
            if (!_profile.HasKit) return true;
            long targetRaw = ResolveReturnTargetX(in observation);
            long distanceRaw = Absolute(targetRaw - observation.SelfPositionXRaw);
            long reachRaw = _profile.JumpHorizontalReach.RawValue * observation.RemainingJumps;
            return reachRaw >= distanceRaw;
        }

        /// <summary>The X the fighter is trying to get back to.</summary>
        private static long ResolveReturnTargetX(in CpuDecisionObservation observation) {
            if (observation.HasFloorSegments != 0) {
                bool hasLeft = observation.HasFloorEdgeLeft != 0;
                bool hasRight = observation.HasFloorEdgeRight != 0;
                if (hasLeft && hasRight) {
                    long leftGap = Absolute(observation.SelfPositionXRaw - observation.NearestFloorEdgeLeftXRaw);
                    long rightGap = Absolute(observation.NearestFloorEdgeRightXRaw - observation.SelfPositionXRaw);
                    return leftGap < rightGap
                        ? observation.NearestFloorEdgeLeftXRaw
                        : observation.NearestFloorEdgeRightXRaw;
                }
                if (hasLeft) return observation.NearestFloorEdgeLeftXRaw;
                if (hasRight) return observation.NearestFloorEdgeRightXRaw;
            }
            return (observation.LeftWallRaw + observation.RightWallRaw) / 2;
        }

        /// <summary>
        /// Aims the movement ability the way the <i>shipped simulation</i> reads
        /// it, which is the difference between a recovery and a suicide:
        /// Blink/Teleport/Warp translate along the held stick, while Glide, Dash
        /// and Float launch along current <b>facing</b>. For the facing-driven
        /// kinds the stick is already held toward the stage (it is the return
        /// steer), which is also what turns the fighter around before the boost.
        /// Lincoln's purely horizontal Rail Charge is never aimed upward, because
        /// it cannot go there — his height comes from legal jumps.
        /// </summary>
        private void AimMovementAbility(
            in CpuDecisionObservation observation, ref sbyte moveX, ref sbyte moveY) {
            if (!_profile.HasKit || _profile.MovementIsDirectional) {
                // World Y is up and the stick is Y-down, so a negative axis aims a
                // directional warp/blink upward. The simulation applies the
                // authored distance on each held axis independently, so a diagonal
                // hold is strictly better than either axis alone — the return
                // steer in <paramref name="moveX"/> stays, and the up-hold is
                // added while the fighter is still below or falling toward the
                // floor plane.
                bool needsHeight = !_profile.HasKit
                    || observation.SelfPositionYRaw < 0
                    || observation.SelfVelocityYRaw < 0;
                if (needsHeight) moveY = -127;
                return;
            }
            // Glide, Dash and Float launch along current facing, which the return
            // steer has already been turning toward the stage. Never hold up here:
            // an Up hold selects the up-attack variant on the shared verb resolver
            // and buys no height on a facing-driven ability.
            moveY = 0;
        }

        // === F19 verb policy: grabs and Echo Step (CPU_COMBAT_POLICY.md) ===

        /// <summary>
        /// Whether the CPU could still legally start <paramref name="verb"/> right
        /// now, from <b>self</b> state only. Called at delivery, after the tier's
        /// reaction delay, because a plan formed 4–20 frames ago may have been
        /// overtaken by the CPU's own state. Opponent conditions are deliberately
        /// not rechecked: the contract says contact resolves against the actual
        /// current world and a planned grab is allowed to miss.
        /// </summary>
        private static bool VerbStillLegalForSelf(
            CpuVerbAction verb, in CpuDecisionObservation observation) => verb switch {
                CpuVerbAction.Grab => CanStartGrab(in observation),
                CpuVerbAction.EchoStep => CanStartEchoStep(in observation),
                _ => true
            };

        /// <summary>
        /// Self-side grab legality, mirroring <c>FighterGrabRules.CanStartGrab</c>
        /// rather than inventing a second rule: grounded, alive, free of
        /// hitstun/daze/shieldstun/roll/swing/hang, and not already in a grab.
        /// </summary>
        private static bool CanStartGrab(in CpuDecisionObservation observation) =>
            observation.HasVerbState != 0
            && observation.SuppressGameplayInput == 0
            && observation.Stocks > 0
            && observation.IsGrounded != 0
            && observation.HitstunFrames <= 0
            && observation.DazeFrames <= 0
            && observation.SelfShieldStunFrames <= 0
            && observation.SelfGrabPhase == 0
            && observation.SelfBeingHeld == 0
            && observation.SelfRolling == 0
            && observation.IsLedgeHanging == 0
            && observation.SelfAttackPhase == FighterBasicAttackRules.PhaseNone;

        /// <summary>
        /// Self-side Echo Step legality, mirroring the simulation's own
        /// <c>TryStartEchoStep</c> gate: the recovery frames of the CPU's own
        /// swing, 30 meter, the 120-frame cooldown ready, no armed wind-up, and
        /// none of the canonical forbidden states (hitstun, daze, ledge hang, any
        /// grab state). The destination must also be resolvable — the temporal
        /// contract forbids a nearby substitute, so "no destination" means "do not
        /// select the action" rather than "teleport somewhere else".
        /// </summary>
        private static bool CanStartEchoStep(in CpuDecisionObservation observation) =>
            observation.HasVerbState != 0
            && observation.SuppressGameplayInput == 0
            && observation.Stocks > 0
            && observation.HitstunFrames <= 0
            && observation.DazeFrames <= 0
            && observation.SelfShieldStunFrames <= 0
            && observation.SelfGrabPhase == 0
            && observation.SelfBeingHeld == 0
            && observation.IsLedgeHanging == 0
            && observation.SelfAttackPhase == FighterBasicAttackRules.PhaseRecovery
            && observation.SelfEchoStepCooldownFrames <= 0
            && observation.SelfEchoStepWindupFrames <= 0
            && observation.HasEchoStepDestination != 0
            && observation.InfluenceRaw >= EchoStepMeterCost.RawValue;

        /// <summary>
        /// The Echo Step decision. Medium undoes a vulnerable whiff at its
        /// provisional 25% admission per attack execution; Hard scores the
        /// 30-meter spend against a viable Ultimate setup and an unused, eligible
        /// Defy, and may still spend it to dodge a credible punish.
        /// </summary>
        private bool TryDecideEchoStep(
            in CpuDecisionObservation observation, out GameplayButtons held, out sbyte moveX) {
            held = GameplayButtons.None;
            moveX = 0;
            if (_tuning.EchoStepAdmissionPercent <= 0) return false;
            if (!CanStartEchoStep(in observation)) return false;
            // The design's candidate is a WHIFF: an attack execution with no hit
            // and no block contact. A swing that connected is not being undone.
            if (observation.SelfAttackMadeContact != 0) return false;
            if (!IsSafeEchoStepDestination(in observation)) return false;

            bool ultimateLegal = CanCommitUltimate(in observation);
            // Medium keeps its immediate-Ultimate policy: when both are legal in
            // one decision, the Ultimate takes priority. Hard values a viable
            // Ultimate setup for the same reason without the hard rule.
            if (ultimateLegal && (_tuning.UltimateBeatsEchoStep || _tuning.ReservesMeterForDefy)) {
                return false;
            }
            // Hard's meter tradeoff: full meter behind an unused, eligible Defy is
            // worth keeping — but this is a utility judgement, not a meter floor,
            // so a credible punish threat still buys the escape.
            if (_tuning.ReservesMeterForDefy
                && observation.SelfDefyAvailable != 0
                && observation.InfluenceRaw >= MaxInfluence.RawValue
                && !IsCrediblePunishThreat(in observation)) {
                return false;
            }

            if (_echoOpportunityRolledExecutionID != _attackExecutionID) {
                _echoOpportunityRolledExecutionID = _attackExecutionID;
                _echoOpportunityAdmitted = NextPercent() < _tuning.EchoStepAdmissionPercent;
            }
            if (!_echoOpportunityAdmitted) return false;

            _pendingVerb = CpuVerbAction.EchoStep;
            // The shared chord, through the ordinary action resolver: Block held
            // plus a Roll edge during the swing's recovery frames. The CPU never
            // reaches into the ability directly.
            held = GameplayButtons.Block | GameplayButtons.Roll;
            return true;
        }

        /// <summary>
        /// "Check the resolved historical destination against current geometry and
        /// perceived danger before selection, including pit risk; a teleport
        /// toward a worse or unsupported location is not a defensive improvement."
        /// Inside the walls, above the blast zone, not inside an active hazard
        /// band, and — on an Open stage — not hanging over a pit.
        /// </summary>
        private static bool IsSafeEchoStepDestination(in CpuDecisionObservation observation) {
            if (observation.HasEchoStepDestination == 0) return false;
            if (observation.HasStageBounds == 0) return true;
            long destinationX = observation.EchoStepDestinationXRaw;
            long destinationY = observation.EchoStepDestinationYRaw;
            if (destinationX < observation.LeftWallRaw || destinationX > observation.RightWallRaw) return false;
            if (destinationY <= observation.BottomBlastZoneRaw) return false;
            if (observation.HasHazard != 0 && observation.HazardPhase == 1) {
                long hazardGap = Absolute(observation.HazardPositionXRaw - destinationX)
                    - observation.HazardHalfWidthRaw;
                if (hazardGap < 0) return false;
            }
            if (observation.HasFloorSegments == 0) return true;
            // Pit risk. The observation reports support under the fighter's own X,
            // so the only exactly-known pit span is the one bounded by the two
            // nearest floor edges; a destination inside it is over the hole.
            if (observation.HasFloorSupportUnderSelf == 0
                && observation.HasFloorEdgeLeft != 0
                && observation.HasFloorEdgeRight != 0
                && destinationX > observation.NearestFloorEdgeLeftXRaw
                && destinationX < observation.NearestFloorEdgeRightXRaw) {
                return false;
            }
            return true;
        }

        /// <summary>The opponent is close and swinging — the punish Echo Step exists to dodge.</summary>
        private static bool IsCrediblePunishThreat(in CpuDecisionObservation observation) {
            long gap = Absolute(observation.TargetPositionXRaw - observation.SelfPositionXRaw);
            if (gap > CloseRange.RawValue) return false;
            return (observation.TargetPressedButtons
                & ((int)GameplayButtons.BasicAttack
                    | (int)GameplayButtons.Special1
                    | (int)GameplayButtons.Special2)) != 0;
        }

        /// <summary>
        /// Whether the delayed observation shows a grab opportunity: a blocking,
        /// grounded opponent inside the canonical grab reach and in front, none of
        /// the states a grab may not intentionally target. Reach and the forbidden
        /// states come from the shared rules, never from AI constants.
        /// </summary>
        private static bool IsGrabOpportunity(in CpuDecisionObservation observation) {
            if (!CanStartGrab(in observation)) return false;
            if (observation.HasTargetVerbState == 0) return false;
            if (observation.TargetBlockStance == 0) return false;
            if (observation.TargetIsGrounded == 0) return false;
            // Never intentionally target a victim the shared rules would whiff on.
            if (observation.TargetHitstunFrames > 0
                || observation.TargetDazeFrames > 0
                || observation.TargetShieldStunFrames > 0
                || observation.TargetInvulnerabilityFrames > 0
                || observation.TargetRolling != 0
                || observation.TargetThrowImmune != 0) {
                return false;
            }
            long dx = observation.TargetPositionXRaw - observation.SelfPositionXRaw;
            long front = observation.SelfFacingRight != 0 ? dx : -dx;
            return front >= 0 && front <= GrabReach.RawValue;
        }

        /// <summary>
        /// The grab decision. Medium takes its provisional 25% admission per
        /// eligible blocking opportunity; Hard scores it — it will not grab every
        /// block, and it wants a throw direction that actually improves position.
        /// Either way the roll happens <b>once per opportunity</b>: a failed roll
        /// never reopens the same continuing opportunity.
        /// </summary>
        private bool TryDecideGrab(in CpuDecisionObservation observation, out GameplayButtons held) {
            held = GameplayButtons.None;
            if (!IsGrabOpportunity(in observation)) {
                _grabOpportunityOpen = false;
                return false;
            }
            // The opportunity is tracked before the band gate, so "one
            // opportunity" means the same run of decisions on every band — a
            // disabled band simply never rolls against it.
            if (!_grabOpportunityOpen) {
                _grabOpportunityOpen = true;
                _grabOpportunityID++;
            }
            if (_tuning.GrabAdmissionPercent <= 0) return false;
            if (_tuning.ScoresGrabTactically && !HasUsefulThrow(in observation)) return false;
            if (_grabOpportunityRolledID != _grabOpportunityID) {
                _grabOpportunityRolledID = _grabOpportunityID;
                _grabOpportunityAdmitted = NextPercent() < _tuning.GrabAdmissionPercent;
            }
            if (!_grabOpportunityAdmitted) return false;

            _pendingVerb = CpuVerbAction.Grab;
            // The canonical chord through the shared resolver: BasicAttack pressed
            // while Block is held. The grabber drops block as part of the attempt,
            // which is the shared rule, not an AI special case.
            held = GameplayButtons.Block | GameplayButtons.BasicAttack;
            return true;
        }

        /// <summary>
        /// Hard's positional test: a throw is worth taking when it sends the
        /// opponent toward a stage edge or pit they are not already past. A
        /// blocking opponent already backed into the edge offers nothing, so Hard
        /// keeps attacking instead of grabbing on reflex.
        /// </summary>
        private static bool HasUsefulThrow(in CpuDecisionObservation observation) {
            long edgeX = ResolveThrowEdgeX(in observation);
            long selfToEdge = Absolute(edgeX - observation.SelfPositionXRaw);
            long targetToEdge = Absolute(edgeX - observation.TargetPositionXRaw);
            return targetToEdge < selfToEdge;
        }

        /// <summary>
        /// The edge a throw should aim at: the nearest pit-facing floor edge on an
        /// Open stage, otherwise the nearer side wall. Hard additionally refuses an
        /// edge that would put the opponent inside an active hazard band and takes
        /// the other side instead.
        /// </summary>
        private static long ResolveThrowEdgeX(in CpuDecisionObservation observation) {
            long left = observation.HasFloorSegments != 0 && observation.HasFloorEdgeLeft != 0
                ? observation.NearestFloorEdgeLeftXRaw
                : observation.LeftWallRaw;
            long right = observation.HasFloorSegments != 0 && observation.HasFloorEdgeRight != 0
                ? observation.NearestFloorEdgeRightXRaw
                : observation.RightWallRaw;
            long toLeft = Absolute(observation.SelfPositionXRaw - left);
            long toRight = Absolute(right - observation.SelfPositionXRaw);
            // Deterministic tie-break: the right edge.
            return toLeft < toRight ? left : right;
        }

        /// <summary>
        /// While the grab holds, the stick <i>is</i> the throw choice — the shared
        /// <c>FighterGrabRules</c> resolves direction from held input when the
        /// decision window closes. Holding toward the chosen edge yields the
        /// forward throw when that edge lies ahead and the back throw when it lies
        /// behind, with no invented throw of any kind.
        /// </summary>
        private static sbyte ResolveThrowHold(in CpuDecisionObservation observation) {
            if (observation.HasStageBounds == 0) return observation.SelfFacingRight != 0 ? (sbyte)127 : (sbyte)-127;
            long edgeX = ResolveThrowEdgeX(in observation);
            return edgeX >= observation.SelfPositionXRaw ? (sbyte)127 : (sbyte)-127;
        }

        // === Projectile defense (M-8) ===

        /// <summary>
        /// Reaction to an incoming hostile projectile, per the design difficulty
        /// matrices: Easy "rarely blocks projectiles" (:3161), Medium "will attempt
        /// to block projectiles if they are far enough away" (:3175), Hard blinks
        /// through them with its movement ability when available and shields
        /// otherwise (:3186).
        /// </summary>
        private bool TryDecideProjectileDefense(
            in CpuDecisionObservation observation, out GameplayButtons held, out sbyte moveX) {
            held = GameplayButtons.None;
            moveX = 0;
            if (observation.HasHostileProjectile == 0) return false;
            if (_tuning.ProjectileBlockPercent <= 0 && _tuning.ProjectileBlinkPercent <= 0) return false;

            long relativeXRaw = observation.ProjectileRelativeXRaw;
            long absoluteXRaw = Absolute(relativeXRaw);
            if (absoluteXRaw > ProjectileAwarenessRange.RawValue) return false;
            if (Absolute(observation.ProjectileRelativeYRaw) > ProjectileVerticalBand.RawValue) return false;
            // Only shots actually closing the horizontal gap are a threat; one that
            // already passed, or flies parallel, is ignored.
            bool closing = relativeXRaw >= 0
                ? observation.ProjectileVelocityXRaw < 0
                : observation.ProjectileVelocityXRaw > 0;
            if (!closing) return false;

            // Hard's signature answer: blink through the shot with the movement
            // ability while it is off cooldown. The stick aims at the projectile so
            // a directional warp travels through it, not away from it.
            if (observation.MovementCooldownFrames <= 0
                && NextPercent() < _tuning.ProjectileBlinkPercent) {
                held = GameplayButtons.MovementAbility;
                moveX = relativeXRaw >= 0 ? (sbyte)127 : (sbyte)-127;
                return true;
            }

            // The shield answer. Blocking is a grounded stance, and Medium only
            // commits when the shot is still far enough away for its 15–20-frame
            // reflex window to catch it.
            if (observation.IsGrounded == 0) return false;
            if (absoluteXRaw < _tuning.ProjectileBlockMinRangeRaw) return false;
            if (NextPercent() >= _tuning.ProjectileBlockPercent) return false;
            held = GameplayButtons.Block;
            return true;
        }

        // === Hazard avoidance ===

        private bool TryDecideHazardEvasion(
            in CpuDecisionObservation observation, out GameplayButtons held, out sbyte moveX) {
            held = GameplayButtons.None;
            moveX = 0;
            if (observation.HasHazard == 0 || _tuning.HazardAvoidPercent <= 0) return false;
            // Easy never reacts; Normal reacts only once the zone is damaging; Hard
            // vacates during the telegraph (design-godot.md §10 difficulty matrices).
            if (observation.HazardPhase == 0 && !_tuning.AvoidsHazardWarning) return false;

            // M-9: when the danger region spans (or nearly spans) the walkable
            // width — the Globe heckle authors HalfExtents (10,10) against ±9
            // walls — no escape band exists and blanket flight just wall-ping-pongs
            // for the whole active window. The idle-punisher archetype is answered
            // by staying active, which normal combat/pursuit behaviour already
            // provides, so flee-evasion is skipped entirely.
            if (observation.HasStageBounds != 0) {
                long escapeRoomRaw = (observation.RightWallRaw - observation.LeftWallRaw)
                    - 2 * observation.HazardHalfWidthRaw;
                if (escapeRoomRaw < MinimumHazardEscapeRoom.RawValue) return false;
            }

            long gapRaw = Absolute(observation.HazardPositionXRaw - observation.SelfPositionXRaw)
                - observation.HazardHalfWidthRaw - _tuning.HazardClearanceRaw;
            if (gapRaw >= 0) return false;
            if (NextPercent() >= _tuning.HazardAvoidPercent) return false;

            bool hazardIsRight = observation.HazardPositionXRaw >= observation.SelfPositionXRaw;
            moveX = hazardIsRight ? (sbyte)-127 : (sbyte)127;
            // Fleeing into a wall traps the fighter inside the zone; cut through instead.
            if (observation.HasStageBounds != 0) {
                long escapeRaw = observation.SelfPositionXRaw
                    + (hazardIsRight ? -observation.HazardHalfWidthRaw : observation.HazardHalfWidthRaw);
                if (escapeRaw < observation.LeftWallRaw || escapeRaw > observation.RightWallRaw) {
                    moveX = (sbyte)-moveX;
                }
            }
            if (observation.MovementCooldownFrames <= 0
                && NextPercent() < _tuning.HazardEscapeMovementPercent) {
                held |= GameplayButtons.MovementAbility;
            }
            return true;
        }

        // === Chronal Orb pursuit ===

        private bool TryDecideOrbPursuit(
            in CpuDecisionObservation observation, out GameplayButtons held, out sbyte moveX) {
            held = GameplayButtons.None;
            moveX = 0;
            if (observation.HasOrb == 0 || _tuning.OrbPursuitPercent <= 0) return false;

            long orbDeltaRaw = observation.OrbPositionXRaw - observation.SelfPositionXRaw;
            if (Absolute(orbDeltaRaw) > OrbAwarenessRange.RawValue) return false;

            int chance = _tuning.OrbPursuitPercent;
            bool wounded = observation.SelfMaxHP > 0
                && observation.SelfCurrentHP * 100 <= _tuning.HealingOrbHPPercent * observation.SelfMaxHP;
            if (observation.OrbEffectType == 0 && wounded) chance = _tuning.HealingOrbPursuitPercent;
            // design :3191, Hard only: "strongly prioritizes ... shield orbs when
            // the opponent's Ultimate meter is near full". Aegis is EffectType 3.
            if (observation.OrbEffectType == 3
                && _tuning.ShieldOrbPursuitPercent > 0
                && observation.TargetInfluenceRaw
                    >= FP64.FromInt(_tuning.ShieldOrbTargetMeterFloor).RawValue) {
                chance = _tuning.ShieldOrbPursuitPercent;
            }
            if (NextPercent() >= chance) return false;

            moveX = orbDeltaRaw >= 0 ? (sbyte)110 : (sbyte)-110;
            if (observation.IsGrounded != 0
                && observation.OrbPositionYRaw - observation.SelfPositionYRaw > OrbClimbHeight.RawValue) {
                held |= GameplayButtons.Jump;
            }
            return true;
        }

        // === Neutral combat ===

        private GameplayButtons DecideCombat(in CpuDecisionObservation observation, out sbyte moveX) {
            moveX = 0;
            long deltaRaw = observation.TargetPositionXRaw - observation.SelfPositionXRaw;
            long absoluteRaw = Absolute(deltaRaw);
            long closeRaw = CloseRange.RawValue;
            long farRaw = FarRange.RawValue;

            if (absoluteRaw > closeRaw) moveX = deltaRaw >= 0 ? (sbyte)110 : (sbyte)-110;
            GameplayButtons held = GameplayButtons.None;
            if (absoluteRaw > farRaw && observation.IsGrounded != 0
                && NextPercent() < _tuning.ApproachJumpPercent) {
                held |= GameplayButtons.Jump;
            }

            bool targetThreatening = (observation.TargetPressedButtons
                & ((int)GameplayButtons.BasicAttack | (int)GameplayButtons.Special1 | (int)GameplayButtons.Special2)) != 0;
            if (targetThreatening && absoluteRaw <= closeRaw) {
                int defenseRoll = NextPercent();
                if (defenseRoll < _tuning.EvasiveRollPercent) {
                    moveX = deltaRaw >= 0 ? (sbyte)-127 : (sbyte)127;
                    return held | GameplayButtons.Roll;
                }
                if (defenseRoll < _tuning.BlockPercent) return held | GameplayButtons.Block;
            }

            if (absoluteRaw <= closeRaw) {
                if (CanCommitUltimate(in observation) && NextPercent() < _tuning.UltimatePercent) {
                    held |= GameplayButtons.Ultimate;
                } else if (TryDecideGrab(in observation, out GameplayButtons grab)) {
                    // The grab answers the block stance. It is evaluated after the
                    // Ultimate (which bypasses shields outright) and before the
                    // specials ladder, because a shielding opponent is precisely
                    // the case the specials ladder handles worst.
                    return held | grab;
                } else if (observation.SpecialOneCooldownFrames <= 0
                    && NextPercent() < _tuning.SpecialOneClosePercent) {
                    held |= GameplayButtons.Special1;
                } else if (observation.SpecialTwoCooldownFrames <= 0
                    && NextPercent() < _tuning.SpecialTwoPercent) {
                    held |= GameplayButtons.Special2;
                } else {
                    held |= GameplayButtons.BasicAttack;
                }
                return held;
            }

            // Ranged neutral: zoning special first, then the far-range movement
            // ability as the gap-closer.
            if (observation.SpecialOneCooldownFrames <= 0
                && NextPercent() < _tuning.SpecialOneRangedPercent) {
                held |= GameplayButtons.Special1;
            } else if (absoluteRaw > farRaw && observation.MovementCooldownFrames <= 0
                && NextPercent() < _tuning.MovementAbilityPercent) {
                held |= GameplayButtons.MovementAbility;
            }
            return held;
        }

        /// <summary>
        /// Meter gate plus, on Hard, the design's "confirmed kill setup" requirement:
        /// the opponent is in hitstun, low enough to finish, or cornered against a wall.
        /// </summary>
        private bool CanCommitUltimate(in CpuDecisionObservation observation) {
            if (_tuning.UltimatePercent <= 0) return false;
            if (observation.InfluenceRaw < MaxInfluence.RawValue) return false;
            if (!UltimateActivationCanConnect(in observation)) return false;
            if (!_tuning.RequiresUltimateSetup) return true;
            if (observation.TargetHitstunFrames > 0) return true;
            if (observation.TargetMaxHP > 0
                && observation.TargetCurrentHP * 100 <= _tuning.UltimateFinishHPPercent * observation.TargetMaxHP) {
                return true;
            }
            if (observation.HasStageBounds == 0) return false;
            long margin = CorneredMargin.RawValue;
            return observation.TargetPositionXRaw - observation.LeftWallRaw <= margin
                || observation.RightWallRaw - observation.TargetPositionXRaw <= margin;
        }

        /// <summary>
        /// A02 (Package 13 W6) CPU Ultimate rule: the activation strike is
        /// avoidable and a whiff costs the whole meter plus a 45-frame punish, so
        /// every band commits only when the strike can plausibly connect — the
        /// target is inside close range (<see cref="CloseRange"/>, well inside
        /// every character's 5–7 unit activation reach, so the wind-up's travel
        /// cannot carry it out of range), not invulnerable, not rolling, and on
        /// the ground (a grounded target has not already started the jump that
        /// clears a straight shot or a ground wave). Without the target verb
        /// state (the Story Mirror adapter) only the range test applies. "First
        /// legal opportunity" (Easy/Medium, MIRROR_PARADOX's Easy/Normal) now
        /// reads "first opportunity at which the strike can connect".
        /// </summary>
        private static bool UltimateActivationCanConnect(in CpuDecisionObservation observation) {
            long gap = Absolute(observation.TargetPositionXRaw - observation.SelfPositionXRaw);
            if (gap > CloseRange.RawValue) return false;
            if (observation.HasTargetVerbState == 0) return true;
            return observation.TargetInvulnerabilityFrames <= 0
                && observation.TargetRolling == 0
                && observation.TargetIsGrounded != 0;
        }

        private static long Absolute(long value) => value < 0 ? -value : value;

        private int NextReactionDelay() {
            int minimum = GetReactionDelayBounds(out int maximum);
            return minimum + NextPercent() % (maximum - minimum + 1);
        }

        private int NextPercent() {
            uint value = _randomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _randomState = value;
            return (int)(value % 100u);
        }

        private struct ScheduledDecision {
            public int Tick;
            public sbyte MoveX;
            public sbyte MoveY;
            public GameplayButtons Held;
            public CpuVerbAction Verb;
            public bool SpendsMovementActivation;
        }
    }

    /// <summary>
    /// Per-difficulty behaviour rates for <see cref="FighterCpuController"/>. These
    /// are deterministic code-owned constants, never resources: Fighter Mode stays
    /// competitively normalized and no Story tuning may reach them.
    /// </summary>
    /// <remarks>
    /// Values follow <c>design-godot.md</c> §10 "Difficulty Settings &amp; Behavior
    /// Matrices". Easy walks and jabs; Normal zones, hops, blocks 40%, and
    /// fires the Ultimate the moment the meter fills; Hard blocks/rolls 80%, uses the
    /// full kit, and holds the Ultimate for a confirmed finish.
    /// </remarks>
    public readonly struct CpuBandTuning {
        private static readonly FP64 HardHazardClearance = FP64.FromDouble(1.5);
        private static readonly FP64 NormalProjectileBlockMinRange = FP64.FromDouble(2.5);

        /// <summary>Chance to hop while approaching from beyond far range.</summary>
        public int ApproachJumpPercent { get; init; }
        public int MovementAbilityPercent { get; init; }
        public int UltimatePercent { get; init; }
        /// <summary>Hard only: the Ultimate waits for a confirmed kill setup.</summary>
        public bool RequiresUltimateSetup { get; init; }
        public int UltimateFinishHPPercent { get; init; }
        public int SpecialOneClosePercent { get; init; }
        public int SpecialOneRangedPercent { get; init; }
        public int SpecialTwoPercent { get; init; }
        public int EvasiveRollPercent { get; init; }
        /// <summary>Cumulative with the roll share: design's 10/40/80 shield rates.</summary>
        public int BlockPercent { get; init; }
        /// <summary>
        /// V7.4 Hitstun Defense: chance, rolled once per hitstun instance, to
        /// hold Block through the hitstun and tumble — buying the hit-2 escape
        /// and the landing tech through the ordinary sim rules. Design §10:
        /// Easy 10, Medium 45, Hard 85.
        /// </summary>
        public int HitstunDefensePercent { get; init; }
        /// <summary>
        /// V7.4: chance per hitstun instance to DI a launching hit toward
        /// stage centre. Design §10: Easy never (0), Medium 40, Hard 80.
        /// </summary>
        public int DiPercent { get; init; }
        public int OrbPursuitPercent { get; init; }
        public int HealingOrbPursuitPercent { get; init; }
        public int HealingOrbHPPercent { get; init; }
        /// <summary>Hard only: priority chance toward Aegis orbs at high opponent meter.</summary>
        public int ShieldOrbPursuitPercent { get; init; }
        /// <summary>Opponent meter (0–100 scale) at or above which the Aegis priority engages.</summary>
        public int ShieldOrbTargetMeterFloor { get; init; }
        /// <summary>Chance to shield an incoming projectile: design's rarely / attempt / 80%.</summary>
        public int ProjectileBlockPercent { get; init; }
        /// <summary>Minimum horizontal gap before the band commits to the block, raw <c>FP64</c>.</summary>
        public long ProjectileBlockMinRangeRaw { get; init; }
        /// <summary>Hard only: chance to blink through the shot with the movement ability.</summary>
        public int ProjectileBlinkPercent { get; init; }
        public int HazardAvoidPercent { get; init; }
        /// <summary>Hard vacates during the 90-frame telegraph; the others wait for damage.</summary>
        public bool AvoidsHazardWarning { get; init; }
        public int HazardEscapeMovementPercent { get; init; }
        /// <summary>Extra clearance beyond the hazard half-width, raw <c>FP64</c>.</summary>
        public long HazardClearanceRaw { get; init; }
        public int RecoveryJumpPercent { get; init; }
        /// <summary>
        /// F19: admission for the character's own movement ability — the default
        /// recovery tool on Medium and Hard, and Easy's single permitted
        /// activation. It was <b>0 on Easy and Medium</b> before Package 11 A9b,
        /// which is the inversion F19 corrects.
        /// </summary>
        public int RecoveryMovementPercent { get; init; }
        /// <summary>
        /// Movement-ability activations the planner may spend per offstage
        /// episode. <b>1</b> on Easy and Medium per <c>CPU_RECOVERY.md</c>; 0 means
        /// "no planning limit", which is Hard, where the ability's own cooldown is
        /// the only constraint.
        /// </summary>
        public int RecoveryMovementActivationsPerEpisode { get; init; }
        /// <summary>
        /// Admission for a <b>validated</b> mobility Special, gated behind
        /// <see cref="CpuRecoveryProfile.MobilitySpecial"/>. This replaces the
        /// retired <c>RecoverySpecialTwoPercent</c>, whose whole premise — that
        /// slot 2 is a universal recovery move — the design rejects: "Special 2 is
        /// not a universal recovery move", and Specials are forbidden outright on
        /// Easy. Only Pocahontas approves one (Spirit Strike, Package 12 W4), so
        /// this rate is live for her alone on Medium and Hard.
        /// </summary>
        public int RecoveryMobilitySpecialPercent { get; init; }
        /// <summary>
        /// Hard only: compare feasible routes and plan longer legal sequences
        /// rather than running a fixed jump-first script.
        /// </summary>
        public bool PlansMultiActionRecovery { get; init; }
        /// <summary>
        /// F19 grab admission per eligible blocking opportunity. Easy 0 (disabled),
        /// Medium the design's provisional 25%, Hard a scored rate — it still does
        /// not grab every block, because <see cref="ScoresGrabTactically"/> also
        /// requires a throw that improves position.
        /// </summary>
        public int GrabAdmissionPercent { get; init; }
        /// <summary>Hard only: score the grab rather than rolling a flat admission.</summary>
        public bool ScoresGrabTactically { get; init; }
        /// <summary>
        /// F19 Echo Step admission per eligible attack execution. Easy 0
        /// (disabled), Medium the design's provisional 25%, Hard tactical.
        /// </summary>
        public int EchoStepAdmissionPercent { get; init; }
        /// <summary>
        /// Medium: "If both actions are legal in one decision, that policy takes
        /// priority" — the immediate-Ultimate policy beats Echo Step.
        /// </summary>
        public bool UltimateBeatsEchoStep { get; init; }
        /// <summary>
        /// Hard: full meter behind an unused, eligible Defy has reserve value, so
        /// the 30-meter Echo Step spend is weighed against it. A utility tradeoff,
        /// never a meter floor — a credible punish still buys the escape.
        /// </summary>
        public bool ReservesMeterForDefy { get; init; }

        public static CpuBandTuning For(CpuDifficulty difficulty) => difficulty switch {
            CpuDifficulty.Easy => Easy,
            CpuDifficulty.Hard => Hard,
            _ => Normal
        };

        /// <summary>
        /// Package 11 A7b (F20). The campaign <b>boss</b> band for a Story encounter
        /// that borrows this engine — today only the Level 13 Mirror Paradox.
        /// <c>MIRROR_PARADOX.md</c>: "Reuse the CPU utility engine with explicit boss
        /// overrides; do not load a complete practice-CPU preset and accidentally
        /// disable the boss's signature abilities." The boss keeps its <b>full core
        /// kit on every difficulty</b>, so only Easy needs overriding — Normal and
        /// Hard already carry the contract's rates verbatim.
        /// </summary>
        /// <remarks>
        /// Deliberately not a Fighter band: <see cref="For"/> still answers for
        /// practice/Holodeck CPUs, and a Holodeck setting can never reach a boss
        /// encounter because the encounter selects its own difficulty from the Story
        /// session.
        /// </remarks>
        public static CpuBandTuning BossOverride(CpuDifficulty difficulty) => difficulty switch {
            CpuDifficulty.Easy => EasyBoss,
            CpuDifficulty.Hard => Hard,
            _ => Normal
        };

        /// <summary>
        /// Easy: straightforward walking and basic attacks. No neutral specials, no
        /// neutral movement ability, no Ultimate, no orb pathing, no hazard
        /// reaction, and — F19, Package 11 A9b — <b>no Specials at all, including
        /// off-stage</b>. Its one recovery tool beyond jumps is a single movement-
        /// ability activation per offstage episode. It grabs nothing and never
        /// Echo Steps.
        /// </summary>
        public static CpuBandTuning Easy { get; } = new() {
            ApproachJumpPercent = 0,
            MovementAbilityPercent = 0,
            UltimatePercent = 0,
            RequiresUltimateSetup = false,
            UltimateFinishHPPercent = 0,
            SpecialOneClosePercent = 0,
            SpecialOneRangedPercent = 0,
            SpecialTwoPercent = 0,
            EvasiveRollPercent = 0,
            BlockPercent = 10,
            // design §10 Easy Hitstun Defense: 10% per instance, never DI —
            // "Easy stays nearly defenseless in disadvantage by contract".
            HitstunDefensePercent = 10,
            DiPercent = 0,
            OrbPursuitPercent = 0,
            HealingOrbPursuitPercent = 0,
            HealingOrbHPPercent = 0,
            ShieldOrbPursuitPercent = 0,
            ShieldOrbTargetMeterFloor = 0,
            // design §10 Easy: "Rarely blocks projectiles" — below even its 10%
            // standard-attack shield rate, and never blinks.
            ProjectileBlockPercent = 5,
            ProjectileBlockMinRangeRaw = 0,
            ProjectileBlinkPercent = 0,
            HazardAvoidPercent = 0,
            AvoidsHazardWarning = false,
            HazardEscapeMovementPercent = 0,
            HazardClearanceRaw = 0,
            // F19 (Package 11 A9b): Easy "steers toward the nearest plausible
            // legal ledge or landing, uses remaining jumps and AT MOST ONE
            // activation of the character's movement ability per offstage
            // episode", and "Specials remain disabled on Easy". The shipped band
            // was exactly inverted — Special 2 at 55%, the movement ability never.
            RecoveryJumpPercent = 60,
            RecoveryMovementPercent = 55,
            RecoveryMovementActivationsPerEpisode = 1,
            RecoveryMobilitySpecialPercent = 0,
            PlansMultiActionRecovery = false,
            // CPU_COMBAT_POLICY.md: "Easy | Disabled. | Disabled."
            GrabAdmissionPercent = 0,
            ScoresGrabTactically = false,
            EchoStepAdmissionPercent = 0,
            UltimateBeatsEchoStep = false,
            ReservesMeterForDefy = false
        };

        public static CpuBandTuning Normal { get; } = new() {
            ApproachJumpPercent = 12,
            MovementAbilityPercent = 18,
            UltimatePercent = 100,
            RequiresUltimateSetup = false,
            UltimateFinishHPPercent = 0,
            SpecialOneClosePercent = 12,
            SpecialOneRangedPercent = 22,
            SpecialTwoPercent = 20,
            EvasiveRollPercent = 0,
            BlockPercent = 40,
            // design §10 Medium Hitstun Defense: 45% Block-through, 40% DI —
            // the teaching tier, where loops visibly work and visibly fail.
            HitstunDefensePercent = 45,
            DiPercent = 40,
            OrbPursuitPercent = 25,
            HealingOrbPursuitPercent = 60,
            HealingOrbHPPercent = 40,
            ShieldOrbPursuitPercent = 0,
            ShieldOrbTargetMeterFloor = 0,
            // design §10 Medium: "will attempt to block projectiles if they are far
            // enough away" — the 40% shield rate, gated behind a 2.5-unit gap its
            // 15–20-frame reflex window can still catch.
            ProjectileBlockPercent = 40,
            ProjectileBlockMinRangeRaw = NormalProjectileBlockMinRange.RawValue,
            ProjectileBlinkPercent = 0,
            HazardAvoidPercent = 85,
            AvoidsHazardWarning = false,
            HazardEscapeMovementPercent = 0,
            HazardClearanceRaw = 0,
            // F19: "remaining jumps plus ONE suitable recovery ability
            // activation. The movement ability is the default; a validated
            // mobility Special may substitute where its trajectory fits better."
            // No multi-ability or platform/refund chains.
            RecoveryJumpPercent = 90,
            RecoveryMovementPercent = 85,
            RecoveryMovementActivationsPerEpisode = 1,
            RecoveryMobilitySpecialPercent = 85,
            PlansMultiActionRecovery = false,
            // CPU_COMBAT_POLICY.md Medium: "Initial tuning target: 25% admission
            // chance per eligible blocking opportunity" and the same per eligible
            // attack execution for Echo Step. Provisional tuning values, not
            // measured balance results.
            GrabAdmissionPercent = 25,
            ScoresGrabTactically = false,
            EchoStepAdmissionPercent = 25,
            UltimateBeatsEchoStep = true,
            ReservesMeterForDefy = false
        };

        public static CpuBandTuning Hard { get; } = new() {
            ApproachJumpPercent = 12,
            MovementAbilityPercent = 30,
            UltimatePercent = 85,
            RequiresUltimateSetup = true,
            UltimateFinishHPPercent = 35,
            SpecialOneClosePercent = 28,
            SpecialOneRangedPercent = 34,
            SpecialTwoPercent = 26,
            EvasiveRollPercent = 35,
            BlockPercent = 80,
            // design §10 Hard Hitstun Defense: 85% Block-through, 80% DI —
            // damage must be earned through grabs, delays, and shatter pressure.
            HitstunDefensePercent = 85,
            DiPercent = 80,
            OrbPursuitPercent = 75,
            HealingOrbPursuitPercent = 95,
            HealingOrbHPPercent = 50,
            // design :3191: "shield orbs when the opponent's Ultimate meter is
            // near full" — same strength as the wounded healing-orb priority.
            ShieldOrbPursuitPercent = 95,
            ShieldOrbTargetMeterFloor = 85,
            // design :3186: blink through player projectiles with the movement
            // ability when available; the 80% shield rate is the fallback.
            ProjectileBlockPercent = 80,
            ProjectileBlockMinRangeRaw = 0,
            ProjectileBlinkPercent = 85,
            HazardAvoidPercent = 100,
            AvoidsHazardWarning = true,
            HazardEscapeMovementPercent = 45,
            // 1.5 units of extra clearance so Hard leaves the telegraphed band, not
            // merely its edge.
            HazardClearanceRaw = HardHazardClearance.RawValue,
            // F19: "Compare feasible routes and plan longer legal sequences...
            // do not execute a fixed jump -> movement -> Special 2 script."
            // The episode limit is lifted (0); the ability's own cooldown is the
            // constraint, exactly as it is for a human.
            RecoveryJumpPercent = 100,
            RecoveryMovementPercent = 90,
            RecoveryMovementActivationsPerEpisode = 0,
            RecoveryMobilitySpecialPercent = 100,
            PlansMultiActionRecovery = true,
            // CPU_COMBAT_POLICY.md Hard: score both tactically. "No automatic grab
            // on every block" and "No automatic cancel of every whiff", hence a
            // scored gate plus a rate below 100.
            GrabAdmissionPercent = 70,
            ScoresGrabTactically = true,
            EchoStepAdmissionPercent = 75,
            UltimateBeatsEchoStep = false,
            ReservesMeterForDefy = true
        };

        /// <summary>
        /// Easy's boss band. The practice Easy CPU zeroes both Specials, the movement
        /// ability and the Ultimate; F20 forbids inheriting those restrictions, so the
        /// kit rates are taken <b>from the Normal band</b> rather than invented as a
        /// third ladder — no Easy-boss kit ladder is authored anywhere, and the
        /// contract only asks for "suitable Specials rather than only basics". Easy's
        /// separation comes from the numbers F20 does specify and which are untouched
        /// here: the 30-45-frame reaction window, the 10% block rate, 10%/0% hitstun
        /// defense and DI, 0.7x HP and 0.5x outgoing damage. The Ultimate follows the
        /// contract's "first legal opportunity at full meter" — Normal's policy, not
        /// Hard's confirmed-setup policy. Grabs and Echo Step stay off: the override
        /// "does not enable Easy grabs or Echo Step", and neither verb exists in this
        /// controller yet (A9b's F19 work).
        /// </summary>
        public static CpuBandTuning EasyBoss { get; } = Easy with {
            ApproachJumpPercent = Normal.ApproachJumpPercent,
            MovementAbilityPercent = Normal.MovementAbilityPercent,
            UltimatePercent = Normal.UltimatePercent,
            RequiresUltimateSetup = false,
            UltimateFinishHPPercent = Normal.UltimateFinishHPPercent,
            SpecialOneClosePercent = Normal.SpecialOneClosePercent,
            SpecialOneRangedPercent = Normal.SpecialOneRangedPercent,
            SpecialTwoPercent = Normal.SpecialTwoPercent
        };
    }
}
