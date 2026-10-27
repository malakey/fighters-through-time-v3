using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.UI.Motion {

    /// <summary>
    /// A panel decor that plays MOT-A row 4's light band (a white band crossing the panel once along the glass
    /// streak direction) when <see cref="PulpMotion.PopIn"/> shows its panel. <c>PopIn</c> calls it on every direct
    /// child of the panel that implements it, and never under Reduced Temporal Effects. The glass sheen decor
    /// (TG-WIDGETS' <c>GlassSheen</c>) is the implementer; the band's numbers are <see cref="PulpMotionRules.PanelBandSeconds"/>,
    /// <see cref="PulpMotionRules.PanelBandWidth"/> and <see cref="PulpMotionRules.PanelBandAlpha"/>.
    /// </summary>
    public interface IPanelLightBand {
        /// <summary>Starts the band's single crossing, over <paramref name="seconds"/>, linear.</summary>
        void PlayLightBand(float seconds);
    }

    /// <summary>
    /// TG-THEME (first written by UR-THEME; the class keeps its legacy name). Engine-side MOT-A helpers
    /// (UI_SPEC_GLASS section 5): the panel fade-rise and fade-sink, the damage tracker's hold-and-drain, the
    /// healing rise and the single ready bloom. The button focus pose is <see cref="FocusPose"/> and the screen change
    /// is <see cref="ScreenWipe"/>. The pulp hit flash stays until the closeout deletes it (MOT-A has none).
    ///
    /// <para>Every helper reads <see cref="ComfortSettings.ReducedTemporalEffects"/> when it runs (never
    /// <c>scripts/FighterSim/</c>) and falls back to a stable cue: an instant value, a plain fade. Every tween runs
    /// with <see cref="Tween.TweenPauseMode.Process"/>, so a pause menu or a gameplay-pausing dialogue never freezes
    /// a half-played motion. A rise or sink moves the panel by its position and survives a container sort (it re-bases
    /// on the slot the container gives it), and always ends on the exact resting position.</para>
    ///
    /// <para>One motion owns a panel at a time (TG-WIDGETS, Codex review 1 finding 1): <see cref="PopIn"/> and
    /// <see cref="PopOut"/> first stop the panel's running fade-rise or fade-sink and put it back on its resting position,
    /// so a panel re-opened, or closed, mid-motion never takes the other motion's offset for its slot.</para>
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
        /// Panel appears (row 4): alpha 0 to 1 and a 24 px rise over 0.22 s (Cubic Out), and every direct child that is
        /// an <see cref="IPanelLightBand"/> plays its band once. Under Reduced Temporal Effects a 0.15 s alpha fade with no
        /// rise and no band. Returns the tween (never null) so a caller can await or kill it.
        /// </summary>
        public static Tween PopIn(Control panel) {
            TakeOver(panel);
            Tween tween = NewTween(panel);
            panel.Scale = Vector2.One;
            panel.Modulate = WithAlpha(panel.Modulate, 0f);
            if (Reduced) {
                tween.TweenProperty(panel, "modulate:a", 1f, PulpMotionRules.PanelFadeSeconds);
                return Own(panel, tween, null);
            }
            var rise = new OffsetDriver(panel, PulpMotionRules.PanelRiseAt);
            rise.Apply(0f);
            tween.SetParallel(true);
            tween.TweenProperty(panel, "modulate:a", 1f, PulpMotionRules.PanelInSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenMethod(Callable.From<float>(rise.Apply), 0f, 1f, PulpMotionRules.PanelInSeconds);
            tween.Chain().TweenCallback(Callable.From(rise.Settle));
            for (int i = 0, n = panel.GetChildCount(); i < n; i++) {
                if (panel.GetChild(i) is IPanelLightBand band) band.PlayLightBand(PulpMotionRules.PanelBandSeconds);
            }
            return Own(panel, tween, rise);
        }

        /// <summary>
        /// Panel leaves (row 5): alpha 1 to 0 and a 12 px sink over 0.16 s (Quad In); the panel ends invisible, not freed,
        /// back on its resting position. Under Reduced Temporal Effects a 0.12 s alpha fade.
        /// </summary>
        public static Tween PopOut(Control panel) {
            TakeOver(panel);
            Tween tween = NewTween(panel);
            if (Reduced) {
                tween.TweenProperty(panel, "modulate:a", 0f, PulpMotionRules.PanelFadeOutSeconds);
                return Own(panel, tween, null);
            }
            var sink = new OffsetDriver(panel, SinkAt);
            tween.SetParallel(true);
            tween.TweenProperty(panel, "modulate:a", 0f, PulpMotionRules.PanelOutSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.TweenMethod(Callable.From<float>(sink.Apply), 0f, 1f, PulpMotionRules.PanelOutSeconds);
            tween.Chain().TweenCallback(Callable.From(sink.Settle));
            return Own(panel, tween, sink);
        }

        // The panel motion each panel is running (by instance id): its tween and, unless Reduced, its offset driver.
        private sealed class PanelMotion {
            public Tween Tween;
            public OffsetDriver Driver;
        }

        private static readonly Dictionary<ulong, PanelMotion> Running = new();
        private static readonly List<ulong> Stale = new();

        // Stops the panel's running motion, if any, and puts it back on its resting position (its laid-out slot when a
        // container moved it meanwhile), before a new motion reads that position as its base.
        private static void TakeOver(Control panel) {
            if (!Running.Remove(panel.GetInstanceId(), out PanelMotion previous)) return;
            if (previous.Tween.IsValid()) previous.Tween.Kill();
            previous.Driver?.Settle();
        }

        private static Tween Own(Control panel, Tween tween, OffsetDriver driver) {
            ulong id = panel.GetInstanceId();
            var motion = new PanelMotion { Tween = tween, Driver = driver };
            if (Running.Count > 16) Purge();
            Running[id] = motion;
            tween.Chain().TweenCallback(Callable.From(() => {
                if (Running.TryGetValue(id, out PanelMotion current) && ReferenceEquals(current, motion)) Running.Remove(id);
            }));
            return tween;
        }

        // A panel freed mid-motion kills its tween without the closing callback: drop such entries now and then.
        private static void Purge() {
            Stale.Clear();
            foreach (KeyValuePair<ulong, PanelMotion> entry in Running) {
                if (!entry.Value.Tween.IsValid()) Stale.Add(entry.Key);
            }
            foreach (ulong id in Stale) Running.Remove(id);
        }

        private static float SinkAt(float progress) => PulpMotionRules.PanelSinkPixels * progress * progress;

        /// <summary>
        /// The HP damage tracker (row 7): holds at <paramref name="from"/> for 0.5 s, then drains to <paramref name="to"/>
        /// over 0.6 s (Sine InOut). Under Reduced Temporal Effects it fades out over 0.2 s instead of draining:
        /// <paramref name="fadeAlpha"/> gets 1 to 0, then the tracker snaps to <paramref name="to"/> and the alpha returns
        /// to 1; with no <paramref name="fadeAlpha"/> the tracker simply snaps after 0.2 s.
        /// </summary>
        public static Tween TrackerDrop(Node host, float from, float to, Action<float> apply, Action<float> fadeAlpha = null) {
            Tween tween = NewTween(host);
            if (Reduced) {
                if (fadeAlpha != null) {
                    tween.TweenMethod(Callable.From<float>(fadeAlpha), 1f, 0f, PulpMotionRules.TrackerFadeSeconds);
                    tween.TweenCallback(Callable.From(() => { apply(to); fadeAlpha(1f); }));
                } else {
                    tween.TweenInterval(PulpMotionRules.TrackerFadeSeconds);
                    tween.TweenCallback(Callable.From(() => apply(to)));
                }
                return tween;
            }
            tween.TweenInterval(PulpMotionRules.TrackerHoldSeconds);
            tween.TweenMethod(Callable.From<float>(apply), from, to, PulpMotionRules.TrackerDrainSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            return tween;
        }

        /// <summary>
        /// Healing (row 7): the fill rises from <paramref name="from"/> to <paramref name="to"/> over 0.2 s (Quad Out); the
        /// caller snaps the tracker to it. Under Reduced Temporal Effects <paramref name="apply"/> gets the target at once and
        /// null is returned.
        /// </summary>
        public static Tween HealRise(Node host, float from, float to, Action<float> apply) {
            if (Reduced || from == to) {
                apply(to);
                return null;
            }
            Tween tween = NewTween(host);
            tween.TweenMethod(Callable.From<float>(apply), from, to, PulpMotionRules.HealRiseSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            return tween;
        }

        /// <summary>
        /// The single ready bloom (row 10): <paramref name="target"/>'s modulate brightens to 1.35 and back while
        /// <paramref name="glow"/> (a multiple of the steady ready glow alpha) rises from 0 to 1.5 and settles at 1, over
        /// 0.6 s; it never loops and ends on the exact modulate it started from. Under Reduced Temporal Effects the steady
        /// ready look at once: <paramref name="glow"/> gets 1 and null is returned.
        /// </summary>
        public static Tween Bloom(CanvasItem target, Action<float> glow = null) {
            if (Reduced) {
                glow?.Invoke(1f);
                return null;
            }
            Color rest = target.Modulate;
            Tween tween = NewTween(target);
            tween.TweenMethod(Callable.From<float>(elapsed => {
                (float modulate, float bloomGlow) = PulpMotionRules.BloomAt(elapsed);
                target.Modulate = new Color(rest.R * modulate, rest.G * modulate, rest.B * modulate, rest.A);
                glow?.Invoke(bloomGlow);
            }), 0f, PulpMotionRules.BloomSeconds, PulpMotionRules.BloomSeconds);
            tween.TweenCallback(Callable.From(() => {
                if (GodotObject.IsInstanceValid(target)) target.Modulate = rest;
                glow?.Invoke(1f);
            }));
            return tween;
        }

        /// <summary>
        /// Legacy (MOT-A defines no hit flash; the closeout deletes this and <see cref="HitFlashLimiter"/>). The 1-frame
        /// ink-black hit flash on a bar plate. Returns false, flashing nothing, under Reduced Temporal Effects or inside the
        /// limiter's once-per-second window. <paramref name="nowSeconds"/> is any monotonic clock (<c>Time.GetTicksMsec() / 1000.0</c>).
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

        /// <summary>
        /// Moves a panel by a vertical offset that follows a curve of the tween's progress, and keeps working when a
        /// container re-lays the panel out mid-motion: a position it did not write is the panel's new slot.
        /// </summary>
        private sealed class OffsetDriver {
            private readonly Control _panel;
            private readonly Func<float, float> _offsetAt;
            private Vector2 _base;
            private Vector2 _applied;

            public OffsetDriver(Control panel, Func<float, float> offsetAt) {
                _panel = panel;
                _offsetAt = offsetAt;
                _base = panel.Position;
                _applied = _base;
            }

            public void Apply(float progress) {
                if (!GodotObject.IsInstanceValid(_panel)) return;
                if (!_panel.Position.IsEqualApprox(_applied)) _base = _panel.Position;
                _applied = _base + new Vector2(0f, _offsetAt(progress));
                _panel.Position = _applied;
            }

            public void Settle() {
                if (!GodotObject.IsInstanceValid(_panel)) return;
                if (!_panel.Position.IsEqualApprox(_applied)) _base = _panel.Position;
                _panel.Position = _base;
                _applied = _base;
            }
        }
    }
}
