using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Solid Environment-layer barrier driven by a <see cref="ShieldGeneratorTower"/>.
    /// Active means solid and visible; inactive disables the collision shape and
    /// hides the placeholder pane.
    /// </summary>
    public partial class ForcefieldBarrier : StaticBody2D {
        [Signal] public delegate void ActiveChangedEventHandler(bool isActive);

        [Export] public string BarrierID = "";
        [Export] public bool StartActive = true;
        [Export] public NodePath CollisionShapePath = "CollisionShape2D";
        [Export] public NodePath VisualPath = "Visual";

        public bool IsActive { get; private set; } = true;

        public override void _Ready() {
            AddToGroup("forcefield_barrier");
            CollisionLayer = CollisionLayers.Environment;
            CollisionMask = 0;
            SetActive(StartActive);
        }

        public void SetActive(bool active) {
            IsActive = active;
            if (GetNodeOrNull<CollisionShape2D>(CollisionShapePath) is CollisionShape2D shape) shape.Disabled = !active;
            if (GetNodeOrNull<CanvasItem>(VisualPath) is CanvasItem visual) visual.Visible = active;
            EmitSignal(SignalName.ActiveChanged, active);
        }
    }
}
