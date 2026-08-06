using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Environment {

    public partial class PuzzleManager : Node, IStoryRewindable {
        [Signal] public delegate void PuzzleCompletedEventHandler(string puzzleID);
        [Signal] public delegate void PuzzleResetEventHandler(string puzzleID);
        [Signal] public delegate void ConditionChangedEventHandler(string conditionID, bool isSatisfied);

        [Export] public string PuzzleID = "";
        [Export] public string[] PrerequisitePuzzleIDs = Array.Empty<string>();
        [Export] public string[] RequiredConditionIDs = Array.Empty<string>();
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;
        [Export] public bool PersistCompletionToSave = true;

        private readonly Dictionary<string, bool> _conditions = new(StringComparer.Ordinal);
        private bool _checkpointCompleted;
        private Dictionary<string, bool> _checkpointConditions = new(StringComparer.Ordinal);

        public bool IsCompleted { get; private set; }

        public override void _Ready() {
            if (string.IsNullOrWhiteSpace(PuzzleID)) {
                GD.PushError($"PuzzleManager at '{GetPath()}' requires a stable PuzzleID.");
            }
            AddToGroup("puzzle_manager");
            foreach (string conditionID in RequiredConditionIDs) {
                if (!string.IsNullOrWhiteSpace(conditionID)) _conditions.TryAdd(conditionID, false);
            }
            if (PersistCompletionToSave && SaveManager.Instance?.IsPuzzleCompleted(PuzzleID) == true) {
                IsCompleted = true;
            }
            _checkpointCompleted = IsCompleted;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
            }
        }

        public bool SetCondition(string conditionID, bool isSatisfied) {
            if (string.IsNullOrWhiteSpace(conditionID)) return false;
            if (_conditions.TryGetValue(conditionID, out bool existing) && existing == isSatisfied) return false;
            _conditions[conditionID] = isSatisfied;
            EmitSignal(SignalName.ConditionChanged, conditionID, isSatisfied);
            if (!IsCompleted && AllConditionsSatisfied()) TryComplete("conditions");
            return true;
        }

        public bool IsConditionSatisfied(string conditionID) =>
            !string.IsNullOrWhiteSpace(conditionID) && _conditions.GetValueOrDefault(conditionID);

        public bool TryComplete(string reason = "manual") {
            if (IsCompleted || !PrerequisitesSatisfied() || !AllConditionsSatisfied()) return false;
            IsCompleted = true;
            if (PersistCompletionToSave) SaveManager.Instance?.SetPuzzleCompleted(PuzzleID, true);
            EmitSignal(SignalName.PuzzleCompleted, PuzzleID);
            Publish(reason);
            return true;
        }

        public void ResetPuzzle(string reason = "reset") {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState && reason == "rewind") return;
            IsCompleted = false;
            foreach (string key in new List<string>(_conditions.Keys)) _conditions[key] = false;
            if (PersistCompletionToSave) SaveManager.Instance?.SetPuzzleCompleted(PuzzleID, false);
            EmitSignal(SignalName.PuzzleReset, PuzzleID);
            Publish(reason);
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointCompleted = IsCompleted;
            _checkpointConditions = new Dictionary<string, bool>(_conditions, StringComparer.Ordinal);
        }

        public void ApplyStoryRewind() {
            switch (RewindPolicy) {
                case StoryRewindPolicy.PreserveCurrentState:
                    return;
                case StoryRewindPolicy.ResetToInitialState:
                    ResetPuzzle("rewind");
                    return;
                case StoryRewindPolicy.RestoreCheckpointState:
                    IsCompleted = _checkpointCompleted;
                    _conditions.Clear();
                    foreach ((string id, bool satisfied) in _checkpointConditions) _conditions[id] = satisfied;
                    if (PersistCompletionToSave) SaveManager.Instance?.SetPuzzleCompleted(PuzzleID, IsCompleted);
                    if (IsCompleted) EmitSignal(SignalName.PuzzleCompleted, PuzzleID);
                    else EmitSignal(SignalName.PuzzleReset, PuzzleID);
                    Publish("rewind_checkpoint");
                    return;
            }
        }

        public bool PrerequisitesSatisfied() {
            foreach (string prerequisiteID in PrerequisitePuzzleIDs) {
                if (string.IsNullOrWhiteSpace(prerequisiteID)) continue;
                bool complete = SaveManager.Instance?.IsPuzzleCompleted(prerequisiteID) == true;
                if (!complete && GetTree() != null) {
                    foreach (Node node in GetTree().GetNodesInGroup("puzzle_manager")) {
                        if (node is PuzzleManager manager && manager.PuzzleID == prerequisiteID) {
                            complete = manager.IsCompleted;
                            break;
                        }
                    }
                }
                if (!complete) return false;
            }
            return true;
        }

        private bool AllConditionsSatisfied() {
            foreach (string conditionID in RequiredConditionIDs) {
                if (!_conditions.GetValueOrDefault(conditionID)) return false;
            }
            return true;
        }

        private void OnRewindTriggered(Vector2 targetPosition) => ApplyStoryRewind();

        private void Publish(string reason) => EventBus.Instance?.RaisePuzzleStateChanged(new PuzzleStatePayload {
            PuzzleID = PuzzleID,
            IsCompleted = IsCompleted,
            Reason = reason ?? ""
        });
    }
}
