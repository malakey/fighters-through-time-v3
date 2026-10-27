using System.Collections.Generic;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-MENU, a kit widget since TG-CLOSE (it was <c>MainMenu.SlabFrame</c>). The edge a focusable glass slab (the
    /// Continue card, a slot) draws over its plate (UI_SPEC_GLASS 3.8): focused, the button focus edge (100 degrees,
    /// <c>focus</c> to rgba(glow,.55)) along its chamfer-18 hairline and the 14 px gold glow outside it; an empty slot's
    /// quiet dashed edge (<c>edge-dim</c>, 6 px dashes with 4 px gaps); or nothing. The outline, its colours and the
    /// dashes are rebuilt only when the size changes; the draw allocates nothing.
    /// </summary>
    public sealed partial class SlabFrame : Control {
        public enum FrameMode { None, Focus, Dashed }

        public const float EdgeWidth = GlassGeometry.EdgeWidth;
        public const float Dash = 6f, DashGap = 4f;
        public const int GlowSize = 14;

        private FrameMode _mode;
        private readonly Vector2[] _corners = new Vector2[6];
        private readonly Vector2[] _outline = new Vector2[7];
        private readonly Color[] _outlineColors = new Color[7];
        private Vector2[] _dashes = System.Array.Empty<Vector2>();
        private readonly StyleBoxFlat _glow;
        private Vector2 _builtFor = new(-1f, -1f);

        /// <summary>How many times the outline and dashes were rebuilt.</summary>
        public int GeometryBuilds { get; private set; }

        public SlabFrame() {
            MouseFilter = MouseFilterEnum.Ignore;
            int cut = Mathf.RoundToInt(GlassSheen.Chamfer);
            _glow = new StyleBoxFlat {
                DrawCenter = false, BgColor = new Color(0f, 0f, 0f, 0f), ShadowColor = UIPalette.FocusGlow, ShadowSize = GlowSize,
                AntiAliasing = true, CornerDetail = 1, CornerRadiusTopLeft = cut, CornerRadiusBottomRight = cut
            };
        }

        public FrameMode Mode {
            get => _mode;
            set { if (_mode == value) return; _mode = value; QueueRedraw(); }
        }

        /// <summary>The outline's vertex colours (the focus gradient), for tests.</summary>
        public Color[] OutlineColors { get { EnsureGeometry(); return _outlineColors; } }

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        private void EnsureGeometry() {
            if (Size == _builtFor) return;
            _builtFor = Size;
            GeometryBuilds++;
            var box = new Rect2(Vector2.Zero, Size);
            // The 1.5 px line is centred on the slab's hairline: half an edge inside the box, the cut eased to match.
            GlassGeometry.Chamfer(box.Grow(-EdgeWidth / 2f), Mathf.Max(0f, GlassSheen.Chamfer - EdgeWidth / 2f / 1.5f), _corners);
            for (int i = 0; i < 6; i++) _outline[i] = _corners[i];
            _outline[6] = _corners[0];
            for (int i = 0; i < 7; i++) {
                _outlineColors[i] = GlassGeometry.Stop2(UIPalette.Focus, UIPalette.FocusGlow, GlassGeometry.GradientT(_outline[i], box, 100f));
            }
            var dashes = new List<Vector2>();
            for (int i = 0; i < 6; i++) AddDashes(dashes, _outline[i], _outline[i + 1]);
            _dashes = dashes.ToArray();
        }

        private static void AddDashes(List<Vector2> into, Vector2 from, Vector2 to) {
            float length = from.DistanceTo(to);
            if (length <= 0f) return;
            Vector2 step = (to - from) / length;
            for (float at = 0f; at < length; at += Dash + DashGap) {
                into.Add(from + step * at);
                into.Add(from + step * Mathf.Min(length, at + Dash));
            }
        }

        public override void _Draw() {
            if (_mode == FrameMode.None) return;
            EnsureGeometry();
            if (_mode == FrameMode.Focus) {
                DrawStyleBox(_glow, new Rect2(Vector2.Zero, Size));
                DrawPolylineColors(_outline, _outlineColors, EdgeWidth, true);
            } else if (_dashes.Length >= 2) {
                DrawMultiline(_dashes, UIPalette.EdgeDim, EdgeWidth, true);
            }
        }
    }
}
