using Godot;

namespace FTT.Core {

    public partial class ViewportEnforcer : Node {
        public static readonly Vector2I ReferenceSize = new(1920, 1080);

        public override void _Ready() {
            Window window = GetTree().Root;
            window.ContentScaleSize = ReferenceSize;
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            RenderingServer.SetDefaultClearColor(Colors.Black);
        }
    }
}
