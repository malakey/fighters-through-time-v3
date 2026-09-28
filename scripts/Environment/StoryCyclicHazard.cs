using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class StoryCyclicHazard : Area2D, IStoryRewindable, IStoryTimeFreezable {
        [Export] public string HazardID = "";
        [Export] public float WarningDuration = 1f;
        [Export] public float ActiveDuration = 1f;
        [Export] public float CooldownDuration = 2f;
        [Export] public int Damage = 15;
        [Export] public Vector2 Knockback = new(240f, -180f);
        [Export] public bool Enabled = true;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private readonly HashSet<ulong> _hitThisCycle = new();
        private HazardPhase _checkpointPhase = HazardPhase.Cooldown;
        private float _checkpointTimer;
        private float _timer;
        public HazardPhase Phase { get; private set; } = HazardPhase.Cooldown;

        public override void _Ready() {
            AddToGroup("story_hazard");
            _timer = CooldownDuration;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyPresentation();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            if (!Enabled) return;
            _timer -= (float)delta;
            if (_timer <= 0f) AdvancePhase();
            if (Phase != HazardPhase.Active) return;
            Godot.Collections.Array<Node2D> bodies = GetOverlappingBodies();
            using var bodiesLifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body is PlayerController player) ApplyToPlayer(player);
            }
        }

        public bool ApplyToPlayer(PlayerController player) {
            if (player == null || Phase != HazardPhase.Active) return false;
            // Package 12 W8 (M10): a player sheltered by one of this hazard's own
            // VolleyCover children takes nothing, and the cycle's hit is not spent —
            // stepping out of cover mid-volley is still a hit.
            if (IsSheltered(player.GlobalPosition)) return false;
            if (!_hitThisCycle.Add(player.GetInstanceId())) return false;
            // V7.3: environmental chokepoint — Defy flag consumption, Rally
            // echo, victim meter (never raw ApplyDamage).
            player.ApplyEnvironmentalDamage(Damage);
            float direction = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
            player.Velocity += new Vector2(Knockback.X * direction, Knockback.Y);
            return true;
        }

        // === Volley cover (Package 12 W8, M10) =================================

        private readonly List<VolleyCover> _covers = new();

        /// <summary>The <see cref="VolleyCover"/> children currently sheltering from this hazard.</summary>
        public IReadOnlyList<VolleyCover> Covers => _covers;

        internal void RegisterCover(VolleyCover cover) {
            if (cover != null && !_covers.Contains(cover)) _covers.Add(cover);
        }

        internal void UnregisterCover(VolleyCover cover) => _covers.Remove(cover);

        /// <summary>
        /// True when <paramref name="globalPoint"/> lies inside any of this hazard's
        /// own cover rectangles. Deterministic, area-based, no physics query.
        /// </summary>
        public bool IsSheltered(Vector2 globalPoint) {
            foreach (VolleyCover cover in _covers) {
                if (IsInstanceValid(cover) && cover.Shelters(globalPoint)) return true;
            }
            return false;
        }

        public void ForcePhase(HazardPhase phase, float duration) {
            Phase = phase;
            _timer = Mathf.Max(0f, duration);
            if (phase == HazardPhase.Active) _hitThisCycle.Clear();
            ApplyPresentation();
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = HazardID,
                Phase = Phase,
                Duration = _timer
            });
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointPhase = Phase;
            _checkpointTimer = _timer;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            if (RewindPolicy == StoryRewindPolicy.ResetToInitialState) ForcePhase(HazardPhase.Cooldown, CooldownDuration);
            else ForcePhase(_checkpointPhase, _checkpointTimer);
        }

        private void AdvancePhase() {
            switch (Phase) {
                case HazardPhase.Cooldown: ForcePhase(HazardPhase.Warning, WarningDuration); break;
                case HazardPhase.Warning: ForcePhase(HazardPhase.Active, ActiveDuration); break;
                case HazardPhase.Active: ForcePhase(HazardPhase.Cooldown, CooldownDuration); break;
            }
        }

        private void ApplyPresentation() {
            if (GetNodeOrNull<CanvasItem>("WarningVisual") is CanvasItem warning) warning.Visible = Phase == HazardPhase.Warning;
            if (GetNodeOrNull<CanvasItem>("ActiveVisual") is CanvasItem active) active.Visible = Phase == HazardPhase.Active;
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();

        // === IStoryTimeFreezable (V7.6 Time Freeze) ===========================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the world. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Stops simulating in place. Nothing else is mutated, so the phase, the
        /// timer and the position all survive and resume with no catch-up tick —
        /// the collision shape stays live throughout.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

    }
}
