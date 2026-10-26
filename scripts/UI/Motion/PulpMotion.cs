using System;
using FTT.Core;
using Godot;

namespace FTT.UI.Motion {

    /// <summary>
    /// UR-THEME. Engine-side MOT-B helpers (UI_SPEC section 5): the panel pop-in,
    /// the stepped number thunk, the stepped tracker drop and the rate-limited hit
    /// flash. The button focus pose is <see cref="FocusPose"/> and the screen
    /// change is <see cref="ScreenWipe"/>.
    ///
    /// <para>Every helper reads <see cref="ComfortSettings.ReducedTemporalEffects"/>
    /// when it runs (never <c>scripts/FighterSim/</c>) and falls back to a stable
    /// cue: an instant value, a plain fade. Every tween runs with
    /// <see cref="Tween.TweenPauseMode.Process"/>, so a pause menu or a gameplay
    /// pausing dialogue never freezes a half-played motion.</para>
    /// </summary>
    public static class PulpMotion {

        /// <summary>The comfort preset every helper reads.</summary>
        public static bool Reduced => ComfortSettings.ReducedTemporalEffects;

        /// <summary>A tween bound to <paramref name="owner"/> that keeps running while the tree is paused.</summary>
        public static Tween NewTween(Node owner) {
            Tween tween = owner.CreateTween();
            tween.SetPauseMode(Tween.TweenPauseMode.Process);
            return tween;
        }

        /// <summary>
        /// Panel appears (UI_SPEC row 4). A 2-frame pop: scale 0.96 to 1.0 with alpha 0 to 1 over
        /// 0.1 s (back-eased). Under Reduced Temporal Effects a 0.15 s alpha fade with no scale.
        /// Returns the tween (never null) so a caller can await or kill it.
        /// </summary>
        public static Tween PopIn(Control panel) {
            Tween tween = NewTween(panel);
            if (Reduced) {
                panel.Scale = Vector2.One;
                panel.Modulate = WithAlpha(panel.Modulate, 0f);
                tween.TweenProperty(panel, "modulate:a", 1f, PulpMotionRules.PanelFadeSeconds);
                return tween;
            }
            panel.PivotOffset = panel.Size / 2f;
            panel.Scale = Vector2.One * PulpMotionRules.PanelPopFromScale;
            panel.Modulate = WithAlpha(panel.Modulate, 0f);
            tween.SetParallel(true);
            tween.TweenProperty(panel, "scale", Vector2.One, PulpMotionRules.PanelPopSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(panel, "modulate:a", 1f, PulpMotionRules.PanelPopSeconds);
            return tween;
        }

        /// <summary>Panel leaves (the mirror of <see cref="PopIn"/>); the panel ends invisible, not freed.</summary>
        public static Tween PopOut(Control panel) {
            Tween tween = NewTween(panel);
            tween.TweenProperty(panel, "modulate:a", 0f,
                Reduced ? PulpMotionRules.PanelFadeSeconds : PulpMotionRules.PanelPopSeconds);
            return tween;
        }

        /// <summary>
        /// Number thunk (row 8). Calls <paramref name="apply"/> with the first two of three equal steps on
        /// consecutive frames and then the exact target. Under Reduced Temporal Effects, or when nothing
        /// changes, <paramref name="apply"/> gets the target at once and null is returned.
        /// </summary>
        public static Tween Thunk(Node host, int from, int to, Action<int> apply) {
            if (Reduced || from == to) {
                apply(to);
                return null;
            }
            int[] values = PulpMotionRules.ThunkValues(from, to);
            Tween tween = NewTween(host);
            for (int i = 0; i < values.Length; i++) {
                int value = values[i];
                tween.TweenCallback(Callable.From(() => apply(value)));
                if (i < values.Length - 1) tween.TweenInterval(PulpMotionRules.FrameSeconds);
            }
            return tween;
        }

        /// <summary>
        /// The HP damage tracker (row 6): holds, then drops from <paramref name="from"/> to
        /// <paramref name="to"/> in <see cref="PulpMotionRules.TrackerSteps"/> equal steps. Under Reduced
        /// Temporal Effects it slides linearly over 0.2 s with no hold.
        /// </summary>
        public static Tween TrackerDrop(Node host, float from, float to, Action<float> apply) {
            Tween tween = NewTween(host);
            if (Reduced) {
                tween.TweenMethod(Callable.From<float>(apply), from, to, PulpMotionRules.TrackerFadeSeconds);
                return tween;
            }
            tween.TweenInterval(PulpMotionRules.TrackerHoldSeconds);
            float stepSeconds = PulpMotionRules.TrackerDropSeconds / PulpMotionRules.TrackerSteps;
            for (int i = 1; i <= PulpMotionRules.TrackerSteps; i++) {
                float value = Mathf.Lerp(from, to, i / (float)PulpMotionRules.TrackerSteps);
                tween.TweenCallback(Callable.From(() => apply(value)));
                if (i < PulpMotionRules.TrackerSteps) tween.TweenInterval(stepSeconds);
            }
            return tween;
        }

        /// <summary>
        /// The 1-frame ink-black hit flash on a bar plate (row 7), never on the fill or the screen.
        /// Returns false, flashing nothing, under Reduced Temporal Effects or inside the limiter's
        /// once-per-second window. <paramref name="nowSeconds"/> is any monotonic clock
        /// (<c>Time.GetTicksMsec() / 1000.0</c>).
        /// </summary>
        public static bool HitFlash(CanvasItem plate, HitFlashLimiter limiter, double nowSeconds) {
            if (Reduced || !limiter.TryFlash(nowSeconds)) return false;
            Color before = plate.SelfModulate;
            plate.SelfModulate = new Color(0f, 0f, 0f, before.A);
            Tween tween = plate.CreateTween();
            tween.SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenInterval(PulpMotionRules.FrameSeconds);
            tween.TweenCallback(Callable.From(() => {
                if (GodotObject.IsInstanceValid(plate)) plate.SelfModulate = before;
            }));
            return true;
        }

        private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);
    }
}
