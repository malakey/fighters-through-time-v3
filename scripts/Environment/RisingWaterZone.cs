using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Flood zone for Level 5 Titanic (and the Tidal Eraser phase-2 arena hook).
    /// The water line sits at the node origin for step 0 and rises by the authored
    /// step heights. A player whose origin is at or below the line is submerged:
    /// slowed by <see cref="SubmergedMoveMultiplier"/> and taking drowning ticks
    /// after <see cref="DrownGraceSeconds"/>.
    ///
    /// The slow goes through <see cref="EnvironmentPlayerModifiers"/>, NOT the
    /// status system — terrain must not evict a real status effect.
    /// </summary>
    public partial class RisingWaterZone : Area2D, IStoryRewindable {
        [Signal] public delegate void WaterLevelChangedEventHandler(int step);
        [Signal] public delegate void PlayerSubmergedEventHandler(int playerIndex);
        [Signal] public delegate void PlayerSurfacedEventHandler(int playerIndex);
        [Signal] public delegate void DrownTickEventHandler(int playerIndex, int damage);

        [Export] public string WaterID = "";
        /// <summary>Water-line heights above the node origin, one per step. Step 0 must be present.</summary>
        [Export] public float[] StepHeights = { 0f, 180f, 360f, 540f };
        /// <summary>Seconds between automatic step advances. Zero keeps the zone manual.</summary>
        [Export(PropertyHint.Range, "0,600,0.1")] public float AutoAdvanceSeconds;
        [Export(PropertyHint.Range, "0.05,1,0.01")] public float SubmergedMoveMultiplier = 0.5f;
        [Export(PropertyHint.Range, "0,60,0.1")] public float DrownGraceSeconds = 3f;
        [Export(PropertyHint.Range, "0.05,10,0.05")] public float DrownTickSeconds = 1f;
        [Export(PropertyHint.Range, "0,500,1")] public int DrownDamage = 5;
        [Export] public NodePath WaterVisualPath = "WaterVisual";
        [Export] public float VisualWidth = 1920f;
        [Export] public float VisualDepthBelowLine = 720f;
        [Export] public bool Enabled = true;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private readonly HashSet<PlayerController> _tracked = new();
        private readonly Dictionary<PlayerController, float> _submergedTime = new();
        private readonly Dictionary<PlayerController, float> _tickTimer = new();
        private float _autoTimer;
        private int _checkpointStep;

        public int CurrentStep { get; private set; }
        public int StepCount => StepHeights?.Length ?? 0;
        public float CurrentWaterHeight =>
            StepHeights != null && StepHeights.Length > 0
                ? StepHeights[Mathf.Clamp(CurrentStep, 0, StepHeights.Length - 1)]
                : 0f;
        /// <summary>Global Y of the water line. Rising water means a smaller Y.</summary>
        public float WaterLineGlobalY => GlobalPosition.Y - CurrentWaterHeight;
        public int TrackedPlayerCount => _tracked.Count;

        public override void _Ready() {
            AddToGroup("story_hazard");
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            if (StepHeights == null || StepHeights.Length == 0) StepHeights = new[] { 0f };
            _autoTimer = AutoAdvanceSeconds;
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            RefreshVisual();
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEntered;
            BodyExited -= OnBodyExited;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
            EnvironmentPlayerModifiers.ClearSource(this);
            _tracked.Clear();
            _submergedTime.Clear();
            _tickTimer.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            if (!Enabled) return;
            float dt = (float)delta;
            if (AutoAdvanceSeconds > 0f && CurrentStep < StepCount - 1) {
                _autoTimer -= dt;
                if (_autoTimer <= 0f) {
                    _autoTimer = AutoAdvanceSeconds;
                    AdvanceWaterLevel();
                }
            }
            TickSubmersion(dt);
        }

        public void TickSubmersion(float dt) {
            foreach (PlayerController player in new List<PlayerController>(_tracked)) {
                if (!GodotObject.IsInstanceValid(player)) { ForgetPlayer(player); continue; }
                bool submerged = IsPlayerSubmerged(player);
                bool wasSubmerged = _submergedTime.ContainsKey(player);
                if (submerged && !wasSubmerged) {
                    _submergedTime[player] = 0f;
                    _tickTimer[player] = DrownTickSeconds;
                    EnvironmentPlayerModifiers.SetMoveMultiplier(player, this, SubmergedMoveMultiplier);
                    EmitSignal(SignalName.PlayerSubmerged, player.PlayerIndex);
                } else if (!submerged && wasSubmerged) {
                    Surface(player);
                    continue;
                }
                if (!submerged) continue;

                _submergedTime[player] += dt;
                if (_submergedTime[player] < DrownGraceSeconds) continue;
                _tickTimer[player] -= dt;
                if (_tickTimer[player] > 0f) continue;
                _tickTimer[player] = Mathf.Max(0.05f, DrownTickSeconds);
                int applied = player.ApplyDamage(DrownDamage);
                EmitSignal(SignalName.DrownTick, player.PlayerIndex, applied);
            }
        }

        public bool IsPlayerSubmerged(PlayerController player) =>
            player != null && GodotObject.IsInstanceValid(player) && player.GlobalPosition.Y >= WaterLineGlobalY;

        public bool AddPlayer(PlayerController player) {
            if (player == null || !_tracked.Add(player)) return false;
            return true;
        }

        public bool RemovePlayer(PlayerController player) {
            if (player == null || !_tracked.Remove(player)) return false;
            Surface(player);
            return true;
        }

        /// <summary>Raises the water line one authored step. Returns false at the top step.</summary>
        public bool AdvanceWaterLevel() {
            if (CurrentStep >= StepCount - 1) return false;
            SetWaterLevel(CurrentStep + 1);
            return true;
        }

        public void SetWaterLevel(int step) {
            int clamped = Mathf.Clamp(step, 0, Math.Max(0, StepCount - 1));
            if (clamped == CurrentStep) { RefreshVisual(); return; }
            CurrentStep = clamped;
            RefreshVisual();
            EmitSignal(SignalName.WaterLevelChanged, CurrentStep);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = WaterID,
                Phase = HazardPhase.Active,
                Duration = 0f
            });
        }

        public float SubmergedSecondsFor(PlayerController player) =>
            player != null && _submergedTime.TryGetValue(player, out float seconds) ? seconds : 0f;

        public void CaptureCheckpointState(string checkpointID) => _checkpointStep = CurrentStep;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SetWaterLevel(RewindPolicy == StoryRewindPolicy.ResetToInitialState ? 0 : _checkpointStep);
            foreach (PlayerController player in new List<PlayerController>(_submergedTime.Keys)) Surface(player);
        }

        private void Surface(PlayerController player) {
            _submergedTime.Remove(player);
            _tickTimer.Remove(player);
            EnvironmentPlayerModifiers.ClearMoveMultiplier(player, this);
            if (GodotObject.IsInstanceValid(player)) EmitSignal(SignalName.PlayerSurfaced, player.PlayerIndex);
        }

        private void ForgetPlayer(PlayerController player) {
            _tracked.Remove(player);
            _submergedTime.Remove(player);
            _tickTimer.Remove(player);
        }

        private void RefreshVisual() {
            if (GetNodeOrNull<Control>(WaterVisualPath) is not Control visual) return;
            float height = CurrentWaterHeight + VisualDepthBelowLine;
            visual.Position = new Vector2(-VisualWidth * 0.5f, -CurrentWaterHeight);
            visual.Size = new Vector2(VisualWidth, height);
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) AddPlayer(player); }
        private void OnBodyExited(Node2D body) { if (body is PlayerController player) RemovePlayer(player); }
        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
