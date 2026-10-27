using System;

namespace FTT.UI.Motion {

    /// <summary>
    /// TG-THEME (Temporal Glass rebuild, first written by UR-THEME for the pulp redesign; the class keeps its
    /// legacy name). The MOT-A "glass drift" numbers (UI_SPEC_GLASS section 5) as plain C#, with no engine
    /// dependency so they are testable without a Godot runtime. <see cref="PulpMotion"/>, <see cref="FocusPose"/>
    /// and <see cref="ScreenWipe"/> are the engine halves; nothing here reads <c>ComfortSettings</c>, the callers do.
    ///
    /// <para>Rows 4 (the light band), 5, 7, 13-15, 18 and 19 and the single ready bloom are the spec's agent choices
    /// (AC20); everything else is the mock's note. Nothing flashes faster than once a second.</para>
    /// </summary>
    public static class PulpMotionRules {

        // ---- Button focus pose (rows 1-3) -------------------------------------

        /// <summary>Focus slides the row and lights its edge over 120 ms, ease-out (Quad).</summary>
        public const float FocusInSeconds = 0.12f;
        /// <summary>Losing focus slides it back over the same 120 ms.</summary>
        public const float FocusOutSeconds = 0.12f;
        /// <summary>The focused row's slide in design pixels (the only geometry focus moves).</summary>
        public const float FocusTranslateX = 14f;
        /// <summary>MOT-A tilts nothing (kept as a named zero: the focus row gap and its tests read it).</summary>
        public const float FocusTiltDegrees = 0f;
        /// <summary>MOT-A scales nothing on focus (kept as a named one: the focus row gap and its tests read it).</summary>
        public const float FocusScale = 1f;
        /// <summary>The pressed row's slide from rest: from focus the row moves 8 px back.</summary>
        public const float PressTranslate = 6f;
        /// <summary>The pressed row's scale about its centre.</summary>
        public const float PressScale = 0.985f;
        /// <summary>Releasing a press returns to the focus state over this long.</summary>
        public const float PressReleaseSeconds = 0.12f;
        /// <summary>Legacy (pulp trading cards; the closeout deletes it): the card pick pose's lift and tilt.</summary>
        public const float CardLift = -10f;
        public const float CardTiltDegrees = 1.6f;

        // ---- Panels (rows 4, 5, 14) ------------------------------------------

        /// <summary>A panel fades in and rises 24 px over 220 ms, ease-out (Cubic).</summary>
        public const float PanelInSeconds = 0.22f;
        public const float PanelRisePixels = 24f;
        /// <summary>A panel fades out and sinks 12 px over 160 ms, ease-in (Quad).</summary>
        public const float PanelOutSeconds = 0.16f;
        public const float PanelSinkPixels = 12f;
        /// <summary>Reduced Temporal Effects: a plain alpha fade in (no rise, no band).</summary>
        public const float PanelFadeSeconds = 0.15f;
        /// <summary>Reduced Temporal Effects: a plain alpha fade out.</summary>
        public const float PanelFadeOutSeconds = 0.12f;
        /// <summary>The light band that crosses a panel once as it appears: white at 0.10, 40 px wide, 600 ms, linear.</summary>
        public const float PanelBandSeconds = 0.6f;
        public const float PanelBandWidth = 40f;
        public const float PanelBandAlpha = 0.10f;
        /// <summary>The pause dim fades to 0.70 over 160 ms, linear, and back the same.</summary>
        public const float PauseDimSeconds = 0.16f;

        // ---- Bars and lenses (rows 7, 10, 11) ----------------------------------

        /// <summary>The HP damage tracker holds this long after a hit...</summary>
        public const float TrackerHoldSeconds = 0.5f;
        /// <summary>...then drains to the fill over this long (Sine InOut).</summary>
        public const float TrackerDrainSeconds = 0.6f;
        /// <summary>Reduced Temporal Effects: the tracker fades out over 200 ms instead of draining.</summary>
        public const float TrackerFadeSeconds = 0.2f;
        /// <summary>Healing: the fill rises over 200 ms (ease-out) and the tracker snaps to it.</summary>
        public const float HealRiseSeconds = 0.2f;
        /// <summary>Entering the ready state: one 600 ms bloom, peaking at 300 ms, never looping.</summary>
        public const float BloomSeconds = 0.6f;
        public const float BloomPeakSeconds = 0.3f;
        /// <summary>At the bloom's peak the lens is modulated this bright...</summary>
        public const float BloomPeakModulate = 1.35f;
        /// <summary>...and its outer glow is this many times its steady ready alpha (it starts at 0, settles at 1).</summary>
        public const float BloomPeakGlow = 1.5f;
        /// <summary>Low HP: the track's 7 px danger glow breathes 0 to 1 to 0 over 2 s (0.5 Hz); steady under Reduced.</summary>
        public const float LowPulseSeconds = 2f;

        // ---- Small cues (rows 13, 15, 16, 18, 19) --------------------------------

