using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class StoryCyclicHazard : Area2D, IStoryRewindable {
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
            if (player == null || Phase != HazardPhase.Active || !_hitThisCycle.Add(player.GetInstanceId())) return false;
            player.ApplyDamage(Damage);
            float direction = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
            player.Velocity += new Vector2(Knockback.X * direction, Knockback.Y);
            return true;
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
    }
}
