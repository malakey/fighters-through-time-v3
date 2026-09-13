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
        private Difficulty _difficulty;
        private PlayerController _player;
        private List<RewindFrame> _playbackPath;
        private int _playbackTick;
        private int _playbackTicks;
        private int _holdTicksRemaining;
        private bool _isRewinding;
        private readonly List<IStoryRewindSimulation> _frozenSimulations = new();

        /// <summary>Scene-tree group the HUD resolves the live manager through.</summary>
        public const string ManagerGroup = "chronal_rewind_manager";

        public override void _Ready() {
            _difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            int maximum = GetMaximumRewinds(_difficulty);
            RemainingRewinds = StoryManager.Instance != null
                ? Math.Clamp(StoryManager.Instance.ChronalRewindsRemaining, 0, maximum)
                : maximum;
            AddToGroup(ManagerGroup);
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerDied += OnPlayerDied;
                EventBus.Instance.OnCheckpointActivated += OnCheckpointActivated;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerDied -= OnPlayerDied;
                EventBus.Instance.OnCheckpointActivated -= OnCheckpointActivated;
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (_collapseBeatActive) {
                AdvanceCollapseBeat(
                    (float)delta,
                    Input.IsActionJustPressed(InputManager.Actions.Jump)
                        || Input.IsActionJustPressed(InputManager.Actions.Interact));
                return;
            }
            ResolvePlayer();
            if (_player == null || !IsInstanceValid(_player)) return;
            if (_isRewinding) {
                AdvancePlayback();
                return;
            }
            if (_player.CurrentState == CharacterState.Dead || _player.CurrentState == CharacterState.Respawning) return;
            // The buffer keeps recording through a Time Freeze: the player really
            // travelled there, and CHECKPOINT_RECOVERY.md counts freeze travel as
            // live route time.
            _buffer.Record(new RewindFrame(
                _player.GlobalPosition,
                _player.IsOnFloor(),
                _player.IsFacingRight,
                _player.ActiveAnimationName));
        }

        // === Path-platform reversal (the world-interaction exemplar) ============
        //
        // V7.6: the manual scrub verb is retired, so this is now purely the DEATH
        // rewind's platform reversal — the platform walks back along its own
        // recorded path in step with the player's playback. There is no cancel
        // path any more, because there is no preview to cancel.

        private readonly List<IRewindScrubbable> _scrubbables = new();

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
            // Package 11 A3 (F01): the death-rewind presentation freezes the
            // world, so the Integrity clock stops for its duration. The rewind
            // itself never REFUNDS a point — it only stops the bleeding while
            // the player is not playing.
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, true);
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
            // V7.6: the voluntary "restores no HP" branch went with the manual verb
            // — every rewind that reaches here is a death or a scripted demo.
            int restoredHP = Math.Max(_player.CurrentHP,
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
            // Package 11 A3 (F01): play resumes, and so does the clock — at the
            // value it froze at. Nothing is given back.
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, false);
            ResumeWorldAfterRewind();
            RaisePresentation(RewindPresentationPhase.Landed, landingPosition, active: false);
        }

        /// <summary>
        /// V7.3: the once-per-attempt gate is the checkpoint event's own
        /// FirstActivation flag (backed by StoryManager's persisted registry),
        /// so a collapse resume-from-anchor can no longer re-pay the refresh
        /// the way the old per-scene local set did.
        /// </summary>
        private void OnCheckpointActivated(CheckpointReachedPayload payload) {
            if (string.IsNullOrWhiteSpace(payload.CheckpointID) || !payload.FirstActivation) return;
            RemainingRewinds = ApplyCheckpointRefresh(_difficulty, RemainingRewinds);
            StoryManager.Instance?.SetRewinds(RemainingRewinds);
        }

        private Vector2 GetCheckpointPosition() {
            LevelManager manager = GetTree().CurrentScene?.GetNodeOrNull<LevelManager>("LevelManager");
            return manager?.GetRespawnPosition() ?? Vector2.Zero;
        }

        // === Timeline Collapse beat (V7.3 — the campaign's only failure state
        // earns a beat) =======================================================
        // The old collapse was a bare cut: presentation event, then straight to
        // the hub. Now the fracture presentation holds the frozen world for
        // ~4 seconds — skippable once the beat has been seen, never on the
        // first viewing — before the extraction to the hub runs.

        /// <summary>The collapse presentation's authored length.</summary>
        public const float CollapseBeatSeconds = 4f;

        private bool _collapseBeatActive;
        private float _collapseBeatRemaining;

        /// <summary>True while the collapse presentation is holding the world. Test seam.</summary>
        public bool IsCollapseBeatActive => _collapseBeatActive;

        /// <summary>Seconds of collapse presentation left. Test seam.</summary>
        public float CollapseBeatSecondsRemaining => _collapseBeatRemaining;

        /// <summary>True when this viewing may be skipped (not the first).</summary>
        public bool IsCollapseBeatSkippable { get; private set; }

        /// <summary>Test seam: replaces the hub-extraction side effect.</summary>
        internal Action CollapseCompletionOverrideForTesting;

        private void CollapseTimeline() {
            if (_collapseBeatActive) return;
            _collapseCause = TimelineCollapseCause.Death;
            BeginCollapseBeat();
        }

        /// <summary>
        /// Package 11 A3 (V7.6 F01): Timeline Integrity reached zero. The same
        /// fracture beat as a death-caused collapse, but tagged
        /// <see cref="TimelineCollapseCause.Timer"/> so the F11 recovery
        /// minimum is applied — that grant is the ONE thing a timer collapse
        /// does that a death collapse does not. Called by
        /// <c>StoryManager.FireIntegrityCollapse</c>; Act III replaces it with
        /// Anchor Snap / Smothered (A3b).
        /// </summary>
        internal void BeginTimerCollapse() {
            if (_collapseBeatActive) return;
            _collapseCause = TimelineCollapseCause.Timer;
            // The era-lost Sarah variant: a timer collapse is a different
            // failure from dying, and she says so. Non-blocking, so it rides
            // over the fracture beat rather than gating it. A6 owns the
            // English value; A6b may promote it to a full presentation beat.
            ResolvePlayer();
            EnvironmentNotice.Post(EraLostCollapseLineKey, _player, seconds: CollapseBeatSeconds);
            BeginCollapseBeat();
        }

        /// <summary>Sarah's line when Timeline Integrity, not death, ends the attempt.</summary>
        public const string EraLostCollapseLineKey = "dialogue_collapse_sarah_era_lost";

        private TimelineCollapseCause _collapseCause = TimelineCollapseCause.Death;

        /// <summary>Starts the fracture presentation over the frozen world.</summary>
        internal void BeginCollapseBeat() {
            _collapseBeatActive = true;
            _collapseBeatRemaining = CollapseBeatSeconds;
            // Package 11 A3 (F01): the collapse beat freezes the world, so the
            // Integrity clock stops with it.
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, true);
            IsCollapseBeatSkippable = StoryManager.Instance?.HasSeenCollapseBeat ?? false;
            FreezeWorldForRewind();
            EventBus.Instance?.RaiseRewindPresentation(CreateCollapsePayload(
                GetCheckpointPosition(), IsCollapseBeatSkippable));
        }

        /// <summary>
        /// The pure countdown rule: a skip press only lands once the beat has
        /// been seen before; the first viewing always runs its full length.
        /// </summary>
        public static float TickCollapseBeat(float remaining, float dt, bool skipPressed, bool skippable) =>
            skipPressed && skippable ? 0f : Math.Max(0f, remaining - dt);

        /// <summary>Advances the beat; on completion marks it seen and extracts to the hub.</summary>
        internal bool AdvanceCollapseBeat(float dt, bool skipPressed) {
            if (!_collapseBeatActive) return false;
            _collapseBeatRemaining = TickCollapseBeat(
                _collapseBeatRemaining, dt, skipPressed, IsCollapseBeatSkippable);
            if (_collapseBeatRemaining > 0f) return false;
            _collapseBeatActive = false;
            // The viewing is complete (or skipped): later collapses may skip.
            StoryManager.Instance?.MarkCollapseBeatSeen();
            if (CollapseCompletionOverrideForTesting != null) {
                CollapseCompletionOverrideForTesting();
                return true;
            }
            StoryManager.Instance?.BeginTimelineCollapse(GetCheckpointID(), _collapseCause);
            return true;
        }

        /// <summary>Collapse presentation payload: fracture treatment plus the skip prompt.</summary>
        public static RewindPresentationPayload CreateCollapsePayload(Vector2 target, bool skippable) {
            RewindPresentationPayload payload = CreatePresentationPayload(
                RewindPresentationPhase.TimelineCollapse, target, active: true);
            payload.CollapseSkipPromptEnabled = skippable;
            return payload;
        }

        /// <summary>
        /// Scene-tree groups swept for <see cref="IStoryRewindSimulation"/> members
        /// when a rewind freezes the world. The Level 13 Mirror Paradox joins its
        /// own group rather than "Enemies" (its clone is a PlayerController, not an
        /// EnemyController), so the sweep names that group explicitly — audit H-8.
        /// V7.3 adds Chronal Extractors: their discharge cycle, siphon drain, and
        /// grace timer all pause while the world is frozen for a rewind or scrub.
        /// Internal for the freeze-membership pin in TimelineIntegrityTests.
        /// </summary>
        internal static readonly string[] FrozenSimulationGroups = {
            "Enemies",
            "persistent_construct",
            "chronal_extractor",
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
