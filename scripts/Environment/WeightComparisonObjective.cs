using Godot;

namespace FTT.Environment {

    public partial class WeightComparisonObjective : Node {
        [Export] public NodePath LeftPlatePath;
        [Export] public NodePath RightPlatePath;
        [Export] public NodePath CounterweightPath;
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "weights_balanced";
        [Export(PropertyHint.Range, "0,10,0.1")] public float Tolerance = 0.1f;
        [Export] public bool RequireBothThresholds = true;

        private PressurePlate _left;
        private PressurePlate _right;

        public override void _Ready() {
            _left = GetNodeOrNull<PressurePlate>(LeftPlatePath);
            _right = GetNodeOrNull<PressurePlate>(RightPlatePath);
            if (_left != null) _left.WeightChanged += OnWeightChanged;
            if (_right != null) _right.WeightChanged += OnWeightChanged;
            Evaluate();
        }

        public override void _ExitTree() {
            if (_left != null) _left.WeightChanged -= OnWeightChanged;
            if (_right != null) _right.WeightChanged -= OnWeightChanged;
        }

        public bool Evaluate() {
            if (_left == null || _right == null) return false;
            bool balanced = Mathf.Abs(_left.CurrentWeight - _right.CurrentWeight) <= Tolerance;
            if (RequireBothThresholds) balanced &= _left.IsPressed && _right.IsPressed;
            GetNodeOrNull<Counterweight>(CounterweightPath)?.SetLoad(_left.CurrentWeight + _right.CurrentWeight);
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, balanced);
            return balanced;
        }

        private void OnWeightChanged(float currentWeight, bool thresholdReached) => Evaluate();
    }
}
