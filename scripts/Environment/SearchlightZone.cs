using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>Paris drains the Ultimate meter; Berlin fires a delayed strike.</summary>
    public enum SearchlightMode { UltimateDrain, DelayedStrike }

    /// <summary>Arc sweeps rotate the beam about the node; Path sweeps slide it along an axis.</summary>
    public enum SearchlightSweepMode { Arc, Path }

    /// <summary>
    /// Sweeping detection beam (Level 4 Paris corridors, Level 9 Berlin stealth
    /// sections). Detection is Player-layer only; the beam itself never blocks
    /// movement.
    /// </summary>
    public partial class SearchlightZone : Area2D, IStoryRewindable, IStoryTimeFreezable {
        [Signal] public delegate void PlayerDetectedEventHandler(int playerIndex);
        [Signal] public delegate void PlayerLostEventHandler(int playerIndex);
        [Signal] public delegate void StrikeTriggeredEventHandler(int playerIndex, int damage);

        [Export] public string SearchlightID = "";
        [Export] public SearchlightMode Mode = SearchlightMode.UltimateDrain;
        [Export] public SearchlightSweepMode SweepMode = SearchlightSweepMode.Arc;
        [Export(PropertyHint.Range, "0.1,60,0.05")] public float SweepPeriodSeconds = 4f;
        [Export(PropertyHint.Range, "0,180,1")] public float SweepArcDegrees = 60f;
        /// <summary>Half-extent of a Path sweep: the node slides between -extent and +extent.</summary>
        [Export] public Vector2 SweepPathExtent = new(320f, 0f);
        [Export(PropertyHint.Range, "0,100,0.5")] public float UltimateDrainPerSecond = 5f;
        [Export(PropertyHint.Range, "0,30,0.05")] public float ExposureGraceSeconds = 1.5f;
        [Export(PropertyHint.Range, "0,500,1")] public int StrikeDamage = 20;
        [Export] public Vector2 StrikeKnockback = new(220f, -180f);
        [Export] public bool Enabled = true;

        /// <summary>Translation key for the template's placeholder sign; blank hides it.</summary>
        [Export] public string LabelKey = "toolkit_searchlight";
        [Export] public NodePath LabelPath = ToolkitLabel.DefaultLabelPath;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private readonly Dictionary<PlayerController, float> _exposure = new();
        private Vector2 _baseOffset;
        private float _baseRotationDegrees;
        private float _checkpointPhase;

        /// <summary>Normalized 0-1 sweep position; 0.25 is one extreme, 0.75 the other.</summary>
        public float SweepPhase { get; private set; }
        public int TrackedPlayerCount => _exposure.Count;
        public int StrikeCount { get; private set; }

        public override void _Ready() {
            AddToGroup("story_hazard");
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            _baseOffset = Position;
            ToolkitLabel.Apply(this, LabelPath, LabelKey);
            _baseRotationDegrees = RotationDegrees;
            ApplySweepTransform();
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEntered;
            BodyExited -= OnBodyExited;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
            _exposure.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            if (!Enabled) return;
            float dt = (float)delta;
            AdvanceSweep(dt);
            TickExposure(dt);
        }

        public void AdvanceSweep(float dt) {
            SweepPhase = Mathf.PosMod(SweepPhase + dt / Mathf.Max(0.01f, SweepPeriodSeconds), 1f);
            ApplySweepTransform();
        }

        public void TickExposure(float dt) {
            foreach (PlayerController player in new List<PlayerController>(_exposure.Keys)) {
                if (!GodotObject.IsInstanceValid(player)) { _exposure.Remove(player); continue; }
                // V7.6: the Stasis Echo was the only thing that ever occluded a
                // beam, so this is now always false. The check is kept so the
                // Level 4/9 call sites and any future occluder have a seam.
                if (IsBeamOccludedForPlayer(player)) {
                    _exposure[player] = 0f;
                    continue;
                }
                float elapsed = _exposure[player] + dt;
                _exposure[player] = elapsed;
                if (Mode == SearchlightMode.UltimateDrain) {
                    player.DrainUltimateMeter(UltimateDrainPerSecond * dt);
                } else if (elapsed >= ExposureGraceSeconds) {
                    TriggerStrike(player);
                }
            }
        }

        /// <summary>
        /// Whether anything blocks the beam between its origin and the player.
        ///
        /// <para><b>V7.6 ruling: always false.</b> The Stasis Echo was the only
        /// occluder the game ever had and it is retired with the manual rewind.
        /// The method survives (returning false) rather than being deleted so the
        /// Level 4 corridor and Level 9 beam call sites are untouched — neither
        /// level ever required occlusion to progress: both are timing/route
        /// puzzles with <see cref="Enabled"/> kill switches.</para>
        /// </summary>
        public bool IsBeamOccludedForPlayer(PlayerController player) {
            _ = player;
            return false;
        }

        public bool AddPlayer(PlayerController player) {
            if (player == null || _exposure.ContainsKey(player)) return false;
            _exposure[player] = 0f;
            EmitSignal(SignalName.PlayerDetected, player.PlayerIndex);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = SearchlightID,
                Phase = HazardPhase.Warning,
                Duration = ExposureGraceSeconds
            });
            return true;
        }

        public bool RemovePlayer(PlayerController player) {
            if (player == null || !_exposure.Remove(player)) return false;
            EmitSignal(SignalName.PlayerLost, player.PlayerIndex);
            return true;
        }

        public float ExposureFor(PlayerController player) =>
            player != null && _exposure.TryGetValue(player, out float elapsed) ? elapsed : 0f;

        public int TriggerStrike(PlayerController player) {
            if (player == null) return 0;
            // V7.3: environmental chokepoint — Defy/echo/meter accounting.
            int applied = player.ApplyEnvironmentalDamage(StrikeDamage);
            float direction = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
            player.Velocity += new Vector2(StrikeKnockback.X * direction, StrikeKnockback.Y);
            if (_exposure.ContainsKey(player)) _exposure[player] = 0f;
            StrikeCount++;
            EmitSignal(SignalName.StrikeTriggered, player.PlayerIndex, applied);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = SearchlightID,
                Phase = HazardPhase.Active,
                Duration = 0f
            });
            return applied;
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointPhase = SweepPhase;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SweepPhase = RewindPolicy == StoryRewindPolicy.ResetToInitialState ? 0f : _checkpointPhase;
            foreach (PlayerController player in new List<PlayerController>(_exposure.Keys)) _exposure[player] = 0f;
            ApplySweepTransform();
        }

        private void ApplySweepTransform() {
            float swing = Mathf.Sin(SweepPhase * Mathf.Tau);
            if (SweepMode == SearchlightSweepMode.Arc) {
                RotationDegrees = _baseRotationDegrees + swing * SweepArcDegrees * 0.5f;
            } else {
                Position = _baseOffset + SweepPathExtent * swing;
            }
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) AddPlayer(player); }
        private void OnBodyExited(Node2D body) { if (body is PlayerController player) RemovePlayer(player); }
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
