using Godot;

namespace FTT.Enemies {

    /// <summary>Which block-class glyph the telegraph draws (V7.3 ruling #11).</summary>
    public enum TelegraphGlyphShape {
        /// <summary>Open circle — Basic, blockable for 1 charge.</summary>
        Basic = 0,
        /// <summary>Diamond — Guard-Crush, 2 charges.</summary>
        GuardCrush = 1,
        /// <summary>X — Unblockable (boss-only).</summary>
        Unblockable = 2
    }

    /// <summary>
    /// V7.3 dual-channel telegraph (ruling #11): the second, colour-independent
    /// channel of the telegraph promise. The tint says it with colour
    /// (white/yellow = Basic, orange = Guard-Crush, red = Unblockable); this
    /// glyph says the same thing with SHAPE — open circle / diamond / X — drawn
    /// with <c>_Draw()</c> primitives so the class stays readable without
    /// colour vision. <see cref="EnemyAbilityExecutor"/> resolves both channels
    /// from the same class flags, so they can never disagree.
    /// </summary>
    public partial class TelegraphGlyph : Node2D {

        /// <summary>Glyph radius in pixels.</summary>
        public const float RadiusPixels = 14f;
        private const float StrokeWidth = 3f;

        /// <summary>The shape currently presented. Test seam.</summary>
        public TelegraphGlyphShape Shape { get; private set; } = TelegraphGlyphShape.Basic;

        /// <summary>The colour currently presented (same value as the tint channel).</summary>
        public Color GlyphColor { get; private set; } = Colors.White;

        /// <summary>Shows the glyph for one telegraph.</summary>
        public void Present(TelegraphGlyphShape shape, Color color) {
            Shape = shape;
            GlyphColor = color;
            Visible = true;
            QueueRedraw();
        }

        public override void _Draw() {
            const float r = RadiusPixels;
            switch (Shape) {
                case TelegraphGlyphShape.Unblockable:
                    DrawLine(new Vector2(-r, -r), new Vector2(r, r), GlyphColor, StrokeWidth);
                    DrawLine(new Vector2(-r, r), new Vector2(r, -r), GlyphColor, StrokeWidth);
                    break;
                case TelegraphGlyphShape.GuardCrush:
                    DrawPolyline(new[] {
                        new Vector2(0f, -r), new Vector2(r, 0f),
                        new Vector2(0f, r), new Vector2(-r, 0f), new Vector2(0f, -r)
                    }, GlyphColor, StrokeWidth);
                    break;
                default:
                    DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 24, GlyphColor, StrokeWidth);
                    break;
            }
        }
    }
}
