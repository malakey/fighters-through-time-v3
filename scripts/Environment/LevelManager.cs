using Godot;
using System.Collections.Generic;

namespace FTT.Environment {

    public partial class LevelManager : Node {
        [Export] public string LevelID = "";
        [Export] public string LevelDisplayName = "";

        private string _lastCheckpointID;
        public string LastCheckpointID => _lastCheckpointID ?? "";
        private Vector2 _lastCheckpointPosition;
        private List<string> _activatedCheckpoints = new();
        private readonly Dictionary<string, Vector2> _checkpointPositions = new();

        public override void _Ready() {
            FTT.Core.EventBus.Instance.OnCheckpointReached += OnCheckpointActivated;
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnCheckpointReached -= OnCheckpointActivated;
        }

        private void OnCheckpointActivated(string checkpointID) {
            if (!_activatedCheckpoints.Contains(checkpointID)) {
                _activatedCheckpoints.Add(checkpointID);
            }
            _lastCheckpointID = checkpointID;
        }

        public void SetCheckpointPosition(string id, Vector2 pos) {
            RegisterCheckpoint(id, pos);
            _lastCheckpointID = id;
            _lastCheckpointPosition = pos;
        }

        public void RegisterCheckpoint(string id, Vector2 position) {
            if (!string.IsNullOrWhiteSpace(id)) _checkpointPositions[id] = position;
        }

        public bool TryGetCheckpointPosition(string id, out Vector2 position) {
            position = default;
            return !string.IsNullOrWhiteSpace(id) && _checkpointPositions.TryGetValue(id, out position);
        }

        public Vector2 GetRespawnPosition() {
            return _lastCheckpointPosition;
        }

        public void CompleteLevel() {
            FTT.Core.EventBus.Instance?.RaiseLevelComplete(LevelID);
        }
    }

    public partial class CheckpointTrigger : Area2D {
        [Export] public string CheckpointID = "";
        [Export] public Vector2 RespawnOffset = new(0, -50);

        private bool _activated;

        public override void _Ready() {
            BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node2D body) {
            if (_activated) return;
            if (body is FTT.Characters.PlayerController) {
                _activated = true;
                var levelManager = GetTree().CurrentScene.GetNodeOrNull<LevelManager>("LevelManager");
                levelManager?.SetCheckpointPosition(CheckpointID, GlobalPosition + RespawnOffset);
                FTT.Core.EventBus.Instance?.RaiseCheckpointReached(CheckpointID);
            }
        }
    }
}
