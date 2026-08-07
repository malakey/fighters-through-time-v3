using FTT.Core;

namespace FTT.FighterSim {

    /// <summary>
    /// Deterministic CPU input generator and the single owner of the utility
    /// decision table. Decisions are evaluated every three simulation ticks and
    /// delivered after the difficulty-specific reaction delay, so generated frames
    /// can be recorded and replayed like human/network input.
    /// </summary>
    /// <remarks>
    /// The decision table reads a mode-neutral <see cref="CpuDecisionObservation"/>,
    /// so the Story-side Level 13 Mirror Paradox drives this exact engine through
    /// the observation overload instead of maintaining a parallel port. Nothing here
    /// may take a Godot or Story dependency.
    /// </remarks>
    public sealed class FighterCpuController {
        private const int DecisionIntervalTicks = 3;
        private const int ScheduleCapacity = 128;

        /// <summary>design Section 6/10: the Mirror Paradox reaction window.</summary>
        public const int HardReactionDelayMinFrames = 4;
        public const int HardReactionDelayMaxFrames = 8;

        private readonly CpuDifficulty _difficulty;
        private readonly ScheduledDecision[] _schedule = new ScheduledDecision[ScheduleCapacity];
        private uint _randomState;
        private GameplayButtons _currentHeld;

        public FighterCpuController(CpuDifficulty difficulty, int seed) {
            _difficulty = difficulty;
            _randomState = unchecked((uint)seed) ^ 0xA511E9B3u;
            if (_randomState == 0) _randomState = 0x6D2B79F5u;
        }

        public CpuDifficulty Difficulty => _difficulty;

        public PlayerInputFrame Sample(
            uint tick,
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            in PlayerInputFrame previousFrame) {
            CpuDecisionObservation observation = Observe(in self, in selfRuntime, in target, in targetRuntime);
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
                GameplayButtons decision = Decide(in observation, out sbyte moveX);
                int deliveryTick = checked((int)tick + NextReactionDelay());
                int slot = deliveryTick % ScheduleCapacity;
                _schedule[slot] = new ScheduledDecision {
                    Tick = deliveryTick,
                    MoveX = moveX,
                    Held = decision
                };
            }

            sbyte outputMoveX = previousFrame.MoveX;
            int outputSlot = (int)(tick % ScheduleCapacity);
            if (_schedule[outputSlot].Tick == (int)tick) {
                outputMoveX = _schedule[outputSlot].MoveX;
                _currentHeld = _schedule[outputSlot].Held;
                _schedule[outputSlot].Tick = -1;
            }
            return PlayerInputFrame.Create(tick, outputMoveX / 127f, 0f, _currentHeld, previousFrame.Held);
        }

        /// <summary>Projects deterministic simulation components onto the shared observation.</summary>
        public static CpuDecisionObservation Observe(
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime) => new() {
                SelfPositionXRaw = self.Position.x.RawValue,
                TargetPositionXRaw = target.Position.x.RawValue,
                Stocks = self.Stocks,
                HitstunFrames = self.HitstunFrames,
                DazeFrames = self.DazeFrames,
                IsGrounded = self.IsGrounded,
                InfluenceRaw = self.Influence.RawValue,
                SpecialOneCooldownFrames = selfRuntime.SpecialOneCooldownFrames,
                SpecialTwoCooldownFrames = selfRuntime.SpecialTwoCooldownFrames,
                MovementCooldownFrames = selfRuntime.MovementCooldownFrames,
                TargetPressedButtons = targetRuntime.PressedButtons
            };

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

        /// <summary>The utility decision table. One implementation, both modes.</summary>
        public GameplayButtons Decide(in CpuDecisionObservation observation, out sbyte moveX) {
            moveX = 0;
            if (observation.Stocks <= 0 || observation.HitstunFrames > 0 || observation.DazeFrames > 0) {
                return GameplayButtons.None;
            }
            long deltaRaw = observation.TargetPositionXRaw - observation.SelfPositionXRaw;
            long absoluteRaw = deltaRaw < 0 ? -deltaRaw : deltaRaw;
            long closeRaw = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2).RawValue;
            long farRaw = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(5).RawValue;

            if (absoluteRaw > closeRaw) moveX = deltaRaw >= 0 ? (sbyte)110 : (sbyte)-110;
            GameplayButtons held = GameplayButtons.None;
            if (absoluteRaw > farRaw && observation.IsGrounded != 0 && NextPercent() < 12) held |= GameplayButtons.Jump;

            bool targetThreatening = (observation.TargetPressedButtons
                & ((int)GameplayButtons.BasicAttack | (int)GameplayButtons.Special1 | (int)GameplayButtons.Special2)) != 0;
            if (_difficulty == CpuDifficulty.Hard && targetThreatening && absoluteRaw <= closeRaw) {
                int defenseRoll = NextPercent();
                if (defenseRoll < 35) {
                    moveX = deltaRaw >= 0 ? (sbyte)-127 : (sbyte)127;
                    return held | GameplayButtons.Roll;
                }
                if (defenseRoll < 80) return held | GameplayButtons.Block;
            }

            if (absoluteRaw <= closeRaw) {
                int roll = NextPercent();
                if (_difficulty != CpuDifficulty.Easy && observation.InfluenceRaw >= xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100).RawValue && roll < 35) {
                    held |= GameplayButtons.Ultimate;
                } else if (_difficulty == CpuDifficulty.Hard && observation.SpecialOneCooldownFrames <= 0 && roll < 28) {
                    held |= GameplayButtons.Special1;
                } else if (_difficulty != CpuDifficulty.Easy && observation.SpecialTwoCooldownFrames <= 0 && roll < 20) {
                    held |= GameplayButtons.Special2;
                } else {
                    held |= GameplayButtons.BasicAttack;
                }
            } else if (_difficulty != CpuDifficulty.Easy && absoluteRaw > farRaw && observation.IsGrounded != 0 && NextPercent() < 25) {
                held |= GameplayButtons.Dash;
            } else if (_difficulty == CpuDifficulty.Hard && observation.MovementCooldownFrames <= 0 && absoluteRaw > farRaw && NextPercent() < 18) {
                held |= GameplayButtons.MovementAbility;
            }
            return held;
        }

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
            public GameplayButtons Held;
        }
    }
}
