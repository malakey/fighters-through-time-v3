using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class PressurePlate : Area2D {
        [Signal] public delegate void WeightChangedEventHandler(float currentWeight, bool thresholdReached);

        [Export] public string PlateID = "";
        [Export(PropertyHint.Range, "0.1,100,0.1")] public float RequiredWeight = 1f;
        [Export(PropertyHint.Range, "0.1,100,0.1")] public float PlayerWeight = 1f;
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "weight_threshold";

        private readonly Dictionary<ulong, float> _loads = new();
        public float CurrentWeight { get; private set; }
        public bool IsPressed => CurrentWeight + 0.001f >= RequiredWeight;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            BodyEntered += OnBodyEnteredSignal;
            BodyExited += OnBodyExitedSignal;
            ApplyPresentation();
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEnteredSignal;
            BodyExited -= OnBodyExitedSignal;
        }

        // Signal wrappers: a weight change can flip puzzle conditions that
        // toggle collision shapes (trapdoors, barriers), which the engine
        // blocks during the in/out flush these signals run in. The guard makes
        // those toggles defer; direct RegisterBody/UnregisterBody calls (tests)
        // stay synchronous.
        private void OnBodyEnteredSignal(Node2D body) {
            using var scope = PhysicsCallbackGuard.Enter();
            RegisterBody(body);
        }

        private void OnBodyExitedSignal(Node2D body) {
            using var scope = PhysicsCallbackGuard.Enter();
            UnregisterBody(body);
        }

        public void RegisterBody(Node2D body) {
            if (body == null) return;
            float weight = body switch {
                WeightedObject weighted => Mathf.Max(0f, weighted.WeightUnits),
                PlayerController => PlayerWeight,
                // V7.6: the Stasis Echo arm went with the retired manual rewind.
                // Plates are weighted by the player and by authored weights only.
                _ => 0f
            };
            if (weight <= 0f) return;
            _loads[body.GetInstanceId()] = weight;
            Recalculate();
        }

        public void UnregisterBody(Node2D body) {
            if (body == null || !_loads.Remove(body.GetInstanceId())) return;
            Recalculate();
        }

        private void Recalculate() {
            CurrentWeight = 0f;
            foreach (float weight in _loads.Values) CurrentWeight += weight;
            bool pressed = IsPressed;
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, pressed);
            ApplyPresentation();
            EmitSignal(SignalName.WeightChanged, CurrentWeight, pressed);
        }

        private void ApplyPresentation() {
            if (GetNodeOrNull<Node2D>("Visual") is Node2D visual) {
                visual.Position = new Vector2(visual.Position.X, IsPressed ? 5f : 0f);
            }
        }
    }
}
