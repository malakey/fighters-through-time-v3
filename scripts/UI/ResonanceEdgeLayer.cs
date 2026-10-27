using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 11 A4 (Resonance V7.6). Draws the constellation's prerequisite
    /// edges behind the node chips.
    ///
    /// <para>Design contract: <b>solid</b> for an All-of prerequisite,
    /// <b>dotted</b> for an Any-of prerequisite, and a brighter glowing line
    /// once the prerequisite end is unlocked. Before V7.6 there was no edge
    /// rendering at all — prerequisites were conveyed only by tooltip text.</para>
    ///
    /// <para>TG-SCREENS (Temporal Glass, orchestrator ruling R10): the satisfied edge is Resonance gold
    /// (<see cref="UIPalette.Res2"/>) with a soft glow under it, where it was cyan; resonance is gold under PAL-B, and cold
    /// cyan belongs to player 1 and the Severed. An unsatisfied edge keeps the built dormant slate. The pulp ink stroke
    /// under every edge is gone (glass draws no black outlines). The draw allocates nothing.</para>
    /// </summary>
    public partial class ResonanceEdgeLayer : Control {

        public readonly record struct Edge(
            Vector2 From,
            Vector2 To,
            bool AnyOf,
            bool Satisfied);

        private const float SolidWidth = 2.5f;
        private const float SatisfiedWidth = 4f;
        /// <summary>The soft glow drawn under a satisfied edge (its width, its alpha relative to the edge colour).</summary>
        private const float SatisfiedGlowWidth = 11f;
        private const float SatisfiedGlowAlpha = 0.22f;
        private const float DashLength = 9f;
        private const float DashGap = 7f;

        /// <summary>An unsatisfied prerequisite: the built dormant slate, quieter than the gold.</summary>
        public static readonly Color DormantColor = new(0.32f, 0.36f, 0.46f, 0.75f);
        /// <summary>A satisfied prerequisite: Resonance gold (R10).</summary>
        private static readonly Color SatisfiedColor = new(UIPalette.Res2, 0.95f);
        private static readonly Color SatisfiedGlow = new(UIPalette.Res2, SatisfiedGlowAlpha);

        /// <summary>The colour an edge draws in. Test surface.</summary>
        public static Color ColorFor(Edge edge) => edge.Satisfied ? SatisfiedColor : DormantColor;

        /// <summary>The line width an edge draws at. Test surface.</summary>
        public static float WidthFor(Edge edge) => edge.Satisfied ? SatisfiedWidth : SolidWidth;

        /// <summary>The width of the soft glow under an edge (0: none; only a satisfied edge glows). Test surface.</summary>
        public static float GlowWidthFor(Edge edge) => edge.Satisfied ? SatisfiedGlowWidth : 0f;

        /// <summary>True when an edge draws dotted (an Any-of prerequisite). Test surface.</summary>
        public static bool IsDotted(Edge edge) => edge.AnyOf;

        private readonly List<Edge> _edges = new();

        public override void _Ready() {
            // Edges are decoration behind the chips: never eat a click.
            MouseFilter = MouseFilterEnum.Ignore;
        }

        /// <summary>The edges currently drawn. Test surface.</summary>
        public IReadOnlyList<Edge> Edges => _edges;

        public void SetEdges(IEnumerable<Edge> edges) {
            _edges.Clear();
            if (edges != null) _edges.AddRange(edges);
            QueueRedraw();
        }

        public override void _Draw() {
            // Glows first, so no edge's glow lies over another edge's line.
            foreach (Edge edge in _edges) {
                if (!edge.Satisfied) continue;
                if (IsDotted(edge)) DrawDashed(edge.From, edge.To, SatisfiedGlow, SatisfiedGlowWidth);
                else DrawLine(edge.From, edge.To, SatisfiedGlow, SatisfiedGlowWidth, true);
            }
            foreach (Edge edge in _edges) {
                Color color = ColorFor(edge);
                float width = WidthFor(edge);
                if (IsDotted(edge)) DrawDashed(edge.From, edge.To, color, width);
                else DrawLine(edge.From, edge.To, color, width, true);
            }
        }

        private void DrawDashed(Vector2 from, Vector2 to, Color color, float width) {
            Vector2 span = to - from;
            float length = span.Length();
            if (length <= 0.01f) return;
            Vector2 step = span / length;
            float travelled = 0f;
            while (travelled < length) {
                float segment = Mathf.Min(DashLength, length - travelled);
                DrawLine(from + step * travelled, from + step * (travelled + segment), color, width, true);
                travelled += DashLength + DashGap;
            }
        }
    }
}
