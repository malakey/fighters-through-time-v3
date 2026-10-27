using Godot;

namespace FTT.UI.Pulp {

    /// <summary>Which status family a chip shows; it only picks the glyph colour (a glyph and a name always carry it too).</summary>
    public enum PulpChipKind { Damage, Control, Suppression }

    /// <summary>
    /// TG-WIDGETS (G7 badge, UI_SPEC_GLASS 3.4.3; first written by UR-WIDGETS as the GAUGE-B chip). A 44 (Story) or 40
    /// (Fighter) glass badge: the radial badge fill (<c>glass_badge_radial</c>) with its 14 px glow and a 1.5 px gold edge just
    /// outside it, the status glyph at 62 % in the bold weight (below 30 px) and the status colour, a 3 px duration ring with
    /// its outer edge 5 px outside the badge over the remaining share, and an optional name / time column 12 px to the
    /// right. An empty (reserved) slot draws G8: a 1.5 px <c>edge-dim</c> ring, no fill, no glyph, the text column at 0.35
    /// opacity; it keeps its size. The owner writes every value; the badge geometry is built once per size.
    /// </summary>
    public partial class PulpStatusChip : Control {

        public const float TextGap = 12f;
        public const float ColumnGap = 4f;
        /// <summary>The duration ring's width (3 px) and how far its outer edge sits outside the badge (5 px: 44 -> 54).</summary>
        public const float RingWidth = 3f, RingOutset = 5f;
        public const float GlyphShare = 0.62f;
        private const int CircleSegments = 48;
        private static readonly Color BadgeGlow = new(UIPalette.Accent, 0.12f);

        private float _badge = 44f;
        private PulpChipKind _kind = PulpChipKind.Damage;
        private string _glyphName = "venom";
        private float _remaining = 1f;
        private bool _showRing = true;
        private bool _empty;
        private bool _showText = true;

        private readonly PulpIcon _glyph;
        private readonly VBoxContainer _column;
        private readonly Label _name;
        private readonly Label _time;
        private readonly StyleBoxFlat _glowBox = new() { DrawCenter = false, CornerDetail = 16, ShadowSize = 14 };
        private readonly Vector2[] _disc = new Vector2[CircleSegments];
        private readonly Vector2[] _discUvs = new Vector2[CircleSegments];
        private readonly Color[] _discColors = new Color[CircleSegments];
        private float _builtBadge = -1f;
        private float _builtTop = -1f;

        public PulpStatusChip() {
            Name = "PulpStatusChip";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            _glowBox.ShadowColor = BadgeGlow;
            _glyph = new PulpIcon { Name = "Glyph", IconName = _glyphName };
            AddChild(_glyph);
            _column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
            _column.AddThemeConstantOverride("separation", (int)ColumnGap);
            _name = new Label { Name = "StatusName", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.StatusName, MouseFilter = MouseFilterEnum.Ignore };
            _time = new Label { Name = "StatusTime", ThemeTypeVariation = UIPalette.LabelRoles.StatusTime, MouseFilter = MouseFilterEnum.Ignore };
            _column.AddChild(_name);
            _column.AddChild(_time);
            AddChild(_column);
            Layout();
        }

        /// <summary>The glyph colour: damage #FF7A5C, control #8FC7FF, Suppression cold #38F2FF.</summary>
        public static Color GlyphColor(PulpChipKind kind) => kind switch {
            PulpChipKind.Control => UIPalette.StatusControl,
            PulpChipKind.Suppression => UIPalette.UnboundCold,
            _ => UIPalette.StatusDamage
        };

        /// <summary>The share of the ring that is drawn (0 when the chip is empty or has no ring).</summary>
        public static float RingFraction(bool empty, bool showRing, float remaining) =>
            empty || !showRing ? 0f : Mathf.Clamp(remaining, 0f, 1f);

        /// <summary>The duration ring's centre-line radius for a badge: its outer edge <see cref="RingOutset"/> outside (44 -> 25.5).</summary>
        public static float RingRadius(float badge) => badge / 2f + RingOutset - RingWidth / 2f;

        /// <summary>The empty slot's ring (G8): <c>edge-dim</c>, 1.5 px, just outside the badge.</summary>
        public static Color EmptyRingColor => UIPalette.EdgeDim;

