using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 9.2 (2026-10-16, CM-INPUT; ledger <c>USER-PAD-ULTIMATE-CHORD-LENIENCY</c>,
/// <c>PROVISIONAL-ULTIMATE-CHORD-LENIENCY</c>): the pad Ultimate chord's leniency
/// window. While the Ultimate is affordable a lone LB (Movement Ability) or RB
/// (Special 2) waits <see cref="UltimateChordWindow.LeniencyFrames"/> frames for
/// its partner; the partner turns it into the Ultimate alone; a released tap
/// still yields one press; otherwise the bits pass through.
///
/// <para>Pure C#: the window is plain data, and every frame here is built with
/// <see cref="PlayerInputFrame.Create"/> from the window's output, the same call
/// the sampler makes, so the edges are the ones the game would see.</para>
/// </summary>
[TestSuite]
public class UltimateChordWindowTests {

    private const int Window = UltimateChordWindow.LeniencyFrames;

    /// <summary>One frame of the window as the sampler would turn it into an input frame.</summary>
    private sealed class Pad {
        private UltimateChordWindow _window;
        private GameplayButtons _previous;
        private uint _tick;

        public bool ChordEnabled = true;
        public bool Affordable = true;

        public PlayerInputFrame Step(bool lb, bool rb) {
            UltimateChordWindow.Output output = _window.Step(lb, rb, ChordEnabled, Affordable);
            GameplayButtons held = GameplayButtons.None;
            if (output.MovementHeld) held |= GameplayButtons.MovementAbility;
            if (output.Special2Held) held |= GameplayButtons.Special2;
            if (output.UltimateHeld) held |= GameplayButtons.Ultimate;
            PlayerInputFrame frame = PlayerInputFrame.Create(_tick++, 0f, 0f, held, _previous);
            _previous = held;
            return frame;
        }

        public bool Waiting => _window.IsWaiting;

        public void Reset() => _window.Reset();

        public void ForgetWaits() => _window.ForgetWaits();
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): a pause or a focus loss forgets waits only.
    /// LB + RB that cast the Ultimate and are still held when the pause opens stay
    /// masked across it, so once the meter is spent (no longer lenient) the
    /// resume tick carries no Movement Ability or Special 2 press, and letting go
    /// fires nothing. Until the fix the pause dropped the masks with the waits.
    /// </summary>
    [TestCase]
    public void APauseKeepsAHeldChordsMasksSoItsComponentsNeverFireOnTheWayBack() {
        var pad = new Pad();
        AssertThat(pad.Step(lb: true, rb: true).IsPressed(GameplayButtons.Ultimate)).IsTrue();
        // The Ultimate is cast and the meter spent: no longer affordable, still held.
        pad.Affordable = false;
        AssertThat(pad.Step(lb: true, rb: true).Held).IsEqual(GameplayButtons.Ultimate);

        pad.ForgetWaits();
        for (int frame = 0; frame < 2 * Window; frame++) {
            PlayerInputFrame resumed = pad.Step(lb: true, rb: true);
            AssertThat(resumed.Held)
                .OverrideFailureMessage($"Frame {frame} after the pause: the held chord leaked {resumed.Held}.")
                .IsEqual(GameplayButtons.Ultimate);
            AssertThat(resumed.Pressed).IsEqual(GameplayButtons.None);
        }
        // Letting go of either shoulder, then the other, fires nothing.
        AssertThat(pad.Step(lb: false, rb: true).Held).IsEqual(GameplayButtons.None);
        AssertThat(pad.Step(lb: false, rb: false).Held).IsEqual(GameplayButtons.None);
        // The released shoulders are ordinary buttons again.
        AssertThat(pad.Step(lb: true, rb: false).IsPressed(GameplayButtons.MovementAbility)).IsTrue();

        // A lone shoulder held through the pause after its window ran out is still
        // the same press, held: no release and no second edge.
        var lone = new Pad();
        for (int frame = 0; frame <= Window; frame++) lone.Step(lb: true, rb: false);
        AssertThat(lone.Step(lb: true, rb: false).IsHeld(GameplayButtons.MovementAbility)).IsTrue();
        lone.ForgetWaits();
        PlayerInputFrame heldOn = lone.Step(lb: true, rb: false);
        AssertThat(heldOn.IsHeld(GameplayButtons.MovementAbility)).IsTrue();
        AssertThat(heldOn.IsPressed(GameplayButtons.MovementAbility)).IsFalse();
        AssertThat(heldOn.IsReleased(GameplayButtons.MovementAbility)).IsFalse();
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): what a pause does forget is a wait. A lone
    /// shoulder still waiting for its partner when the pause opens and let go
    /// while away never comes back as a one-frame tap; held on, it starts a fresh
    /// wait and then presses once.
    /// </summary>
    [TestCase]
    public void APauseForgetsAWaitingShoulderSoALetGoNeverComesBackAsATap() {
        var released = new Pad();
        AssertThat(released.Step(lb: true, rb: false).Held).IsEqual(GameplayButtons.None);
        AssertThat(released.Waiting).IsTrue();
        released.ForgetWaits();
        AssertThat(released.Waiting).IsFalse();
        for (int frame = 0; frame < Window + 2; frame++) {
            AssertThat(released.Step(lb: false, rb: false).Held).IsEqual(GameplayButtons.None);
        }

        var heldOn = new Pad();
        heldOn.Step(lb: true, rb: false);
        heldOn.ForgetWaits();
        int presses = 0;
        for (int frame = 0; frame < Window + 3; frame++) {
            if (heldOn.Step(lb: true, rb: false).IsPressed(GameplayButtons.MovementAbility)) presses++;
        }
        AssertThat(presses).IsEqual(1);
    }

    [TestCase]
    public void ALoneShoulderIsHeldBackExactlyTheWindowThenPressed() {
        AssertThat(Window).IsEqual(3);
        foreach (bool leftShoulder in new[] { true, false }) {
            var pad = new Pad();
            GameplayButtons component = leftShoulder ? GameplayButtons.MovementAbility : GameplayButtons.Special2;
            for (int frame = 0; frame < Window; frame++) {
                PlayerInputFrame masked = pad.Step(leftShoulder, !leftShoulder);
                AssertThat(masked.Held)
                    .OverrideFailureMessage($"Frame {frame}: the lone component was not held back ({masked.Held}).")
                    .IsEqual(GameplayButtons.None);
                AssertThat(pad.Waiting).IsTrue();
            }
            PlayerInputFrame late = pad.Step(leftShoulder, !leftShoulder);
            AssertThat(late.IsPressed(component))
                .OverrideFailureMessage($"The lone component was not pressed on frame {Window}.")
                .IsTrue();
            AssertThat(late.IsHeld(GameplayButtons.Ultimate)).IsFalse();
            // From then on it is simply held, one edge only.
            PlayerInputFrame held = pad.Step(leftShoulder, !leftShoulder);
            AssertThat(held.IsHeld(component)).IsTrue();
            AssertThat(held.IsPressed(component)).IsFalse();
        }
    }

    [TestCase]
    public void APartnerInsideTheWindowGivesOnlyTheUltimateWithBothMaskedUntilReleased() {
        for (int arrival = 0; arrival < Window; arrival++) {
            var pad = new Pad();
            // LB first, RB on frame `arrival` (0 = both on the same frame).
            for (int frame = 0; frame < arrival; frame++) pad.Step(lb: true, rb: false);
            PlayerInputFrame chord = pad.Step(lb: true, rb: true);
            AssertThat(chord.Held)
                .OverrideFailureMessage($"RB on frame {arrival}: the chord carried {chord.Held}.")
                .IsEqual(GameplayButtons.Ultimate);
            AssertThat(chord.IsPressed(GameplayButtons.Ultimate)).IsTrue();

            // Held long past the window: still the Ultimate alone.
            for (int frame = 0; frame < 2 * Window; frame++) {
                AssertThat(pad.Step(lb: true, rb: true).Held).IsEqual(GameplayButtons.Ultimate);
            }
            // LB up: the Ultimate releases and RB stays masked while it is held.
            for (int frame = 0; frame < 2 * Window; frame++) {
                PlayerInputFrame afterLb = pad.Step(lb: false, rb: true);
                AssertThat(afterLb.Held)
                    .OverrideFailureMessage($"RB alone after the chord leaked {afterLb.Held}.")
                    .IsEqual(GameplayButtons.None);
            }
            AssertThat(pad.Step(lb: false, rb: false).Held).IsEqual(GameplayButtons.None);
        }
    }

    [TestCase]
    public void AShortTapStillYieldsExactlyOnePress() {
        for (int tapFrames = 1; tapFrames <= Window; tapFrames++) {
            var pad = new Pad();
            var frames = new List<PlayerInputFrame>();
            for (int frame = 0; frame < tapFrames; frame++) frames.Add(pad.Step(lb: true, rb: false));
            for (int frame = 0; frame < Window + 2; frame++) frames.Add(pad.Step(lb: false, rb: false));
            int presses = 0;
            int heldFrames = 0;
            foreach (PlayerInputFrame frame in frames) {
                if (frame.IsPressed(GameplayButtons.MovementAbility)) presses++;
                if (frame.IsHeld(GameplayButtons.MovementAbility)) heldFrames++;
            }
            AssertThat(presses)
                .OverrideFailureMessage($"A {tapFrames}-frame tap produced {presses} press(es).")
                .IsEqual(1);
            // Released inside the window: held for exactly the release frame.
            if (tapFrames < Window) AssertThat(heldFrames).IsEqual(1);
        }
    }

    [TestCase]
    public void WithoutAnAffordableUltimateNothingIsDelayed() {
        var pad = new Pad { Affordable = false };
        PlayerInputFrame lb = pad.Step(lb: true, rb: false);
        AssertThat(lb.IsPressed(GameplayButtons.MovementAbility)).IsTrue();
        AssertThat(pad.Waiting).IsFalse();
        // The chord passes through with today's bits: both components and the Ultimate.
        PlayerInputFrame both = pad.Step(lb: true, rb: true);
        AssertThat(both.IsHeld(GameplayButtons.MovementAbility)).IsTrue();
        AssertThat(both.IsPressed(GameplayButtons.Special2)).IsTrue();
        AssertThat(both.IsPressed(GameplayButtons.Ultimate)).IsTrue();

        // Affordability lost mid-wait: the waiting component is emitted at once.
        var lapsing = new Pad();
        AssertThat(lapsing.Step(lb: false, rb: true).Held).IsEqual(GameplayButtons.None);
        lapsing.Affordable = false;
        AssertThat(lapsing.Step(lb: false, rb: true).IsPressed(GameplayButtons.Special2)).IsTrue();
    }

    [TestCase]
    public void WithTheShortcutOffTheComponentsGiveNoUltimate() {
        var pad = new Pad { ChordEnabled = false };
        PlayerInputFrame both = pad.Step(lb: true, rb: true);
        AssertThat(both.IsHeld(GameplayButtons.Ultimate))
            .OverrideFailureMessage("The chord fired with the C01c Ultimate shortcut off.")
            .IsFalse();
        AssertThat(both.IsPressed(GameplayButtons.MovementAbility)).IsTrue();
        AssertThat(both.IsPressed(GameplayButtons.Special2)).IsTrue();
        AssertThat(pad.Waiting).IsFalse();
    }

    [TestCase]
    public void ARePressAfterTheChordIsAFreshEdge() {
        var pad = new Pad();
        AssertThat(pad.Step(lb: true, rb: true).IsPressed(GameplayButtons.Ultimate)).IsTrue();
        pad.Step(lb: true, rb: true);
        // Let go of both, then chord again: a new Ultimate press.
        AssertThat(pad.Step(lb: false, rb: false).IsReleased(GameplayButtons.Ultimate)).IsTrue();
        AssertThat(pad.Step(lb: true, rb: true).IsPressed(GameplayButtons.Ultimate)).IsTrue();

        // Re-pressing one shoulder while the other is still held re-forms the chord.
        pad.Step(lb: false, rb: true);
        PlayerInputFrame reformed = pad.Step(lb: true, rb: true);
        AssertThat(reformed.Held).IsEqual(GameplayButtons.Ultimate);
        AssertThat(reformed.IsPressed(GameplayButtons.Ultimate)).IsTrue();

        // After a full release a lone shoulder waits again, as on a fresh pad.
        pad.Step(lb: false, rb: false);
        AssertThat(pad.Step(lb: true, rb: false).Held).IsEqual(GameplayButtons.None);
        AssertThat(pad.Waiting).IsTrue();

        // Reset (pause, focus loss, a hand-over) forgets the wait.
        pad.Reset();
        AssertThat(pad.Waiting).IsFalse();
    }
}
