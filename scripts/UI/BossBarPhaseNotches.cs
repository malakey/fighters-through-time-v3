using System;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B1. Turns an authored <c>BossData.PhaseThresholds</c> array into
    /// the normalised notch positions the Story HUD draws across the boss bar.
    ///
    /// The thresholds are the single canonical source: <c>BossController</c>
    /// advances a phase when the remaining HP fraction drops to or below the next
    /// entry, so a notch belongs at exactly that fraction of the bar's width and
    /// the player sees where the fight is about to change. Nothing here invents a
    /// number — a boss with no authored thresholds (the Level 13 Mirror Paradox is
    /// the one in the roster) draws no notches at all.
    ///
    /// This is deliberately a pure model with no Godot dependency so the content
    /// suite can walk every authored <c>BossData</c> and prove the derivation
    /// without instantiating a HUD.
    /// </summary>
    public static class BossBarPhaseNotches {

        /// <summary>
        /// Normalised notch positions (0 = empty end of the bar, 1 = full),
        /// ordered from the highest HP fraction downwards — the order the fight
        /// crosses them.
        ///
        /// Entries outside the open interval (0, 1) are dropped rather than
        /// clamped: a notch pinned to either end of the bar would be invisible
        /// under the bar's own border and would read as a rendering bug rather
        /// than as authored data. Duplicates are collapsed for the same reason.
        /// </summary>
        public static List<float> Normalized(IReadOnlyList<float> thresholds) {
            var positions = new List<float>();
            if (thresholds == null) return positions;

            for (int index = 0; index < thresholds.Count; index++) {
                float value = thresholds[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                if (value <= 0f || value >= 1f) continue;
                if (positions.Contains(value)) continue;
                positions.Add(value);
            }

            positions.Sort((left, right) => right.CompareTo(left));
            return positions;
        }

        /// <summary>
        /// Phases the bar represents: one more than the number of usable notches.
        /// Mirrors <c>BossData.PhaseCount</c> for the thresholds that survive
        /// <see cref="Normalized"/>, which is what the player can actually see.
        /// </summary>
        public static int VisiblePhaseCount(IReadOnlyList<float> thresholds) =>
            Normalized(thresholds).Count + 1;

        /// <summary>
        /// Pixel offsets of the notches along a bar of <paramref name="barWidth"/>.
        /// A non-positive width yields no offsets: a bar that has not been laid out
        /// yet must not scatter notches at zero.
        /// </summary>
        public static List<float> Offsets(IReadOnlyList<float> thresholds, float barWidth) {
            var offsets = new List<float>();
            if (barWidth <= 0f) return offsets;
            foreach (float position in Normalized(thresholds)) {
                offsets.Add(position * barWidth);
            }
            return offsets;
        }

        /// <summary>
        /// Index of the phase a boss is in at <paramref name="hpFraction"/>, using
        /// the same "at or below the threshold" rule as
        /// <c>BossController.CheckPhaseTransition</c>. Present so a HUD rebuilt
        /// mid-fight (a rewind, a scene resume) can colour the crossed notches
        /// without asking the boss.
        /// </summary>
        public static int PhaseAt(IReadOnlyList<float> thresholds, float hpFraction) {
            List<float> positions = Normalized(thresholds);
            int phase = 0;
            while (phase < positions.Count && hpFraction <= positions[phase]) phase++;
            return Math.Min(phase, positions.Count);
        }
    }
}