        /// <summary>44 (Story) or 40 (Fighter).</summary>
        public float BadgeSize { get => _badge; set { _badge = value; Layout(); } }
        public PulpChipKind Kind { get => _kind; set { _kind = value; Layout(); } }
        public string GlyphName { get => _glyphName; set { _glyphName = value; Layout(); } }
        /// <summary>The remaining share of the duration, 0 to 1 (clockwise from 12 o'clock).</summary>
        public float Remaining { get => _remaining; set { _remaining = value; QueueRedraw(); } }
        public bool ShowRing { get => _showRing; set { _showRing = value; QueueRedraw(); } }
        public bool Empty { get => _empty; set { _empty = value; Layout(); } }
        public bool ShowText { get => _showText; set { _showText = value; Layout(); } }
        /// <summary>A raw translation key (auto-translated); ignored while the chip is empty (it then shows a blank name).</summary>
        public string NameText { get => _name.Text; set { _name.Text = value ?? ""; Layout(); } }
        public string TimeText { get => _time.Text; set { _time.Text = value ?? ""; Layout(); } }
        public PulpIcon Glyph => _glyph;
        /// <summary>
        /// The badge's top inside the chip's rect: centred when the rect is taller than the badge (TG-CLOSE, TG-HUDF kit gap 3:
        /// it sat at the top, so a row taller than the badge had to shrink-centre the chip).
        /// </summary>
        public float BadgeTop => Mathf.Max(0f, (Size.Y - _badge) / 2f);
        public Control TextColumn => _column;

        public override Vector2 _GetMinimumSize() {
            float width = _badge;
            if (_showText) width += TextGap + _column.GetCombinedMinimumSize().X;
            return new Vector2(width, _badge);
        }

        private void Layout() {
            if (_glyph == null || _column == null) return;
            float glyphSize = _badge * GlyphShare;
            _glyph.Size = new Vector2(glyphSize, glyphSize);
            float top = BadgeTop;
            _glyph.Position = new Vector2((_badge - glyphSize) / 2f, top + (_badge - glyphSize) / 2f);
            _glyph.IconName = _glyphName;
            _glyph.Tint = GlyphColor(_kind);
            _glyph.Visible = !_empty;
            _column.Visible = _showText;
            _column.Modulate = new Color(1f, 1f, 1f, _empty ? 0.35f : 1f);
            if (_empty) _name.Text = "";
            _column.ResetSize();
            _column.Position = new Vector2(_badge + TextGap, top + (_badge - _column.Size.Y) / 2f);
            UpdateMinimumSize();
            QueueRedraw();
        }

        public override void _Notification(int what) {
            if (what == NotificationThemeChanged) CallDeferred(nameof(Layout));
            else if (what == NotificationResized) Layout();
        }

        public override void _Ready() => CallDeferred(nameof(Layout));

        private void EnsureDisc() {
            float top = BadgeTop;
            if (Mathf.IsEqualApprox(_badge, _builtBadge) && Mathf.IsEqualApprox(top, _builtTop)) return;
            _builtBadge = _badge;
            _builtTop = top;
            var box = new Rect2(new Vector2(0f, top), new Vector2(_badge, _badge));
            GlassGeometry.Circle(box.GetCenter(), _badge / 2f, CircleSegments, box, _disc, _discUvs);
            for (int i = 0; i < CircleSegments; i++) _discColors[i] = Colors.White;
        }

        public override void _Draw() {
            float radius = _badge / 2f;
            float top = BadgeTop;
            var center = new Vector2(radius, top + radius);
            if (_empty) {
                DrawArc(center, radius + GlassGeometry.EdgeWidth / 2f, 0f, Mathf.Tau, 64, EmptyRingColor, GlassGeometry.EdgeWidth, true);
                return;
            }
            EnsureDisc();
            int corner = Mathf.RoundToInt(radius);
            _glowBox.CornerRadiusTopLeft = _glowBox.CornerRadiusTopRight = corner;
            _glowBox.CornerRadiusBottomLeft = _glowBox.CornerRadiusBottomRight = corner;
            DrawStyleBox(_glowBox, new Rect2(new Vector2(0f, top), new Vector2(_badge, _badge)));
            Texture2D fill = PulpKit.Texture("glass_badge_radial");
            if (fill != null) DrawPolygon(_disc, _discColors, _discUvs, fill);
            else DrawColoredPolygon(_disc, UIPalette.BadgeFill);
            DrawArc(center, radius + GlassGeometry.EdgeWidth / 2f, 0f, Mathf.Tau, 64, UIPalette.Edge, GlassGeometry.EdgeWidth, true);
            float fraction = RingFraction(_empty, _showRing, _remaining);
            if (fraction > 0f) {
                DrawArc(center, RingRadius(_badge), -Mathf.Pi / 2f, -Mathf.Pi / 2f + fraction * Mathf.Tau, 64, GlyphColor(_kind), RingWidth, true);
            }
        }
    }
}
