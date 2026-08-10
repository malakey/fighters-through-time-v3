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
            | GameplayButtons.Roll | GameplayButtons.Dash;

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

        private readonly CpuDifficulty _difficulty;
        private readonly CpuBandTuning _tuning;
        private readonly FighterStageGeometry _geometry;
        private readonly ICpuWorldObserver _world;
        private readonly ScheduledDecision[] _schedule = new ScheduledDecision[ScheduleCapacity];
        private uint _randomState;
        private GameplayButtons _sustainedHeld;
        private GameplayButtons _activeEdges;
        private GameplayButtons _pendingEdges;
        private int _edgeFramesRemaining;
        private int _edgeGapFramesRemaining;

        public FighterCpuController(CpuDifficulty difficulty, int seed)
            : this(difficulty, seed, null, null) {
        }

        /// <summary>
        /// Full construction. <paramref name="geometry"/> supplies the stage bounds
        /// and platform summary the observation carries; <paramref name="world"/>
        /// supplies live orbs/hazards and the match-live gate. Both are optional —
        /// omitting them yields the Story-equivalent "absent" sentinels.
        /// </summary>
        public FighterCpuController(
            CpuDifficulty difficulty,
            int seed,
            FighterStageGeometry geometry,
            ICpuWorldObserver world) {
            _difficulty = difficulty;
            _tuning = CpuBandTuning.For(difficulty);
            _geometry = geometry;
            _world = world;
            _randomState = unchecked((uint)seed) ^ 0xA511E9B3u;
            if (_randomState == 0) _randomState = 0x6D2B79F5u;
        }

        public CpuDifficulty Difficulty => _difficulty;

        /// <summary>Per-band rates in force, exposed so tests can pin the matrices.</summary>
        public CpuBandTuning Tuning => _tuning;

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
            if (tick % DecisionIntervalTicks == 0) {
                GameplayButtons decision = Decide(in observation, out sbyte moveX, out sbyte moveY);
                int deliveryTick = checked((int)tick + NextReactionDelay());
                int slot = deliveryTick % ScheduleCapacity;
                _schedule[slot] = new ScheduledDecision {
                    Tick = deliveryTick,
                    MoveX = moveX,
                    MoveY = moveY,
                    Held = decision
                };
            }

            sbyte outputMoveX = previousFrame.MoveX;
            sbyte outputMoveY = previousFrame.MoveY;
            int outputSlot = (int)(tick % ScheduleCapacity);
            if (_schedule[outputSlot].Tick == (int)tick) {
                outputMoveX = _schedule[outputSlot].MoveX;
                outputMoveY = _schedule[outputSlot].MoveY;
                _sustainedHeld = _schedule[outputSlot].Held & ~EdgeButtons;
                GameplayButtons edges = _schedule[outputSlot].Held & EdgeButtons;
                if (edges != GameplayButtons.None) _pendingEdges = edges;
                _schedule[outputSlot].Tick = -1;
            }

            AdvanceEdgePulse();
            return PlayerInputFrame.Create(
                tick, outputMoveX / 127f, outputMoveY / 127f,
                _sustainedHeld | _activeEdges, previousFrame.Held);
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
            }

            return observation;
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
            if (observation.SuppressGameplayInput != 0
                || observation.Stocks <= 0
                || observation.HitstunFrames > 0
                || observation.DazeFrames > 0) {
                return GameplayButtons.None;
            }

            if (IsOffStage(in observation)) return DecideRecovery(in observation, out moveX, out moveY);
            if (TryDecideHazardEvasion(in observation, out GameplayButtons evasion, out moveX)) return evasion;
            if (TryDecideProjectileDefense(in observation, out GameplayButtons defense, out moveX)) return defense;
            if (TryDecideOrbPursuit(in observation, out GameplayButtons pursuit, out moveX)) return pursuit;
            return DecideCombat(in observation, out moveX);
        }

        // === Off-stage recovery ===

        /// <summary>
        /// True when the fighter is past a solid wall or has fallen below the stage
        /// floor plane with nothing beneath it but the blast zone. Requires authored
        /// bounds, which is what keeps a Story adapter (no stage concept, arbitrary
        /// world origin) out of this branch entirely.
        /// </summary>
        private static bool IsOffStage(in CpuDecisionObservation observation) {
            if (observation.HasStageBounds == 0) return false;
            if (observation.SelfPositionXRaw < observation.LeftWallRaw
                || observation.SelfPositionXRaw > observation.RightWallRaw) return true;
            return observation.SelfPositionYRaw < 0 && observation.IsGrounded == 0;
        }

        private GameplayButtons DecideRecovery(
            in CpuDecisionObservation observation, out sbyte moveX, out sbyte moveY) {
            moveY = 0;
            long centerRaw = (observation.LeftWallRaw + observation.RightWallRaw) / 2;
            moveX = observation.SelfPositionXRaw < centerRaw ? (sbyte)127 : (sbyte)-127;

            GameplayButtons held = GameplayButtons.None;
            bool falling = observation.SelfVelocityYRaw <= 0;
            // One recovery tool per decision, in priority order, so the options chain
            // across successive decisions instead of firing on the same frame.
            if (observation.RemainingJumps > 0 && falling
                && NextPercent() < _tuning.RecoveryJumpPercent) {
                held |= GameplayButtons.Jump;
            } else if (observation.MovementCooldownFrames <= 0
                && NextPercent() < _tuning.RecoveryMovementPercent) {
                held |= GameplayButtons.MovementAbility;
                // World Y is up and the stick is Y-down, so a negative axis aims the
                // directional warp/blink upward.
                moveY = -127;
            } else if (observation.SpecialTwoCooldownFrames <= 0
                && NextPercent() < _tuning.RecoverySpecialTwoPercent) {
                held |= GameplayButtons.Special2;
            }
            return held;
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

            // Ranged neutral. MovementAbility is evaluated before Dash: it used to sit
            // behind the Dash roll in an else-if chain and was effectively starved.
            if (observation.SpecialOneCooldownFrames <= 0
                && NextPercent() < _tuning.SpecialOneRangedPercent) {
                held |= GameplayButtons.Special1;
            } else if (absoluteRaw > farRaw && observation.MovementCooldownFrames <= 0
                && NextPercent() < _tuning.MovementAbilityPercent) {
                held |= GameplayButtons.MovementAbility;
            } else if (absoluteRaw > farRaw && observation.IsGrounded != 0
                && NextPercent() < _tuning.DashPercent) {
                held |= GameplayButtons.Dash;
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
        }
    }

    /// <summary>
    /// Per-difficulty behaviour rates for <see cref="FighterCpuController"/>. These
    /// are deterministic code-owned constants, never resources: Fighter Mode stays
    /// competitively normalized and no Story tuning may reach them.
    /// </summary>
    /// <remarks>
    /// Values follow <c>design-godot.md</c> §10 "Difficulty Settings &amp; Behavior
    /// Matrices". Easy walks and jabs; Normal zones, hops, dashes, blocks 40%, and
    /// fires the Ultimate the moment the meter fills; Hard blocks/rolls 80%, uses the
    /// full kit, and holds the Ultimate for a confirmed finish.
    /// </remarks>
    public readonly struct CpuBandTuning {
        private static readonly FP64 HardHazardClearance = FP64.FromDouble(1.5);
        private static readonly FP64 NormalProjectileBlockMinRange = FP64.FromDouble(2.5);

        /// <summary>Chance to hop while approaching from beyond far range.</summary>
        public int ApproachJumpPercent { get; init; }
        public int DashPercent { get; init; }
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
        public int RecoveryMovementPercent { get; init; }
        public int RecoverySpecialTwoPercent { get; init; }

        public static CpuBandTuning For(CpuDifficulty difficulty) => difficulty switch {
            CpuDifficulty.Easy => Easy,
            CpuDifficulty.Hard => Hard,
            _ => Normal
        };

        /// <summary>
        /// Easy: straightforward walking and basic attacks. No neutral specials, no
        /// movement ability, no Ultimate, no orb pathing, no hazard reaction. Special 2
        /// exists only as the off-stage recovery button, exactly as the design states.
        /// </summary>
        public static CpuBandTuning Easy { get; } = new() {
            ApproachJumpPercent = 0,
            DashPercent = 0,
            MovementAbilityPercent = 0,
            UltimatePercent = 0,
            RequiresUltimateSetup = false,
            UltimateFinishHPPercent = 0,
            SpecialOneClosePercent = 0,
            SpecialOneRangedPercent = 0,
            SpecialTwoPercent = 0,
            EvasiveRollPercent = 0,
            BlockPercent = 10,
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
            RecoveryJumpPercent = 60,
            RecoveryMovementPercent = 0,
            RecoverySpecialTwoPercent = 55
        };

        public static CpuBandTuning Normal { get; } = new() {
            ApproachJumpPercent = 12,
            DashPercent = 25,
            MovementAbilityPercent = 18,
            UltimatePercent = 100,
            RequiresUltimateSetup = false,
            UltimateFinishHPPercent = 0,
            SpecialOneClosePercent = 12,
            SpecialOneRangedPercent = 22,
            SpecialTwoPercent = 20,
            EvasiveRollPercent = 0,
            BlockPercent = 40,
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
            RecoveryJumpPercent = 90,
            RecoveryMovementPercent = 0,
            RecoverySpecialTwoPercent = 85
        };

        public static CpuBandTuning Hard { get; } = new() {
            ApproachJumpPercent = 12,
            DashPercent = 30,
            MovementAbilityPercent = 30,
            UltimatePercent = 85,
            RequiresUltimateSetup = true,
            UltimateFinishHPPercent = 35,
            SpecialOneClosePercent = 28,
            SpecialOneRangedPercent = 34,
            SpecialTwoPercent = 26,
            EvasiveRollPercent = 35,
            BlockPercent = 80,
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
            RecoveryJumpPercent = 100,
            RecoveryMovementPercent = 90,
            RecoverySpecialTwoPercent = 100
        };
    }
}
