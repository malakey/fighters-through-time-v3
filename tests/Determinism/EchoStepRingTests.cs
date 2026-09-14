using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A1c. The V7.6 Echo Step position history:
/// <c>docs/design-contracts/TEMPORAL_STATE_CONTRACT.md</c> "Echo Step: exact
/// destination, validated twice".
///
/// <para>What replaced what: the shipped V7.1 ring held <b>5</b> samples written
/// every <b>6</b> frames and read the oldest, so the destination was somewhere
/// between 24 and 30 frames back <em>depending on the phase the chord happened to
/// land on</em>. The contract requires 31 consecutive per-tick samples and the
/// exact <c>t - 30</c> one. That is the difference these cases exist to hold.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EchoStepRingTests {

    /// <summary>
    /// One sample per authoritative tick, and never more. The valid count is what
    /// gates activation, so it climbing exactly in step with the tick count is the
    /// whole "31 consecutive samples" claim.
    /// </summary>
    [TestCase]
    public void TheRingRecordsExactlyOneSamplePerTick() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);

        for (int tick = 0; tick < FighterEchoStepRing.SampleCount + 20; tick++) {
            AssertThat(simulation.TryGetEchoStepRing(0, out FighterEchoStepRing0Component ring)).IsTrue();
            int expected = tick < FighterEchoStepRing.SampleCount ? tick : FighterEchoStepRing.SampleCount;
            AssertThat(ring.ValidCount)
                .OverrideFailureMessage(
                    $"After {tick} ticks the ring held {ring.ValidCount} samples, not {expected}.")
                .IsEqual(expected);
            AssertThat(ring.LatestTick).IsEqual(tick);
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
    }

    /// <summary>
    /// The ring is 31 deep, not 30: the newest sample is written at the start of
    /// tick <c>t</c>, so <c>t - 30</c> has to survive that write.
    /// </summary>
    [TestCase]
    public void TheRingIsThirtyOneDeepBecauseTheNewestSampleIsWrittenFirst() {
        AssertThat(FighterEchoStepRing.SampleCount)
            .IsEqual(BasicComboRules.EchoStepLookbackFrames + 1);
        AssertThat(BasicComboRules.EchoStepRingSamples).IsEqual(FighterEchoStepRing.SampleCount);
    }

    /// <summary>
    /// The destination is the <b>exact</b> position held 30 ticks ago. The fighter
    /// walks at a constant speed so every tick's position is distinct and an
    /// off-by-a-few-frames answer is arithmetically visible rather than merely
    /// "close".
    /// </summary>
    [TestCase]
    public void TheLookbackIsTheExactPositionThirtyTicksEarlier() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        var recorded = new System.Collections.Generic.List<FP64>();

        for (int tick = 0; tick < 200; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent state)).IsTrue();
            recorded.Add(state.Position.x);

            if (recorded.Count > BasicComboRules.EchoStepLookbackFrames) {
                AssertThat(simulation.TryGetEchoStepLookback(0, out FPVector2 lookback)).IsTrue();
                FP64 expected = recorded[recorded.Count - 1 - BasicComboRules.EchoStepLookbackFrames];
                AssertThat(lookback.x.RawValue)
                    .OverrideFailureMessage(
                        $"Tick {tick}: t-30 read {lookback.x.ToFloat()} but the fighter held " +
                        $"{expected.ToFloat()} thirty ticks earlier.")
                    .IsEqual(expected.RawValue);
            }
            simulation.Advance(Frame(tick, 96, GameplayButtons.None), Neutral(tick));
        }
    }

    /// <summary>
    /// The sample is taken BEFORE this tick's input and movement, so the newest
    /// entry is where the fighter stood at the start of the tick, not after it
    /// moved. That ordering is what makes "t - 30" mean thirty ticks of motion.
    /// </summary>
    [TestCase]
    public void TheSampleIsTakenBeforeThatTicksMovement() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        // Warm the ring so a lookback exists at all.
        for (int tick = 0; tick < FighterEchoStepRing.SampleCount; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        // Thirty stationary ticks precede this one, so t-30 is the standing spot.
        AssertThat(simulation.TryGetEchoStepLookback(0, out FPVector2 standing)).IsTrue();
        AssertThat(standing.x.RawValue).IsEqual(before.Position.x.RawValue);

        // Now walk one tick. The ring's newest sample must be the PRE-move position.
        simulation.Advance(Frame(100, 127, GameplayButtons.None), Neutral(100));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Position.x.RawValue != before.Position.x.RawValue)
            .OverrideFailureMessage("Harness: the fighter must actually have moved on that tick.")
            .IsTrue();
    }

    /// <summary>
    /// Spawn-filled storage is not history. A brand-new generation reports zero
    /// valid samples and refuses to hand out a lookback until 30 further ticks have
    /// genuinely happened — "never treat filled slots as a fabricated earlier
    /// timeline".
    /// </summary>
    [TestCase]
    public void AFreshGenerationRequiresThirtySubsequentTicksBeforeALookbackExists() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);

        AssertThat(simulation.TryGetEchoStepRing(0, out FighterEchoStepRing0Component fresh)).IsTrue();
        AssertThat(fresh.ValidCount).IsEqual(0);
        AssertThat(fresh.Generation).IsEqual(1);

        for (int tick = 0; tick < BasicComboRules.EchoStepLookbackFrames; tick++) {
            AssertThat(simulation.TryGetEchoStepLookback(0, out FPVector2 _))
                .OverrideFailureMessage($"A lookback existed after only {tick} real samples.")
                .IsFalse();
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        // The 31st sample completes the window.
        simulation.Advance(
            Neutral(BasicComboRules.EchoStepLookbackFrames),
            Neutral(BasicComboRules.EchoStepLookbackFrames));
        AssertThat(simulation.TryGetEchoStepLookback(0, out FPVector2 _)).IsTrue();
    }

    /// <summary>
    /// A stock loss is a forced relocation: the generation advances and the history
    /// starts again at the respawn point. Without this the next Echo Step would
    /// snap the fighter back into the life they just lost — very possibly into the
    /// blast zone that took it.
    /// </summary>
    [TestCase]
    public void AStockLossOpensANewHistoryGeneration() {
        var simulation = new FighterSimulation(
            stocks: 3, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));

        for (int tick = 0; tick < 120; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        AssertThat(simulation.TryGetEchoStepRing(1, out FighterEchoStepRing0Component warm)).IsTrue();
        AssertThat(warm.ValidCount).IsEqual(FighterEchoStepRing.SampleCount);
        int generationBefore = warm.Generation;

        DriveOffTheBottom(simulation, 1);

        AssertThat(simulation.TryGetEchoStepRing(1, out FighterEchoStepRing0Component reset)).IsTrue();
        AssertThat(reset.Generation)
            .OverrideFailureMessage("A stock loss must open a new Echo Step history generation.")
            .IsGreater(generationBefore);
        AssertThat(reset.ValidCount)
            .OverrideFailureMessage("The new generation counted spawn-filled slots as history.")
            .IsLess(FighterEchoStepRing.SampleCount);
        AssertThat(simulation.TryGetEchoStepLookback(1, out FPVector2 _)).IsFalse();
    }

    /// <summary>
    /// Sudden Death setup initializes a new generation at the spawn, so the decider
    /// can never teleport into regulation history.
    /// </summary>
    [TestCase]
    public void SuddenDeathOpensANewHistoryGenerationAtTheSpawn() {
        var simulation = new FighterSimulation(
            matchSeconds: 3, rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));

        for (int tick = 0; tick < 180; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        AssertThat(simulation.GetMatchState().SuddenDeathActive).IsEqual(1);

        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetEchoStepRing(playerID, out FighterEchoStepRing0Component ring)).IsTrue();
            AssertThat(ring.Generation)
                .OverrideFailureMessage("Sudden Death must open a new Echo Step generation.")
                .IsGreater(1);
            AssertThat(simulation.TryGetEchoStepLookback(playerID, out FPVector2 _))
                .OverrideFailureMessage("Sudden Death must require 30 fresh ticks before Echo Step.")
                .IsFalse();
        }
    }

    /// <summary>
    /// The ring is snapshot and hash state like everything else: a rollback across
    /// the window restores the same history, so a resimulated Echo Step reads the
    /// same destination. Two simulations fed identical inputs, one of them rolled
    /// back and replayed, must agree on hash, valid count and lookback.
    /// </summary>
    [TestCase]
    public void TheRingSurvivesRollbackAndResimulation() {
        var reference = new FighterSimulation(seed: 4242, rules: FighterMatchRules.Disabled);
        var rolled = new FighterSimulation(seed: 4242, rules: FighterMatchRules.Disabled);

        for (int tick = 0; tick < 90; tick++) {
            PlayerInputFrame one = Frame(tick, (sbyte)(tick % 2 == 0 ? 90 : -70), GameplayButtons.None);
            PlayerInputFrame two = Frame(tick, (sbyte)(tick % 3 == 0 ? -60 : 40), GameplayButtons.None);
            reference.Advance(one, two);
            rolled.AdvanceWithPredictedPlayerTwo(one);
            rolled.CorrectPlayerTwoInput(tick, two);
        }

        AssertThat(rolled.CurrentTick).IsEqual(reference.CurrentTick);
        AssertThat(rolled.CurrentHash)
            .OverrideFailureMessage("The Echo Step ring must converge across a rollback.")
            .IsEqual(reference.CurrentHash);
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(reference.TryGetEchoStepLookback(playerID, out FPVector2 expected)).IsTrue();
            AssertThat(rolled.TryGetEchoStepLookback(playerID, out FPVector2 actual)).IsTrue();
            AssertThat(actual.x.RawValue).IsEqual(expected.x.RawValue);
            AssertThat(actual.y.RawValue).IsEqual(expected.y.RawValue);
        }
    }

    // === Helpers ===

    /// <summary>Drop through the legacy arena's floor and ride out the blast zone.</summary>
    private static void DriveOffTheBottom(FighterSimulation simulation, int playerID) {
        for (int offset = 0; offset < 300; offset++) {
            int tick = simulation.CurrentTick;
            PlayerInputFrame falling = offset == 0
                ? Frame(tick, 0, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, 0, GameplayButtons.Down);
            PlayerInputFrame one = playerID == 0 ? falling : Neutral(tick);
            PlayerInputFrame two = playerID == 1 ? falling : Neutral(tick);
            simulation.Advance(one, two);
            AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.KnockoutsSuffered > 0) return;
        }
        AssertThat(false).OverrideFailureMessage("Harness: the fall never reached the blast zone.").IsTrue();
    }

    private static PlayerInputFrame Neutral(int tick) => Frame(tick, 0, GameplayButtons.None);

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
