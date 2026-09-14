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
    /// V7.6 F14 (Package 11 A7a): the ADDITIVE second glyph channel, drawn
    /// beside the block-class glyph rather than replacing it.
    ///
    /// <para>The Siphon Snare is Basic-class against the block — its circle must
    /// stay, because a shield answers it for one charge — but it is not a hit,
    /// and the class channel alone would promise the wrong thing. The accent is
    /// therefore additive: circle <i>and</i> tether mark. Append-only, like
    /// <see cref="TelegraphGlyphShape"/>.</para>
    /// </summary>
    public enum TelegraphGlyphAccent {
        /// <summary>No accent — the ordinary single-channel class telegraph.</summary>
        None = 0,
        /// <summary>Ringed downward arrow: a meter-drain tether, not a strike.</summary>
        Tether = 1
    }

    /// <summary>
    /// V7.3 dual-channel telegraph (ruling #11): the second, colour-independent
    /// channel of the telegraph promise. The tint says it with colour
    /// (white/yellow = Basic, orange = Guard-Crush, red = Unblockable); this
    /// glyph says the same thing with SHAPE — open circle / diamond / X — drawn
    /// with <c>_Draw()</c> primitives so the class stays readable without
    /// colour vision. <see cref="EnemyAbilityExecutor"/> resolves both channels
    /// from the same class flags, so they can never disagree.
    ///
    /// <para>V7.6 F14 (A7a) adds two more colour-free channels on top, both
    /// optional and both additive: a <see cref="TelegraphGlyphAccent"/> drawn
    /// beside the class glyph, and a <see cref="BoundaryRadiusPixels"/> ring
    /// drawn around the caster's own origin so the Siphon Snare's 3-unit
    /// attachment range is visible before it resolves. Neither flashes and
    /// neither carries meaning in its colour.</para>
    /// </summary>
    public partial class TelegraphGlyph : Node2D {

        /// <summary>
        /// Localized explanation of the V7.6 tether accent, for the HUD/Systems
        /// Card surface that names the colour-free glyph vocabulary. Declared here
        /// so the key lives beside the shape it describes.
        /// </summary>
        public const string TetherTooltipKey = "telegraph_siphon_tether_tooltip";

        /// <summary>Glyph radius in pixels.</summary>
        public const float RadiusPixels = 14f;
        private const float StrokeWidth = 3f;

        /// <summary>Horizontal gap between the class glyph and the accent mark.</summary>
        public const float AccentOffsetPixels = 34f;

        /// <summary>Accent mark radius in pixels.</summary>
        public const float AccentRadiusPixels = 10f;

        /// <summary>The shape currently presented. Test seam.</summary>
        public TelegraphGlyphShape Shape { get; private set; } = TelegraphGlyphShape.Basic;

        /// <summary>The additive second channel currently presented. Test seam.</summary>
        public TelegraphGlyphAccent Accent { get; private set; } = TelegraphGlyphAccent.None;

        /// <summary>
        /// Radius of the caster-centred boundary ring, or 0 for no ring. Drawn
        /// around the OWNER's origin (this node hovers above it), so what the
        /// player sees is the actual attachment circle, not a decoration.
        /// </summary>
        public float BoundaryRadiusPixels { get; private set; }

        /// <summary>The colour currently presented (same value as the tint channel).</summary>
        public Color GlyphColor { get; private set; } = Colors.White;

        /// <summary>Shows the class glyph for one telegraph, with no accent or ring.</summary>
        public void Present(TelegraphGlyphShape shape, Color color) =>
            Present(shape, color, TelegraphGlyphAccent.None, 0f);

        /// <summary>
        /// Shows the class glyph plus the optional additive channels. The class
        /// shape is never replaced — an accent sits beside it.
        /// </summary>
        public void Present(TelegraphGlyphShape shape, Color color,
            TelegraphGlyphAccent accent, float boundaryRadiusPixels) {
            Shape = shape;
            GlyphColor = color;
            Accent = accent;
            BoundaryRadiusPixels = Mathf.Max(0f, boundaryRadiusPixels);
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

            if (Accent == TelegraphGlyphAccent.Tether) DrawTetherAccent();
            if (BoundaryRadiusPixels > 0f) {
                // Centred on the owner, not on this node: -Position walks back
                // down the offset the executor lifted the glyph by.
                DrawArc(-Position, BoundaryRadiusPixels, 0f, Mathf.Tau, 48, GlyphColor, StrokeWidth - 1f);
            }
        }

        /// <summary>A ringed downward arrow — "something is being pulled out of you".</summary>
        private void DrawTetherAccent() {
            const float a = AccentRadiusPixels;
            var centre = new Vector2(AccentOffsetPixels, 0f);
            DrawArc(centre, a, 0f, Mathf.Tau, 20, GlyphColor, StrokeWidth - 1f);
            DrawLine(centre + new Vector2(0f, -a * 0.7f), centre + new Vector2(0f, a * 0.7f),
                GlyphColor, StrokeWidth - 1f);
            DrawPolyline(new[] {
                centre + new Vector2(-a * 0.45f, a * 0.1f),
                centre + new Vector2(0f, a * 0.7f),
                centre + new Vector2(a * 0.45f, a * 0.1f)
            }, GlyphColor, StrokeWidth - 1f);
        }
    }
}
