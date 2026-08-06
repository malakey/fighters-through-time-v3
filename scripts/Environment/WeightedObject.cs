using Godot;

namespace FTT.Environment {

    public partial class WeightedObject : RigidBody2D {
        [Export(PropertyHint.Range, "0.1,100,0.1")] public float WeightUnits = 1f;

        public override void _Ready() {
            AddToGroup("movable_weight");
            Mass = Mathf.Max(0.1f, WeightUnits);
        }
    }
}
