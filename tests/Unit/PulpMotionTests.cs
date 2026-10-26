using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI.Motion;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-THEME. The engine half of MOT-B: the focus pose, the panel pop, the number thunk, the hit flash
/// and the screen wipe, each under both the standard treatment and Reduced Temporal Effects, and the
/// wipe under a paused tree. Every case restores the comfort preset and the tree pause in a finally.
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
            // No tween: the pose is complete in the same frame, with no overshoot.
            AssertThat(button.Scale.IsEqualApprox(Vector2.One * PulpMotionRules.FocusScale)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 100f + PulpMotionRules.FocusTranslateX)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(button.RotationDegrees, PulpMotionRules.FocusTiltDegrees)).IsTrue();
            AssertThat(button.PivotOffset.IsEqualApprox(button.Size / 2f)).IsTrue();

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
            AssertThat(button.Scale.IsEqualApprox(Vector2.One)).IsTrue();   // not instant
            await Frames(20);                                              // 0.14 s is 9 frames at 60 Hz
            AssertThat(button.Scale.IsEqualApprox(Vector2.One * PulpMotionRules.FocusScale)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(button.Position.X, 10f + PulpMotionRules.FocusTranslateX)).IsTrue();

            button.EmitSignal(Control.SignalName.FocusExited);
            await Frames(20);
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

    // === Panel, thunk, flash ===

    [TestCase]
    public async Task APanelPopsInScaledNormallyAndFadesWithNoScaleUnderReducedEffects() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control();
        var normal = new Control { Size = new Vector2(100, 100) };
        var reduced = new Control { Size = new Vector2(100, 100) };
        host.AddChild(normal);
        host.AddChild(reduced);
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            PulpMotion.PopIn(normal);
            AssertThat(Mathf.IsEqualApprox(normal.Scale.X, PulpMotionRules.PanelPopFromScale)).IsTrue();
            AssertThat(normal.Modulate.A).IsEqual(0f);

            SetReduced(true);
            PulpMotion.PopIn(reduced);
            AssertThat(reduced.Scale).IsEqual(Vector2.One);     // no scale cue at all
            AssertThat(reduced.Modulate.A).IsEqual(0f);

            await Frames(30);
            AssertThat(normal.Scale.IsEqualApprox(Vector2.One)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(normal.Modulate.A, 1f)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(reduced.Modulate.A, 1f)).IsTrue();
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task ANumberThunkStepsOverThreeFramesAndIsInstantUnderReducedEffects() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Control();
        Tree.Root.AddChild(host);
        try {
            SetReduced(true);
            var instant = new List<int>();
            AssertObject(PulpMotion.Thunk(host, 100, 70, instant.Add)).IsNull();
            AssertThat(instant.Count).IsEqual(1);
            AssertThat(instant[0]).IsEqual(70);

            SetReduced(false);
            var steps = new List<int>();
            PulpMotion.Thunk(host, 100, 70, steps.Add);
            await Frames(12);
            AssertThat(steps.Count).IsEqual(PulpMotionRules.ThunkSteps);
            AssertThat(steps[steps.Count - 1]).IsEqual(70);
            AssertThat(steps[0]).IsNotEqual(70);

            var unchanged = new List<int>();
            PulpMotion.Thunk(host, 5, 5, unchanged.Add);
            AssertThat(unchanged.Count).IsEqual(1);
        } finally {
            SetReduced(before);
            host.Free();
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

    // === Screen wipe ===

    [TestCase]
    public async Task TheWipeCoversAndRevealsAndRaisesCoveredWhileTheScreenIsFullyCovered() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Node();
        Tree.Root.AddChild(host);
        try {
            SetReduced(false);
            ScreenWipe wipe = ScreenWipe.EnsureFor(host);
            AssertThat(ScreenWipe.EnsureFor(host)).IsSame(wipe);
            AssertThat(wipe.Layer).IsEqual(ScreenWipe.WipeLayer);
            AssertThat(wipe.Layer > 100).IsTrue();     // above every loading screen
            AssertThat(wipe.CoverSeconds).IsEqual(PulpMotionRules.WipeSeconds);

            int coveredRaised = 0;
            wipe.Covered += () => coveredRaised++;
            wipe.BeginCover();
            AssertThat(wipe.IsActive).IsTrue();
            AssertThat(wipe.IsCovered).IsFalse();
            for (int i = 0; i < 60 && !wipe.IsCovered; i++) await Frames(1);
            AssertThat(wipe.IsCovered).IsTrue();
            AssertThat(coveredRaised).IsEqual(1);

            wipe.BeginReveal();
            for (int i = 0; i < 60 && wipe.IsActive; i++) await Frames(1);
            AssertThat(wipe.IsActive).IsFalse();
        } finally {
            SetReduced(before);
            host.Free();
        }
    }

    [TestCase]
    public async Task UnderReducedEffectsTheWipeIsAShorterStableFadeNotASlab() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var host = new Node();
        Tree.Root.AddChild(host);
        try {
            SetReduced(true);
            ScreenWipe wipe = ScreenWipe.EnsureFor(host);
            AssertThat(wipe.CoverSeconds).IsEqual(PulpMotionRules.WipeFadeSeconds);
            AssertThat(wipe.CoverSeconds < PulpMotionRules.WipeSeconds).IsTrue();
            wipe.BeginCover();
            for (int i = 0; i < 60 && !wipe.IsCovered; i++) await Frames(1);
            AssertThat(wipe.IsCovered).IsTrue();
            wipe.Release();
            AssertThat(wipe.IsActive).IsFalse();
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
