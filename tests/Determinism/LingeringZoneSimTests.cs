using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The lingering-ability kit pass (KL-SIM, 2026-10-31), zones in the Fighter sim, on the AUTHORED loadouts:
/// the Rift's second collapse trigger (the owner's landed string finisher) and its 8 damage, the Vortex's
/// 7 u/s pull with the roll and air-dodge exemption and its slow on exit, and Golden Ratio's pull.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LingeringZoneSimTests {
    private const int FinisherOwed = FighterLingeringKitRules.OwedFinisherBit | 1;

    // --- Rift ----------------------------------------------------------------------

    [TestCase]
    public void TheOwnersLandedFinisherCollapsesTheRiftAndTheCollapseHitsForEightAndPaysOnce() {
        FighterSimulation sim = RiftDuel(8301);
        AssertThat(sim.TryGetFirstZone(out FighterZoneComponent rift)).IsTrue();
        AssertThat(rift.CollapseFramesRemaining).IsEqual(0);
        int momentumBefore = MomentumTestKit.Units(sim, 0);
        sim.SeedFighterOwedStrikesForTest(0, FinisherOwed);
        sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        AssertThat(sim.TryGetFirstZone(out FighterZoneComponent collapsing)).IsTrue();
        AssertThat(collapsing.CollapseFramesRemaining)
            .OverrideFailureMessage("The finisher collapsed the rift (only an E=mc2 burst did before).")
            .IsGreater(0);
        sim.TryGetFighter(1, out FighterStateComponent target);
        int hpBeforeLaunch = target.CurrentHP;
        for (int i = 0; i < KitMotionRules.RiftCollapsePullFrames; i++) sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        AssertThat(sim.ZoneCount).IsEqual(0);
        sim.TryGetFighter(1, out FighterStateComponent launched);
        AssertThat(hpBeforeLaunch - launched.CurrentHP).IsEqual(LingeringKitRules.RiftCollapseDamage);
        int paid = MomentumTestKit.Units(sim, 0) - momentumBefore;
        AssertThat(paid >= LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation)).IsTrue();
        AssertThat(paid < LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation) + 200)
            .OverrideFailureMessage("0.2 pip once per collapse (plus a few frames of regen).").IsTrue();
    }

    [TestCase]
    public void AnOrdinaryOwedHitOrAFinisherOutsideTheRiftDoesNotCollapseIt() {
        FighterSimulation ordinary = RiftDuel(8302);
        ordinary.SeedFighterOwedStrikesForTest(0, 1);
        ordinary.Advance(Neutral(ordinary.CurrentTick), Neutral(ordinary.CurrentTick));
        ordinary.TryGetFirstZone(out FighterZoneComponent rift);
        AssertThat(rift.CollapseFramesRemaining).IsEqual(0);

        FighterSimulation outside = RiftDuel(8303);
        outside.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) =>
            state.Position = new FPVector2(FP64.FromInt(9), state.Position.y));
        outside.SeedFighterOwedStrikesForTest(0, FinisherOwed);
        outside.Advance(Neutral(outside.CurrentTick), Neutral(outside.CurrentTick));
        outside.TryGetFirstZone(out FighterZoneComponent untouched);
        AssertThat(untouched.CollapseFramesRemaining).IsEqual(0);
    }

    [TestCase]
    public void ARealThreeHitStringCollapsesTheRiftOnItsFinisher() {
        FighterSimulation sim = RiftDuel(8304);
        bool collapsed = false;
        for (int i = 0; i < 160 && !collapsed; i++) {
            sim.Advance(Frame(sim.CurrentTick, GameplayButtons.BasicAttack, held: true), Neutral(sim.CurrentTick));
            if (sim.TryGetFirstZone(out FighterZoneComponent zone) && zone.CollapseFramesRemaining > 0) collapsed = true;
        }
        AssertThat(collapsed).OverrideFailureMessage("A held string's hit 3 should collapse the rift around the opponent.").IsTrue();
    }

    // --- Vortex --------------------------------------------------------------------

    [TestCase]
    public void TheVortexPullsAtSevenUnitsASecond() {
        FighterSimulation sim = VortexDuel(8311, out _);
        sim.TryGetFighter(1, out FighterStateComponent before);
        sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        sim.TryGetFighter(1, out FighterStateComponent after);
        FP64 moved = before.Position.x - after.Position.x;
        FP64 expected = FighterLingeringKitRules.VortexPullPerFrame;
        AssertThat(FP64.Abs(moved - expected) < FP64.FromDouble(0.01))
            .OverrideFailureMessage($"Pulled {moved.ToFloat()} u a frame; 7 u/s is {expected.ToFloat()} (the old pull was 0.05).")
            .IsTrue();
    }

    [TestCase]
    public void ARollsTravelAndAnAirDodgeIgnoreThePull() {
        foreach (UniversalMovementPhase phase in new[] { UniversalMovementPhase.RollTravel, UniversalMovementPhase.AirDodgeInvulnerable }) {
            FighterSimulation pulled = VortexDuel(8312, out _);
            FighterSimulation exempt = VortexDuel(8312, out _);
            UniversalMovementPhase captured = phase;
            exempt.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) => {
                runtime.UniversalMovementState = (int)captured;
                runtime.UniversalMovementFramesRemaining = 6;
                runtime.UniversalMovementDirection = 0;
            });
            // The same roll without the Vortex would travel by itself; the pulled control shows the pull alone.
            pulled.Advance(Neutral(pulled.CurrentTick), Neutral(pulled.CurrentTick));
            exempt.Advance(Neutral(exempt.CurrentTick), Neutral(exempt.CurrentTick));
            pulled.TryGetFighter(1, out FighterStateComponent a);
            exempt.TryGetFighter(1, out FighterStateComponent b);
            AssertThat(b.Position.x > a.Position.x)
                .OverrideFailureMessage($"{phase}: the pull must not move the dodging fighter.").IsTrue();
        }
        FighterRuntimeComponent travel = default;
        travel.UniversalMovementState = (int)UniversalMovementPhase.RollTravel;
        AssertThat(FighterVortexRules.IgnoresPull(in travel)).IsTrue();
        travel.UniversalMovementState = (int)UniversalMovementPhase.RollRecovery;
        AssertThat(FighterVortexRules.IgnoresPull(in travel)).OverrideFailureMessage("Only the travel frames are exempt.").IsFalse();
    }

    [TestCase]
    public void TheVortexSlowsOnlyOnLeavingAndWhenItEnds() {
        FighterSimulation sim = VortexDuel(8313, out _);
        for (int i = 0; i < 30; i++) sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        sim.TryGetFighterRuntime(1, out FighterRuntimeComponent inside);
        AssertThat(inside.StatusType)
            .OverrideFailureMessage("No slow while the victim is inside the Vortex.")
            .IsNotEqual((int)StatusType.TimeDilation);

        // Leaving: the lingering slow starts.
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) =>
            state.Position = new FPVector2(FP64.FromInt(9), state.Position.y));
        sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        sim.TryGetFighterRuntime(1, out FighterRuntimeComponent left);
        AssertThat(left.StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(left.StatusFrames).IsGreater(LingeringKitRules.HeroSlowLingerFrames - 3);

        // Ending with the victim inside: the slow starts then.
        FighterSimulation ends = VortexDuel(8314, out _);
        for (int i = 0; i < 200; i++) ends.Advance(Neutral(ends.CurrentTick), Neutral(ends.CurrentTick));
        AssertThat(ends.ZoneCount).IsEqual(0);
        ends.TryGetFighterRuntime(1, out FighterRuntimeComponent after);
        AssertThat(after.StatusType).IsEqual((int)StatusType.TimeDilation);
    }

    [TestCase]
    public void TheVortexFinalPulseIsADetonationPayingZeroPointTwoPipOnce() {
        FighterSimulation sim = VortexDuel(8315, out _);
        int big = 0;
        int previous = MomentumTestKit.Units(sim, 0);
        for (int i = 0; i < 160; i++) {
            sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
            int now = MomentumTestKit.Units(sim, 0);
            if (now - previous >= LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation)) big++;
            previous = now;
        }
        AssertThat(big).OverrideFailureMessage("One 0.2-pip payout across the five pulses.").IsEqual(1);
    }

    [TestCase]
    public void TheVortexLeavesAHeldVictimOnItsGrabber() {
        // Merge review (grab + kit): the catch freeze skips the hold's re-pin, so a pull would drift the victim.
        FighterSimulation sim = VortexDuel(8314, out _);
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) =>
            verb.BeingHeld = 1);
        sim.TryGetFighter(1, out FighterStateComponent before);
        sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        sim.TryGetFighter(1, out FighterStateComponent after);
        AssertThat(after.Position.x == before.Position.x)
            .OverrideFailureMessage($"A held victim moved {(before.Position.x - after.Position.x).ToFloat()} u toward the Vortex.")
            .IsTrue();
    }

    // --- Golden Ratio --------------------------------------------------------------

    [TestCase]
    public void GoldenRatioPullsTheTargetInAndPaysOncePerCast() {
        FighterSimulation sim = new(
            ProjectileBurstSimTests.Authored("leonardo"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 8321, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        sim.TryGetFighter(1, out FighterStateComponent start);
        sim.Advance(Press(0, GameplayButtons.Special1), Neutral(0));
        int tick = SpecialCastTestKit.StepUntilFired(sim, 0, 1);
        sim.TryGetFighter(1, out FighterStateComponent pulled);
        AssertThat(start.Position.x - pulled.Position.x > FP64.FromDouble(0.8))
            .OverrideFailureMessage("The first tick draws the target toward the spiral's centre by half the way (the old tick left it where it stood).")
            .IsTrue();
        AssertThat(pulled.HitstunFrames).IsGreater(0);

        int payouts = 0;
        int previous = MomentumTestKit.Units(sim, 0);
        for (; tick < 90; tick++) {
            sim.Advance(Neutral(tick), Neutral(tick));
            int now = MomentumTestKit.Units(sim, 0);
            if (now - previous >= LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.HoldCast)) payouts++;
            previous = now;
        }
        AssertThat(payouts).OverrideFailureMessage("0.05 pip once per cast, however many of the three ticks land.").IsLessEqual(1);
    }

    [TestCase]
    public void ThePullIsCappedAtOneAndAHalfUnitsATick() {
        FighterStateComponent target = default;
        target.IsGrounded = 1;
        target.Position = new FPVector2(FP64.FromInt(6), FP64.Zero);
        FighterSpiralRules.Pull(ref target, new FPVector2(FP64.Zero, FP64.Zero));
        AssertThat(target.Position.x).OverrideFailureMessage("Half of 6 is 3: capped at 1.5.").IsEqual(FP64.FromDouble(4.5));
        target.Position = new FPVector2(FP64.FromInt(2), FP64.Zero);
        FighterSpiralRules.Pull(ref target, new FPVector2(FP64.Zero, FP64.Zero));
        AssertThat(target.Position.x).IsEqual(FP64.One);
    }

    // --- helpers ------------------------------------------------------------------

    /// <summary>Einstein (P1) opens his rift over the opponent (P2), which stands inside it.</summary>
    [TestCase]
    public void GoldenRatioLeavesAHeldVictimOnItsGrabber() {
        FighterSimulation sim = new(
            ProjectileBurstSimTests.Authored("leonardo"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 8322, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        sim.Advance(Press(0, GameplayButtons.Special1), Neutral(0));
        SpecialCastTestKit.StepUntilFired(sim, 0, 1);
        FPVector2 centre = default;
        AssertThat(sim.MutateZoneForTest(0, (ref FighterZoneComponent zone) => {
            zone.TickFramesRemaining = 1;
            centre = zone.Position;
        })).IsTrue();
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) => {
            verb.BeingHeld = 1;
            state.HitstunFrames = 0;
            state.Position = new FPVector2(centre.x + FP64.One, state.Position.y);
        });
        sim.TryGetFighter(1, out FighterStateComponent before);
        sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
        sim.TryGetFighter(1, out FighterStateComponent after);
        AssertThat(after.Position.x == before.Position.x)
            .OverrideFailureMessage($"A held victim moved {(before.Position.x - after.Position.x).ToFloat()} u toward the spiral.")
            .IsTrue();
    }

    private static FighterSimulation RiftDuel(int seed) {
        FighterSimulation sim = new(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        sim.Advance(Press(0, GameplayButtons.Special2), Neutral(0));
        SpecialCastTestKit.StepUntilFired(sim, 0, 1);
        sim.TryGetFighter(1, out FighterStateComponent target);
        sim.MutateZoneForTest(0, (ref FighterZoneComponent zone) => zone.Position = new FPVector2(target.Position.x, target.Position.y));
        return sim;
    }

    /// <summary>Cleopatra (P1) opens her Vortex; the opponent is inside it, 1 unit from its centre.</summary>
    private static FighterSimulation VortexDuel(int seed, out int tick) {
        FighterSimulation sim = new(
            ProjectileBurstSimTests.Authored("cleopatra"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        sim.Advance(Press(0, GameplayButtons.Special2), Neutral(0));
        tick = SpecialCastTestKit.StepUntilFired(sim, 0, 1);
        sim.TryGetFighter(1, out FighterStateComponent target);
        sim.MutateZoneForTest(0, (ref FighterZoneComponent zone) => zone.Position = new FPVector2(target.Position.x - FP64.One, target.Position.y));
        return sim;
    }

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };

    private static PlayerInputFrame Press(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, bool held) => new() {
        Tick = (uint)tick,
        Held = held ? pressed : GameplayButtons.None,
        Pressed = pressed
    };
}
