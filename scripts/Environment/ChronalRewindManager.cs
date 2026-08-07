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
        private int _playbackIndex;
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

            RemainingRewinds--;
            StoryManager.Instance?.SetRewinds(RemainingRewinds);
            Vector2 checkpoint = GetCheckpointPosition();
            _playbackPath = _buffer.BuildPlaybackPath(checkpoint, 4);
            _playbackIndex = 0;
            _isRewinding = true;
            FreezeWorldForRewind();
            _player.SetRewindSuspended(true);
            RaisePresentation(RewindPresentationPhase.Started, checkpoint, active: true);
            RaisePresentation(RewindPresentationPhase.Playback, checkpoint, active: true);
        }

        private void AdvancePlayback() {
            if (_playbackPath == null || _playbackIndex >= _playbackPath.Count) {
                CompleteRewind(GetCheckpointPosition());
                return;
            }
            RewindFrame frame = _playbackPath[_playbackIndex++];
            _player.GlobalPosition = frame.Position;
            _player.IsFacingRight = frame.IsFacingRight;
            _player.PlayPresentationAnimation(frame.AnimationName);
            if (_playbackIndex >= _playbackPath.Count) CompleteRewind(frame.Position);
        }

        private void CompleteRewind(Vector2 landingPosition) {
            int maximumHP = _player.MaximumHP;
            int restoredHP = Math.Max(1, Mathf.CeilToInt(maximumHP * GetHPRestorePercent(_difficulty)));
            _player.CompleteStoryRewind(landingPosition, restoredHP);
            _isRewinding = false;
            _playbackPath = null;
            _playbackIndex = 0;
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

        private void FreezeWorldForRewind() {
            _frozenSimulations.Clear();
            Godot.Collections.Array<Node> enemies = GetTree().GetNodesInGroup("Enemies");
            using var enemiesLifetime = enemies.AsDisposable();
            foreach (Node node in enemies) {
                if (node is IStoryRewindSimulation simulation) {
                    simulation.SetStoryRewindFrozen(true);
                    _frozenSimulations.Add(simulation);
                }
            }
            Godot.Collections.Array<Node> constructs = GetTree().GetNodesInGroup("persistent_construct");
            using var constructsLifetime = constructs.AsDisposable();
            foreach (Node node in constructs) {
                if (node is IStoryRewindSimulation simulation && !_frozenSimulations.Contains(simulation)) {
                    simulation.SetStoryRewindFrozen(true);
                    _frozenSimulations.Add(simulation);
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
