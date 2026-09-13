using System;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// Pure adjacency navigation for controller/D-pad and keyboard movement over
    /// a Resonance constellation.
    ///
    /// <para>Package 11 A4 replaced the old row-major index arithmetic: V7.6
    /// gives every character a UNIQUE topology (mesh, spine, gear rings,
    /// circuit, three acts, scale, delta, fence, crossing currents) authored as
    /// normalized <c>LayoutPosition</c> points, so "one column left" is no
    /// longer a meaningful move. Navigation now picks the nearest node lying in
    /// the pressed direction.</para>
    ///
    /// <para>Selection rule: among nodes whose offset from the current node
    /// points more along the pressed axis than across it, take the one with the
    /// smallest weighted distance (cross-axis error counts double, so a node
    /// straight ahead beats a closer node far off to the side). No candidate
    /// means the selection CLAMPS - pressing into the edge of the constellation
    /// keeps the current node, matching the previous behaviour and typical
    /// talent-grid conventions.</para>
    ///
    /// <para>No Godot dependency, so it is testable without a scene tree.</para>
    /// </summary>
    public static class ResonanceGridNavigation {

        /// <summary>Cross-axis error weight; a node straight ahead wins over a nearer node off-axis.</summary>
        public const float CrossAxisPenalty = 2.0f;

        /// <summary>
        /// Moves the focused index one step in the pressed direction over the
        /// authored layout. <paramref name="deltaColumn"/> is -1/0/+1 for
        /// left/right and <paramref name="deltaRow"/> is -1/0/+1 for up/down
        /// (screen space: +1 row is DOWN, matching normalized Y). Returns the
        /// current index unchanged when nothing lies that way.
        /// </summary>
        public static int Move(
            int currentIndex,
            int deltaColumn,
            int deltaRow,
            IReadOnlyList<(float X, float Y)> positions) {
            if (positions == null || positions.Count == 0) return 0;
            int clamped = Math.Clamp(currentIndex, 0, positions.Count - 1);
            if (deltaColumn == 0 && deltaRow == 0) return clamped;

            (float X, float Y) origin = positions[clamped];
            int best = clamped;
            float bestScore = float.MaxValue;
            for (int i = 0; i < positions.Count; i++) {
                if (i == clamped) continue;
                float dx = positions[i].X - origin.X;
                float dy = positions[i].Y - origin.Y;
                float along = deltaColumn * dx + deltaRow * dy;
                if (along <= 0f) continue;
                float across = deltaColumn != 0 ? Math.Abs(dy) : Math.Abs(dx);
                // Reject anything more sideways than forward: it is not in the
                // pressed direction in any useful sense.
                if (across > along) continue;
                float score = along + CrossAxisPenalty * across;
                if (score < bestScore) {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}
