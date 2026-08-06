using FTT.Core;

namespace FTT.FighterSim {

    /// <summary>
    /// Deterministic CPU input generator. Decisions are evaluated every three
    /// simulation ticks and delivered after the difficulty-specific reaction delay,
    /// so generated frames can be recorded and replayed like human/network input.
    /// </summary>
    public sealed class FighterCpuController {
        private const int DecisionIntervalTicks = 3;
        private const int ScheduleCapacity = 128;
        private readonly CpuDifficulty _difficulty;
        private readonly ScheduledDecision[] _schedule = new ScheduledDecision[ScheduleCapacity];
        private uint _randomState;
        private GameplayButtons _currentHeld;

        public FighterCpuController(CpuDifficulty difficulty, int seed) {
            _difficulty = difficulty;
            _randomState = unchecked((uint)seed) ^ 0xA511E9B3u;
            if (_randomState == 0) _randomState = 0x6D2B79F5u;
        }

        public PlayerInputFrame Sample(
            uint tick,
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            in PlayerInputFrame previousFrame) {
            if (tick % DecisionIntervalTicks == 0) {
                GameplayButtons decision = Decide(in self, in selfRuntime, in target, in targetRuntime, out sbyte moveX);
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

        public int GetReactionDelayBounds(out int maximum) {
            int minimum = _difficulty switch {
                CpuDifficulty.Easy => 30,
                CpuDifficulty.Normal => 15,
                CpuDifficulty.Hard => 4,
                _ => 15
            };
            maximum = _difficulty switch {
                CpuDifficulty.Easy => 45,
                CpuDifficulty.Normal => 20,
                CpuDifficulty.Hard => 8,
                _ => 20
            };
            return minimum;
        }

        private GameplayButtons Decide(
            in FighterStateComponent self,
            in FighterRuntimeComponent selfRuntime,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            out sbyte moveX) {
            moveX = 0;
            if (self.Stocks <= 0 || self.HitstunFrames > 0 || self.DazeFrames > 0) return GameplayButtons.None;
            long deltaRaw = target.Position.x.RawValue - self.Position.x.RawValue;
            long absoluteRaw = deltaRaw < 0 ? -deltaRaw : deltaRaw;
            long closeRaw = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2).RawValue;
            long farRaw = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(5).RawValue;

            if (absoluteRaw > closeRaw) moveX = deltaRaw >= 0 ? (sbyte)110 : (sbyte)-110;
            GameplayButtons held = GameplayButtons.None;
            if (absoluteRaw > farRaw && self.IsGrounded != 0 && NextPercent() < 12) held |= GameplayButtons.Jump;

            bool targetThreatening = (targetRuntime.PressedButtons
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
                if (_difficulty != CpuDifficulty.Easy && self.Influence.RawValue >= xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100).RawValue && roll < 35) {
                    held |= GameplayButtons.Ultimate;
                } else if (_difficulty == CpuDifficulty.Hard && selfRuntime.SpecialOneCooldownFrames <= 0 && roll < 28) {
                    held |= GameplayButtons.Special1;
                } else if (_difficulty != CpuDifficulty.Easy && selfRuntime.SpecialTwoCooldownFrames <= 0 && roll < 20) {
                    held |= GameplayButtons.Special2;
                } else {
                    held |= GameplayButtons.BasicAttack;
                }
            } else if (_difficulty != CpuDifficulty.Easy && absoluteRaw > farRaw && self.IsGrounded != 0 && NextPercent() < 25) {
                held |= GameplayButtons.Dash;
            } else if (_difficulty == CpuDifficulty.Hard && selfRuntime.MovementCooldownFrames <= 0 && absoluteRaw > farRaw && NextPercent() < 18) {
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
