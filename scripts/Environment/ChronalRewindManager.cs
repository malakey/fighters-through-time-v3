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
            if (_isScrubbing) {
                AdvanceScrub();
                return;
            }
            if (_player.CurrentState == CharacterState.Dead || _player.CurrentState == CharacterState.Respawning) return;
            TrackManualRewindHold((float)delta);
            if (_isScrubbing) return;
            _buffer.Record(new RewindFrame(
                _player.GlobalPosition,
                _player.IsOnFloor(),
                _player.IsFacingRight,
                _player.ActiveAnimationName));
        }

        // === Manual rewind (V7.2 — a scrubbed verb with its own input) ==========

        /// <summary>Hold gameplay_rewind this long to begin the scrub.</summary>
        public const float ManualHoldSeconds = 0.5f;

        /// <summary>The scrub walks the history at 4x while the input is held.</summary>
        public const int ScrubFramesPerTick = 4;

        /// <summary>The Stasis Echo persists this long, counted from playback end.</summary>
        public const float StasisEchoSeconds = 10f;

        private float _manualHoldSeconds;
        private bool _isScrubbing;
        private int _scrubDepthFrames;
        private Vector2 _scrubOrigin;
        private bool _manualCommit;
        private bool _spawnEchoOnComplete;
        private Vector2 _pendingEchoOrigin;
        private readonly List<IRewindScrubbable> _scrubbables = new();

        /// <summary>True while the world-frozen scrub preview is running. Test seam.</summary>
        public bool IsScrubbing => _isScrubbing;

        /// <summary>Current scrub depth in history frames. Test seam.</summary>
        public int ScrubDepthFrames => _scrubDepthFrames;

        /// <summary>
        /// Level 0's calibration sets this during its scripted manual-rewind
        /// beat: scripted tutorial uses are free on every difficulty.
        /// </summary>
        public bool ScriptedFreeRewind { get; set; }

        /// <summary>Easy manual rewinds are free (the learning sandbox); the
        /// scripted tutorial uses are free everywhere. Test seam.</summary>
        public bool IsManualRewindFree => _difficulty == Difficulty.Easy || ScriptedFreeRewind;

        private void TrackManualRewindHold(float dt) {
            if (!Input.IsActionPressed(InputManager.Actions.Rewind) || !CanBeginManualRewind()) {
                _manualHoldSeconds = 0f;
                return;
            }
            _manualHoldSeconds += dt;
            if (_manualHoldSeconds < ManualHoldSeconds) return;
            _manualHoldSeconds = 0f;
            BeginScrub();
        }

        /// <summary>
        /// Usable in any state except hitstun, daze, Dead, and mid-ability —
        /// and only when a charge is available (or the rewind is free) and
        /// there is real history to scrub.
        /// </summary>
        private bool CanBeginManualRewind() =>
            _player.CurrentState is not (CharacterState.Stunned or CharacterState.Dazed
                or CharacterState.Dead or CharacterState.Respawning
                or CharacterState.UsingSpecial or CharacterState.UsingUltimate
                or CharacterState.UsingMovementAbility or CharacterState.Grabbing)
            && (IsManualRewindFree || RemainingRewinds > 0)
            && _buffer.Count > MinimumPlaybackFrames;

        private void BeginScrub() {
            _isScrubbing = true;
            _scrubDepthFrames = 0;
            _scrubOrigin = _player.GlobalPosition;
            FreezeWorldForRewind();
            BeginPlatformScrub();
            _player.SetRewindSuspended(true);
            RaisePresentation(RewindPresentationPhase.Started, _scrubOrigin, active: true);
        }

        private void AdvanceScrub() {
            // Jump cancels: snap back to the present, no charge spent.
            if (Input.IsActionJustPressed(InputManager.Actions.Jump)) {
                CancelScrub();
                return;
            }
            bool held = Input.IsActionPressed(InputManager.Actions.Rewind);
            if (held) {
                if (_scrubDepthFrames < _buffer.Count) {
                    _scrubDepthFrames = Math.Min(_buffer.Count, _scrubDepthFrames + ScrubFramesPerTick);
                    ApplyPlatformScrub(_scrubDepthFrames);
                }
                return;
            }
            CommitScrub();
        }

        /// <summary>The scrub preview's ghost position (the frame the commit
        /// would target). Presentation polls this while scrubbing.</summary>
        public Vector2 ScrubPreviewPosition =>
            _buffer.TryPeek(Math.Max(1, _scrubDepthFrames), out RewindFrame frame)
                ? frame.Position
                : _scrubOrigin;

        private void CancelScrub() {
            _isScrubbing = false;
            _scrubDepthFrames = 0;
            EndPlatformScrub();
            _player.SetRewindSuspended(false);
            ResumeWorldAfterRewind();
            RaisePresentation(RewindPresentationPhase.Landed, _player.GlobalPosition, active: false);
        }

        /// <summary>
        /// Releasing the input commits: one charge is spent (never on cancel,
        /// free on Easy and for scripted tutorial uses), the standard playback
        /// runs for the scrubbed span, and a Stasis Echo is left at the origin.
        /// </summary>
        private void CommitScrub() {
            _isScrubbing = false;
            int depth = Math.Max(1, _scrubDepthFrames);
            if (!IsManualRewindFree) {
                RemainingRewinds--;
                StoryManager.Instance?.SetRewinds(RemainingRewinds);
            }
            _manualCommit = true;
            _spawnEchoOnComplete = true;
            _pendingEchoOrigin = _scrubOrigin;
            Vector2 checkpoint = GetCheckpointPosition();
            _playbackPath = _buffer.BuildPlaybackPath(checkpoint, 1, depth);
            _playbackTick = 0;
            _playbackTicks = ComputePlaybackTicks(_playbackPath.Count);
            _holdTicksRemaining = PreRewindHoldFrames;
            _isRewinding = true;
            // World and player are already frozen from the scrub.
            RaisePresentation(RewindPresentationPhase.Playback, _playbackPath[^1].Position, active: true);
        }

        // === Path-platform scrubbing (the world-interaction exemplar) ===========

        private void BeginPlatformScrub() {
            _scrubbables.Clear();
            Godot.Collections.Array<Node> members = GetTree().GetNodesInGroup(PathMovingPlatform.ScrubGroup);
            using var membersLifetime = members.AsDisposable();
            foreach (Node node in members) {
                if (node is IRewindScrubbable scrubbable) {
                    scrubbable.BeginRewindScrub();
                    _scrubbables.Add(scrubbable);
                }
            }
        }

        private void ApplyPlatformScrub(int depthFrames) {
            foreach (IRewindScrubbable scrubbable in _scrubbables) {
                if (scrubbable is GodotObject godotObject && !IsInstanceValid(godotObject)) continue;
                scrubbable.ApplyRewindScrub(depthFrames);
            }
        }

        private void EndPlatformScrub() {
            foreach (IRewindScrubbable scrubbable in _scrubbables) {
                if (scrubbable is GodotObject godotObject && !IsInstanceValid(godotObject)) continue;
                scrubbable.EndRewindScrub();
            }
            _scrubbables.Clear();
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
            // V7.2: Easy dropped from 100% — with Checkpoint Mending and
            // Restoration Fonts in the loop, a full-heal death made deliberately
            // dying strictly better than surviving to the next heal source.
            Difficulty.Easy => 0.7f,
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
            BeginPlatformScrub();
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
            int pathIndex = PathIndexForTick(_playbackTick, _playbackTicks, _playbackPath.Count);
            RewindFrame frame = _playbackPath[pathIndex];
            _player.GlobalPosition = frame.Position;
            _player.IsFacingRight = frame.IsFacingRight;
            _player.PlayPresentationAnimation(frame.AnimationName);
            // The exemplar platform scrubs back along its own recorded path in
            // step with the player's playback progress.
            ApplyPlatformScrub(pathIndex + 1);
            if (_playbackTick >= _playbackTicks) CompleteRewind(_playbackPath[^1].Position);
        }

        private void CompleteRewind(Vector2 landingPosition) {
            int maximumHP = _player.MaximumHP;
            // On the death path CurrentHP is 0, so the difficulty restore applies
            // unchanged; a scripted demonstration must not damage a healthy player.
            // A voluntary (manual) rewind restores no HP at all — unchanged rule.
            int restoredHP = _manualCommit
                ? Math.Max(1, _player.CurrentHP)
                : Math.Max(_player.CurrentHP,
                    Math.Max(1, Mathf.CeilToInt(maximumHP * GetHPRestorePercent(_difficulty))));
            _player.CompleteStoryRewind(landingPosition, restoredHP);
            _isRewinding = false;
            _playbackPath = null;
            _playbackTick = 0;
            _playbackTicks = 0;
            _holdTicksRemaining = 0;
            _buffer.Clear();
            EndPlatformScrub();
            EventBus.Instance?.RaiseRewindTriggered(landingPosition);
            ResumeWorldAfterRewind();
            RaisePresentation(RewindPresentationPhase.Landed, landingPosition, active: false);
            // Stasis Echo (V7.2): a committed manual rewind leaves a frozen copy
            // at the rewind origin, living 10 s from the moment playback ends.
            // Death-rewinds leave no Echo — the constructive half belongs to the
            // deliberate verb only.
            if (_spawnEchoOnComplete) {
                StasisEcho.Spawn(GetTree(), _pendingEchoOrigin, _player, StasisEchoSeconds);
            }
            _manualCommit = false;
            _spawnEchoOnComplete = false;
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
