using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    public partial class ChronalRewindManager : Node {
        public int RemainingRewinds { get; private set; }
        public bool IsRewinding => _isRewinding;

        private readonly ChronalRewindBuffer _buffer = new();
        private readonly HashSet<string> _activatedCheckpoints = new(StringComparer.Ordinal);
        private Difficulty _difficulty;
        private PlayerController _player;
        private List<RewindFrame> _playbackPath;
        private int _playbackTick;
        private int _playbackTicks;
        private int _holdTicksRemaining;
        private bool _isRewinding;
        private readonly List<IStoryRewindSimulation> _frozenSimulations = new();

        public override void _Ready() {
            _difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            int maximum = GetMaximumRewinds(_difficulty);
            RemainingRewinds = StoryManager.Instance != null
                ? Math.Clamp(StoryManager.Instance.ChronalRewindsRemaining, 0, maximum)
                : maximum;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerDied += OnPlayerDied;
                EventBus.Instance.OnCheckpointReached += OnCheckpointReached;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerDied -= OnPlayerDied;
                EventBus.Instance.OnCheckpointReached -= OnCheckpointReached;
            }
        }

        public override void _PhysicsProcess(double delta) {
            ResolvePlayer();
            if (_player == null || !IsInstanceValid(_player)) return;
            if (_isRewinding) {
                AdvancePlayback();
                return;
            }
            if (_player.CurrentState == CharacterState.Dead || _player.CurrentState == CharacterState.Respawning) return;
            _buffer.Record(new RewindFrame(
                _player.GlobalPosition,
                _player.IsOnFloor(),
                _player.IsFacingRight,
                _player.ActiveAnimationName));
        }

        /// <summary>
        /// Refunds one rewind, used by the tutorial calibration step so the guided
        /// demonstration never spends the player's real pool (Hard has only one).
        /// </summary>
        public void RefundRewind() {
            RemainingRewinds = Math.Min(RemainingRewinds + 1, GetMaximumRewinds(_difficulty));
            StoryManager.Instance?.SetRewinds(RemainingRewinds);
        }

        public static int GetMaximumRewinds(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 5,
            Difficulty.Normal => 3,
            Difficulty.Hard => 1,
            _ => 3
        };

        public static float GetHPRestorePercent(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 1.0f,
            Difficulty.Normal => 0.5f,
            Difficulty.Hard => 0.3f,
            _ => 0.5f
        };

        public static int ApplyCheckpointRefresh(Difficulty difficulty, int currentRewinds) => difficulty switch {
            Difficulty.Easy => GetMaximumRewinds(difficulty),
            Difficulty.Normal => Math.Min(GetMaximumRewinds(difficulty), Math.Max(0, currentRewinds) + 1),
            Difficulty.Hard => Math.Clamp(currentRewinds, 0, GetMaximumRewinds(difficulty)),
            _ => currentRewinds
        };

        private void ResolvePlayer() {
            if (_player != null && IsInstanceValid(_player)) return;
            _player = GetTree().GetFirstNodeInGroup("StoryPlayer") as PlayerController;
        }

        private void OnPlayerDied(int playerIndex) {
            ResolvePlayer();
            if (_player == null || _player.PlayerIndex != playerIndex) return;
            if (RemainingRewinds <= 0) {
                CollapseTimeline();
                return;
            }
            BeginRewind();
        }

        /// <summary>
        /// Starts the full rewind sequence (world freeze, playback, restore,
        /// presentation) without requiring a lethal hit. The tutorial's
        /// calibration step uses this to demonstrate the mechanic — rewind is
        /// otherwise only ever triggered by death. Callers decide whether to
        /// refund the spent charge afterwards. Returns false when a rewind
        /// cannot start: already rewinding, no resolvable player, or an empty
        /// pool (a scripted demonstration must never trigger a Timeline
        /// Collapse).
        /// </summary>
        public bool TriggerScriptedRewind() {
            ResolvePlayer();
            if (_isRewinding || _player == null || !IsInstanceValid(_player)) return false;
            if (_player.CurrentState == CharacterState.Dead
                || _player.CurrentState == CharacterState.Respawning) return false;
            if (RemainingRewinds <= 0) return false;
            BeginRewind();
            return true;
        }

        // ---- Pacing (2026-08-15 rework, user direction) ------------------------
        //
        // The old playback scrubbed every 12th buffered frame per physics tick:
        // fifteen seconds of history flew past in ~1.25 s, the character
        // teleported 12 frames at a time (the "glitchy" read), and it began on
        // the very tick of the lethal hit. Now:
        //   * the whole mechanic lasts HALF the duration it rewinds (an 8 s
        //     history takes 4 s; a 3 s history takes 1.5 s),
        //   * it opens with a hold — world frozen, player suspended in the death
        //     pose, presentation live — before any frame plays back,
        //   * playback then walks the FULL-resolution history (stride 1) at the
        //     pace that fills the remaining time, so motion is continuous
        //     rather than stepped.

        /// <summary>Hold before playback begins: 0.75 s at 60 Hz.</summary>
        public const int PreRewindHoldFrames = 45;

        /// <summary>The whole mechanic (hold + playback) takes this fraction of the rewound duration.</summary>
        public const float MechanicDurationFraction = 0.5f;

        /// <summary>Playback never compresses below half a second, even for a very young buffer.</summary>
        public const int MinimumPlaybackFrames = 30;

        /// <summary>
        /// Physics ticks of playback for a history of <paramref name="rewoundFrames"/>
        /// frames: half the rewound duration less the opening hold, floored at
        /// <see cref="MinimumPlaybackFrames"/>. A full 480-frame (8 s) buffer
        /// gives 45 + 195 = 240 ticks, exactly 4 s.
        /// </summary>
        public static int ComputePlaybackTicks(int rewoundFrames) {
            int mechanic = Mathf.RoundToInt(Math.Max(0, rewoundFrames) * MechanicDurationFraction);
            return Math.Max(MinimumPlaybackFrames, mechanic - PreRewindHoldFrames);
        }

        /// <summary>Total physics ticks the mechanic occupies for a history of the given depth. Test surface.</summary>
        public static int ComputeTotalMechanicTicks(int rewoundFrames) =>
            PreRewindHoldFrames + ComputePlaybackTicks(rewoundFrames);

        /// <summary>
        /// Which full-resolution history frame plays on playback tick
        /// <paramref name="tick"/> (1-based) of <paramref name="playbackTicks"/>:
        /// linear progress across the path, so the last tick lands on the last frame.
        /// </summary>
        public static int PathIndexForTick(int tick, int playbackTicks, int pathCount) {
            if (pathCount <= 1 || playbackTicks <= 0) return Math.Max(0, pathCount - 1);
            float progress = Mathf.Clamp((float)tick / playbackTicks, 0f, 1f);
            return Math.Min(pathCount - 1, Mathf.FloorToInt(progress * (pathCount - 1) + 0.0001f));
        }

        /// <summary>True while the opening hold is running (rewinding, but no frame has played back yet). Test surface.</summary>
        public bool IsInPreRewindHold => _isRewinding && _holdTicksRemaining > 0;

        private void BeginRewind() {
            RemainingRewinds--;
            StoryManager.Instance?.SetRewinds(RemainingRewinds);
            Vector2 checkpoint = GetCheckpointPosition();
            // Target the full buffer depth: the rewind lands as far back as the
            // recorded history allows (up to 8 s), on a grounded frame. Stride 1
            // — the pacing below decides how fast the path is walked.
            _playbackPath = _buffer.BuildPlaybackPath(
                checkpoint, 1, ChronalRewindBuffer.DefaultCapacity);
            _playbackTick = 0;
            _playbackTicks = ComputePlaybackTicks(_playbackPath.Count);
            _holdTicksRemaining = PreRewindHoldFrames;
            _isRewinding = true;
            FreezeWorldForRewind();
            _player.SetRewindSuspended(true);
            RaisePresentation(RewindPresentationPhase.Started, checkpoint, active: true);
        }

        private void AdvancePlayback() {
            if (_playbackPath == null || _playbackPath.Count == 0) {
                CompleteRewind(GetCheckpointPosition());
                return;
            }
            if (_holdTicksRemaining > 0) {
                _holdTicksRemaining--;
                if (_holdTicksRemaining == 0) {
                    RaisePresentation(RewindPresentationPhase.Playback, _playbackPath[^1].Position, active: true);
                }
                return;
            }
            _playbackTick++;
            RewindFrame frame = _playbackPath[PathIndexForTick(_playbackTick, _playbackTicks, _playbackPath.Count)];
            _player.GlobalPosition = frame.Position;
            _player.IsFacingRight = frame.IsFacingRight;
            _player.PlayPresentationAnimation(frame.AnimationName);
            if (_playbackTick >= _playbackTicks) CompleteRewind(_playbackPath[^1].Position);
        }

        private void CompleteRewind(Vector2 landingPosition) {
            int maximumHP = _player.MaximumHP;
            // On the death path CurrentHP is 0, so the difficulty restore applies
            // unchanged; a scripted demonstration must not damage a healthy player.
            int restoredHP = Math.Max(_player.CurrentHP,
                Math.Max(1, Mathf.CeilToInt(maximumHP * GetHPRestorePercent(_difficulty))));
            _player.CompleteStoryRewind(landingPosition, restoredHP);
            _isRewinding = false;
            _playbackPath = null;
            _playbackTick = 0;
            _playbackTicks = 0;
            _holdTicksRemaining = 0;
            _buffer.Clear();
            EventBus.Instance?.RaiseRewindTriggered(landingPosition);
            ResumeWorldAfterRewind();
            RaisePresentation(RewindPresentationPhase.Landed, landingPosition, active: false);
        }

        private void OnCheckpointReached(string checkpointID) {
            if (string.IsNullOrWhiteSpace(checkpointID) || !_activatedCheckpoints.Add(checkpointID)) return;
            RemainingRewinds = ApplyCheckpointRefresh(_difficulty, RemainingRewinds);
            StoryManager.Instance?.SetRewinds(RemainingRewinds);
        }

        private Vector2 GetCheckpointPosition() {
            LevelManager manager = GetTree().CurrentScene?.GetNodeOrNull<LevelManager>("LevelManager");
            return manager?.GetRespawnPosition() ?? Vector2.Zero;
        }

        private void CollapseTimeline() {
            Vector2 checkpoint = GetCheckpointPosition();
            RaisePresentation(RewindPresentationPhase.TimelineCollapse, checkpoint, active: true);
            StoryManager.Instance?.BeginTimelineCollapse(GetCheckpointID());
        }

        /// <summary>
        /// Scene-tree groups swept for <see cref="IStoryRewindSimulation"/> members
        /// when a rewind freezes the world. The Level 13 Mirror Paradox joins its
        /// own group rather than "Enemies" (its clone is a PlayerController, not an
        /// EnemyController), so the sweep names that group explicitly — audit H-8.
        /// </summary>
        private static readonly string[] FrozenSimulationGroups = {
            "Enemies",
            "persistent_construct",
            FTT.Enemies.MirrorParadoxController.MirrorGroup
        };

        private void FreezeWorldForRewind() {
            _frozenSimulations.Clear();
            foreach (string groupName in FrozenSimulationGroups) {
                Godot.Collections.Array<Node> members = GetTree().GetNodesInGroup(groupName);
                using var membersLifetime = members.AsDisposable();
                foreach (Node node in members) {
                    if (node is IStoryRewindSimulation simulation && !_frozenSimulations.Contains(simulation)) {
                        simulation.SetStoryRewindFrozen(true);
                        _frozenSimulations.Add(simulation);
                    }
                }
            }
            ClearEnemyProjectiles();
        }

        private void ResumeWorldAfterRewind() {
            foreach (IStoryRewindSimulation simulation in _frozenSimulations) {
                if (simulation is GodotObject godotObject && IsInstanceValid(godotObject)) {
                    simulation.SetStoryRewindFrozen(false);
                }
            }
            _frozenSimulations.Clear();
        }

        private void ClearEnemyProjectiles() {
            Godot.Collections.Array<Node> projectiles = GetTree().GetNodesInGroup("enemy_projectile");
            using var projectilesLifetime = projectiles.AsDisposable();
            foreach (Node projectile in projectiles) {
                if (!IsInstanceValid(projectile)) continue;
                if (PoolManager.Instance != null) PoolManager.Instance.Release(projectile);
                else projectile.QueueFree();
            }
        }

        private string GetCheckpointID() {
            LevelManager manager = GetTree().CurrentScene?.GetNodeOrNull<LevelManager>("LevelManager");
            return manager?.LastCheckpointID ?? "";
        }

        private static void RaisePresentation(RewindPresentationPhase phase, Vector2 target, bool active) {
            EventBus.Instance?.RaiseRewindPresentation(CreatePresentationPayload(phase, target, active));
        }

        public static RewindPresentationPayload CreatePresentationPayload(
            RewindPresentationPhase phase,
            Vector2 target,
            bool active) => new() {
                Phase = phase,
                TargetPosition = target,
                GhostTrailEnabled = active,
                ScreenTintEnabled = active,
                ScanlinesEnabled = active,
                MusicDuckDecibels = active ? -12f : 0f,
                ReverseSweepEnabled = active,
                ClockTickEnabled = active
            };
    }
}
