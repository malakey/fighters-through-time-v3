using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;

namespace FTT.Environment {

    public partial class ChronalRiftZone : Area2D {
        [Signal] public delegate void TimeLoopSnappedEventHandler(int playerIndex, Vector2 destination);

        [Export] public string RiftID = "";
        [Export] public float SnapDelay = 2f;
        [Export] public int HistoryFramesAgo = 180;
        [Export] public int SnapDamage = 15;

        private readonly Dictionary<PlayerController, float> _insideDurations = new();
        private readonly Dictionary<PlayerController, Vector2> _entryPositions = new();

        public override void _Ready() {
            AddToGroup("story_hazard");
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEntered;
            BodyExited -= OnBodyExited;
            foreach (PlayerController player in new List<PlayerController>(_insideDurations.Keys)) RemovePlayer(player);
        }

        public override void _PhysicsProcess(double delta) {
            foreach (PlayerController player in new List<PlayerController>(_insideDurations.Keys)) {
                if (!GodotObject.IsInstanceValid(player)) { _insideDurations.Remove(player); _entryPositions.Remove(player); continue; }
                float elapsed = _insideDurations[player] + (float)delta;
                _insideDurations[player] = elapsed;
                if (elapsed >= SnapDelay) TriggerTimeLoopSnap(player);
            }
        }

        public void AddPlayer(PlayerController player) {
            if (player == null || _insideDurations.ContainsKey(player)) return;
            _insideDurations[player] = 0f;
            _entryPositions[player] = player.GlobalPosition;
            player.GetNodeOrNull<StatusController>("StatusController")?.ApplyStatus(StatusType.TimeDilation, 3600f, 1f);
        }

        public void RemovePlayer(PlayerController player) {
            if (player == null || !_insideDurations.Remove(player)) return;
            _entryPositions.Remove(player);
            player.GetNodeOrNull<StatusController>("StatusController")
                ?.ClearStatus(StatusType.TimeDilation);
        }

        public Vector2 TriggerTimeLoopSnap(PlayerController player) {
            Vector2 destination = _entryPositions.GetValueOrDefault(player, player.GlobalPosition);
            if (player.GetNodeOrNull<TemporalPositionHistory>("TemporalPositionHistory") is TemporalPositionHistory history) {
                history.TryGetFramesAgo(HistoryFramesAgo, out destination);
            }
            player.GlobalPosition = destination;
            player.Velocity = Vector2.Zero;
            player.ApplyDamage(SnapDamage);
            _insideDurations[player] = 0f;
            EmitSignal(SignalName.TimeLoopSnapped, player.PlayerIndex, destination);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = RiftID,
                Phase = HazardPhase.Active,
                Duration = 0f
            });
            return destination;
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) AddPlayer(player); }
        private void OnBodyExited(Node2D body) { if (body is PlayerController player) RemovePlayer(player); }
    }
}
