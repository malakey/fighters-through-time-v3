using Godot;
using System.Collections.Generic;
using FTT.Characters;

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
            BodyEntered += RegisterBody;
            BodyExited += UnregisterBody;
            ApplyPresentation();
        }

        public override void _ExitTree() {
            BodyEntered -= RegisterBody;
            BodyExited -= UnregisterBody;
        }

        public void RegisterBody(Node2D body) {
            if (body == null) return;
            float weight = body switch {
                WeightedObject weighted => Mathf.Max(0f, weighted.WeightUnits),
                PlayerController => PlayerWeight,
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
