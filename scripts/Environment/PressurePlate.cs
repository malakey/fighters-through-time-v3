using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// A weighted plate: the sum of the loads <see cref="RegisterBody"/> accepts.
    ///
    /// <para><b>Detection (G1, 2026-10-04).</b> The template's collision mask has to
    /// cover the player's body layer AND the layer the <see cref="WeightedObject"/>
    /// props live on — Environment since Package 12 W8 (M02) made the props solid.
    /// The mask was left on the props' old PersistentObject layer, so no prop ever
    /// entered a plate by physics and Pompeii's winch could never balance (only the
    /// west pan's authored load was registered, by hand). The plate sees every
    /// Environment body it touches, but only weighs what <see cref="RegisterBody"/>
    /// accepts: a floor, a wall or a moving platform weighs nothing.</para>
    /// </summary>
    public partial class PressurePlate : Area2D {
        [Signal] public delegate void WeightChangedEventHandler(float currentWeight, bool thresholdReached);

        [Export] public string PlateID = "";
        [Export(PropertyHint.Range, "0.1,100,0.1")] public float RequiredWeight = 1f;
        [Export(PropertyHint.Range, "0.1,100,0.1")] public float PlayerWeight = 1f;
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "weight_threshold";

        /// <summary>
        /// Package 12 W8 (V01c / GAP-08): the <see cref="WeightedObject.PuzzleOwnerID"/>s
        /// this plate accepts. <b>Empty keeps the pre-W8 behaviour</b> (any authored
        /// weight counts) for back-compat; non-empty rejects every prop owned by
        /// another puzzle, and every unowned prop, so one room's props cannot solve
        /// another room's plate.
        /// </summary>
        [Export] public string[] AcceptedPuzzleOwnerIDs = Array.Empty<string>();

        /// <summary>
        /// V01c's explicit player-occupancy opt-in. True (the default) keeps today's
        /// <see cref="PlayerWeight"/> contribution — Pompeii's winch is authored so the
        /// hero's own weight completes the east pan. Every eligible hero weighs the
        /// same <see cref="PlayerWeight"/>; combat character weight never counts.
        /// </summary>
        [Export] public bool AcceptsPlayerOccupancy = true;

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
                WeightedObject weighted => AcceptsProp(weighted) ? Mathf.Max(0f, weighted.WeightUnits) : 0f,
                PlayerController => AcceptsPlayerOccupancy ? PlayerWeight : 0f,
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

        /// <summary>
        /// The V01c source allowlist. An empty list accepts any authored weight
        /// (back-compat); otherwise only props owned by a listed puzzle count.
        /// </summary>
        public bool AcceptsProp(WeightedObject prop) {
            if (prop == null) return false;
            if (AcceptedPuzzleOwnerIDs == null || AcceptedPuzzleOwnerIDs.Length == 0) return true;
            if (string.IsNullOrWhiteSpace(prop.PuzzleOwnerID)) return false;
            return Array.IndexOf(AcceptedPuzzleOwnerIDs, prop.PuzzleOwnerID) >= 0;
        }

        /// <summary>True when the body is currently counted on this plate.</summary>
        public bool IsCounting(Node2D body) => body != null && _loads.ContainsKey(body.GetInstanceId());

        /// <summary>The puzzle this plate reports to, or null.</summary>
        public PuzzleManager ResolvePuzzleManager() =>
            PuzzleManagerPath == null || PuzzleManagerPath.IsEmpty
                ? null
                : GetNodeOrNull<PuzzleManager>(PuzzleManagerPath);

        /// <summary>
        /// V01b reset: drops every stale contact held for <paramref name="props"/>,
        /// re-registers the ones whose restored collision shape now overlaps this
        /// plate, and recomputes the plate from the restored arrangement in one step.
        /// Other occupants (the player) are untouched. Idempotent with the physics
        /// callbacks that follow, because loads are keyed by instance id.
        /// </summary>
        public void RecomputeAfterReset(IEnumerable<WeightedObject> props) {
            if (props == null) return;
            foreach (WeightedObject prop in props) {
                if (prop == null) continue;
                _loads.Remove(prop.GetInstanceId());
                if (!GodotObject.IsInstanceValid(prop) || !prop.IsInsideTree()) continue;
                if (OverlapsProp(prop)) {
                    float weight = AcceptsProp(prop) ? Mathf.Max(0f, prop.WeightUnits) : 0f;
                    if (weight > 0f) _loads[prop.GetInstanceId()] = weight;
                }
            }
            Recalculate();
        }

        private bool OverlapsProp(WeightedObject prop) {
            if (!IsInsideTree()) return false;
            foreach (Node plateChild in GetChildren()) {
                if (plateChild is not CollisionShape2D plateShape || plateShape.Shape == null || plateShape.Disabled) continue;
                foreach (Node propChild in prop.GetChildren()) {
                    if (propChild is not CollisionShape2D propShape || propShape.Shape == null || propShape.Disabled) continue;
                    if (plateShape.Shape.Collide(plateShape.GlobalTransform, propShape.Shape, propShape.GlobalTransform)) {
                        return true;
                    }
                }
            }
            return false;
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
