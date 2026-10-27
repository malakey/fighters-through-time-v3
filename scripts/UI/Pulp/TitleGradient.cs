using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The main menu title's gradient fill (UI_SPEC_GLASS 2.5, 6.5, T01): <c>ui_title_gradient.gdshader</c> on one
    /// title <see cref="Label"/>, one <see cref="ShaderMaterial"/> per label (no instance uniforms on
    /// <c>gl_compatibility</c>). The gradient spans the CSS line box (line height 0.94 of the font size, centred on the
    /// font's own line), so <c>line_top</c> and <c>line_height</c> follow the label's font metrics, its layout and the UI
    /// scale (the theme's font size). A theme change re-applies them once it has reached the label. The label draws white
    /// under the shader (its colour override); without the shader it keeps its role colour, the flat #DFE8FF fallback.
    /// Never animated. TG-CLOSE: a wrapped title repeats the gradient on every line (<c>line_count</c> lines,
    /// <c>line_pitch</c> apart), so a title may wrap (the credits title does).
    /// </summary>
    public partial class TitleGradient : Node {

        public const string NodeName = "TitleGradient";
        public const string ShaderPath = "res://assets/shaders/ui_title_gradient.gdshader";
        /// <summary>The CSS line height of T01 (0.94 of the font size).</summary>
        public const float LineHeightRatio = 0.94f;

        private static readonly StringName LineTopName = "line_top", LineHeightName = "line_height";
        private static readonly StringName LinePitchName = "line_pitch", LineCountName = "line_count";
        private static readonly StringName FontName = "font", FontSizeName = "font_size", NormalName = "normal";
        private static readonly StringName LineSpacingName = "line_spacing";
        private static Shader _shader;

        private Label _label;
        private ShaderMaterial _material;
        private bool _queued;
        private float _lineTop = float.NaN, _lineHeight = float.NaN, _linePitch = float.NaN;
        private int _lineCount = -1;

        /// <summary>The label's own material (null when the shader is missing: the flat fallback).</summary>
        public ShaderMaterial Material => _material;
        public Label Label => _label;
        /// <summary>The line box's top and height the shader was last given (label-local pixels).</summary>
        public float LineTop => _lineTop;
        public float LineHeight => _lineHeight;
        /// <summary>The distance between two lines' tops and the number of lines the shader was last given.</summary>
        public float LinePitch => _linePitch;
        public int LineCount => _lineCount;

        /// <summary>Installs the gradient on <paramref name="label"/> (idempotent: a second call returns the first).</summary>
        public static TitleGradient Attach(Label label) {
            if (label == null) return null;
            if (label.GetNodeOrNull<TitleGradient>(NodeName) is { } existing) return existing;
            var gradient = new TitleGradient { Name = NodeName, _label = label };
            _shader ??= ResourceLoader.Exists(ShaderPath) ? GD.Load<Shader>(ShaderPath) : null;
            if (_shader != null) {
                gradient._material = new ShaderMaterial { Shader = _shader };
                label.Material = gradient._material;
                label.AddThemeColorOverride("font_color", Colors.White);
            }
            label.AddChild(gradient);
            gradient.Apply();
            return gradient;
        }

        public override void _EnterTree() {
            _label ??= GetParent() as Label;
            if (_label == null) return;
            _label.ThemeChanged += Queue;
            _label.Resized += Queue;
            Queue();
        }

        public override void _ExitTree() {
            if (_label == null || !IsInstanceValid(_label)) return;
            _label.ThemeChanged -= Queue;
            _label.Resized -= Queue;
        }

        // The label raises theme_changed before it drops its cached font size: apply once the change has propagated.
        private void Queue() {
            if (_queued) return;
            _queued = true;
            Callable.From(Applied).CallDeferred();
        }

        private void Applied() {
            _queued = false;
            if (IsInstanceValid(this) && _label != null && IsInstanceValid(_label)) Apply();
        }

        /// <summary>Recomputes the line box from the label's font, size and layout and writes the uniforms on a change.</summary>
        public void Apply() {
            if (_label == null || _material == null) return;
            Font font = _label.GetThemeFont(FontName);
            int size = _label.GetThemeFontSize(FontSizeName);
            if (font == null || size <= 0) return;
            float fontHeight = font.GetHeight(size);
            StyleBox style = _label.GetThemeStylebox(NormalName);
            float top = style?.GetContentMargin(Side.Top) ?? 0f;
            float bottom = style?.GetContentMargin(Side.Bottom) ?? 0f;
            int lines = Mathf.Max(1, _label.GetLineCount());
            float spacing = _label.GetThemeConstant(LineSpacingName);
            float textHeight = lines * fontHeight + (lines - 1) * spacing;
            float pitch = fontHeight + spacing;
            float avail = _label.Size.Y - top - bottom;
            float y0 = _label.VerticalAlignment switch {
                VerticalAlignment.Center => top + (avail - textHeight) / 2f,
                VerticalAlignment.Bottom => top + avail - textHeight,
                _ => top
            };
            float lineHeight = LineHeightRatio * size;
            float lineTop = y0 + (fontHeight - lineHeight) / 2f;
            if (!Mathf.IsEqualApprox(lineTop, _lineTop)) { _lineTop = lineTop; _material.SetShaderParameter(LineTopName, lineTop); }
            if (!Mathf.IsEqualApprox(lineHeight, _lineHeight)) { _lineHeight = lineHeight; _material.SetShaderParameter(LineHeightName, lineHeight); }
            if (!Mathf.IsEqualApprox(pitch, _linePitch)) { _linePitch = pitch; _material.SetShaderParameter(LinePitchName, pitch); }
            if (lines != _lineCount) { _lineCount = lines; _material.SetShaderParameter(LineCountName, lines); }
        }
    }
}
