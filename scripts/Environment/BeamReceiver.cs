using Godot;

namespace FTT.Environment {

    public partial class BeamReceiver : PowerRoutingNode {
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "power_connected";

        protected override void OnPowerStateChanged(bool powered) {
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, powered);
            if (GetNodeOrNull<CanvasItem>("PoweredVisual") is CanvasItem visual) visual.Visible = powered;
        }
    }
}
