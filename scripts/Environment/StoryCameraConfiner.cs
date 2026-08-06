using Godot;

namespace FTT.Environment {

    public partial class StoryCameraConfiner : Camera2D {
        [Signal] public delegate void BoundsChangedEventHandler(Rect2 bounds);

        [Export] public Rect2 ActiveBounds = new(0, 0, 1920, 1080);

        public override void _Ready() => SetBounds(ActiveBounds);

        public void SetBounds(Rect2 bounds) {
            ActiveBounds = bounds.Abs();
            LimitLeft = Mathf.RoundToInt(ActiveBounds.Position.X);
            LimitTop = Mathf.RoundToInt(ActiveBounds.Position.Y);
            LimitRight = Mathf.RoundToInt(ActiveBounds.End.X);
            LimitBottom = Mathf.RoundToInt(ActiveBounds.End.Y);
            EmitSignal(SignalName.BoundsChanged, ActiveBounds);
        }
    }
}
