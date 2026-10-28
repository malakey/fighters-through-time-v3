using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-HUDS (Temporal Glass, UI_SPEC_GLASS 3.4.4 and 6.2; first written by UR-HUDS). The Integrity dial's siphon streams:
    /// one zig-zag per living Extractor, fanned out between the spec's two polylines A and B (a 100-unit box scaled to the
    /// control, 87.36 px at (dial x + 69.4, dial y - 40.3)). Each stream is a <see cref="Line2D"/> 1.92 px wide with miter
    /// joints in the Severed's cold light (<see cref="UIPalette.UnboundCold"/>), over a 6 px glow line at .35 (the 4 px CSS
    /// glow, agent choice 6.2). The lines are made once and their points rewritten only when the count or the size
    /// changes; nothing is drawn per frame. <see cref="Count"/> 0 hides the layer.
    /// </summary>
    public partial class SiphonStreamsLayer : Control {

        /// <summary>Most streams drawn; more Extractors read as the same fan (the count stays in the tooltip).</summary>
        public const int MaxStreams = 5;
        /// <summary>The stream stroke: 2.2 units of the 100-unit box (1.92 px at 87.36).</summary>
        public const float StrokeUnits = 2.2f;
        /// <summary>The glow line under each stream (agent choice for the CSS 0 0 4px glow).</summary>
        public const float GlowWidth = 6f, GlowAlpha = 0.35f;
        /// <summary>The streams' box at 100 % (UI_SPEC_GLASS 4.4).</summary>
        public const float BoxSize = 87.36f;

        private static readonly Vector2[] PathA = {
            new(100, 2), new(87, 10), new(91, 17), new(76, 26), new(80, 33), new(60, 46), new(63, 52), new(44, 60), new(31, 66)
        };
        private static readonly Vector2[] PathB = {
            new(100, 36), new(88, 44), new(92, 50), new(76, 60), new(79, 66), new(62, 76), new(64, 82), new(47, 92)
        };

        private int _count;
        private readonly Line2D[] _lines = new Line2D[MaxStreams];
        private readonly Line2D[] _glows = new Line2D[MaxStreams];
        private Vector2 _builtSize = new(-1f, -1f);
        private int _builtCount = -1;

        public SiphonStreamsLayer() {
            Name = "SiphonDraw";
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;
            for (int k = 0; k < MaxStreams; k++) {
                _glows[k] = new Line2D {
                    Name = $"Glow{k}", Width = GlowWidth, DefaultColor = new Color(UIPalette.UnboundCold, GlowAlpha),
                    JointMode = Line2D.LineJointMode.Sharp, Antialiased = true, Visible = false
                };
                AddChild(_glows[k]);
            }
            for (int k = 0; k < MaxStreams; k++) {
                _lines[k] = new Line2D {
                    Name = $"Stream{k}", DefaultColor = UIPalette.UnboundCold, JointMode = Line2D.LineJointMode.Sharp,
                    Antialiased = true, Visible = false
                };
                AddChild(_lines[k]);
            }
        }

        public int Count {
            get => _count;
            set { if (_count == value) return; _count = value; Visible = value > 0; Rebuild(); }
        }

        /// <summary>The stream lines (read-only), each over its glow line; only the first <c>min(Count, MaxStreams)</c> show.</summary>
        public Line2D[] StreamLines => _lines;

        /// <summary>The glow lines under the streams (read-only).</summary>
        public Line2D[] GlowLines => _glows;

        /// <summary>The streams' points in the 100-unit box: <paramref name="index"/> of <paramref name="count"/>.</summary>
        public static Vector2[] Stream(int index, int count) {
            int drawn = Mathf.Clamp(count, 1, MaxStreams);
            float t = drawn == 1 ? 0.5f : Mathf.Clamp(index, 0, drawn - 1) / (float)(drawn - 1);
            const int points = 9;
            var result = new Vector2[points];
            for (int i = 0; i < points; i++) {
                float u = i / (float)(points - 1);
                result[i] = Sample(PathA, u).Lerp(Sample(PathB, u), t);
            }
            return result;
        }

        private static Vector2 Sample(Vector2[] path, float u) {
            float f = u * (path.Length - 1);
            int i = Mathf.Min((int)f, path.Length - 2);
            return path[i].Lerp(path[i + 1], f - i);
        }

        public override void _Notification(int what) {
            if (what == NotificationResized) Rebuild();
        }

        // Rewrites the lines' points: only when the count or the box changes (never per frame).
        private void Rebuild() {
            Vector2 size = Size.X > 0f && Size.Y > 0f ? Size : new Vector2(BoxSize, BoxSize);
            if (size == _builtSize && _count == _builtCount) return;
            _builtSize = size;
            _builtCount = _count;
            float scale = Mathf.Min(size.X, size.Y) / 100f;
            int drawn = Mathf.Clamp(_count, 0, MaxStreams);
            for (int k = 0; k < MaxStreams; k++) {
                bool show = k < drawn;
                _lines[k].Visible = show;
                _glows[k].Visible = show;
                if (!show) continue;
                Vector2[] stream = Stream(k, _count);
                for (int i = 0; i < stream.Length; i++) stream[i] *= scale;
                _lines[k].Width = StrokeUnits * scale;
                _glows[k].Width = GlowWidth * Mathf.Min(1f, scale * 100f / BoxSize);
                _lines[k].Points = stream;
                _glows[k].Points = stream;
            }
        }
    }
}
