using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. The PNL-B halftone corner (UI_SPEC 3.1): dots of pitch 10 in the bottom-right 42 % x 52 %
    /// of its rect, faded along the diagonal by a shader. It fills whatever rect its parent gives it, so
    /// inside a <see cref="PanelContainer"/> that is the padding box; add it as the panel's first child so it
    /// draws behind the content (<see cref="Attach"/> does that).
    /// </summary>
    public partial class HalftoneCorner : Control {

        public const string ShaderPath = "res://assets/shaders/halftone_corner.gdshader";
        public static readonly Color DarkDots = new(1f, 1f, 1f, 0.17f);
        public static readonly Color PaperDots = new(0f, 0f, 0f, 0.16f);

        private readonly ShaderMaterial _material;
        private bool _paper;

        public HalftoneCorner() {
            Name = "HalftoneCorner";
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
            _material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
            _material.SetShaderParameter("dot_color", DarkDots);
            Material = _material;
        }

        /// <summary>Paper panels get black dots at 0.16, dark panels white at 0.17.</summary>
        public bool Paper {
            get => _paper;
            set { _paper = value; _material.SetShaderParameter("dot_color", value ? PaperDots : DarkDots); }
        }

        /// <summary>Adds a corner as <paramref name="panel"/>'s first child (behind its content) and returns it.</summary>
        public static HalftoneCorner Attach(Control panel, bool paper = false) {
            var corner = new HalftoneCorner { Paper = paper };
            panel.AddChild(corner);
            panel.MoveChild(corner, 0);
            return corner;
        }

        public override void _Ready() {
            Resized += UpdateSize;
            UpdateSize();
        }

        public override void _ExitTree() => Resized -= UpdateSize;

        private void UpdateSize() {
            _material.SetShaderParameter("rect_size", Size);
            QueueRedraw();
        }

        // The shader needs something to paint: one white rect over the whole layer.
        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);
    }
}
