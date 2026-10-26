using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static FTT.Tests.Determinism.KnockbackFeelTestKit;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// FU-KNOCK R2 (feel and UI update 2026-10-25; ledger
/// <c>USER-FIGHTER-WALL-BOUNCE</c>): a launched fighter driven into a side wall
/// at speed rebounds at half speed, once per launch, with a short freeze of the
/// bouncing fighter alone. No damage, hitstun or meter change. Component 323's
/// <c>ActionFlags</c> bit 4 is the once-per-launch flag and bits 8-11 the
/// wrapping bounce counter the driver edge-detects. Pure C#: no engine.
/// </summary>
[TestSuite]
public class FighterWallBounceTests {

    private static readonly FP64 Left = FP64.FromInt(-10);
    private static readonly FP64 Right = FP64.FromInt(10);

    private static FighterStateComponent Flying(double vx) {
        var f = new FighterStateComponent();
        f.Velocity = new FPVector2(FP64.FromDouble(vx), FP64.Zero);
        return f;
    }

    private static FighterVerbComponent Launched() => new() { Tumble = FighterVerbRules.TumbleInHitstun };

    [TestCase]
    public void ALaunchedFighterIntoTheRightWallReversesAtHalfSpeedAndFreezesSixTicks() {
        FighterStateComponent f = Flying(10);
        FighterVerbComponent verb = Launched();
        FighterActionStateComponent action = default;
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(10.2), Left, Right)).IsTrue();
        AssertThat(D(f.Velocity.x)).IsEqual(-5.0);
        AssertThat(verb.HitstopFrames).IsEqual(6);
        AssertThat(FighterKnockbackFeelRules.HasBounced(in action)).IsTrue();
        AssertThat(FighterKnockbackFeelRules.BounceCount(in action)).IsEqual(1);
        AssertThat(f.HitstunFrames).IsEqual(0); // untouched by the rule
        AssertThat(f.CurrentHP).IsEqual(0);     // no damage path
    }

    [TestCase]
    public void TheLeftWallBouncesToo() {
        FighterStateComponent f = Flying(-8);
        FighterVerbComponent verb = Launched();
        FighterActionStateComponent action = default;
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(-10.1), Left, Right)).IsTrue();
        AssertThat(D(f.Velocity.x)).IsEqual(4.0);
    }

    [TestCase]
    public void BelowTheThresholdOrNotPressedIntoTheWallOrNotLaunchedNothingBounces() {
        FighterVerbComponent launched = Launched();
        FighterActionStateComponent action = default;

        FighterStateComponent slow = Flying(4.9);
        long slowRaw = slow.Velocity.x.RawValue;
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref slow, ref launched, ref action, FP64.FromDouble(10.1), Left, Right))
            .OverrideFailureMessage("below the 5 u/s threshold it stays pressed").IsFalse();
        AssertThat(slow.Velocity.x.RawValue).IsEqual(slowRaw);

        FighterStateComponent away = Flying(-9);
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref away, ref launched, ref action, FP64.FromDouble(10.1), Left, Right))
            .OverrideFailureMessage("moving away from the wall it crossed").IsFalse();

        FighterStateComponent inside = Flying(9);
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref inside, ref launched, ref action, FP64.FromDouble(9.9), Left, Right))
            .OverrideFailureMessage("not clamped this tick").IsFalse();

        FighterVerbComponent grounded = default; // Tumble 0: a grounded slide is not a launch
        FighterStateComponent slide = Flying(9);
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref slide, ref grounded, ref action, FP64.FromDouble(10.4), Left, Right))
            .OverrideFailureMessage("a grounded slide never bounces").IsFalse();
        AssertThat(FighterKnockbackFeelRules.BounceCount(in action)).IsEqual(0);
    }

    [TestCase]
    public void ItBouncesOncePerLaunchAndANewLaunchRearmsIt() {
        FighterStateComponent f = Flying(10);
        FighterVerbComponent verb = Launched();
        FighterActionStateComponent action = default;
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(10.2), Left, Right)).IsTrue();
        f.Velocity.x = FP64.FromInt(9); // driven back into the same wall
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(10.2), Left, Right))
            .OverrideFailureMessage("one bounce per launch").IsFalse();
        FighterKnockbackFeelRules.RearmBounce(ref action);
        AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(10.2), Left, Right)).IsTrue();
        AssertThat(FighterKnockbackFeelRules.BounceCount(in action)).IsEqual(2);
    }

    [TestCase]
    public void TheCounterWrapsAtSixteenAndTheBounceNeverShortensAnExistingFreeze() {
        FighterStateComponent f = Flying(10);
        FighterVerbComponent verb = Launched();
        FighterActionStateComponent action = default;
        for (int i = 1; i <= 17; i++) {
            f.Velocity.x = FP64.FromInt(10);
            FighterKnockbackFeelRules.RearmBounce(ref action);
            AssertThat(FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(10.2), Left, Right)).IsTrue();
            AssertThat(FighterKnockbackFeelRules.BounceCount(in action)).IsEqual(i & 0xF);
        }
        verb.HitstopFrames = 9;
        f.Velocity.x = FP64.FromInt(10);
        FighterKnockbackFeelRules.RearmBounce(ref action);
        FighterKnockbackFeelRules.TryWallBounce(ref f, ref verb, ref action, FP64.FromDouble(10.2), Left, Right);
        AssertThat(verb.HitstopFrames).IsEqual(9);
        // The counter lives apart from the other 323 flag bits.
        AssertThat(action.ActionFlags & FighterActionStateComponent.ActionFlagRespawnProtection).IsEqual(0);
    }

    [TestCase]
    public void ALaunchDrivenIntoTheStageWallBouncesInTheSimulationWithoutTouchingHitstunOrHP() {
        List<Sample> samples = RunLaunchIntoRightWall(seed: 5101, out int bounceIndex);
        AssertThat(bounceIndex).OverrideFailureMessage("the launch must reach the wall and bounce").IsGreater(0);
        Sample before = samples[bounceIndex - 1];
        Sample at = samples[bounceIndex];

        AssertThat(at.Count).IsEqual(1);
        AssertThat(at.Flag).IsTrue();
        AssertThat(D(at.X)).IsEqual(10.0); // clamped at the wall
        AssertThat(at.Vx).IsLess(0.0);
        double ratio = -at.Vx / System.Math.Abs(before.Vx);
        AssertThat(ratio >= 0.4 && ratio <= 0.6)
            .OverrideFailureMessage($"rebound should keep about half the speed, kept {ratio:0.00}").IsTrue();
        AssertThat(at.VictimHitstop).IsEqual(6);
        AssertThat(at.AttackerHitstop).IsEqual(0);
        AssertThat(at.HP).OverrideFailureMessage("no damage from the wall").IsEqual(before.HP);
        AssertThat(at.Hitstun).OverrideFailureMessage("the bounce adds no hitstun").IsEqual(before.Hitstun - 1);

        // Frozen for the six ticks, then it travels back from the wall.
        for (int i = 1; i <= 5; i++) AssertThat(samples[bounceIndex + i].X.RawValue).IsEqual(at.X.RawValue);
        AssertThat(D(samples[bounceIndex + 12].X)).IsLess(10.0);
        // One bounce per launch: the counter holds until the tumble ends.
        for (int i = bounceIndex; i < samples.Count && samples[i].Tumble != 0; i++) {
            AssertThat(samples[i].Count).IsEqual(1);
        }
        Sample last = samples[samples.Count - 1];
        AssertThat(last.Tumble).OverrideFailureMessage("setup: the run must outlast the tumble").IsEqual(0);
        AssertThat(last.Flag).OverrideFailureMessage("the flag clears when the launch is over").IsFalse();
    }

    [TestCase]
    public void ADepthSevenRollbackReplaysTheBounceIdentically() {
        List<Sample> probe = RunLaunchIntoRightWall(seed: 5102, out int bounceIndex);
        AssertThat(bounceIndex).IsGreater(8);
        int predictFrom = bounceIndex - 3; // sample index == tick
        int predictTo = predictFrom + 7;

        FighterSimulation authoritative = NewSim(5102);
        FighterSimulation predicted = NewSim(5102);
        int total = bounceIndex + 40;
        for (int tick = 0; tick < total; tick++) {
            PlayerInputFrame one = Frame(tick, tick == 0 ? GameplayButtons.Special1 : GameplayButtons.None);
            // The remote player holds the stick toward the wall for the window; the prediction repeats idle.
            PlayerInputFrame two = Frame(tick, GameplayButtons.None, tick >= predictFrom && tick < predictTo ? (sbyte)127 : (sbyte)0);
            authoritative.Advance(one, two);
            if (tick >= predictFrom && tick < predictTo) predicted.AdvanceWithPredictedPlayerTwo(one);
            else predicted.Advance(one, two);
            if (tick != predictTo - 1) continue;
            for (int c = predictFrom; c < predictTo; c++) {
                AssertThat(predicted.CorrectPlayerTwoInput(c, Frame(c, GameplayButtons.None, 127))).IsTrue();
            }
            AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        }
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        AssertThat(authoritative.TryGetFighterActionState(1, out FighterActionStateComponent action)).IsTrue();
        AssertThat(FighterKnockbackFeelRules.BounceCount(in action)).IsEqual(1);
    }

    // === Harness ===

    private readonly struct Sample {
        public Sample(FighterSimulation sim) {
            sim.TryGetFighter(1, out FighterStateComponent f);
            sim.TryGetFighterVerb(1, out FighterVerbComponent v);
            sim.TryGetFighterVerb(0, out FighterVerbComponent av);
            sim.TryGetFighterActionState(1, out FighterActionStateComponent a);
            X = f.Position.x;
            Vx = D(f.Velocity.x);
            HP = f.CurrentHP;
            Hitstun = f.HitstunFrames;
            VictimHitstop = v.HitstopFrames;
            AttackerHitstop = av.HitstopFrames;
            Tumble = v.Tumble;
            Count = FighterKnockbackFeelRules.BounceCount(in a);
            Flag = FighterKnockbackFeelRules.HasBounced(in a);
        }
        public readonly FP64 X;
        public readonly double Vx;
        public readonly int HP, Hitstun, VictimHitstop, AttackerHitstop, Tumble, Count;
        public readonly bool Flag;
    }

    private static FighterSimulation NewSim(int seed) => new(
        Launcher(), FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    /// <summary>Tick 0 launches the victim toward the right wall; samples follow every tick until well past the landing.</summary>
    private static List<Sample> RunLaunchIntoRightWall(int seed, out int bounceIndex) {
        FighterSimulation sim = NewSim(seed);
        var samples = new List<Sample>();
        bounceIndex = -1;
        for (int tick = 0; tick < 400; tick++) {
            sim.Advance(Frame(tick, tick == 0 ? GameplayButtons.Special1 : GameplayButtons.None), Frame(tick));
            var s = new Sample(sim);
            samples.Add(s);
            if (bounceIndex < 0 && s.Count == 1) bounceIndex = tick;
            if (bounceIndex >= 0 && tick > bounceIndex + 20 && s.Tumble == 0 && !s.Flag) break;
        }
        return samples;
    }

    /// <summary>A melee-Special launcher: a long hitstun (60) and a strong shove, so the victim reaches the wall inside the stun.</summary>
    private static FighterLoadout Launcher() => new(
        (int)FighterCharacterID.Einstein,
        100, 3, 2,
        10, 14, 12, 20,
        600, 600,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        FP64.FromInt(3),
        FP64.FromInt(24),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = 60,
                BlockClass = (int)BlockClass.Special,
                Launches = true,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        });
}
