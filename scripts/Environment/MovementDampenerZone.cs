using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Flat movement slow while the player is inside (Level 8 deep sand, snow drifts,
    /// mud). Mirrors the Chronal Rift zone's slow WITHOUT its Time-Loop Snap and
    /// without touching the status system: the multiplier goes through
    /// <see cref="EnvironmentPlayerModifiers"/> so it stacks with — and never evicts —
    /// a real TimeDilation/Root application.
    /// </summary>
    public partial class MovementDampenerZone : Area2D {
        [Signal] public delegate void PlayerDampenedEventHandler(int playerIndex, float multiplier);
        [Signal] public delegate void PlayerReleasedEventHandler(int playerIndex);

        [Export] public string ZoneID = "";
        [Export(PropertyHint.Range, "0.05,1,0.01")] public float MoveMultiplier = 0.5f;
        [Export] public bool Enabled = true;

        private readonly HashSet<PlayerController> _inside = new();

        public int TrackedPlayerCount => _inside.Count;

        public override void _Ready() {
            AddToGroup("story_hazard");
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEntered;
            BodyExited -= OnBodyExited;
            EnvironmentPlayerModifiers.ClearSource(this);
            _inside.Clear();
        }

        public bool AddPlayer(PlayerController player) {
            if (!Enabled || player == null || !_inside.Add(player)) return false;
            EnvironmentPlayerModifiers.SetMoveMultiplier(player, this, MoveMultiplier);
            EmitSignal(SignalName.PlayerDampened, player.PlayerIndex, MoveMultiplier);
            return true;
        }

        public bool RemovePlayer(PlayerController player) {
            if (player == null || !_inside.Remove(player)) return false;
            EnvironmentPlayerModifiers.ClearMoveMultiplier(player, this);
            if (GodotObject.IsInstanceValid(player)) EmitSignal(SignalName.PlayerReleased, player.PlayerIndex);
            return true;
        }

        /// <summary>Re-applies the current multiplier to everyone inside after a runtime tuning change.</summary>
        public void RefreshMultiplier() {
            foreach (PlayerController player in _inside) {
                EnvironmentPlayerModifiers.SetMoveMultiplier(player, this, MoveMultiplier);
            }
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) AddPlayer(player); }
        private void OnBodyExited(Node2D body) { if (body is PlayerController player) RemovePlayer(player); }
    }
}
