using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-HUDS. The 46 px sticker badge of the Story HUD's time tools (UI_SPEC 3.3.4: the rewind counter's
    /// hourglass and the Time Freeze snowflake): the same disc, 3 px ink ring and (3, 4) shadow the status
    /// chip draws, a centred <see cref="PulpIcon"/> at 62 % and an optional duration ring. Written by the
    /// HUD; it never ticks.
    /// </summary>
    public partial class HudBadge : Control {

        public const float RingWidth = 5f;
        private readonly PulpIcon _icon;
        private float _size = 46f;
        private float _ring;
        private Color _ringColor = UIPalette.Accent;

        public HudBadge() {
            Name = "HudBadge";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            _icon = new PulpIcon { Name = "Glyph", Tint = UIPalette.Accent2 };
            AddChild(_icon);
            Relayout();
        }

        /// <summary>The disc's diameter (46 for the time tools).</summary>
        public float BadgeSize { get => _size; set { _size = value; Relayout(); } }
        public string IconName { get => _icon.IconName; set => _icon.IconName = value; }
        public Color Tint { get => _icon.Tint; set => _icon.Tint = value; }
        /// <summary>Remaining share of a ring around the disc, 0 (no ring) to 1.</summary>
        public float Ring { get => _ring; set { if (!Mathf.IsEqualApprox(_ring, value)) { _ring = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); } } }
        public Color RingColor { get => _ringColor; set { _ringColor = value; QueueRedraw(); } }
        public PulpIcon Glyph => _icon;

        private void Relayout() {
            CustomMinimumSize = new Vector2(_size, _size);
            Size = CustomMinimumSize;
            float glyph = _size * 0.62f;
            _icon.Size = new Vector2(glyph, glyph);
            _icon.Position = Vector2.One * ((_size - glyph) / 2f);
            QueueRedraw();
        }

        public override void _Draw() {
            float radius = _size / 2f;
            var centre = new Vector2(radius, radius);
            DrawCircle(centre + new Vector2(3f, 4f), radius, Colors.Black);
            Texture2D disc = PulpKit.Texture("badge_disc");
            if (disc != null) DrawTextureRect(disc, new Rect2(Vector2.Zero, new Vector2(_size, _size)), false);
            DrawArc(centre, radius - 1.5f, 0f, Mathf.Tau, 64, Colors.Black, 3f, true);
            if (_ring > 0f) {
                float ringRadius = radius + 1f - RingWidth / 2f;
                DrawArc(centre, ringRadius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + _ring * Mathf.Tau, 64, _ringColor, RingWidth, true);
            }
        }
    }
}
