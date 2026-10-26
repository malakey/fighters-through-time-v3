using Godot;

namespace FTT.UI.Pulp {

    /// <summary>Which status family a chip shows; it only picks the glyph colour (a glyph and a name always carry it too).</summary>
    public enum PulpChipKind { Damage, Control, Suppression }

    /// <summary>
    /// UR-WIDGETS. The GAUGE-B status chip (UI_SPEC 3.3.3): a 44 (Story) or 40 (Fighter) badge disc with an ink
    /// ring and a (3, 4) shadow drawn in code, a glyph at 62 %, a 5 px duration ring over the remaining share,
    /// and an optional name / time column 12 px to the right. An empty (reserved) slot draws a dashed ring, no
    /// glyph and the text column at 0.35 opacity, and keeps its size. The owner writes every value.
    /// </summary>
    public partial class PulpStatusChip : Control {

        public const float TextGap = 12f;
        public const float ColumnGap = 4f;
        public const float RingWidth = 5f;
        private static readonly Color DashColor = new(1f, 243f / 255f, 216f / 255f, 0.8f);

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

        public PulpStatusChip() {
            Name = "PulpStatusChip";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
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
        public Control TextColumn => _column;

        public override Vector2 _GetMinimumSize() {
            float width = _badge;
            if (_showText) width += TextGap + _column.GetCombinedMinimumSize().X;
            return new Vector2(width, _badge);
        }

        private void Layout() {
            if (_glyph == null || _column == null) return;
            float glyphSize = _badge * 0.62f;
            _glyph.Size = new Vector2(glyphSize, glyphSize);
            _glyph.Position = Vector2.One * ((_badge - glyphSize) / 2f);
            _glyph.IconName = _glyphName;
            _glyph.Tint = GlyphColor(_kind);
            _glyph.Visible = !_empty;
            _column.Visible = _showText;
            _column.Modulate = new Color(1f, 1f, 1f, _empty ? 0.35f : 1f);
            if (_empty) _name.Text = "";
            _column.ResetSize();
            _column.Position = new Vector2(_badge + TextGap, (_badge - _column.Size.Y) / 2f);
            UpdateMinimumSize();
            QueueRedraw();
        }

        public override void _Notification(int what) {
            if (what == NotificationThemeChanged) CallDeferred(nameof(Layout));
        }

        public override void _Ready() => CallDeferred(nameof(Layout));

        public override void _Draw() {
            float radius = _badge / 2f;
            var center = new Vector2(radius, radius);
            if (_empty) {
                DrawCircle(center, radius, new Color(0f, 0f, 0f, 0.16f));
                const int dashes = 16;
                for (int i = 0; i < dashes; i++) {
                    float a0 = i * Mathf.Tau / dashes;
                    DrawArc(center, radius - 1.5f, a0, a0 + Mathf.Tau / dashes * 0.55f, 6, DashColor, 3f, true);
                }
                return;
            }
            DrawCircle(center + new Vector2(3f, 4f), radius, Colors.Black);
            Texture2D disc = PulpKit.Texture("badge_disc");
            if (disc != null) DrawTextureRect(disc, new Rect2(Vector2.Zero, new Vector2(_badge, _badge)), false);
            DrawArc(center, radius - 1.5f, 0f, Mathf.Tau, 64, Colors.Black, 3f, true);
            float fraction = RingFraction(_empty, _showRing, _remaining);
            if (fraction > 0f) {
                // Inset -1 px, 5 px wide: the ring's outer edge is one pixel outside the badge.
                float ringRadius = radius + 1f - RingWidth / 2f;
                DrawArc(center, ringRadius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + fraction * Mathf.Tau, 64, GlyphColor(_kind), RingWidth, true);
            }
        }
    }
}
