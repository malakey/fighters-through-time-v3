using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-PAUSE. The dialogue box's second-speaker frame (UI_SPEC_GLASS 3.9; plan 0.4 P6): the other speaker's 150 px G5
    /// portrait frame at opacity .4 holding a silhouette instead of art (a head circle of radius 19 at (50, 36) and the
    /// shoulders <c>M12 100 C12 70 30 60 50 60 C70 60 88 70 88 100 Z</c> in a 100-unit box, inset 14 % into the frame's inner
    /// box, in <c>ink3</c>). The dialogue box shows it only while the sequence names a second speaker.
    ///
    /// <para>The silhouette's polygons are built once (the frame has a fixed size); the draw allocates nothing.</para>
    /// </summary>
    public partial class DialogueSilhouetteFrame : Control {

        public const float Opacity = 0.4f;
        public const float SilhouetteInset = 0.14f;
        private const int HeadSegments = 32;
        private const int CurveSegments = 12;

        private readonly PulpPortraitRoundel _frame;

        public DialogueSilhouetteFrame() {
            MouseFilter = MouseFilterEnum.Ignore;
            Modulate = new Color(1f, 1f, 1f, Opacity);
            CustomMinimumSize = new Vector2(DialogueTheme.PortraitSize, DialogueTheme.PortraitSize);
            _frame = new PulpPortraitRoundel { Name = "Frame", Diameter = DialogueTheme.PortraitSize };
            AddChild(_frame);
            var layer = new SilhouetteLayer { Name = "Silhouette", MouseFilter = MouseFilterEnum.Ignore };
            layer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(layer);
        }

        /// <summary>The G5 frame under the silhouette. Test surface.</summary>
        public PulpPortraitRoundel Frame => _frame;

        /// <summary>The 14 % inset box the 100-unit silhouette is drawn into, for a frame of side <paramref name="side"/>.</summary>
        public static Rect2 SilhouetteBox(float side) {
            Rect2 inner = PulpPortraitRoundel.InnerBox(side);
            return inner.Grow(-inner.Size.X * SilhouetteInset);
        }

        private partial class SilhouetteLayer : Control {
            private Vector2[] _head;
            private Vector2[] _shoulders;

            public override void _Draw() {
                if (_head == null) Build();
                DrawColoredPolygon(_head, UIPalette.Ink3);
                DrawColoredPolygon(_shoulders, UIPalette.Ink3);
            }

            private void Build() {
                Rect2 box = SilhouetteBox(DialogueTheme.PortraitSize);
                Vector2 Map(float u, float v) => box.Position + new Vector2(u, v) / 100f * box.Size;
                _head = new Vector2[HeadSegments];
                Vector2 centre = Map(50f, 36f);
                float radius = 19f / 100f * box.Size.X;
                for (int i = 0; i < HeadSegments; i++) {
                    float angle = Mathf.Tau * i / HeadSegments;
                    _head[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                // Two cubic curves from (12, 100) over the shoulders to (88, 100); the base closes the shape.
                _shoulders = new Vector2[2 * CurveSegments + 1];
                int n = 0;
                for (int i = 0; i <= CurveSegments; i++) {
                    float t = (float)i / CurveSegments;
                    _shoulders[n++] = Cubic(Map(12f, 100f), Map(12f, 70f), Map(30f, 60f), Map(50f, 60f), t);
                }
                for (int i = 1; i <= CurveSegments; i++) {
                    float t = (float)i / CurveSegments;
                    _shoulders[n++] = Cubic(Map(50f, 60f), Map(70f, 60f), Map(88f, 70f), Map(88f, 100f), t);
                }
            }

            private static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
                float u = 1f - t;
                return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            }
        }
    }
}
