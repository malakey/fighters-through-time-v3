using Godot;

namespace FTT.Environment {
    /// <summary>Depth scenery cropped from the approved plate, covering the camera through pans and zooms.</summary>
    public partial class ReferenceSceneBackdrop : Node2D {
        private Camera2D _camera;
        private Sprite2D _sprite;
        public Rect2 CoverageRect { get; private set; }
        public Rect2 ViewRect { get; private set; }

        public static ReferenceSceneBackdrop Attach(Node2D parent, Texture2D source, Camera2D camera,
            Rect2? normalizedCrop = null, int zIndex = -2, float brightness = 0.72f) {
            var backdrop = new ReferenceSceneBackdrop {
                Name = "ReferenceBackdrop", _camera = camera, ZIndex = zIndex,
                TopLevel = true, ProcessMode = ProcessModeEnum.Always, ProcessPriority = 100
            };
            Rect2 crop = normalizedCrop ?? new Rect2(0f, 0f, 1f, 0.65f);
            Vector2 size = source.GetSize();
            backdrop._sprite = new Sprite2D {
                Name = "DepthScenery",
                Texture = new AtlasTexture { Atlas = source, Region = new Rect2(crop.Position * size, crop.Size * size), FilterClip = true },
                Modulate = new Color(brightness, brightness, brightness, 1f)
            };
            parent.AddChild(backdrop);
            backdrop.AddChild(backdrop._sprite);
            backdrop.Refresh();
            return backdrop;
        }

        public static Vector2 CoverSize(Vector2 textureSize, Vector2 viewSize) {
            if (textureSize.X <= 0f || textureSize.Y <= 0f) return viewSize;
            return textureSize * Mathf.Max(viewSize.X / textureSize.X, viewSize.Y / textureSize.Y);
        }

        public void Refresh() {
            if (!IsInstanceValid(_camera) || !IsInstanceValid(_sprite)) return;
            Viewport viewport = _camera.GetViewport();
            ViewRect = viewport.CanvasTransform.AffineInverse() * viewport.GetVisibleRect();
            Vector2 covered = CoverSize(_sprite.Texture.GetSize(), ViewRect.Size + Vector2.One * 2f);
            GlobalPosition = ViewRect.GetCenter();
            _sprite.Scale = covered / _sprite.Texture.GetSize();
            CoverageRect = new Rect2(GlobalPosition - covered / 2f, covered);
        }

        public override void _Process(double delta) => Refresh();
    }
}
