using System;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// UR-SELECT. The pure rules of the Fighter select's trading-card grid (UI_SPEC 4.2, plan decision 14): five
    /// columns, as many rows as the roster needs, a compact scale once it outgrows two rows. Nothing here names a
    /// roster size, so a tenth fighter simply adds a row.
    /// </summary>
    public static class CardGridRules {

        public const int Columns = 5;
        public const int MaxFullRows = 2;
        public const float CardWidth = 196f;
        public const float CardHeight = 276f;
        public const float ColumnGap = 18f;
        public const float RowGap = 24f;
        public const float MinScale = 0.5f;

        /// <summary>Rows needed for <paramref name="tileCount"/> tiles (the roster plus the Random tile).</summary>
        public static int RowsFor(int tileCount) => Math.Max(1, (tileCount + Columns - 1) / Columns);

        /// <summary>1 while the grid fits two rows, else the factor that brings its height back to two rows (never below <see cref="MinScale"/>).</summary>
        public static float ScaleFor(int tileCount) {
            int rows = RowsFor(tileCount);
            return rows <= MaxFullRows ? 1f : Math.Max(MinScale, (float)MaxFullRows / rows);
        }
    }

    /// <summary>The largest value of each stat-bar stat across the roster: the bars' full scale (UI_SPEC 8.3 item 2).</summary>
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
