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
    /// </summary>
    public partial class ResonanceEdgeLayer : Control {

        public readonly record struct Edge(
            Vector2 From,
            Vector2 To,
            bool AnyOf,
            bool Satisfied);

        private const float SolidWidth = 2.5f;
        private const float SatisfiedWidth = 4f;
        private const float DashLength = 9f;
        private const float DashGap = 7f;

        private static readonly Color DormantColor = new(0.32f, 0.36f, 0.46f, 0.75f);
        private static readonly Color SatisfiedColor = new(0.30f, 0.95f, 0.92f, 0.95f);

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
            foreach (Edge edge in _edges) {
                Color color = edge.Satisfied ? SatisfiedColor : DormantColor;
                float width = edge.Satisfied ? SatisfiedWidth : SolidWidth;
                if (edge.AnyOf) DrawDashed(edge.From, edge.To, color, width);
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