        /// <summary>A dust "+N" fades in and rises 12 px over 220 ms and fades out over 300 ms (Reduced: 150 ms fades).</summary>
        public const float DustFloatInSeconds = 0.22f;
        public const float DustFloatRisePixels = 12f;
        public const float DustFloatOutSeconds = 0.3f;
        public const float DustFloatReducedSeconds = 0.15f;
        /// <summary>A select tile's edge and glow cross-fade, and the READY badge fades in, over 120 ms.</summary>
        public const float CrossFadeSeconds = 0.12f;
        /// <summary>The boss phase label cross-fades over 220 ms.</summary>
        public const float PhaseLabelSeconds = 0.22f;
        /// <summary>A spent Act III anchor pip splits and fades over 600 ms (Reduced: a 150 ms fade).</summary>
        public const float AnchorSplitSeconds = 0.6f;
        public const float AnchorSplitReducedSeconds = 0.15f;
        /// <summary>A list scrolls to its focused row over 120 ms, ease-out (Reduced: at once).</summary>
        public const float ListScrollSeconds = 0.12f;

        // ---- Screen change (row 6) ---------------------------------------------

        /// <summary>
        /// The screen dissolve: a full-screen bg0 layer fades 0 to 1 over this long (Sine InOut), the scene swaps
        /// behind it, then it fades 1 to 0 over the same. The same under Reduced Temporal Effects (already stable).
        /// </summary>
        public const float DissolveSeconds = 0.2f;

        // ---- Legacy (pulp) until the closeout -------------------------------------

        /// <summary>One frame at the 60 Hz the game runs at (the legacy hit flash's length).</summary>
        public const float FrameSeconds = 1f / 60f;
        /// <summary>
        /// Legacy (MOT-A has no hit flash; the closeout deletes it with <see cref="HitFlashLimiter"/>): the ink-black hit
        /// flash on a bar plate is rate-limited to once per second per bar, so nothing strobes.
        /// </summary>
        public const float HitFlashMinIntervalSeconds = 1.0f;

        // ---- Curves (pure, so the engine half and the tests share them) ----------------

        /// <summary>Sine ease-in-out of a 0..1 progress (clamped).</summary>
        public static float SineInOut(float u) {
            u = Math.Clamp(u, 0f, 1f);
            return 0.5f - 0.5f * (float)Math.Cos(Math.PI * u);
        }

        /// <summary>Cubic ease-out of a 0..1 progress (clamped).</summary>
        public static float CubicOut(float u) {
            u = Math.Clamp(u, 0f, 1f);
            float v = 1f - u;
            return 1f - v * v * v;
        }

        /// <summary>
        /// The damage tracker <paramref name="elapsed"/> seconds after a hit: it holds at <paramref name="from"/> for
        /// <see cref="TrackerHoldSeconds"/>, then drains to <paramref name="to"/> over <see cref="TrackerDrainSeconds"/>
        /// (Sine InOut), and stays there.
        /// </summary>
        public static float TrackerValueAt(float elapsed, float from, float to) {
            if (elapsed <= TrackerHoldSeconds) return from;
            float u = (elapsed - TrackerHoldSeconds) / TrackerDrainSeconds;
            if (u >= 1f) return to;
            return from + (to - from) * SineInOut(u);
        }

        /// <summary>Reduced Temporal Effects: the tracker's alpha <paramref name="elapsed"/> seconds after a hit (1 to 0, linear).</summary>
        public static float TrackerFadeAlphaAt(float elapsed) =>
            Math.Clamp(1f - elapsed / TrackerFadeSeconds, 0f, 1f);

        /// <summary>
        /// The ready bloom <paramref name="elapsed"/> seconds after entering ready: the lens modulate (1, rising to
        /// <see cref="BloomPeakModulate"/> at <see cref="BloomPeakSeconds"/>, back to 1 at <see cref="BloomSeconds"/>) and the
        /// outer glow as a multiple of its steady alpha (0, rising to <see cref="BloomPeakGlow"/>, settling at 1). Both ease
        /// in and out on each half. After <see cref="BloomSeconds"/> it is the steady ready look (1, 1): it never loops.
        /// </summary>
        public static (float Modulate, float Glow) BloomAt(float elapsed) {
            if (elapsed <= 0f) return (1f, 0f);
            if (elapsed >= BloomSeconds) return (1f, 1f);
            if (elapsed <= BloomPeakSeconds) {
                float u = SineInOut(elapsed / BloomPeakSeconds);
                return (1f + (BloomPeakModulate - 1f) * u, BloomPeakGlow * u);
            }
            float d = SineInOut((elapsed - BloomPeakSeconds) / (BloomSeconds - BloomPeakSeconds));
            return (BloomPeakModulate + (1f - BloomPeakModulate) * d, BloomPeakGlow + (1f - BloomPeakGlow) * d);
        }

        /// <summary>A panel's rise offset in pixels at <paramref name="progress"/> of its appearance (24 down to 0, Cubic Out).</summary>
        public static float PanelRiseAt(float progress) => PanelRisePixels * (1f - CubicOut(progress));

        /// <summary>The low-HP glow's alpha at <paramref name="clock"/> seconds (0 at the ends of each 2 s cycle, 1 in its middle).</summary>
        public static float LowPulseAt(float clock) {
            float phase = clock / LowPulseSeconds;
            phase -= (float)Math.Floor(phase);
            return 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * phase);
        }
    }

    /// <summary>
    /// Legacy (MOT-A has no hit flash; the closeout deletes this with <c>PulpMotion.HitFlash</c>). One bar's hit-flash
    /// rate limit: a multi-hit string lands faster than 1 Hz, and the flash must never strobe, so a flash inside the
    /// interval is swallowed.
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
