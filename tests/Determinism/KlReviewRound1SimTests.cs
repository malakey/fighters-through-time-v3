using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Lingering-kit pass, review round 1 (sim half): a construct's own attack never shortens a fresh
/// synced hold; Golden Ratio's hitstun is tick 1 only; the strike counter on 323 survives a construct
/// destroyed by its last charge and its presentation edge never replays or fires on a lowered count.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class KlReviewRound1SimTests {
    private const int NestType = FighterConstructCharges.NestObjectTypeID;
    private const int CoilType = FighterConstructCharges.CoilObjectTypeID;

    [TestCase]
    public void ANestsOwnNipDueOnTheSameFrameAsASyncedLungeLeavesTheSixtyFrameHoldIntact() {
        // Control: find the tick on which the nest's own nip first lands.
        FighterSimulation control = Duel(8401);
        PlaceConstruct(control, NestType, 2f);
        int hp = Hp(control, 1);
        int nipTick = 0;
        for (int i = 1; i <= 600 && Hp(control, 1) == hp; i++) {
            Step(control, 1);
            nipTick = i;
        }
        AssertThat(Hp(control, 1)).OverrideFailureMessage("Harness: the own nip must land.").IsLess(hp);

        FighterSimulation sim = Duel(8401);
        PlaceConstruct(sim, NestType, 2f);
        Step(sim, nipTick - 1);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        sim.TryGetFighter(1, out FighterStateComponent target);
        AssertThat(target.HitstunFrames)
            .OverrideFailureMessage("The own nip's 12 frames must not replace the lunge's 60-frame hold.")
            .IsGreaterEqual(LingeringKitRules.NestSyncedLungeHoldFrames - 3);
    }

    [TestCase]
    public void TheFinalSyncedStrikeLeavesACountOnTheOwnerAfterItsConstructIsDestroyed() {
        FighterSimulation sim = Duel(8402);
        PlaceConstruct(sim, CoilType, 0f);
        sim.TryGetFighterActionState(0, out FighterActionStateComponent before);
        AssertThat(FighterSyncedStrikeRules.StrikeCount(in before)).IsEqual(0);
        sim.SeedFighterOwedStrikesForTest(0, LingeringKitRules.CoilCharges);
        Step(sim, 1);
        AssertThat(sim.PersistentObjectCount).OverrideFailureMessage("The spent coil is destroyed in the same update.").IsEqual(0);
        sim.TryGetFighterActionState(0, out FighterActionStateComponent after);
        AssertThat(FighterSyncedStrikeRules.StrikeCount(in after)).IsEqual(LingeringKitRules.CoilCharges);
    }

    [TestCase]
    public void TheStrikeWatchPresentsOnceOnAShortForwardStepAndNeverOnALoweredCount() {
        var watch = new StrikeCounterWatch();
        AssertThat(watch.Observe(2)).OverrideFailureMessage("First sight baselines.").IsFalse();
        AssertThat(watch.Observe(2)).IsFalse();
        AssertThat(watch.Observe(3)).OverrideFailureMessage("A new strike.").IsTrue();
        AssertThat(watch.Observe(2)).OverrideFailureMessage("A rollback correction lowers the count: nothing.").IsFalse();
        AssertThat(watch.Observe(3)).OverrideFailureMessage("The same strike re-reached after a correction is not replayed.").IsFalse();
        AssertThat(watch.Observe(4)).OverrideFailureMessage("A genuinely new strike still shows.").IsTrue();
        AssertThat(watch.Observe(15)).OverrideFailureMessage("A long jump is not a short forward step.").IsFalse();
        watch.Reset();
        AssertThat(watch.Observe(7)).OverrideFailureMessage("After a reset the next read baselines.").IsFalse();
    }

    [TestCase]
    public void GoldenRatioHoldsOnTickOnesHitstunAndLaterTicksAddNone() {
        FighterSimulation sim = new(
            ProjectileBurstSimTests.Authored("leonardo"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 8403, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        sim.Advance(Press(0, GameplayButtons.Special1), Neutral(0));
        int tick = 1;
        var rises = new List<int>();
        int previous = 0;
        for (; tick < 160; tick++) {
            sim.Advance(Neutral(tick), Neutral(tick));
            sim.TryGetFighter(1, out FighterStateComponent target);
            if (target.HitstunFrames > previous) rises.Add(target.HitstunFrames);
            previous = target.HitstunFrames;
        }
        // Tick 1 gives 6 frames; tick 2 none; the zone's expiry pulse then delivers the authored 12-frame
        // launch (the final tick's, which Story's third tick matches). Never a rise from tick 2 or tick 3's pulse.
        AssertThat(rises.Count).OverrideFailureMessage($"rises=[{string.Join(",", rises)}]").IsEqual(2);
        AssertThat(rises[0]).IsLessEqual(LingeringKitRules.GoldenRatioFirstTickHitstunFrames);
        AssertThat(rises[0]).IsGreater(0);
        AssertThat(rises[1]).IsEqual(12);
    }

    private static FighterSimulation Duel(int seed) =>
        new(FighterLoadout.Default(FighterCharacterID.Joan), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    private static void PlaceConstruct(FighterSimulation sim, int type, float dx) {
        sim.TryGetFighter(0, out FighterStateComponent owner);
        ConstructOwnAttack.Spawn(sim, 0, type, new FPVector2(owner.Position.x + FP64.FromDouble(dx), owner.Position.y));
    }

    private static void Step(FighterSimulation sim, int ticks) {
        for (int i = 0; i < ticks; i++) sim.Advance(Neutral(sim.CurrentTick), Neutral(sim.CurrentTick));
    }

    private static int Hp(FighterSimulation sim, int player) {
        sim.TryGetFighter(player, out FighterStateComponent state);
        return state.CurrentHP;
    }

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };

    private static PlayerInputFrame Press(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
