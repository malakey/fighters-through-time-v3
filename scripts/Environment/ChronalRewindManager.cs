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
        private readonly WorldSuspension _suspension = new();
        private TimeFreezeController _timeFreeze;

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
                EventBus.Instance.OnBossHistoricalRecovery += OnBossHistoricalRecovery;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerDied -= OnPlayerDied;
                EventBus.Instance.OnCheckpointActivated -= OnCheckpointActivated;
                EventBus.Instance.OnBossHistoricalRecovery -= OnBossHistoricalRecovery;
            }
            // Whoever freezes the world hands it back, including on teardown: a
            // scene change mid-hold must not leave survivors frozen, a hero
            // flagged as held, or a hero suspended by a boss beat.
            _holdActive = false;
            _holdFramesRemaining = 0;
            if (_player != null && IsInstanceValid(_player)) {
                _player.CancelRecoveryHold();
                _player.SetWorldSuspended(false);
            }
            _bossSuspensionActive = false;
            _suspendingBoss = null;
            _suspension.Release();
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
            TryBeginPendingPlacementHold();
            if (_bossSuspensionActive) {
                // GAP-06: the boss's T01b beat holds every clock the contract
                // names — the recovery history is one of them.
                AdvanceBossSuspension();
                return;
            }
            AdvancePostLandingHold();
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
            BeginRewind(StoryRecoveryHoldCause.DeathRewind);
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
            BeginRewind(StoryRecoveryHoldCause.ScriptedRewind);
            return true;
        }

        // ---- Pacing (2026-08-15 rework, user direction) ------------------------
        //
        // The old playback scrubbed every 12th buffered frame per physics tick:
        // fifteen seconds of history flew past in ~1.25 s, the character
        // teleported 12 frames at a time (the "glitchy" read), and it began on
        // the very tick of the lethal hit. Now:
        //   * the whole mechanic lasts HALF the duration it rewinds (a 5 s
        //     history takes 2.5 s; a 3 s history takes 1.5 s),
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
        /// R03 Post-Landing Hold: after every Story recovery the world stays
        /// frozen for 60 frames (1 s at 60 Hz) while the hero already has full
        /// control, so the hero always gets the first move out of a death.
        /// </summary>
        public const int PostLandingHoldFrames = 60;

        /// <summary>
        /// The 2026-09-14 name for <see cref="PostLandingHoldFrames"/>. The same
        /// constant, not a second value.
        /// </summary>
        public const int PostRewindWorldFreezeFrames = PostLandingHoldFrames;

        /// <summary>
        /// How many frames before the thaw the reused Time Freeze thaw warning
        /// fires ("just before the world resumes"). The design names no number;
        /// half the hold is provisional and recorded in the P12 W1 handoff.
        /// </summary>
        public const int PostLandingThawWarningFrames = 30;

        /// <summary>True while the Post-Landing Hold is holding the world. Test surface.</summary>
        public bool IsPostLandingHoldActive => _holdActive;

        /// <summary>The 2026-09-14 name for <see cref="IsPostLandingHoldActive"/>. Test surface.</summary>
        public bool IsWorldFreezeLingering => IsPostLandingHoldActive;

        /// <summary>Frames of hold left; 0 when no hold is running.</summary>
        public int PostLandingHoldFramesRemaining => _holdActive ? _holdFramesRemaining : 0;

        /// <summary>Which recovery the running hold follows.</summary>
        public StoryRecoveryHoldCause PostLandingHoldCause => _holdCause;

        /// <summary>Thaw warnings played by this manager. Test surface.</summary>
        public int ThawWarningsIssued { get; private set; }

        /// <summary>True while a boss's T01b Historical Recovery beat suspends the world (GAP-06).</summary>
        public bool IsBossSuspensionActive => _bossSuspensionActive;

        /// <summary>True while either the Post-Landing Hold or a boss suspension holds the world.</summary>
        public bool IsHoldingWorld => _holdActive || _bossSuspensionActive;

        /// <summary>
        /// <b>The single shared world-hold query</b> (Package 12 W1). True while
        /// the scene's manager is running the R03 Post-Landing Hold or a GAP-06
        /// boss suspension. The hit gates in <c>Hitbox.OnAreaEntered</c> and
        /// <c>Hurtbox.TakeHit</c> consult it, so a frozen actor is invulnerable
        /// and gives no credit. Side-effect-free: one group lookup.
        /// </summary>
        public static bool IsWorldHeld(SceneTree tree) =>
            tree?.GetFirstNodeInGroup(ManagerGroup) is ChronalRewindManager manager
            && IsInstanceValid(manager)
            && manager.IsHoldingWorld;

        /// <summary>
        /// Physics ticks of playback for a history of <paramref name="rewoundFrames"/>
        /// frames: half the rewound duration less the opening hold, floored at
        /// <see cref="MinimumPlaybackFrames"/>. A full 300-frame (5 s) buffer
        /// gives 45 + 105 = 150 ticks, exactly 2.5 s.
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

        private StoryRecoveryHoldCause _rewindCause = StoryRecoveryHoldCause.DeathRewind;

        private void BeginRewind(StoryRecoveryHoldCause cause) {
            _rewindCause = cause;
            RemainingRewinds--;
            StoryManager.Instance?.SetRewinds(RemainingRewinds);
            Vector2 checkpoint = GetCheckpointPosition();
            // Target the full buffer depth: the rewind lands as far back as the
            // recorded history allows (up to 5 s), on a grounded frame. Stride 1
            // — the pacing below decides how fast the path is walked.
            _playbackPath = _buffer.BuildPlaybackPath(
                checkpoint, 1, ChronalRewindBuffer.DefaultCapacity);
            _playbackTick = 0;
            _playbackTicks = ComputePlaybackTicks(_playbackPath.Count);
            _holdTicksRemaining = PreRewindHoldFrames;
            // A death inside the previous recovery's Post-Landing Hold folds into
            // this one: the world simply stays frozen from here, and the hold's
            // thaw (and its protection) belongs to the new landing.
            if (_holdActive) {
                _holdActive = false;
                _holdFramesRemaining = 0;
                _player.CancelRecoveryHold();
            }
            if (_bossSuspensionActive) EndBossSuspension(releaseWorld: false);
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
            if (_playbackTick >= _playbackTicks) {
                CompleteRewind(_playbackPath[^1].Position, _playbackPath[^1].IsGrounded);
            }
        }

        private void CompleteRewind(Vector2 landingPosition, bool grounded = true) {
            int maximumHP = _player.MaximumHP;
            // On the death path CurrentHP is 0, so the difficulty restore applies
            // unchanged; a scripted demonstration must not damage a healthy player.
            // V7.6: the voluntary "restores no HP" branch went with the manual verb
            // — every rewind that reaches here is a death or a scripted demo.
            int restoredHP = Math.Max(_player.CurrentHP,
                Math.Max(1, Mathf.CeilToInt(maximumHP * GetHPRestorePercent(_difficulty))));
            _player.CompleteStoryRewind(landingPosition, restoredHP, grounded);
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
            // R03: the world stays frozen for the Post-Landing Hold while the
            // hero already has control; the recovery music duck is held with it.
            EventBus.Instance?.RaiseRewindPresentation(CreateLandedHoldPayload(landingPosition));
            BeginPostLandingHold(_rewindCause);
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
            // Package 12 W1 (M17): the era-lost line now rides the beat's own
            // transmission slot (CollapseTransmissionKeyFor), and Act III — which
            // has no extraction — carries no line at all.
            BeginCollapseBeat();
        }

        /// <summary>Sarah's line when Timeline Integrity, not death, ends the attempt.</summary>
        public const string EraLostCollapseLineKey = "dialogue_collapse_sarah_era_lost";

        /// <summary>Sarah's ordinary extraction transmission over the collapse beat.</summary>
        public const string CollapseTransmissionLineKey = "collapse_transmission_line";

        /// <summary>
        /// M17: which Sarah transmission the collapse beat shows. Act III has no
        /// extraction — the Wardens cannot reach past the Void and "no rift
        /// comes" — so it shows none; otherwise a timer collapse plays the
        /// era-lost variant and a death collapse the ordinary line.
        /// </summary>
        public static string CollapseTransmissionKeyFor(bool actIII, TimelineCollapseCause cause) =>
            actIII ? "" : cause == TimelineCollapseCause.Timer ? EraLostCollapseLineKey : CollapseTransmissionLineKey;

        private TimelineCollapseCause _collapseCause = TimelineCollapseCause.Death;

        /// <summary>Starts the fracture presentation over the frozen world.</summary>
        internal void BeginCollapseBeat() {
            _collapseBeatActive = true;
            _collapseBeatRemaining = CollapseBeatSeconds;
            if (_holdActive) {
                _holdActive = false;
                _holdFramesRemaining = 0;
                ResolvePlayer();
                if (_player != null && IsInstanceValid(_player)) _player.CancelRecoveryHold();
            }
            // Package 11 A3 (F01): the collapse beat freezes the world, so the
            // Integrity clock stops with it.
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, true);
            IsCollapseBeatSkippable = StoryManager.Instance?.HasSeenCollapseBeat ?? false;
            FreezeWorldForRewind();
            bool actIII = StoryManager.Instance != null && StoryManager.IsActIIILevel(StoryManager.Instance.CurrentLevel);
            EventBus.Instance?.RaiseRewindPresentation(CreateCollapsePayload(
                GetCheckpointPosition(), IsCollapseBeatSkippable,
                CollapseTransmissionKeyFor(actIII, _collapseCause)));
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
        public static RewindPresentationPayload CreateCollapsePayload(
            Vector2 target, bool skippable, string transmissionLineKey = CollapseTransmissionLineKey) {
            RewindPresentationPayload payload = CreatePresentationPayload(
                RewindPresentationPhase.TimelineCollapse, target, active: true);
            payload.CollapseSkipPromptEnabled = skippable;
            payload.TransmissionLineKey = transmissionLineKey ?? "";
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
            // A live Time Freeze ends here, handing its parked projectiles over
            // rather than restoring them: both freezes latch the same per-actor
            // flag, so a freeze that thawed a tick later would release the world
            // mid-rewind. Ending it also arms its cooldown, as any early end does.
            ResolveTimeFreeze();
            if (_timeFreeze != null && IsInstanceValid(_timeFreeze)) {
                _suspension.AdoptParked(_timeFreeze.EndFreezeForRecovery());
            }
            // The death rewind clears on-screen enemy projectiles on initiation
            // (T01a), so those are released rather than parked.
            ClearEnemyProjectiles();
            _suspension.Suspend(GetTree(), WorldSuspensionMode.DeathRewind, exclude: null);
        }

        /// <summary>
        /// Freezes whatever has entered the tree since the sweep ran — a room
        /// revealed during the Post-Landing Hold (or a boss suspension) freezes
        /// before its actors take a tick. Idempotent.
        /// </summary>
        internal void RefreshWorldHold() {
            if (!_suspension.IsActive || !IsInsideTree()) return;
            _suspension.Refresh(GetTree());
        }

        private void ResolveTimeFreeze() {
            if (_timeFreeze != null && IsInstanceValid(_timeFreeze)) return;
            if (!IsInsideTree()) return;
            _timeFreeze = GetTree().GetFirstNodeInGroup(TimeFreezeController.ControllerGroup) as TimeFreezeController;
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

        // === Package 12 W1 — the R03 Post-Landing Hold ======================
        //
        // Every Story recovery — the death rewind, the in-session checkpoint
        // placement that resumes a Collapse, the Anchor Snap and the Level 0
        // scripted rewind — ends with a 60-frame hold: the hero is live, the
        // world is not. The frozen set is the union of the Time Freeze sweep and
        // the rewind sweep (plan D3(a)), so Extractors, the Mirror and the hero's
        // own constructs freeze too. Frozen actors are invulnerable and give no
        // credit (IsWorldHeld). Every clock runs: Integrity, the Time Freeze
        // cooldown and the hero's own timers. Protection starts at the thaw.
        // Transient: nothing here is ever persisted, and a reload never grants
        // a hold.

        private bool _holdActive;
        private int _holdFramesRemaining;
        private StoryRecoveryHoldCause _holdCause;

        private void BeginPostLandingHold(StoryRecoveryHoldCause cause) {
            _holdActive = true;
            _holdCause = cause;
            _holdFramesRemaining = PostLandingHoldFrames;
            _player?.BeginRecoveryHold(cause, PostLandingHoldFrames);
        }

        /// <summary>
        /// Starts the hold after a placement that did not come through this
        /// manager's own playback — the Collapse resume and the Anchor Snap,
        /// both of which reconstruct the level in the same session. The world
        /// freezes through the Time Freeze suspend path (nothing is cancelled,
        /// nothing is cleared). Returns false when there is no living hero or a
        /// presentation already owns the world.
        /// </summary>
        public bool BeginPlacementHold(StoryRecoveryHoldCause cause) {
            ResolvePlayer();
            if (_isRewinding || _collapseBeatActive || _holdActive) return false;
            if (_player == null || !IsInstanceValid(_player) || _player.CurrentHP <= 0) return false;
            if (_bossSuspensionActive) EndBossSuspension(releaseWorld: true);
            _suspension.Suspend(GetTree(), WorldSuspensionMode.TimeFreezePath, exclude: null);
            // Protection starts at the thaw, never before it.
            BeginPostLandingHold(cause);
            return true;
        }

        private void TryBeginPendingPlacementHold() {
            if (_holdActive || StoryManager.Instance == null
                || !StoryManager.Instance.HasPendingRecoveryPlacementHold) return;
            string scenePath = GetTree()?.CurrentScene?.SceneFilePath ?? "";
            if (StoryManager.Instance.TryConsumeRecoveryPlacementHold(scenePath, out StoryRecoveryHoldCause cause)) {
                BeginPlacementHold(cause);
            }
        }

        private void AdvancePostLandingHold() {
            if (!_holdActive) return;
            if (_player.CurrentState == CharacterState.Dead) {
                // A death inside the hold (a lethal fall) is handled by
                // OnPlayerDied; either way this hold is over.
                _holdActive = false;
                _player.CancelRecoveryHold();
                return;
            }
            // Newly revealed actors freeze before they can act.
            RefreshWorldHold();
            _holdFramesRemaining--;
            if (_holdFramesRemaining == PostLandingThawWarningFrames) PlayThawWarning();
            if (_holdFramesRemaining > 0) return;
            ThawPostLandingHold();
        }

        /// <summary>
        /// Reuses the Time Freeze thaw warning: its HUD string over the hero and
        /// a Critical Cues one-shot. Reduced Temporal Effects never suppresses
        /// it — it is a readability cue, not decoration.
        /// </summary>
        private void PlayThawWarning() {
            ThawWarningsIssued++;
            AudioManager.Instance?.PlayCriticalCue(ThawCueStream);
            EnvironmentNotice.Post(TimeFreezeController.ThawWarningLabelKey, _player,
                seconds: PostLandingThawWarningFrames / 60f);
        }

        /// <summary>
        /// The thaw cue stream. Null while the placeholder audio kit is silent
        /// (2026-08-10 directive); Package 10 assigns the real cue here.
        /// </summary>
        public static AudioStream ThawCueStream { get; set; }

        private void ThawPostLandingHold() {
            _holdActive = false;
            _holdFramesRemaining = 0;
            _suspension.Release();
            // The recovery music duck ends at the thaw.
            RaisePresentation(RewindPresentationPhase.Thawed, _player.GlobalPosition, active: false);
            // Protection is a new entitlement starting now; OnRecoveryWorldThawed.
            _player.EndRecoveryHold();
            // A still-valid pending boss transition plays after the thaw: a boss
            // whose T01b beat was running when the hero died has been frozen
            // mid-beat, and its world-wide suspension resumes now.
            ResumePendingBossTransition();
        }

        /// <summary>The Landed payload that keeps the recovery music duck held through the hold.</summary>
        public static RewindPresentationPayload CreateLandedHoldPayload(Vector2 target) {
            RewindPresentationPayload payload = CreatePresentationPayload(
                RewindPresentationPhase.Landed, target, active: false);
            payload.MusicDuckDecibels = -12f;
            return payload;
        }

        // === Package 12 W1 — GAP-06: the boss's Historical Recovery suspension ==

        private bool _bossSuspensionActive;
        private FTT.Enemies.BossController _suspendingBoss;
        private int _bossSuspensionFramesRemaining;

        private void OnBossHistoricalRecovery(BossHistoricalRecoveryPayload payload) {
            // During a rewind, a hold or the collapse beat the boss is frozen with
            // the world; ResumePendingBossTransition picks it up at the thaw.
            if (_isRewinding || _holdActive || _collapseBeatActive) return;
            FTT.Enemies.BossController boss = FindRecoveringBoss(payload.BossID);
            if (boss == null) return;
            BeginBossSuspension(boss, Mathf.RoundToInt(payload.SuspendSeconds * 60f));
        }

        private FTT.Enemies.BossController FindRecoveringBoss(string bossID) {
            if (!IsInsideTree()) return null;
            Godot.Collections.Array<Node> bosses = GetTree().GetNodesInGroup("Enemies");
            using var bossesLifetime = bosses.AsDisposable();
            foreach (Node node in bosses) {
                if (node is FTT.Enemies.BossController boss && IsInstanceValid(boss)
                    && boss.IsHistoricalRecoverySuspended
                    && (string.IsNullOrEmpty(bossID) || boss.Data?.BossID == bossID)) {
                    return boss;
                }
            }
            return null;
        }

        /// <summary>
        /// Suspends the whole world — the hero included — for the boss's own
        /// beat, with no timer catch-up. The recovering boss is the one actor
        /// left running, because its own beat is the clock.
        /// </summary>
        internal void BeginBossSuspension(FTT.Enemies.BossController boss, int frames) {
            ResolvePlayer();
            _bossSuspensionActive = true;
            _suspendingBoss = boss;
            // Slack over the boss's own count: the suspension ends on the boss's
            // completion, and on its own clock only if the boss is lost.
            _bossSuspensionFramesRemaining = Math.Max(1, frames) + 2;
            _suspension.Suspend(GetTree(), WorldSuspensionMode.TimeFreezePath, exclude: boss);
            if (_player != null && IsInstanceValid(_player)) _player.SetWorldSuspended(true);
        }

        private void AdvanceBossSuspension() {
            RefreshWorldHold();
            _bossSuspensionFramesRemaining--;
            bool bossStillRecovering = _suspendingBoss != null && IsInstanceValid(_suspendingBoss)
                && _suspendingBoss.IsHistoricalRecoverySuspended;
            if (bossStillRecovering && _bossSuspensionFramesRemaining > 0) return;
            EndBossSuspension(releaseWorld: true);
        }

        private void EndBossSuspension(bool releaseWorld) {
            _bossSuspensionActive = false;
            _suspendingBoss = null;
            _bossSuspensionFramesRemaining = 0;
            if (_player != null && IsInstanceValid(_player)) _player.SetWorldSuspended(false);
            if (releaseWorld) _suspension.Release();
        }

        private void ResumePendingBossTransition() {
            FTT.Enemies.BossController boss = FindRecoveringBoss("");
            if (boss == null) return;
            BeginBossSuspension(boss, boss.HistoricalRecoverySuspendFramesRemaining);
        }

        // === The world suspension (shared by the rewind, the hold and GAP-06) ==

        internal enum WorldSuspensionMode {
            /// <summary>
            /// The death / scripted rewind: the rewind groups take the rewind
            /// freeze (which cancels in-flight enemy attacks at initiation, as it
            /// always has); everything else takes the Time Freeze path.
            /// </summary>
            DeathRewind,
            /// <summary>Everything takes the Time Freeze path: nothing cancelled, nothing cleared.</summary>
            TimeFreezePath
        }

        /// <summary>
        /// The union frozen set of plan D3(a): the rewind groups plus the Time
        /// Freeze groups. Extractors, the Mirror and the hero's constructs are in
        /// it. Internal for the membership pin.
        /// </summary>
        internal static IEnumerable<string> PostLandingHoldGroups {
            get {
                var seen = new HashSet<string>();
                foreach (string group in FrozenSimulationGroups) if (seen.Add(group)) yield return group;
                foreach (string group in TimeFreezeController.FrozenTimeGroups) if (seen.Add(group)) yield return group;
            }
        }

        /// <summary>
        /// One suspension at a time. Freezing prefers
        /// <see cref="IStoryTimeFreezable"/> (latch a flag, mutate nothing) over
        /// <see cref="IStoryRewindSimulation"/>, except for the rewind groups on
        /// the death-rewind path; members that implement only the rewind hook
        /// (the Chronal Extractor) take it, which for them is a pure latch too.
        /// Pooled projectiles and zones are parked with the Time Freeze
        /// <c>ProcessMode.Disabled</c> fallback and restored to their recorded mode.
        /// </summary>
        private sealed class WorldSuspension {
            private readonly List<IStoryRewindSimulation> _rewindFrozen = new();
            private readonly List<IStoryTimeFreezable> _timeFrozen = new();
            private readonly Dictionary<Node, ProcessModeEnum> _parked = new();
            private readonly HashSet<GodotObject> _members = new();
            private WorldSuspensionMode _mode;
            private Node _exclude;

            public bool IsActive { get; private set; }

            public void Suspend(SceneTree tree, WorldSuspensionMode mode, Node exclude) {
                if (!IsActive) {
                    _mode = mode;
                    _exclude = exclude;
                } else if (mode == WorldSuspensionMode.DeathRewind) {
                    _mode = mode;
                    _exclude = null;
                }
                IsActive = true;
                Sweep(tree);
            }

            public void Refresh(SceneTree tree) {
                if (IsActive) Sweep(tree);
            }

            public void AdoptParked(Dictionary<Node, ProcessModeEnum> parked) {
                if (parked == null) return;
                foreach (KeyValuePair<Node, ProcessModeEnum> entry in parked) {
                    if (entry.Key != null && !_parked.ContainsKey(entry.Key)) _parked[entry.Key] = entry.Value;
                }
            }

            private void Sweep(SceneTree tree) {
                if (tree == null) return;
                var rewindGroups = new HashSet<string>(FrozenSimulationGroups);
                foreach (string groupName in PostLandingHoldGroups) {
                    Godot.Collections.Array<Node> members = tree.GetNodesInGroup(groupName);
                    using var membersLifetime = members.AsDisposable();
                    foreach (Node node in members) {
                        if (node == null || !IsInstanceValid(node) || node == _exclude) continue;
                        if (_members.Contains(node)) continue;
                        bool preferRewind = _mode == WorldSuspensionMode.DeathRewind
                            && rewindGroups.Contains(groupName);
                        if (preferRewind && node is IStoryRewindSimulation rewindFirst) {
                            rewindFirst.SetStoryRewindFrozen(true);
                            _rewindFrozen.Add(rewindFirst);
                            _members.Add(node);
                        } else if (node is IStoryTimeFreezable freezable) {
                            freezable.SetTimeFrozen(true);
                            _timeFrozen.Add(freezable);
                            _members.Add(node);
                        } else if (node is IStoryRewindSimulation latchOnly) {
                            latchOnly.SetStoryRewindFrozen(true);
                            _rewindFrozen.Add(latchOnly);
                            _members.Add(node);
                        }
                    }
                }
                foreach (string groupName in TimeFreezeController.ProcessFallbackGroups) {
                    // The death rewind releases enemy projectiles to the pool on
                    // initiation; parking one mid-release would restore a pooled
                    // node to life at the thaw.
                    if (_mode == WorldSuspensionMode.DeathRewind && groupName == "enemy_projectile") continue;
                    Godot.Collections.Array<Node> members = tree.GetNodesInGroup(groupName);
                    using var membersLifetime = members.AsDisposable();
                    foreach (Node node in members) {
                        if (node == null || !IsInstanceValid(node)) continue;
                        if (node is IStoryTimeFreezable || node is IStoryRewindSimulation) continue;
                        if (_parked.ContainsKey(node)) continue;
                        // Already disabled means pooled-inactive: leave it to its pool.
                        if (node.ProcessMode == ProcessModeEnum.Disabled) continue;
                        _parked[node] = node.ProcessMode;
                        node.SetProcessModeSafe(ProcessModeEnum.Disabled);
                    }
                }
            }

            public void Release() {
                foreach (IStoryRewindSimulation simulation in _rewindFrozen) {
                    if (simulation is GodotObject godotObject && !IsInstanceValid(godotObject)) continue;
                    simulation.SetStoryRewindFrozen(false);
                }
                foreach (IStoryTimeFreezable freezable in _timeFrozen) {
                    if (freezable is GodotObject godotObject && !IsInstanceValid(godotObject)) continue;
                    freezable.SetTimeFrozen(false);
                }
                foreach (KeyValuePair<Node, ProcessModeEnum> entry in _parked) {
                    if (entry.Key == null || !IsInstanceValid(entry.Key)) continue;
                    entry.Key.SetProcessModeSafe(entry.Value);
                }
                _rewindFrozen.Clear();
                _timeFrozen.Clear();
                _parked.Clear();
                _members.Clear();
                _exclude = null;
                IsActive = false;
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
