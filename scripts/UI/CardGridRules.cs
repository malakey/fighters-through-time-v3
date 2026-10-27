using System;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// TG-SELECT (Temporal Glass, CARD-A; UI_SPEC_GLASS 4.2, 8.3; first written by UR-SELECT for the pulp cards). The pure rules
    /// of the Fighter select's tile grid: three columns of glass tiles in roster order, then the Random tile on a row of its own
    /// spanning the grid's width. Nothing here names a roster size: a new manifest member adds a tile, and a fourth row of them
    /// starts the grid higher.
    /// <list type="bullet">
    /// <item>Up to three rows (nine fighters) the grid is drawn where 4.2 places it: 164 px tiles at a 180 px pitch from y 196,
    /// the Random tile 524 x 72 below them, its bottom on y 808.</item>
    /// <item>More rows keep the Random tile's bottom on that line and grow the grid upward (it starts higher), inside a band
    /// from 16 px under the title (y 112) to that line. Four rows of full tiles and the Random tile are 792 px tall and the
    /// band 696, so from the fourth row on the tiles take the compact 140 px size at a 152 px pitch (four rows: 680 px, top
    /// 128); past what the compact size fits they shrink further at the compact proportions, never under half the full pitch.
    /// (UI_SPEC_GLASS 8.3 keeps full tiles for a fourth row and moves the Random tile down 180 px; that block would reach
    /// y 898, through the countdown line and the footer at 878. Recorded in the TG-SELECT handoff.)</item>
    /// </list>
    /// The grid geometry does not follow the UI scale (4.2).
    /// </summary>
    public static class CardGridRules {

        public const int Columns = 3;
        /// <summary>The rows the drawn full-size layout holds (4.2).</summary>
        public const int MaxFullRows = 3;
        public const float TileSize = 164f, TilePitch = 180f;
        public const float CompactTileSize = 140f, CompactPitch = 152f;
        public const float RandomHeight = 72f;
        /// <summary>The drawn grid top (4.2: y 196) and the Random tile's bottom line (y 808), in screen pixels.</summary>
        public const float GridTop = 196f, BlockBottom = 808f;
        /// <summary>The highest the grid may start: 16 px under the title's 96 px bottom.</summary>
        public const float BandTop = 112f;
        /// <summary>The smallest pitch the shrink rule goes to (half the full pitch).</summary>
        public const float MinPitch = TilePitch / 2f;

        /// <summary>A layout of the grid: tile size, pitch (tile + gap), the roster's rows and the grid's top on the screen.</summary>
        public readonly record struct GridLayout(float Tile, float Pitch, int Rows, float Top) {
            public float Gap => Pitch - Tile;
            /// <summary>Three columns: 524 at the full size, 444 compact.</summary>
            public float Width => Columns * Pitch - Gap;
            /// <summary>The Random tile's top, relative to the grid's top.</summary>
            public float RandomTop => Rows * Pitch;
            /// <summary>The tiles and the Random tile, top to bottom.</summary>
            public float BlockHeight => BlockHeightAt(Rows, Pitch);
            public float Bottom => Top + BlockHeight;
            public bool Compact => Tile < TileSize;
        }

        /// <summary>Rows the roster's tiles need (the Random tile has a row of its own below them).</summary>
        public static int RowsFor(int rosterCount) => Math.Max(1, (rosterCount + Columns - 1) / Columns);

        /// <summary>The block's height (the tiles, a gap and the Random tile) for <paramref name="rows"/> rows at a pitch.</summary>
        public static float BlockHeightAt(int rows, float pitch) => rows * pitch + RandomHeight;

        /// <summary>The grid's layout for a roster of <paramref name="rosterCount"/> (see the class summary).</summary>
        public static GridLayout LayoutFor(int rosterCount) {
            int rows = RowsFor(rosterCount);
            if (rows <= MaxFullRows) return new GridLayout(TileSize, TilePitch, rows, GridTop);
            float band = BlockBottom - BandTop;
            if (BlockHeightAt(rows, CompactPitch) <= band) {
                return new GridLayout(CompactTileSize, CompactPitch, rows, BlockBottom - BlockHeightAt(rows, CompactPitch));
            }
            float pitch = Math.Max(MinPitch, (band - RandomHeight) / rows);
            float tile = pitch * CompactTileSize / CompactPitch;
            return new GridLayout(tile, pitch, rows, BlockBottom - BlockHeightAt(rows, pitch));
        }

        /// <summary>
        /// One cursor step on the grid. The Random tile is index <paramref name="rosterCount"/>, alone on the last row. A
        /// horizontal step wraps among the filled cells of the cursor's row (the Random row has one); a vertical step wraps
        /// through the roster rows and the Random row, keeping the column, or the row's last tile where the row is short.
        /// </summary>
        public static int Step(int cursor, int dx, int dy, int rosterCount) {
            int random = rosterCount;
            if (rosterCount <= 0) return 0;
            int rows = RowsFor(rosterCount);
            cursor = Math.Clamp(cursor, 0, random);
            int row = cursor == random ? rows : cursor / Columns;
            int column = cursor == random ? 0 : cursor % Columns;
            if (dx != 0 && row < rows) {
                int filled = Math.Min(Columns, rosterCount - row * Columns);
                column = ((column + dx) % filled + filled) % filled;
            }
            if (dy != 0) row = ((row + dy) % (rows + 1) + rows + 1) % (rows + 1);
            if (row == rows) return random;
            return Math.Min(row * Columns + column, rosterCount - 1);
        }
    }

    /// <summary>The largest value of each stat-tube stat across the roster: the tubes' full scale (UR-SELECT; UI_SPEC_GLASS 3.7).</summary>
    public readonly record struct CardStatMaxima(float Hp, float Speed, float Weight) {

        /// <summary>The maxima of the supplied (HP, speed, weight) triples; zero for an empty roster.</summary>
        public static CardStatMaxima FromValues(IEnumerable<(float Hp, float Speed, float Weight)> values) {
            float hp = 0f, speed = 0f, weight = 0f;
            foreach ((float h, float s, float w) in values) {
                hp = Math.Max(hp, h); speed = Math.Max(speed, s); weight = Math.Max(weight, w);
            }
            return new CardStatMaxima(hp, speed, weight);
        }
    }
}
