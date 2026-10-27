using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Motion;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-THEME, re-pinned by TG-THEME for MOT-A (UI_SPEC_GLASS section 5). The engine half of the motion: the focus pose,
/// the panel fade-rise and fade-sink, the ready bloom, the legacy hit flash and the screen dissolve, each under both
/// the standard treatment and Reduced Temporal Effects, and the dissolve under a paused tree. Every case restores the
/// comfort preset and the tree pause in a finally. The runner's fixed 60 fps makes a frame exactly 1/60 s.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpMotionTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void SetReduced(bool value) => ComfortSettings.Apply(value);

    // === Focus pose ===

    [TestCase]
    public async Task TheFocusPoseSnapsInstantlyUnderReducedEffectsAndReturnsToExactIdentity() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control { Name = "PoseHost" };
        var button = new Button { Text = "Go", Position = new Vector2(100, 50), Size = new Vector2(200, 60) };
        host.AddChild(button);
        Tree.Root.AddChild(host);
        try {
            SetReduced(true);
            FocusPose pose = FocusPose.Attach(button);
            await Frames(1);

            button.EmitSignal(Control.SignalName.FocusEntered);
            // No tween: the 14 px slide is complete in the same frame; MOT-A neither tilts nor scales.
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 100f + 14f)).IsTrue();
            AssertThat(button.Position.Y).IsEqual(50f);
            AssertThat(button.Scale).IsEqual(Vector2.One);
            AssertThat(button.Rotation).IsEqual(0f);
            AssertThat(button.PivotOffset.IsEqualApprox(button.Size / 2f)).IsTrue();

            // The press replaces the slide at once: +6 from rest (8 px back from focus) and 0.985 about the centre.
            button.EmitSignal(BaseButton.SignalName.ButtonDown);
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 100f + 6f)).IsTrue();
            AssertThat(button.Scale.IsEqualApprox(Vector2.One * 0.985f)).IsTrue();
            button.EmitSignal(BaseButton.SignalName.ButtonUp);
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 100f + 14f)).IsTrue();
            AssertThat(button.Scale).IsEqual(Vector2.One);

            button.EmitSignal(Control.SignalName.FocusExited);
            AssertThat(button.Scale).IsEqual(Vector2.One);
            AssertThat(button.Position).IsEqual(new Vector2(100, 50));
            AssertThat(button.Rotation).IsEqual(0f);
            AssertThat(pose.PoseActive).IsFalse();
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task TheFocusPoseTweensInAndOutAndSettlesOnExactValues() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control { Name = "PoseHostTween" };
        var button = new Button { Text = "Go", Position = new Vector2(10, 20), Size = new Vector2(200, 60) };
        host.AddChild(button);
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            FocusPose.Attach(button);
            await Frames(1);

            button.EmitSignal(Control.SignalName.FocusEntered);
            AssertThat(button.Position.X).IsEqual(10f);                     // not instant
            await Frames(3);                                                 // 50 ms of a 120 ms Quad Out
            AssertThat(button.Position.X > 10f && button.Position.X < 24f).IsTrue();
            await Frames(12);                                                // 0.12 s is 7.2 frames at 60 Hz
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 10f + PulpMotionRules.FocusTranslateX)).IsTrue();
            AssertThat(button.Scale).IsEqual(Vector2.One);
            AssertThat(button.Rotation).IsEqual(0f);

            // The press applies on the press frame; the release eases back to the focus slide.
            button.EmitSignal(BaseButton.SignalName.ButtonDown);
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 10f + PulpMotionRules.PressTranslate)).IsTrue();
            AssertThat(button.Scale.IsEqualApprox(Vector2.One * PulpMotionRules.PressScale)).IsTrue();
            button.EmitSignal(BaseButton.SignalName.ButtonUp);
            AssertThat(button.Position.X < 10f + PulpMotionRules.FocusTranslateX).IsTrue();
            await Frames(15);
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 10f + PulpMotionRules.FocusTranslateX)).IsTrue();
            AssertThat(button.Scale).IsEqual(Vector2.One);

            button.EmitSignal(Control.SignalName.FocusExited);
            await Frames(15);
            AssertThat(button.Scale).IsEqual(Vector2.One);
            AssertThat(button.Position).IsEqual(new Vector2(10, 20));
            AssertThat(button.Rotation).IsEqual(0f);
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task AContainerSortDoesNotLoseTheFocusPose() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control { Name = "PoseHostContainer", Size = new Vector2(400, 300) };
        var box = new VBoxContainer { Size = new Vector2(400, 300) };
        var first = new Button { Text = "One" };
        var second = new Button { Text = "Two" };
        box.AddChild(first);
        box.AddChild(second);
        host.AddChild(box);
        Tree.Root.AddChild(host);
        try {
            SetReduced(true);
            await Frames(2);
            Vector2 slot = second.Position;
            FocusPose.Attach(second);
            await Frames(1);

            second.EmitSignal(Control.SignalName.FocusEntered);
            // A container resets position, rotation and scale when it sorts.
            box.QueueSort();
            await Frames(3);
            AssertThat(second.Scale.IsEqualApprox(Vector2.One * PulpMotionRules.FocusScale)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(second.Position.X, slot.X + PulpMotionRules.FocusTranslateX)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(second.Position.Y, slot.Y)).IsTrue();

            // The layout itself never moved: the sibling is where it was.
            AssertThat(first.Scale).IsEqual(Vector2.One);
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    // === Panels ===

    /// <summary>TG-THEME rewrite of APanelPopsInScaledNormallyAndFadesWithNoScaleUnderReducedEffects (MOT-A row 4).</summary>
    [TestCase]
    public async Task APanelFadesInRisingTwentyFourPixelsAndOnlyFadesUnderReducedEffects() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control();
        var normal = new Control { Position = new Vector2(100, 200), Size = new Vector2(100, 100) };
        var reduced = new Control { Position = new Vector2(300, 200), Size = new Vector2(100, 100) };
        var band = new PanelBandProbe();
        normal.AddChild(band);
        host.AddChild(normal);
        host.AddChild(reduced);
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            PulpMotion.PopIn(normal);
            AssertThat(normal.Modulate.A).IsEqual(0f);
            AssertThat(Mathf.IsEqualApprox(normal.Position.Y, 200f + PulpMotionRules.PanelRisePixels)).IsTrue();
            AssertThat(normal.Scale).IsEqual(Vector2.One);                   // a rise, never a scale
            AssertThat(band.Plays).IsEqual(1);                               // the light band crosses once
            AssertThat(Mathf.IsEqualApprox(band.Seconds, PulpMotionRules.PanelBandSeconds)).IsTrue();

            SetReduced(true);
            var reducedBand = new PanelBandProbe();
            reduced.AddChild(reducedBand);
            PulpMotion.PopIn(reduced);
            AssertThat(reduced.Modulate.A).IsEqual(0f);
            AssertThat(reduced.Position).IsEqual(new Vector2(300, 200));     // no rise at all
            AssertThat(reducedBand.Plays).IsEqual(0);                        // and no band

            await Frames(4);
            AssertThat(normal.Position.Y > 200f && normal.Position.Y < 224f).IsTrue();   // still rising
            await Frames(26);
            AssertThat(normal.Position).IsEqual(new Vector2(100, 200));      // lands exactly on its slot
            AssertThat(Mathf.IsEqualApprox(normal.Modulate.A, 1f)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(reduced.Modulate.A, 1f)).IsTrue();
            AssertThat(reduced.Position).IsEqual(new Vector2(300, 200));
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task APanelFadesOutSinkingTwelvePixels() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control();
        var panel = new Control { Position = new Vector2(50, 400), Size = new Vector2(100, 100) };
        var reduced = new Control { Position = new Vector2(250, 400), Size = new Vector2(100, 100) };
        host.AddChild(panel);
        host.AddChild(reduced);
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            PulpMotion.PopOut(panel);
            float deepest = 0f, lastAlpha = 1f;
            bool alphaRose = false;
            for (int i = 0; i < 9; i++) {                                    // 0.16 s is 9.6 frames
                await Frames(1);
                deepest = Mathf.Max(deepest, panel.Position.Y - 400f);
                if (panel.Modulate.A > lastAlpha + 1e-4f) alphaRose = true;
                lastAlpha = panel.Modulate.A;
            }
            // Quad In: 12 x p^2, so the last frame before the end shows 8.3 px (p = 8 / 9.6) and the end returns home.
            AssertThat(deepest >= 8f && deepest <= PulpMotionRules.PanelSinkPixels + 0.01f)
                .OverrideFailureMessage($"the panel sank {deepest} px").IsTrue();
            AssertThat(alphaRose).IsFalse();
            await Frames(4);
            AssertThat(panel.Modulate.A).IsEqual(0f);                        // ends invisible...
            AssertThat(panel.Position).IsEqual(new Vector2(50, 400));        // ...back on its slot, ready to rise again

            SetReduced(true);
            PulpMotion.PopOut(reduced);
            await Frames(2);
            AssertThat(reduced.Position).IsEqual(new Vector2(250, 400));     // Reduced: a fade only
            await Frames(10);
            AssertThat(reduced.Modulate.A).IsEqual(0f);
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    /// <summary>
    /// TG-WIDGETS (Codex review 1, finding 1): one motion owns a panel at a time. A sub-screen re-opened inside its
    /// 0.22 s entrance, closed mid-entrance or re-opened mid-sink ends exactly on its slot; under Reduced Temporal Effects
    /// an open and a close a frame apart end closed.
    /// </summary>
    [TestCase]
    public async Task APanelReopenedOrClosedMidMotionEndsExactlyOnItsSlot() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control();
        var panel = new Control { Position = new Vector2(100, 200), Size = new Vector2(100, 100) };
        var reduced = new Control { Position = new Vector2(300, 200), Size = new Vector2(100, 100) };
        host.AddChild(panel);
        host.AddChild(reduced);
        Tree.Root.AddChild(host);
        var slot = new Vector2(100, 200);
        try {
            SetReduced(false);
            PulpMotion.PopIn(panel);
            await Frames(4);
            PulpMotion.PopIn(panel);
            await Frames(3);
            PulpMotion.PopIn(panel);
            await Frames(30);
            AssertThat(panel.Position).IsEqual(slot);
            AssertThat(Mathf.IsEqualApprox(panel.Modulate.A, 1f)).IsTrue();

            PulpMotion.PopIn(panel);
            await Frames(5);
            PulpMotion.PopOut(panel);
            await Frames(20);
            AssertThat(panel.Position).IsEqual(slot);
            AssertThat(panel.Modulate.A).IsEqual(0f);

            PulpMotion.PopIn(panel);
            await Frames(30);
            PulpMotion.PopOut(panel);
            await Frames(4);
            PulpMotion.PopIn(panel);
            await Frames(30);
            AssertThat(panel.Position).IsEqual(slot);
            AssertThat(Mathf.IsEqualApprox(panel.Modulate.A, 1f)).IsTrue();

            SetReduced(true);
            PulpMotion.PopIn(reduced);
            await Frames(1);
            PulpMotion.PopOut(reduced);
            await Frames(20);
            AssertThat(reduced.Modulate.A).IsEqual(0f);
            AssertThat(reduced.Position).IsEqual(new Vector2(300, 200));
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task TheReadyBloomRisesOnceAndSettlesWithNoLoop() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var lens = new ColorRect { Color = Colors.White, Modulate = new Color(0.8f, 0.8f, 0.8f, 0.9f) };
        Tree.Root.AddChild(lens);
        try {
            SetReduced(false);
            var glows = new List<float>();
            AssertObject(PulpMotion.Bloom(lens, glows.Add)).IsNotNull();
            float brightest = 0f;
            for (int i = 0; i < 40; i++) {                                   // 0.6 s is 36 frames
                await Frames(1);
                brightest = Mathf.Max(brightest, lens.Modulate.R);
            }
            // Brightened toward 1.35 x (0.8 x 1.35 = 1.08) and the glow toward 1.5 x its steady alpha...
            AssertThat(brightest > 1.0f && brightest <= 0.8f * PulpMotionRules.BloomPeakModulate + 0.001f).IsTrue();
            float peakGlow = 0f;
            foreach (float g in glows) peakGlow = Mathf.Max(peakGlow, g);
            AssertThat(peakGlow > 1.3f && peakGlow <= PulpMotionRules.BloomPeakGlow + 0.001f).IsTrue();
            // ...then settled exactly on the steady ready look: the starting modulate and the glow at 1.
            AssertThat(lens.Modulate).IsEqual(new Color(0.8f, 0.8f, 0.8f, 0.9f));
            AssertThat(glows[glows.Count - 1]).IsEqual(1f);
            // No loop: nothing moves afterwards.
            int calls = glows.Count;
            await Frames(40);
            AssertThat(glows.Count).IsEqual(calls);
            AssertThat(lens.Modulate).IsEqual(new Color(0.8f, 0.8f, 0.8f, 0.9f));

            // Reduced Temporal Effects: the steady ready look at once.
            SetReduced(true);
            var steady = new List<float>();
            AssertObject(PulpMotion.Bloom(lens, steady.Add)).IsNull();
            AssertThat(steady.Count).IsEqual(1);
            AssertThat(steady[0]).IsEqual(1f);
            AssertThat(lens.Modulate).IsEqual(new Color(0.8f, 0.8f, 0.8f, 0.9f));
        } finally {
            SetReduced(before);
            lens.Free();
        }
    }

    [TestCase]
    public async Task TheHitFlashBlanksOnlyThePlateForOneFrameAndNeverTwiceInASecond() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var plate = new ColorRect { Color = Colors.White };
        Tree.Root.AddChild(plate);
        try {
            SetReduced(false);
            var limiter = new HitFlashLimiter();
            AssertThat(PulpMotion.HitFlash(plate, limiter, 5.0)).IsTrue();
            AssertThat(plate.SelfModulate.IsEqualApprox(new Color(0f, 0f, 0f, 1f))).IsTrue();
            AssertThat(PulpMotion.HitFlash(plate, limiter, 5.3)).IsFalse();   // rate-limited
            await Frames(4);
            AssertThat(plate.SelfModulate.IsEqualApprox(Colors.White)).IsTrue();

            SetReduced(true);
            AssertThat(PulpMotion.HitFlash(plate, new HitFlashLimiter(), 99.0)).IsFalse();
            AssertThat(plate.SelfModulate.IsEqualApprox(Colors.White)).IsTrue();
        } finally {
            SetReduced(before);
            plate.Free();
        }
    }

    // === Screen dissolve (ScreenWipe) ===

    /// <summary>TG-THEME rewrite of TheWipeCoversAndRevealsAndRaisesCoveredWhileTheScreenIsFullyCovered (MOT-A row 6).</summary>
    [TestCase]
    public async Task TheDissolveCoversAndRevealsAndRaisesCoveredWhileTheScreenIsFullyCovered() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Node();
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            ScreenWipe wipe = ScreenWipe.EnsureFor(host);
            AssertThat(ScreenWipe.EnsureFor(host)).IsSame(wipe);
            AssertThat(wipe.Layer).IsEqual(ScreenWipe.WipeLayer);
            AssertThat(wipe.Layer > 100).IsTrue();     // above every loading screen
            AssertThat(wipe.CoverSeconds).IsEqual(PulpMotionRules.DissolveSeconds);
            var layer = wipe.GetNode<ColorRect>("Slab");
            AssertThat(new Color(layer.Color, 1f).IsEqualApprox(UIPalette.Backdrop)).IsTrue();   // a bg0 layer, no slab art

            int coveredRaised = 0;
            float opacityWhenCovered = -1f;
            wipe.Covered += () => { coveredRaised++; opacityWhenCovered = wipe.Opacity; };
            wipe.BeginCover();
            AssertThat(wipe.IsActive).IsTrue();
            AssertThat(wipe.IsCovered).IsFalse();
            await Frames(4);
            AssertThat(wipe.Opacity > 0f && wipe.Opacity < 1f).OverrideFailureMessage($"a dissolve, not a cut: {wipe.Opacity}").IsTrue();
            for (int i = 0; i < 60 && !wipe.IsCovered; i++) await Frames(1);
            AssertThat(wipe.IsCovered).IsTrue();
            AssertThat(coveredRaised).IsEqual(1);
            AssertThat(opacityWhenCovered).IsEqual(1f);

            wipe.BeginReveal();
            await Frames(4);
            AssertThat(wipe.Opacity > 0f && wipe.Opacity < 1f).IsTrue();
            for (int i = 0; i < 60 && wipe.IsActive; i++) await Frames(1);
            AssertThat(wipe.IsActive).IsFalse();
            AssertThat(wipe.Opacity).IsEqual(0f);
            AssertThat(layer.Visible).IsFalse();
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    /// <summary>TG-THEME rewrite of UnderReducedEffectsTheWipeIsAShorterStableFadeNotASlab: a dissolve is already a stable cue.</summary>
    [TestCase]
    public async Task UnderReducedEffectsTheDissolveIsTheSameStableFade() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Node();
        Tree.Root.AddChild(host);
        try {
            SetReduced(true);
            ScreenWipe wipe = ScreenWipe.EnsureFor(host);
            AssertThat(wipe.CoverSeconds).IsEqual(PulpMotionRules.DissolveSeconds);
            wipe.BeginCover();
            await Frames(4);
            AssertThat(wipe.Opacity > 0f && wipe.Opacity < 1f).IsTrue();     // the same fade, not a cut
            for (int i = 0; i < 60 && !wipe.IsCovered; i++) await Frames(1);
            AssertThat(wipe.IsCovered).IsTrue();
            AssertThat(wipe.Opacity).IsEqual(1f);
            wipe.Release();
            AssertThat(wipe.IsActive).IsFalse();
            AssertThat(wipe.Opacity).IsEqual(0f);
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task TheWipeKeepsRunningWhileTheTreeIsPaused() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        bool pausedBefore = Tree.Paused;
        var host = new Node();
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            ScreenWipe wipe = ScreenWipe.EnsureFor(host);
            Tree.Paused = true;
            wipe.BeginCover();
            for (int i = 0; i < 60 && !wipe.IsCovered; i++) await Frames(1);
            AssertThat(wipe.IsCovered)
                .OverrideFailureMessage("A pause menu opening mid-transition must not strand the wipe.").IsTrue();
        } finally {
            Tree.Paused = pausedBefore;     // CLAUDE.md failure signature 4: always hand the pause back
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task AReleasedWipeNeverLeavesTheScreenCoveredOrBlockingInput() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Node();
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            ScreenWipe wipe = ScreenWipe.EnsureFor(host);
            wipe.BeginCover();
            await Frames(3);
            wipe.Release();
            AssertThat(wipe.IsActive).IsFalse();
            var slab = wipe.GetNode<Control>("Slab");
            AssertThat(slab.Visible).IsFalse();
            AssertThat(slab.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        } finally {
            SetReduced(before);
            host.Free();
        }
    }
}

/// <summary>A panel child that records the light band <see cref="PulpMotion.PopIn"/> asks it to play (not a suite).</summary>
public partial class PanelBandProbe : Control, IPanelLightBand {
    public int Plays;
    public float Seconds;
    public void PlayLightBand(float seconds) { Plays++; Seconds = seconds; }
}
