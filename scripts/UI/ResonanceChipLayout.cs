using System;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// UR-POLISH. Pure geometry for the Resonance grid's node chips. The authored normalized positions keep each
    /// topology's shape, but several (Mozart's ascending scale, Joan's spine) put neighbours closer than one chip, so
    /// at the pulp type sizes their chips overlapped. <see cref="Separate"/> keeps every chip at its authored position
    /// where that already clears its neighbours and pushes overlapping pairs apart along the axis of least overlap
    /// (never past the grid area), so the shape survives and no chip covers another.
    /// </summary>
    public static class ResonanceChipLayout {

        /// <summary>
        /// Chip centres, normalized to the area, with every pair at least <paramref name="gap"/> apart on at least one axis
        /// (chip edge to chip edge). <paramref name="resolved"/> is false when the area is too small to hold the chips.
        /// </summary>
        public static (float X, float Y)[] Separate(IReadOnlyList<(float X, float Y)> normalized,
            float areaWidth, float areaHeight, float chipWidth, float chipHeight, float gap, out bool resolved) {
            int count = normalized.Count;
            var x = new float[count];
            var y = new float[count];
            float minX = chipWidth / 2f, maxX = areaWidth - chipWidth / 2f;
            float minY = chipHeight / 2f, maxY = areaHeight - chipHeight / 2f;
            for (int i = 0; i < count; i++) {
                x[i] = Math.Clamp(normalized[i].X * areaWidth, minX, Math.Max(minX, maxX));
                y[i] = Math.Clamp(normalized[i].Y * areaHeight, minY, Math.Max(minY, maxY));
            }

            float needX = chipWidth + gap, needY = chipHeight + gap;
            for (int pass = 0; pass < 600; pass++) {
                bool moved = false;
                for (int i = 0; i < count; i++) {
                    for (int j = i + 1; j < count; j++) {
                        float dx = x[j] - x[i], dy = y[j] - y[i];
                        float overlapX = needX - Math.Abs(dx), overlapY = needY - Math.Abs(dy);
                        if (overlapX <= 0f || overlapY <= 0f) continue;
                        moved = true;
                        if (overlapX <= overlapY) {
                            float push = overlapX / 2f + 0.01f;
                            float sign = dx >= 0f ? 1f : -1f;
                            x[i] = Math.Clamp(x[i] - sign * push, minX, Math.Max(minX, maxX));
                            x[j] = Math.Clamp(x[j] + sign * push, minX, Math.Max(minX, maxX));
                        } else {
                            float push = overlapY / 2f + 0.01f;
                            float sign = dy >= 0f ? 1f : -1f;
                            y[i] = Math.Clamp(y[i] - sign * push, minY, Math.Max(minY, maxY));
                            y[j] = Math.Clamp(y[j] + sign * push, minY, Math.Max(minY, maxY));
                        }
                    }
                }
                if (!moved) break;
            }

            resolved = true;
            for (int i = 0; i < count && resolved; i++) {
                for (int j = i + 1; j < count; j++) {
                    if (needX - Math.Abs(x[j] - x[i]) > 0.001f && needY - Math.Abs(y[j] - y[i]) > 0.001f) { resolved = false; break; }
                }
            }
            var result = new (float X, float Y)[count];
            for (int i = 0; i < count; i++) result[i] = (x[i] / areaWidth, y[i] / areaHeight);
            return result;
        }
    }
}
