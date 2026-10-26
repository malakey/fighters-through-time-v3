using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. The GAUGE-B portrait roundel (UI_SPEC 3.3.4): the <c>portrait_roundel.png</c> back plate
    /// (a 2x bake, drawn at half size), the portrait drawn at 142 % of the diameter offset (-21 %, -6.8 %) and
    /// clipped to the disc by a shader, and a 0.28 black halftone (<c>portrait_halftone.png</c> scaled by
    /// diameter / 112) over it. Diameter 112 (Story), 110 (Fighter) or 86 (continue card). The portrait is
    /// drawn into a square, so it should be square art.
    /// </summary>
    public partial class PulpPortraitRoundel : Control {

        public const float ReferenceDiameter = 112f;
        public const float PortraitZoom = 1.42f;
        public static readonly Vector2 PortraitOffset = new(-0.21f, -0.068f);
        private const string MaskShader = "res://assets/shaders/ui_circle_mask.gdshader";

        private float _diameter = 112f;
        private readonly TextureRect _portrait;
        private readonly TextureRect _halftone;
        private readonly ShaderMaterial _portraitMask;
        private readonly ShaderMaterial _halftoneMask;

        public PulpPortraitRoundel() {
            Name = "PulpPortraitRoundel";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            Shader shader = GD.Load<Shader>(MaskShader);
            _portraitMask = new ShaderMaterial { Shader = shader };
            _halftoneMask = new ShaderMaterial { Shader = shader };
            _portrait = new TextureRect {
                Name = "Portrait", MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, Material = _portraitMask
            };
            _halftone = new TextureRect {
                Name = "Halftone", MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, Material = _halftoneMask,
                Texture = PulpKit.Texture("portrait_halftone")
            };
            AddChild(_portrait);
            AddChild(_halftone);
            Layout();
        }

        /// <summary>112 (Story), 110 (Fighter) or 86.</summary>
        public float Diameter { get => _diameter; set { _diameter = value; Layout(); } }
        public Texture2D Portrait { get => _portrait.Texture; set => _portrait.Texture = value; }

        /// <summary>The portrait's rectangle relative to the roundel's own box, for a given diameter (UI_SPEC 3.3.4).</summary>
        public static Rect2 PortraitRect(float diameter) =>
            new(PortraitOffset * diameter, new Vector2(PortraitZoom, PortraitZoom) * diameter);

        private void Layout() {
            if (_portrait == null) return;
            CustomMinimumSize = new Vector2(_diameter, _diameter);
            Size = CustomMinimumSize;
            Rect2 art = PortraitRect(_diameter);
            _portrait.Position = art.Position;
            _portrait.Size = art.Size;
            var center = (Vector2.One * (_diameter / 2f) - art.Position) / art.Size;
            _portraitMask.SetShaderParameter("circle_center", center);
            _portraitMask.SetShaderParameter("circle_radius", _diameter / 2f / art.Size.X);
            _portraitMask.SetShaderParameter("uv_to_pixels", art.Size);
            _halftone.Position = Vector2.Zero;
            _halftone.Size = new Vector2(_diameter, _diameter);
            _halftoneMask.SetShaderParameter("circle_center", new Vector2(0.5f, 0.5f));
            _halftoneMask.SetShaderParameter("circle_radius", 0.5f);
            _halftoneMask.SetShaderParameter("uv_to_pixels", _halftone.Size);
            QueueRedraw();
        }

        public override void _Draw() {
            // The 2x back plate at half size; the disc's box origin is the catalog's expand (1x units).
            Texture2D back = PulpKit.Texture("portrait_roundel");
            if (back == null) return;
            float scale = _diameter / ReferenceDiameter;
            int[] expand = PulpKit.Expand("portrait_roundel");
            Vector2 half = back.GetSize() / 2f * scale;
            DrawTextureRect(back, new Rect2(new Vector2(-expand[0], -expand[1]) * scale, half), false);
        }
    }
}
