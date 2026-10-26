using System;

namespace FTT.UI.Motion {

    /// <summary>
    /// UR-THEME. The MOT-B "pulp pop" numbers (UI_SPEC section 5) as plain C#,
    /// with no engine dependency so they are testable without a Godot runtime.
    /// <see cref="PulpMotion"/> and <see cref="ScreenWipe"/> are the engine
    /// halves; nothing here reads <c>ComfortSettings</c>, the callers do.
    ///
    /// <para>All timings are the spec's agent defaults (ledger
    /// <c>PROVISIONAL-UI-PULP-KIT</c>): the mockup only animates its demos.</para>
    /// </summary>
    public static class PulpMotionRules {

        // ---- Button focus pose (rows 1-3, 12) --------------------------------

        /// <summary>Pose tween when a button gains focus or the mouse.</summary>
        public const float FocusInSeconds = 0.14f;
        /// <summary>Tween back to identity when focus or the mouse leaves.</summary>
        public const float FocusOutSeconds = 0.10f;
        /// <summary>The focused pose's x travel in design pixels.</summary>
        public const float FocusTranslateX = 16f;
        /// <summary>The focused pose's tilt (negative leans counter-clockwise).</summary>
        public const float FocusTiltDegrees = -1.2f;
        /// <summary>The focused pose's scale about the centre.</summary>
        public const float FocusScale = 1.04f;
        /// <summary>The pressed pose's travel, applied on the press frame with no tween.</summary>
        public const float PressTranslate = 7f;
        /// <summary>The trading-card pick pose: lift in pixels and tilt in degrees.</summary>
        public const float CardLift = -10f;
        public const float CardTiltDegrees = 1.6f;

        // ---- Panels (row 4, 14) ----------------------------------------------

        public const float PanelPopSeconds = 0.10f;
        public const float PanelPopFromScale = 0.96f;
        /// <summary>Reduced Temporal Effects: a plain alpha fade.</summary>
        public const float PanelFadeSeconds = 0.15f;

        // ---- Numbers (row 8) -------------------------------------------------

        /// <summary>A number changes in this many equal steps on consecutive frames.</summary>
        public const int ThunkSteps = 3;
        /// <summary>One frame at the 60 Hz the game runs at.</summary>
        public const float FrameSeconds = 1f / 60f;

        // ---- Bars (rows 6, 7) ------------------------------------------------

        /// <summary>The HP damage tracker holds this long, then drops in <see cref="TrackerSteps"/> steps.</summary>
        public const float TrackerHoldSeconds = 0.5f;
        public const float TrackerDropSeconds = 0.6f;
        public const int TrackerSteps = 6;
        /// <summary>Reduced Temporal Effects: the tracker fades instead.</summary>
        public const float TrackerFadeSeconds = 0.2f;
        /// <summary>
        /// The ink-black hit flash on a bar plate lasts one frame and is rate-limited to
        /// once per second per bar (orchestrator ruling Q3): nothing strobes.
        /// </summary>
        public const float HitFlashMinIntervalSeconds = 1.0f;

        // ---- Screen wipe (row 5) ---------------------------------------------

        /// <summary>The slab slides across in this many discrete steps...</summary>
        public const int WipeSteps = 8;
        /// <summary>...in this long, per direction (cover, then reveal).</summary>
        public const float WipeSeconds = 0.26f;
        /// <summary>Reduced Temporal Effects: cross-fade through the scene.</summary>
        public const float WipeFadeSeconds = 0.20f;
        /// <summary>The slab's ink edge, in pixels.</summary>
        public const float WipeInkEdge = 6f;
        /// <summary>The slab's slanted trailing edge starts here, as a fraction of its width.</summary>
        public const float WipeSlantFraction = 0.88f;

        /// <summary>
        /// The values a number passes through: <see cref="ThunkSteps"/> equal steps ending
        /// exactly on <paramref name="to"/> (so the last one is never rounded away).
        /// </summary>
        public static int[] ThunkValues(int from, int to) {
            var values = new int[ThunkSteps];
            for (int i = 0; i < ThunkSteps; i++) {
                values[i] = i == ThunkSteps - 1
                    ? to
                    : from + (int)Math.Round((to - from) * (i + 1) / (double)ThunkSteps, MidpointRounding.AwayFromZero);
            }
            return values;
        }

        /// <summary>Quantises a 0..1 progress to <paramref name="steps"/> discrete positions (0 stays 0, 1 stays 1).</summary>
        public static float Stepped(float progress, int steps) {
            if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));
            if (progress <= 0f) return 0f;
            if (progress >= 1f) return 1f;
            return (float)Math.Ceiling(progress * steps) / steps;
        }

        /// <summary>
        /// The slab's left edge as a fraction of the slab width while covering (-1 at the start,
        /// 0 fully covered) and revealing (0 to +1), quantised to <see cref="WipeSteps"/>.
        /// </summary>
        public static float WipeOffsetCover(float progress) => Stepped(progress, WipeSteps) - 1f;

        public static float WipeOffsetReveal(float progress) => Stepped(progress, WipeSteps);
    }

    /// <summary>
    /// One bar's hit-flash rate limit. A multi-hit string lands faster than 1 Hz, and the
    /// flash must never strobe, so a flash inside the interval is swallowed.
    /// </summary>
    public sealed class HitFlashLimiter {
        private double _lastFlash = double.NegativeInfinity;

        /// <summary>True when the flash may play at <paramref name="nowSeconds"/>; records it.</summary>
        public bool TryFlash(double nowSeconds) {
            if (nowSeconds - _lastFlash < PulpMotionRules.HitFlashMinIntervalSeconds) return false;
            _lastFlash = nowSeconds;
            return true;
        }

        public void Reset() => _lastFlash = double.NegativeInfinity;
    }
}
