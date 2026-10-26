using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-HUDS. The Integrity dial's siphon streams (UI_SPEC 3.3.4, ruling 8: one zig-zag per living
    /// Extractor, fanned out). The two spec polylines A and B (a 100-unit box) bound the fan; stream k of n
    /// interpolates between them. Cold light with a soft glow; drawn only while <see cref="Count"/> is above 0.
    /// </summary>
    public partial class SiphonStreamsLayer : Control {

        /// <summary>Most streams drawn; more Extractors read as the same fan (the count stays in the tooltip).</summary>
        public const int MaxStreams = 5;

        private static readonly Vector2[] PathA = {
            new(100, 2), new(87, 10), new(91, 17), new(76, 26), new(80, 33), new(60, 46), new(63, 52), new(44, 60), new(31, 66)
        };
        private static readonly Vector2[] PathB = {
            new(100, 36), new(88, 44), new(92, 50), new(76, 60), new(79, 66), new(62, 76), new(64, 82), new(47, 92)
        };

        private int _count;

        public SiphonStreamsLayer() {
            Name = "SiphonDraw";
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public int Count {
            get => _count;
            set { if (_count == value) return; _count = value; Visible = value > 0; QueueRedraw(); }
        }

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

        public override void _Draw() {
            if (_count <= 0) return;
            float scale = Mathf.Min(Size.X, Size.Y) / 100f;
            int drawn = Mathf.Min(_count, MaxStreams);
            for (int k = 0; k < drawn; k++) {
                Vector2[] stream = Stream(k, _count);
                for (int i = 0; i < stream.Length; i++) stream[i] *= scale;
                DrawPolyline(stream, new Color(UIPalette.UnboundCold, 0.28f), 7f, true);
                DrawPolyline(stream, UIPalette.UnboundCold, 2.6f, true);
            }
        }
    }
}
