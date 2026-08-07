using System;

namespace FTT.UI {

    /// <summary>
    /// Pure index math for controller/D-pad and keyboard navigation over a
    /// row-major node grid. Navigation CLAMPS at the grid edges (no wrap):
    /// pressing left on the first column or up on the first row keeps the
    /// current selection, matching typical talent-grid conventions and keeping
    /// branch rows spatially stable. No Godot dependency so it is testable
    /// without a scene tree.
    /// </summary>
    public static class ResonanceGridNavigation {
        /// <summary>
        /// Moves a focused index by one grid step. <paramref name="deltaColumn"/>
        /// is -1/0/+1 for left/right, <paramref name="deltaRow"/> is -1/0/+1 for
        /// up/down. Out-of-range input indices are clamped into the grid first.
        /// </summary>
        public static int Move(int currentIndex, int deltaColumn, int deltaRow, int columns, int nodeCount) {
            if (columns <= 0 || nodeCount <= 0) return 0;
            int clamped = Math.Clamp(currentIndex, 0, nodeCount - 1);
            int rows = (nodeCount + columns - 1) / columns;
            int column = Math.Clamp(clamped % columns + deltaColumn, 0, columns - 1);
            int row = Math.Clamp(clamped / columns + deltaRow, 0, rows - 1);
            return Math.Min(row * columns + column, nodeCount - 1);
        }
    }
}
