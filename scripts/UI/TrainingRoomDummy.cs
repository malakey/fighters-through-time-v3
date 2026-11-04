using FTT.Core;

namespace FTT.UI {

    /// <summary>GR-TRAIN (9c): when the training dummy holds Block while it can act.</summary>
    public enum TrainingBlockMode {
        Never = 0,
        Always = 1,
        /// <summary>From the first hit it takes until <see cref="TrainingDummyRules.BlockAfterHitFrames"/> frames after the last one.</summary>
        AfterHit = 2
    }

    /// <summary>GR-TRAIN (9c): how the training dummy jumps while it can act.</summary>
    public enum TrainingJumpMode {
        Never = 0,
        /// <summary>One jump every <see cref="TrainingDummyRules.AlwaysJumpCycleFrames"/> frames: as soon as it lands.</summary>
        Always = 1,
        /// <summary>One jump every <see cref="TrainingDummyRules.TimedJumpCycleFrames"/> frames.</summary>
        Timed = 2
    }

    /// <summary>GR-TRAIN (9c): what the training dummy does with its stick and shield while it is in hitstun.</summary>
    public enum TrainingHitstunResponse {
        None = 0,
        /// <summary>Hold Block through the stun: the hit-2 escape and the landing tech.</summary>
        HoldBlock = 1,
        /// <summary>Hold the stick toward the stage centre: the launch's directional influence.</summary>
        DirectionalInfluence = 2
    }

    /// <summary>
    /// The three training-room dummy settings. Session-only: they live on the room (and the panel that edits them) for one
    /// visit and are never written to a save, the match settings or the session struct.
    /// </summary>
    public struct TrainingDummySettings {
        public TrainingBlockMode Block;
        public TrainingJumpMode Jump;
        public TrainingHitstunResponse Hitstun;
    }

    /// <summary>What the dummy rules read about the dummy on one tick. Plain data; the room fills it from read-only getters.</summary>
    public struct TrainingDummyContext {
        /// <summary>Live frames since the sandbox started (the jump cycles count these).</summary>
        public int Frame;
        /// <summary>The match is live and the dummy is standing: it has a stock and is off the respawn platform.</summary>
        public bool CanAct;
        /// <summary>Hitstun, daze or a tumble: the dummy has no choice of action but its hitstun response.</summary>
        public bool InHitstun;
        /// <summary>Live frames since the dummy last took a hit (<see cref="TrainingReadoutTracker.NoHit"/> when none).</summary>
        public int FramesSinceHit;
        /// <summary>+1 or −1: the side of the dummy that the stage centre lies on.</summary>
        public float CentreDirection;
    }

    /// <summary>One tick of the dummy's input: a horizontal stick value and the held buttons.</summary>
    public struct TrainingDummyCommand {
        public float MoveX;
        public GameplayButtons Held;
    }

    /// <summary>
    /// GR-TRAIN (9c). The training dummy's whole behaviour as a pure function of its three settings and what it can see.
    /// Engine-free so a pure suite can pin it; the numbers are provisional (<c>PROVISIONAL-TRAINING-ROOM</c>).
    /// </summary>
    public static class TrainingDummyRules {
        /// <summary>The block-after-hit mode keeps the shield up this long after the last hit.</summary>
        public const int BlockAfterHitFrames = 60;
        /// <summary>The "always" jump mode repeats this often.</summary>
        public const int AlwaysJumpCycleFrames = 60;
        /// <summary>The "timed" jump mode repeats this often.</summary>
        public const int TimedJumpCycleFrames = 180;
        /// <summary>Jump is held this many frames of every cycle (a full hop: the short hop cuts only a released jump).</summary>
        public const int JumpHoldFrames = 24;

        public static TrainingDummyCommand Step(in TrainingDummySettings settings, in TrainingDummyContext context) {
            var command = new TrainingDummyCommand();
            if (!context.CanAct) return command;

            if (context.InHitstun) {
                switch (settings.Hitstun) {
                    case TrainingHitstunResponse.HoldBlock:
                        command.Held = GameplayButtons.Block;
                        break;
                    case TrainingHitstunResponse.DirectionalInfluence:
                        command.MoveX = context.CentreDirection >= 0f ? 1f : -1f;
                        break;
                }
                return command;
            }

            bool jumping = false;
            int cycle = settings.Jump switch {
                TrainingJumpMode.Always => AlwaysJumpCycleFrames,
                TrainingJumpMode.Timed => TimedJumpCycleFrames,
                _ => 0
            };
            if (cycle > 0 && context.Frame >= 0 && context.Frame % cycle < JumpHoldFrames) {
                command.Held |= GameplayButtons.Jump;
                jumping = true;
            }

            bool block = settings.Block switch {
                TrainingBlockMode.Always => true,
                TrainingBlockMode.AfterHit => context.FramesSinceHit < BlockAfterHitFrames,
                _ => false
            };
            // A jump cancels a raised shield in the sim, so the jump frames win over a block hold.
            if (block && !jumping) command.Held |= GameplayButtons.Block;
            return command;
        }
    }

    /// <summary>
    /// Injects the training dummy's command as player 1's input frame through <c>InputManager.SetInputSource</c>, the
    /// seam the calibration drills and the Mirror clone use. Edges are derived from the previous frame, so the sim sees a
    /// real press and never a permanently held bit.
    /// </summary>
    public sealed class TrainingDummySource : IPlayerInputSource {
        private TrainingDummyCommand _command;

        public TrainingDummyCommand Command => _command;

        public void SetCommand(in TrainingDummyCommand command) => _command = command;

        public PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame) =>
            PlayerInputFrame.Create(tick, _command.MoveX, 0f, _command.Held, previousFrame.Held);
    }
}
