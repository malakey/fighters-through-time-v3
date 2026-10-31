using Godot;

namespace FTT.UI {

    /// <summary>
    /// MM-FX (plan section 2.11): the Defy's short desaturating screen pulse, a
    /// <see cref="CanvasLayer"/> holding one full-rect <see cref="ColorRect"/> drawn through
    /// <c>assets/shaders/ui_desaturate.gdshader</c> at saturation 0. It fades out over
    /// <see cref="Seconds"/> and frees itself. Gated by
    /// <see cref="FTT.Core.ComfortSettings.ScreenTintPulseAllowed"/>: under Reduced Temporal
    /// Effects <see cref="Show"/> returns null and only the ring plays. Presentation only.
    /// </summary>
    public partial class DefyScreenPulse : CanvasLayer {
        private const string ShaderPath = "res://assets/shaders/ui_desaturate.gdshader";

        public const float Seconds = 0.25f;
        public const float PeakAlpha = 0.30f;
        public const int LayerIndex = 60;

        private ColorRect _rect;
        private float _age;

        public float Age => _age;

        public static DefyScreenPulse Show(Node parent) {
            if (parent == null || !FTT.Core.ComfortSettings.ScreenTintPulseAllowed) return null;
            var pulse = new DefyScreenPulse { Name = "DefyScreenPulse", Layer = LayerIndex };
            parent.AddChild(pulse);
            return pulse;
        }

        public override void _Ready() {
            _rect = new ColorRect {
                Name = "Wash",
                Color = new Color(0.82f, 0.82f, 0.86f, 1f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            Shader shader = GD.Load<Shader>(ShaderPath);
            if (shader != null) {
                var material = new ShaderMaterial { Shader = shader };
                material.SetShaderParameter("saturation", 0.0f);
                _rect.Material = material;
            }
            _rect.Modulate = new Color(1f, 1f, 1f, PeakAlpha);
            AddChild(_rect);
        }

        public override void _Process(double delta) => Advance((float)delta);

        /// <summary>Ages the pulse; public for headless tests.</summary>
        public void Advance(float seconds) {
            _age += seconds;
            if (_age >= Seconds) { QueueFree(); return; }
            if (_rect != null) _rect.Modulate = new Color(1f, 1f, 1f, PeakAlpha * (1f - _age / Seconds));
        }
    }
}
