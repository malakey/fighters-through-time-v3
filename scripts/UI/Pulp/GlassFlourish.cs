using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The glass flourish (UI_SPEC_GLASS 3.7, 3.8): hairline rules fading from transparent to
    /// <c>edge-hi</c> toward a small glowing diamond. Three shapes:
    /// <list type="bullet">
    /// <item><see cref="FlourishShape.RuleDiamondRule"/>, the main menu's title row (two rules filling the width, 16 px
    /// gaps, an 11 px <c>accent</c> diamond with a 14 px glow; the boss intro card's uses the boss-red diamond);</item>
    /// <item><see cref="FlourishShape.RuleDiamond"/>, a heading's left flourish (a 240 px rule, 24 px, the diamond, then
    /// the heading text after another 24 px, which the heading's own row spaces);</item>
    /// <item><see cref="FlourishShape.DiamondRule"/>, the mirror on the heading's right;</item>
    /// <item><see cref="FlourishShape.Rule"/>, a lone rule fading from <see cref="RuleColor"/> to transparent across the width
    /// (the title's middle-word rule, 3.8: <c>edge</c> to transparent).</item>
    /// </list>
    /// The diamond is the kit's baked <c>glass_diamond_11</c> (or <c>_12</c>) glow texture, white and tinted. Rules are
    /// 1 px quads with vertex colours. Never animated; geometry is cached per size (<see cref="GeometryBuilds"/>).
    /// </summary>
    public partial class GlassFlourish : Control {

        public enum FlourishShape { RuleDiamondRule, RuleDiamond, DiamondRule, Rule }

        /// <summary>The menu title row's gap (3.8) and the heading flourish's gap (3.7).</summary>
        public const float MenuGap = 16f, HeadingGap = 24f;
        /// <summary>The heading flourish's rule length (3.7).</summary>
        public const float HeadingRule = 240f;
        /// <summary>The kit draws the diamond's glow into a 48 px canvas around an 11 or 12 px diamond.</summary>
        public const float DiamondCanvas = 48f;

        private FlourishShape _shape = FlourishShape.RuleDiamondRule;
        private float _ruleLength = HeadingRule;
        private float _gap = MenuGap;
        private int _diamond = 11;
        private Color _diamondTint = UIPalette.Accent;
        private Color _ruleColor = UIPalette.EdgeHi;
        private readonly Vector2[] _left = new Vector2[4];
        private readonly Vector2[] _right = new Vector2[4];
        private readonly Color[] _leftColors = new Color[4];
        private readonly Color[] _rightColors = new Color[4];
        private Rect2 _diamondRect;
        private bool _hasLeft, _hasRight;
        private Vector2 _builtFor = new(-1f, -1f);
        private bool _dirty = true;

        public GlassFlourish() {
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            UpdateMinimum();
        }

        /// <summary>A flourish of <paramref name="shape"/> (a heading flourish when the shape is one-sided).</summary>
        public static GlassFlourish Create(FlourishShape shape, float ruleLength = HeadingRule, float gap = HeadingGap) =>
            new() { Shape = shape, RuleLength = ruleLength, Gap = gap };

        public FlourishShape Shape { get => _shape; set { _shape = value; Invalidate(); } }
        /// <summary>A one-sided flourish's rule length; the two-sided shape's rules fill the width instead.</summary>
        public float RuleLength { get => _ruleLength; set { _ruleLength = Mathf.Max(0f, value); Invalidate(); } }
        public float Gap { get => _gap; set { _gap = Mathf.Max(0f, value); Invalidate(); } }
        /// <summary>11 (menu, select) or 12 (the boss name and phase notch); picks the kit's diamond texture.</summary>
        public int DiamondSize { get => _diamond; set { _diamond = value >= 12 ? 12 : 11; Invalidate(); } }
        public Color DiamondTint { get => _diamondTint; set { _diamondTint = value; QueueRedraw(); } }
        public Color RuleColor { get => _ruleColor; set { _ruleColor = value; _dirty = true; QueueRedraw(); } }

        /// <summary>How many times the rules and diamond were laid out.</summary>
        public int GeometryBuilds { get; private set; }
        /// <summary>The diamond's centre in this control's coordinates (after layout).</summary>
        public Vector2 DiamondCentre { get { EnsureGeometry(); return _diamondRect.GetCenter(); } }

        private void Invalidate() {
            _dirty = true;
            UpdateMinimum();
            QueueRedraw();
        }

        private void UpdateMinimum() {
            float width = _shape switch {
                FlourishShape.RuleDiamondRule => _diamond + 2f * _gap,
                FlourishShape.Rule => 0f,
                _ => _ruleLength + _gap + _diamond
            };
            CustomMinimumSize = new Vector2(width, _shape == FlourishShape.Rule ? 1f : _diamond);
        }

        public override void _Notification(int what) {
            if (what == NotificationResized) { _dirty = true; QueueRedraw(); }
        }

        private void EnsureGeometry() {
            if (!_dirty && Size == _builtFor) return;
            _dirty = false;
            _builtFor = Size;
            GeometryBuilds++;
            float y = Size.Y / 2f;
            Color clear = new(_ruleColor, 0f);
            float diamondX;
            switch (_shape) {
                case FlourishShape.RuleDiamond:
                    diamondX = Size.X - _diamond / 2f;
                    _hasLeft = Rule(_left, _leftColors, 0f, Mathf.Max(0f, Size.X - _diamond - _gap), y, clear, _ruleColor);
                    _hasRight = false;
                    break;
                case FlourishShape.DiamondRule:
                    diamondX = _diamond / 2f;
                    _hasLeft = false;
                    _hasRight = Rule(_right, _rightColors, _diamond + _gap, Size.X, y, _ruleColor, clear);
                    break;
                case FlourishShape.Rule:
                    diamondX = float.NaN;
                    _hasLeft = Rule(_left, _leftColors, 0f, Size.X, y, _ruleColor, clear);
                    _hasRight = false;
                    break;
                default:
                    diamondX = Size.X / 2f;
                    float half = Size.X / 2f - _diamond / 2f - _gap;
                    _hasLeft = Rule(_left, _leftColors, 0f, half, y, clear, _ruleColor);
                    _hasRight = Rule(_right, _rightColors, Size.X - half, Size.X, y, _ruleColor, clear);
                    break;
            }
            _diamondRect = new Rect2(diamondX - DiamondCanvas / 2f, y - DiamondCanvas / 2f, DiamondCanvas, DiamondCanvas);
        }

        // A 1 px rule from x0 to x1 centred on y, its colour running from c0 to c1 (0.5 px either side keeps it crisp).
        private static bool Rule(Vector2[] quad, Color[] colors, float x0, float x1, float y, Color c0, Color c1) {
            if (x1 - x0 < 1f) return false;
            quad[0] = new Vector2(x0, y - 0.5f); quad[1] = new Vector2(x1, y - 0.5f);
            quad[2] = new Vector2(x1, y + 0.5f); quad[3] = new Vector2(x0, y + 0.5f);
            colors[0] = c0; colors[1] = c1; colors[2] = c1; colors[3] = c0;
            return true;
        }

        public override void _Draw() {
            EnsureGeometry();
            if (_hasLeft) DrawPolygon(_left, _leftColors);
            if (_hasRight) DrawPolygon(_right, _rightColors);
            if (_shape == FlourishShape.Rule) return;
            Texture2D diamond = PulpKit.Texture(_diamond >= 12 ? "glass_diamond_12" : "glass_diamond_11");
            if (diamond != null) DrawTextureRect(diamond, _diamondRect, false, _diamondTint);
        }
    }
}
