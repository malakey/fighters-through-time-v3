using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A3: the outline/glow priority arbiter's decision rules, exercised
/// without the engine. design-godot.md "Priority &amp; Overwrite": hyper-armor /
/// spawn invincibility override status effects, and status effects override the
/// player slot indicator; when a higher source clears, the shader reverts to the
/// next active source, or disables when none remain.
/// </summary>
// Godot's Color struct puts this suite over the GdUnit0501 analyzer's bar even
// though no engine node is involved; the arbitration rules themselves are pure.
[TestSuite]
[RequireGodotRuntime]
public class GlowStateStackTests {

    [TestCase]
    public void AnEmptyStackResolvesToNothing() {
        var stack = new GlowStateStack();
        AssertThat(stack.ActiveLayerCount).IsEqual(0);
        AssertThat(stack.TryResolve(out GlowState _)).IsFalse();
    }

    [TestCase]
    public void HyperArmorOutranksStatusWhichOutranksTheSlotIndicator() {
        var stack = new GlowStateStack();
        stack.Push(GlowPalette.SlotIndicator(1));
        AssertThat(stack.TryResolve(out GlowState resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.SlotIndicator);

        stack.Push(GlowPalette.Status(StatusType.RadiantBurn));
        AssertThat(stack.TryResolve(out resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.Status);

        stack.Push(GlowPalette.SpawnInvulnerability());
        AssertThat(stack.TryResolve(out resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.SpawnInvulnerability);

        stack.Push(GlowPalette.HyperArmor());
        AssertThat(stack.TryResolve(out resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.HyperArmor);
        AssertThat(stack.ActiveLayerCount).IsEqual(4);
    }

    [TestCase]
    public void ClearingTheTopLayerRevertsToTheNextActiveSource() {
        var stack = new GlowStateStack();
        stack.Push(GlowPalette.SlotIndicator(0));
        stack.Push(GlowPalette.Status(StatusType.Venom));
        stack.Push(GlowPalette.HyperArmor());

        stack.Clear(GlowLayer.HyperArmor);
        AssertThat(stack.TryResolve(out GlowState resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.Status);

        stack.Clear(GlowLayer.Status);
        AssertThat(stack.TryResolve(out resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.SlotIndicator);

        stack.Clear(GlowLayer.SlotIndicator);
        AssertThat(stack.TryResolve(out GlowState _)).IsFalse();
    }

    [TestCase]
    public void ClearingALowerLayerLeavesTheActiveTreatmentAlone() {
        var stack = new GlowStateStack();
        stack.Push(GlowPalette.SlotIndicator(1));
        stack.Push(GlowPalette.HyperArmor());

        stack.Clear(GlowLayer.SlotIndicator);
        AssertThat(stack.TryResolve(out GlowState resolved)).IsTrue();
        AssertThat(resolved.Layer).IsEqual(GlowLayer.HyperArmor);
        AssertThat(stack.IsActive(GlowLayer.SlotIndicator)).IsFalse();
    }

    [TestCase]
    public void PushingTheSameLayerReplacesRatherThanStacks() {
        var stack = new GlowStateStack();
        stack.Push(GlowPalette.Status(StatusType.Root));
        stack.Push(GlowPalette.Status(StatusType.StaticCharge));
        AssertThat(stack.ActiveLayerCount).IsEqual(1);

        AssertThat(stack.TryResolve(out GlowState resolved)).IsTrue();
        AssertThat(resolved.OutlineColor).IsEqual(GlowPalette.StaticChargeColor);

        stack.Clear(GlowLayer.Status);
        AssertThat(stack.TryResolve(out GlowState _)).IsFalse();
    }

    [TestCase]
    public void EveryAuthoredStatusMatchesItsDesignedColourAndPulse() {
        AssertThat(GlowPalette.Status(StatusType.TimeDilation).OutlineColor).IsEqual(GlowPalette.TimeDilationColor);
        AssertThat(GlowPalette.Status(StatusType.TimeDilation).PulseSpeed).IsEqual(0f);
        AssertThat(GlowPalette.Status(StatusType.StaticCharge).PulseSpeed).IsEqual(3f);
        AssertThat(GlowPalette.Status(StatusType.RadiantBurn).Intensity).IsEqual(2.5f);
        AssertThat(GlowPalette.Status(StatusType.Venom).Thickness).IsEqual(2f);
        AssertThat(GlowPalette.Status(StatusType.Root).Thickness).IsEqual(1.5f);
        AssertThat(GlowPalette.Status(StatusType.Root).PulseSpeed).IsEqual(0f);

        AssertThat(GlowPalette.HyperArmor().Thickness).IsEqual(3f);
        AssertThat(GlowPalette.HyperArmor().Intensity).IsEqual(2f);
        AssertThat(GlowPalette.SpawnInvulnerability().Thickness).IsEqual(2.5f);
        AssertThat(GlowPalette.SpawnInvulnerability().PulseSpeed).IsEqual(2f);
    }

    [TestCase]
    public void AStatusOfNoneIsNotAVisibleTreatment() {
        GlowState none = GlowPalette.Status(StatusType.None);
        AssertThat(none.IsVisible).IsFalse();
        AssertThat(GlowPalette.HyperArmor().IsVisible).IsTrue();
    }

    [TestCase]
    public void SlotColoursMatchTheFourDesignedPlayerColours() {
        AssertThat(GlowPalette.SlotColor(0)).IsEqual(new Color(0f, 0.941f, 1f));
        AssertThat(GlowPalette.SlotColor(1)).IsEqual(new Color(1f, 0.2f, 0.4f));
        AssertThat(GlowPalette.SlotColor(2)).IsEqual(new Color(1f, 0.843f, 0f));
        AssertThat(GlowPalette.SlotColor(3)).IsEqual(new Color(0f, 1f, 0.533f));
        // Out-of-range slots fall back to P1 rather than throwing.
        AssertThat(GlowPalette.SlotColor(9)).IsEqual(GlowPalette.SlotColor(0));
    }

    [TestCase]
    public void ConstructionClampsToTheAuthoredUniformRanges() {
        var wild = new GlowState(GlowLayer.Status, Colors.Red, thickness: 99f, intensity: 99f, pulseSpeed: -4f);
        AssertThat(wild.Thickness).IsEqual(5f);
        AssertThat(wild.Intensity).IsEqual(3f);
        AssertThat(wild.PulseSpeed).IsEqual(0f);

        var tiny = new GlowState(GlowLayer.Status, Colors.Red, thickness: -1f, intensity: 0.1f, pulseSpeed: 0f);
        AssertThat(tiny.Thickness).IsEqual(0f);
        AssertThat(tiny.Intensity).IsEqual(0.5f);
    }
}
