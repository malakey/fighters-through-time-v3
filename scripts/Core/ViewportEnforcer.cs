using Godot;

namespace FTT.Core {

    public partial class ViewportEnforcer : Node {
        private const int ReferenceWidth = 1920;
        private const int ReferenceHeight = 1080;
        private const float TargetAspect = 16f / 9f;

        public override void _Ready() {
            GetTree().Root.SizeChanged += OnWindowSizeChanged;
            EnforceAspectRatio();
        }

        public override void _ExitTree() {
            GetTree().Root.SizeChanged -= OnWindowSizeChanged;
        }

        private void OnWindowSizeChanged() {
            EnforceAspectRatio();
        }

        private void EnforceAspectRatio() {
            var windowSize = DisplayServer.WindowGetSize();
            float windowAspect = (float)windowSize.X / windowSize.Y;

            if (Mathf.Abs(windowAspect - TargetAspect) < 0.01f) return;

            var viewport = GetViewport();
            if (windowAspect > TargetAspect) {
                int targetWidth = (int)(windowSize.Y * TargetAspect);
                int margin = (windowSize.X - targetWidth) / 2;
                viewport.GetWindow().ContentScaleSize = new Vector2I(ReferenceWidth, ReferenceHeight);
            } else {
                int targetHeight = (int)(windowSize.X / TargetAspect);
                int margin = (windowSize.Y - targetHeight) / 2;
                viewport.GetWindow().ContentScaleSize = new Vector2I(ReferenceWidth, ReferenceHeight);
            }
        }
    }
}
