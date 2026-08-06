using Godot;

namespace FTT.Environment {

    public partial class BeamEmitter : PowerRoutingNode {
        [Export] public bool StartsEnabled = true;

        public override void _Ready() {
            base._Ready();
            SetLocalPower(StartsEnabled);
        }

        public void SetEnabled(bool enabled) => SetLocalPower(enabled);

        protected override void OnPowerStateChanged(bool powered) {
            if (GetNodeOrNull<CanvasItem>("BeamVisual") is CanvasItem visual) visual.Visible = powered;
        }
    }
}
