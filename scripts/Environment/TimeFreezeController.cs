using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7.6 <b>Time Freeze</b> (F03) — the Story-only escape ability that replaced
    /// the retired V7.2/V7.3 manual rewind, its 12 s cooldown and the Stasis Echo.
    ///
    /// <para>One press of <c>gameplay_time_freeze</c> stops the world for five
    /// seconds while the player keeps running, jumping and rolling. It costs no
    /// charge, no meter and no dust; it is legal at zero death-rewind charges; and
    /// Suppression deliberately does not lock it — it is the universal escape
    /// tool. The 45-second cooldown arms when the freeze <b>ends</b>, including an
    /// early end, and is identical on all three difficulties.</para>
    ///
    /// <para><b>Escape, never advance.</b> Enemies and bosses are invulnerable for
    /// the whole freeze (the gate lives at the shared hit chokepoint in
    /// <c>Hitbox.OnAreaEntered</c>); attacks, grabs, specials, the ultimate, Echo
    /// Step and the movement ability are refused and <i>discarded</i> rather than
    /// buffered; and no pickup, checkpoint, puzzle or objective can be progressed.
    /// Time Freeze may never be a required progression gate.</para>
    ///
    /// <para><b>Clocks.</b> The node runs at <see cref="Node.ProcessModeEnum.Inherit"/>
    /// so <see cref="SceneTree.Paused"/> stops the duration and the cooldown for
    /// free — this controller never writes the pause itself (CLAUDE.md failure
    /// signature 4). Timeline Integrity is the one clock that keeps running: the
    /// freeze sweep deliberately omits <c>chronal_extractor</c>.</para>
    /// </summary>
    public partial class TimeFreezeController : Node {

        /// <summary>Authored freeze duration: 5.000 s.</summary>
        public const float FreezeSeconds = 5f;

        /// <summary>The same duration in 60 Hz physics ticks.</summary>
        public const int FreezeFrames = 300;

        /// <summary>Cooldown armed at thaw. Identical on Easy, Normal and Hard.</summary>
        public const float CooldownSeconds = 45f;

        /// <summary>Scene-tree group the HUD and the save path resolve the live controller through.</summary>
        public const string ControllerGroup = "time_freeze_controller";

        // === HUD string contract (consumed by the Story HUD indicator) ========
        //
        // The keys live here rather than in the HUD so the indicator's three
        // states and its thaw warning have exactly one authoritative naming, and
        // so a rename is a compile error instead of a silent raw-key render. The
        // design forbids attaching any of this to the death-rewind counter.

        /// <summary>Indicator label while the ability is Ready. No format argument.</summary>
        public const string ReadyLabelKey = "hud_time_freeze_ready";

        /// <summary>Indicator label during the freeze. <c>{0}</c> = whole seconds left.</summary>
        public const string ActiveLabelKey = "hud_time_freeze_active";

        /// <summary>Indicator label during the cooldown. <c>{0}</c> = whole seconds left.</summary>
        public const string CooldownLabelKey = "hud_time_freeze_cooldown";

        /// <summary>Brief thaw warning, shown as the freeze runs out.</summary>
        public const string ThawWarningLabelKey = "hud_time_freeze_thaw";

        /// <summary>Seconds of freeze left at which the thaw warning shows.</summary>
        public const float ThawWarningSeconds = 1f;

        /// <summary>
        /// Groups swept for <see cref="IStoryTimeFreezable"/> members when the
        /// world stops.
        ///
        /// <para><b>Two deliberate differences from
        /// <c>ChronalRewindManager.FrozenSimulationGroups</c>.</b> (i)
        /// <c>chronal_extractor</c> is <b>absent</b>: Timeline Integrity keeps
        /// draining at its current normalized rate across the whole five seconds,
        /// and travel during a freeze counts as live route time. (ii) Nothing is
        /// cleared — enemy projectiles freeze in place instead of being released
        /// to the pool, which is what "no projectile clearing" on thaw means.</para>
        /// </summary>
        internal static readonly string[] FrozenTimeGroups = {
            "Enemies",
            "persistent_construct",
            "story_hazard",
            "puzzle_object",
            "story_loot",
            "rescuable_npc",
            FreezableGroup,
            FTT.Enemies.MirrorParadoxController.MirrorGroup
        };

        /// <summary>
        /// Generic opt-in group for an <see cref="IStoryTimeFreezable"/> that has
        /// no natural simulation group of its own (the Restoration Font is the
        /// first member). Joining it is all a new freezable needs to do.
        /// </summary>
        public const string FreezableGroup = "time_freezable";

        /// <summary>
        /// Pooled, non-supporting nodes with no explicit freeze hook: both sides'
        /// projectiles and the placeholder zones. These take the
        /// <see cref="Node.ProcessModeEnum.Disabled"/> fallback, which also parks
        /// their collision — exactly the "frozen projectiles cannot damage the
        /// player" rule. Nothing in these groups is ever a floor, so disabling
        /// them cannot strand the player.
        /// </summary>
        internal static readonly string[] ProcessFallbackGroups = {
            "story_projectile",
            "enemy_projectile",
            "story_zone"
        };

        private bool _isFrozen;
        private int _freezeFramesRemaining;
        private float _cooldownRemaining;
        private PlayerController _player;
        private ChronalRewindManager _rewindManager;
        private readonly List<IStoryTimeFreezable> _frozen = new();
        private readonly Dictionary<Node, ProcessModeEnum> _processParked = new();
        private TimeFreezeState _publishedState = TimeFreezeState.Ready;
        private int _publishedSeconds = -1;

        /// <summary>True while the world is stopped. Read by the hit chokepoint and the HUD.</summary>
        public bool IsFrozen => _isFrozen;

        /// <summary>Seconds of freeze left; 0 when not frozen.</summary>
        public float FreezeSecondsRemaining => _freezeFramesRemaining / 60f;

        /// <summary>Physics ticks of freeze left. Test seam.</summary>
        public int FreezeFramesRemaining => _freezeFramesRemaining;

        /// <summary>Seconds before the ability is available again; 0 when ready.</summary>
        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>True when a press would start a freeze (ignoring player state).</summary>
        public bool IsReady => !_isFrozen && (_cooldownRemaining <= 0f || DrillFreeFreeze);

        /// <summary>
        /// Level 0's escape drill sets this for the duration of the lesson: the
        /// freeze is ready on every retry so a failed attempt never strands the
        /// tutorial behind a 45-second wait. <b>Outside the drill there is no
        /// recharge exemption</b> — this is the only bypass in the game, and it
        /// is cleared when the drill completes.
        /// </summary>
        public bool DrillFreeFreeze { get; set; }

        /// <summary>
        /// The scene's dialogue manager, wired by <see cref="StorySceneBootstrapper"/>.
        /// A running sequence holds the cooldown and refuses activation; null in a
        /// bare test harness, which simply means "no dialogue is playing".
        /// </summary>
        public FTT.UI.DialogueManager Dialogue { get; set; }

        public override void _Ready() {
            AddToGroup(ControllerGroup);
            // Inherit, never Always: a paused tree must stop both clocks.
            ProcessMode = ProcessModeEnum.Inherit;
            _cooldownRemaining = Mathf.Max(0f, StoryManager.Instance?.TimeFreezeCooldownRemaining ?? 0f);
        }

        public override void _ExitTree() {
            // Level exit / scene transition ends the freeze cleanly. The world is
            // being torn down, so the resume sweep is best-effort only.
            if (_isFrozen) EndFreeze(early: true);
        }

        public override void _PhysicsProcess(double delta) {
            ResolvePlayer();
            if (_isFrozen) {
                AdvanceFreeze();
                return;
            }
            if (_cooldownRemaining > 0f && !IsPresentationHolding()) {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - (float)delta);
            }
            PushCooldownToCampaignState();
            PublishStateIfChanged();
            if (Input.IsActionJustPressed(InputManager.Actions.TimeFreeze)) TryBeginTimeFreeze();
        }

        /// <summary>
        /// <b>The public entry point.</b> Starts a five-second freeze when the
        /// player is eligible; returns false and changes nothing otherwise. Level
        /// 0's calibration drill calls exactly this name.
        /// </summary>
        public bool TryBeginTimeFreeze() {
            ResolvePlayer();
            if (IsPresentationHolding()) return false;
            CharacterState state = _player != null && IsInstanceValid(_player)
                ? _player.CurrentState
                : CharacterState.Idle;
            // A harness with no player in the tree reads as a living idle player,
            // the same headless tolerance SearchlightZone's occlusion ray takes.
            bool alive = _player == null || !IsInstanceValid(_player) || _player.CurrentHP > 0;
            if (!CanActivate(state, _isFrozen, DrillFreeFreeze ? 0f : _cooldownRemaining, alive)) return false;

            _isFrozen = true;
            _freezeFramesRemaining = FreezeFrames;
            FreezeWorld();
            if (_player != null && IsInstanceValid(_player)) _player.TimeFrozen = true;
            // F03 save rule: activation commits the conservative full cooldown for
            // ANY reload during this activation. Background autosaves keep that
            // value while the freeze is still live; only thaw writes the truth.
            StoryManager.Instance?.SetTimeFreezeCooldown(CooldownSeconds);
            PublishState(TimeFreezeState.Active, FreezeSeconds, force: true);
            return true;
        }

        /// <summary>
        /// Ends the freeze and arms the 45-second cooldown. <paramref name="early"/>
        /// records a death / save / exit end rather than the clock running out;
        /// both arm the cooldown identically.
        /// </summary>
        internal void EndFreeze(bool early) {
            if (!_isFrozen) return;
            _isFrozen = false;
            _freezeFramesRemaining = 0;
            ResumeWorld();
            if (_player != null && IsInstanceValid(_player)) _player.TimeFrozen = false;
            _cooldownRemaining = DrillFreeFreeze ? 0f : CooldownSeconds;
            PushCooldownToCampaignState();
            PublishState(
                _cooldownRemaining > 0f ? TimeFreezeState.Cooldown : TimeFreezeState.Ready,
                _cooldownRemaining,
                force: true);
            _ = early;
        }

        /// <summary>
        /// F03 / <c>STORY_PERSISTENCE.md</c>: an explicit Save or an exit during a
        /// live freeze ends the freeze and stores the full 45 s. A background
        /// autosave (checkpoint, collapse) must <b>not</b> call this — it keeps the
        /// conservative value while the freeze continues.
        /// </summary>
        public void EndFreezeForExplicitSave() {
            if (!_isFrozen) return;
            // The drill exemption never survives an explicit save: the stored
            // cooldown must be the conservative 45 s.
            DrillFreeFreeze = false;
            EndFreeze(early: true);
            StoryManager.Instance?.SetTimeFreezeCooldown(CooldownSeconds);
        }

        /// <summary>
        /// The pure eligibility rule: a living, controllable player, off cooldown,
        /// not already frozen. Refused during hitstun, daze, grabs, attack/ability
        /// execution, death and respawn.
        ///
        /// <para><b>Suppression is deliberately not a parameter.</b> The design
        /// carves Time Freeze out of the Suppression ability lock explicitly — it
        /// is the escape from exactly the situation Suppression creates.</para>
        /// </summary>
        public static bool CanActivate(CharacterState state, bool isFrozen, float cooldownRemaining, bool alive) {
            if (isFrozen || !alive || cooldownRemaining > 0f) return false;
            return state is not (CharacterState.Stunned or CharacterState.Dazed
                or CharacterState.Dead or CharacterState.Respawning
                or CharacterState.Attacking or CharacterState.UsingSpecial
                or CharacterState.UsingUltimate or CharacterState.UsingMovementAbility
                or CharacterState.Grabbing);
        }

        private void AdvanceFreeze() {
            if (_player != null && IsInstanceValid(_player)
                && _player.CurrentState is CharacterState.Dead or CharacterState.Respawning) {
                EndFreeze(early: true);
                return;
            }
            _freezeFramesRemaining--;
            if (_freezeFramesRemaining <= 0) {
                EndFreeze(early: false);
                return;
            }
            // While the freeze is live the stored cooldown stays conservative.
            StoryManager.Instance?.SetTimeFreezeCooldown(CooldownSeconds);
            PublishStateIfChanged();
        }

        /// <summary>
        /// True while a presentation owns the world: a death rewind, the collapse
        /// beat, or a running dialogue sequence. The cooldown does not advance and
        /// a freeze cannot start during any of them. (The pause menu is covered by
        /// <see cref="Node.ProcessModeEnum.Inherit"/>.)
        /// </summary>
        private bool IsPresentationHolding() {
            if (!IsInsideTree()) return false;
            if (_rewindManager == null || !IsInstanceValid(_rewindManager)) {
                _rewindManager = GetTree()?.GetFirstNodeInGroup(ChronalRewindManager.ManagerGroup)
                    as ChronalRewindManager;
            }
            if (_rewindManager != null && IsInstanceValid(_rewindManager)
                && (_rewindManager.IsRewinding || _rewindManager.IsCollapseBeatActive)) return true;
            return Dialogue != null && IsInstanceValid(Dialogue) && Dialogue.IsSequenceActive;
        }

        private void ResolvePlayer() {
            if (_player != null && IsInstanceValid(_player)) return;
            if (!IsInsideTree()) return;
            _player = GetTree().GetFirstNodeInGroup("StoryPlayer") as PlayerController;
        }

        private void PushCooldownToCampaignState() =>
            StoryManager.Instance?.SetTimeFreezeCooldown(_isFrozen ? CooldownSeconds : _cooldownRemaining);

        // === World freeze =====================================================

        private void FreezeWorld() {
            _frozen.Clear();
            if (!IsInsideTree()) return;
            foreach (string groupName in FrozenTimeGroups) {
                Godot.Collections.Array<Node> members = GetTree().GetNodesInGroup(groupName);
                using var membersLifetime = members.AsDisposable();
                foreach (Node node in members) {
                    if (node is IStoryTimeFreezable freezable && !_frozen.Contains(freezable)) {
                        freezable.SetTimeFrozen(true);
                        _frozen.Add(freezable);
                    }
                }
            }
            ParkProcessFallbackMembers();
        }

        /// <summary>
        /// Freezes whatever has entered the tree since the sweep ran — a room
        /// revealed during an active freeze is frozen <b>before</b> its actors can
        /// take a tick. Idempotent: already-frozen members are skipped.
        /// </summary>
        internal void RefreshFreeze() {
            if (!_isFrozen) return;
            FreezeWorld();
        }

        /// <summary>
        /// A room activated mid-freeze must not let its encounter act. Called by
        /// <see cref="RoomTransitionTrigger.ActivateRoom"/> right after the
        /// encounter root is enabled and before anything has processed.
        /// </summary>
        internal static void FreezeActivatedRoom(SceneTree tree) {
            if (tree?.GetFirstNodeInGroup(ControllerGroup) is TimeFreezeController controller
                && GodotObject.IsInstanceValid(controller)) {
                controller.RefreshFreeze();
            }
        }

        private void ParkProcessFallbackMembers() {
            foreach (string groupName in ProcessFallbackGroups) {
                Godot.Collections.Array<Node> members = GetTree().GetNodesInGroup(groupName);
                using var membersLifetime = members.AsDisposable();
                foreach (Node node in members) {
                    if (node == null || !IsInstanceValid(node)) continue;
                    if (node is IStoryTimeFreezable) continue;
                    if (_processParked.ContainsKey(node)) continue;
                    _processParked[node] = node.ProcessMode;
                    // A hit resolution can cascade here mid-flush; the engine
                    // rejects a ProcessMode write on a collision-bearing subtree
                    // during the in/out signal flush, so it must defer.
                    node.SetProcessModeSafe(ProcessModeEnum.Disabled);
                }
            }
        }

        private void ResumeWorld() {
            foreach (IStoryTimeFreezable freezable in _frozen) {
                if (freezable is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject)) continue;
                freezable.SetTimeFrozen(false);
            }
            _frozen.Clear();
            foreach (KeyValuePair<Node, ProcessModeEnum> entry in _processParked) {
                if (entry.Key == null || !IsInstanceValid(entry.Key)) continue;
                entry.Key.SetProcessModeSafe(entry.Value);
            }
            _processParked.Clear();
        }

        // === Presentation =====================================================

        private void PublishStateIfChanged() {
            TimeFreezeState state = _isFrozen
                ? TimeFreezeState.Active
                : _cooldownRemaining > 0f ? TimeFreezeState.Cooldown : TimeFreezeState.Ready;
            float seconds = _isFrozen ? FreezeSecondsRemaining : _cooldownRemaining;
            int whole = Mathf.CeilToInt(seconds);
            if (state == _publishedState && whole == _publishedSeconds) return;
            PublishState(state, seconds, force: true);
        }

        private void PublishState(TimeFreezeState state, float seconds, bool force) {
            if (!force && state == _publishedState) return;
            _publishedState = state;
            _publishedSeconds = Mathf.CeilToInt(seconds);
            EventBus.Instance?.RaiseTimeFreezeStateChanged(new TimeFreezePayload {
                State = state,
                SecondsRemaining = Mathf.Max(0f, seconds)
            });
        }
    }
}
