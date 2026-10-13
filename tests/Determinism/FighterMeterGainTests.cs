using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The Fighter half of the Influence meter rulebook, through the real hit
/// chokepoint (<see cref="FighterDamageRules.ApplyFighterHit"/>). Moving the
/// cap and rates into <c>InfluenceMeterRules</c> (read through
/// <c>FighterMeterRules</c>) must leave Fighter meter gain exactly where it
/// was: the attacker earns <c>FromInt(damage)</c>, the victim
/// <c>(FromInt(damage) − echo) / FromInt(4)</c> at hit time, both capped at
/// <c>FromInt(100)</c>, with raw-value equality — and no Story lane (Mozart's
/// build rate) ever reaches the sim.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMeterGainTests {

    [TestCase]
    public void AHitPaysTheAttackerOnePointPerHpAndTheVictimAQuarterOfItsPermanentLoss() {
        foreach (int damage in new[] { 1, 7, 13, 20, 37 }) {
            Fixture f = Fixture.New();
            int hpBefore = f.Target.CurrentHP;

            f.Hit(damage);

            int actual = hpBefore - f.Target.CurrentHP;
            AssertThat(actual).IsEqual(damage);
            AssertThat(f.Attacker.Influence.RawValue)
                .OverrideFailureMessage($"{damage} HP dealt must earn exactly FromInt({damage}).")
                .IsEqual(FP64.FromInt(actual).RawValue);
            FP64 echo = f.Verb.EchoPool;
            AssertThat(echo > FP64.Zero).OverrideFailureMessage("The hit must stash a Rally echo.").IsTrue();
            AssertThat(f.Target.Influence.RawValue)
                .OverrideFailureMessage("The victim earns a quarter of the permanent (non-echo) loss at hit time.")
                .IsEqual(((FP64.FromInt(actual) - echo) / FP64.FromInt(4)).RawValue);
        }
    }

    [TestCase]
    public void BothGainsCapAtTheFullBar() {
        Fixture f = Fixture.New();
        f.Attacker.Influence = FP64.FromInt(95);
        f.Target.Influence = FP64.FromInt(99);

        f.Hit(damage: 30);

        AssertThat(f.Attacker.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);
        AssertThat(f.Target.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);
        AssertThat(FighterMeterRules.Cap.RawValue).IsEqual(FP64.FromInt(InfluenceMeterRules.Cap).RawValue);
    }

    private struct Fixture {
        public FighterStateComponent Attacker;
        public FighterRuntimeComponent AttackerRuntime;
        public FighterVerbComponent AttackerVerb;
        public FighterStateComponent Target;
        public FighterRuntimeComponent Runtime;
        public FighterVerbComponent Verb;
        public FighterDefenseComponent Defense;
        public FighterTuningComponent Tuning;

        public static Fixture New() => new() {
            Attacker = NewFighter(0),
            Target = NewFighter(1),
            Tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 }
        };

        public bool Hit(int damage) {
            Attacker.Position = new FPVector2(Target.Position.x - FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                FighterDamageRules.BasicAttackClass, damage, FP64.Zero, 0,
                (int)StatusType.None, 0, FP64.One, Attacker.Position.x);
        }
    }

    private static FighterStateComponent NewFighter(int playerID) => new() {
        PlayerID = playerID,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = 1,
        Weight = FP64.One
    };
}
