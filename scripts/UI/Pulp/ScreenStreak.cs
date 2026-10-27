using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The <c>dir-a</c> screen streak (UI_SPEC_GLASS 1.4, AC19): two faint hard-edged bands on the 118 degree
    /// gradient line of the whole screen, white @0.04 for t 0.69-0.6925 and white @0.022 for t 0.735-0.737. At 1920 x 1080
    /// the first runs from x 1721.0-1727.3 at the top to 1146.8-1153.0 at the bottom, the second from 1833.3-1838.3 to
    /// 1259.0-1264.0. For full-screen menus only (the main menu and its sub-screens, the select, loading, credits), never
    /// over gameplay, the pause dim or an overlay, where a static line reads as a screen fault. Never animated.
    ///
    /// <para>Use <c>PulpScreenKit.AddScreenStreak</c>: it adds the streak as the screen root's last child, full rect, above
    /// the screen's content (spec "z above everything"). The bands are computed from this control's own rect and rebuilt
    /// only when it changes size (<see cref="GeometryBuilds"/>).</para>
    /// </summary>
    public partial class ScreenStreak : Control {

        public const string NodeName = "ScreenStreak";
        public const float Degrees = 118f;
        public const float Band1Start = 0.69f, Band1End = 0.6925f;
        public const float Band2Start = 0.735f, Band2End = 0.737f;
        public static readonly Color Band1Color = new(1f, 1f, 1f, 0.04f);
        public static readonly Color Band2Color = new(1f, 1f, 1f, 0.022f);
        /// <summary>Draws above the screen's other children of the same canvas layer.</summary>
        public const int StreakZ = 10;

        private readonly Vector2[] _box = new Vector2[4];
        private readonly Vector2[] _scratch = new Vector2[8];
        private readonly Vector2[] _scratch2 = new Vector2[8];
        private Vector2[] _band1 = System.Array.Empty<Vector2>();
        private Vector2[] _band2 = System.Array.Empty<Vector2>();
        private Vector2 _builtFor = new(-1f, -1f);

        /// <summary>How many times the bands were rebuilt.</summary>
        public int GeometryBuilds { get; private set; }
        public Vector2[] Band1Polygon { get { EnsureGeometry(); return _band1; } }
        public Vector2[] Band2Polygon { get { EnsureGeometry(); return _band2; } }

        public ScreenStreak() {
            Name = NodeName;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = StreakZ;
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        private void EnsureGeometry() {
            if (Size == _builtFor) return;
            _builtFor = Size;
            GeometryBuilds++;
            var rect = new Rect2(Vector2.Zero, Size);
            _box[0] = rect.Position;
            _box[1] = new Vector2(rect.End.X, rect.Position.Y);
            _box[2] = rect.End;
            _box[3] = new Vector2(rect.Position.X, rect.End.Y);
            _band1 = Band(rect, Band1Start, Band1End);
            _band2 = Band(rect, Band2Start, Band2End);
        }

        private Vector2[] Band(Rect2 rect, float from, float to) {
            Vector2 d = GlassGeometry.GradientDirection(Degrees);
            int count = GlassGeometry.ClipHalfPlane(_box, 4, d, GlassGeometry.GradientOffset(rect, Degrees, to), _scratch);
            count = GlassGeometry.ClipHalfPlane(_scratch, count, -d, -GlassGeometry.GradientOffset(rect, Degrees, from), _scratch2);
            var result = new Vector2[count];
            System.Array.Copy(_scratch2, result, count);
            return result;
        }

        public override void _Draw() {
            EnsureGeometry();
            if (_band1.Length >= 3) DrawColoredPolygon(_band1, Band1Color);
            if (_band2.Length >= 3) DrawColoredPolygon(_band2, Band2Color);
        }
    }
}
