using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A1b — V7.6 D04 protected recovery, deterministic half.
///
/// <para>On a successful Defy the survivor still spends the full 100 meter,
/// survives at 1 HP, generates no echo and consumes its once-per-match use.
/// What is new: it is RELEASED from the triggering hit's forced hitstun,
/// stagger, knockback and capture — it may not be launched or stunned by the
/// very blow it defied — and is invulnerable through the presentation and for
/// exactly 60 active ticks once normal control resumes. Ticks 1–60 are
/// protected; tick 61 is not.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefyProtectedRecoveryTests {

    [TestCase]
    public void ADefiedHitNeitherLaunchesNorStunsTheSurvivor() {
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);

        f.Hit(damage: 40, knockback: FP64.FromInt(8), hitstunFrames: 30);

        AssertThat(f.Target.CurrentHP)
            .OverrideFailureMessage("Defy leaves the survivor at exactly 1 HP.")
            .IsEqual(1);
        AssertThat(f.Verb.DefyHistoryUsed).IsEqual(1);
        AssertThat(f.Target.Influence.RawValue)
            .OverrideFailureMessage("The meter shatters to zero.")
            .IsEqual(0L);
        AssertThat(f.Target.HitstunFrames)
            .OverrideFailureMessage("D04: the survivor is released from the defied hit's hitstun.")
            .IsEqual(0);
        AssertThat(f.Verb.PendingLaunchActive)
            .OverrideFailureMessage("D04: no stashed launch from the blow that was defied.")
            .IsEqual(0);
        AssertThat(f.Target.Velocity.y.RawValue)
            .OverrideFailureMessage("D04: the defied hit contributes no forced motion.")
            .IsEqual(0L);
    }

    [TestCase]
    public void TheDefiedHitImposesNoAttachedStatus() {
        // "The defied hit does not impose a new attached control/status effect
        // that would immediately re-lock the survivor."
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);

        f.Hit(damage: 40, knockback: FP64.FromInt(8), hitstunFrames: 30,
              statusType: (int)StatusType.Root, statusFrames: 120);

        AssertThat(f.Runtime.StatusType).IsEqual((int)StatusType.None);
    }

    [TestCase]
    public void ASameFrameFollowUpHitIsRejectedOutright() {
        // The gate rejects subsequent damaging hits BEFORE Aegis, HP barriers
        // and block, immediately — it covers the presentation, not just the
        // post-control second.
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);
        f.Hit(damage: 40, knockback: FP64.FromInt(8), hitstunFrames: 30);
        // Installed AFTER the proc: the Defy hit itself must reach HP, and the
        // point of this case is that the window rejects the FOLLOW-UP before
        // any of these layers is consulted.
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 10;
        f.Defense.AegisActive = 1;

        bool landed = f.Hit(damage: 10, knockback: FP64.FromInt(4), hitstunFrames: 20);

        AssertThat(landed).IsFalse();
        AssertThat(f.Target.CurrentHP).IsEqual(1);
        AssertThat(f.Defense.AegisActive)
            .OverrideFailureMessage("A rejected contact spends no shield capacity.")
            .IsEqual(1);
        AssertThat(f.Defense.BarrierPoints).IsEqual(10);
        AssertThat(f.Target.BlockCharges)
            .OverrideFailureMessage("A rejected contact spends no block charge.")
            .IsEqual(3);
        AssertThat(f.Target.HitstunFrames).IsEqual(0);
    }

    [TestCase]
    public void TheWindowIsExactlySixtyResumedTicksAndTickSixtyOneLands() {
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);
        f.Hit(damage: 40);

        AssertThat(f.Defense.DefyProtectionAwaitControl)
            .OverrideFailureMessage("The proc opens awaiting control, not counting down.")
            .IsEqual(1);
        AssertThat(f.Defense.DefyProtectionFrames)
            .IsEqual(FighterDefenseRules.DefyProtectionTicks);

        // Sixty resumed control ticks are protected.
        for (int tick = 0; tick < 60; tick++) {
            AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense))
                .OverrideFailureMessage($"Resumed tick {tick + 1} must still be protected.")
                .IsTrue();
            FighterDefenseRules.Tick(ref f.Defense, suspended: false, actionable: true);
        }

        AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense))
            .OverrideFailureMessage("Tick 61 is unprotected.")
            .IsFalse();
        AssertThat(f.Hit(damage: 10))
            .OverrideFailureMessage("A hit on tick 61 lands normally.")
            .IsTrue();
    }

    [TestCase]
    public void TheCountdownPausesWhileSuspendedAndBeforeControlResumes() {
        // "Pause its countdown during global hitstop, menus, Time Freeze and
        // suspended-combat presentations" and "the protected second begins at
        // the first resumed normal-control gameplay tick".
        var defense = new FighterDefenseComponent();
        FighterDefenseRules.BeginDefyProtection(ref defense, playerID: 1);

        for (int tick = 0; tick < 200; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: true, actionable: true);
        }
        AssertThat(defense.DefyProtectionFrames)
            .OverrideFailureMessage("A suspended tick consumes no protection and banks no catch-up.")
            .IsEqual(FighterDefenseRules.DefyProtectionTicks);

        for (int tick = 0; tick < 200; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: false, actionable: false);
        }
        AssertThat(defense.DefyProtectionAwaitControl)
            .OverrideFailureMessage("Without resumed control the window never opens.")
            .IsEqual(1);
        AssertThat(defense.DefyProtectionFrames)
            .IsEqual(FighterDefenseRules.DefyProtectionTicks);
    }

    [TestCase]
    public void ADamagingHazardTickIsRejectedByTheWindow() {
        // The gate "covers all attackers, ongoing damaging-status ticks and
        // damaging stage hazards", not only the triggering attack.
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);
        f.Hit(damage: 40);

        bool hazardLanded = FighterDamageRules.ApplyEnvironmentHit(
            ref f.Target, ref f.Runtime, ref f.Verb, ref f.Defense, in f.Tuning,
            damage: 6, knockback: FP64.FromInt(2), hitstunFrames: 10,
            hitOriginX: f.Target.Position.x - FP64.One);

        AssertThat(hazardLanded).IsFalse();
        AssertThat(f.Target.CurrentHP).IsEqual(1);

        FighterDamageRules.ApplyUnattributedDamage(
            ref f.Target, ref f.Runtime, ref f.Verb, ref f.Defense, in f.Tuning, damage: 4);
        AssertThat(f.Target.CurrentHP)
            .OverrideFailureMessage("A damaging DoT tick is discarded, not banked.")
            .IsEqual(1);
    }

    [TestCase]
    public void TheProcIdentityIsDeterministicAcrossResimulation() {
        // The pending-presentation identity must be deduplicated across a
        // rollback: the same proc replayed must produce the same value.
        var first = new FighterDefenseComponent();
        var second = new FighterDefenseComponent();
        FighterDefenseRules.BeginDefyProtection(ref first, playerID: 1);
        FighterDefenseRules.BeginDefyProtection(ref second, playerID: 1);

        AssertThat(first.DefyProcIdentity).IsEqual(second.DefyProcIdentity);
        AssertThat(first.DefyProcIdentity)
            .OverrideFailureMessage("An installed proc carries a non-zero identity.")
            .IsGreater(0);
    }

    [TestCase]
    public void AKnockoutClearsTheWindowTheBubbleAndTheBarrierButNotTheSpentFlag() {
        // "Death/stock loss, Death Rewind cleanup, scene reconstruction and F22
        // Sudden Death clear the temporary window." Defy itself is once per
        // match, not once per stock, so DefyHistoryUsed survives.
        var fighter = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 0,
            BlockCharges = 0, Weight = FP64.One
        };
        var runtime = new FighterRuntimeComponent { UsesStocks = 1, AegisHits = 1 };
        var verb = new FighterVerbComponent { DefyHistoryUsed = 1 };
        var defense = new FighterDefenseComponent {
            AegisActive = 1, BarrierPoints = 10, BarrierCapacity = 10, BarrierRemainingFrames = 200
        };
        FighterDefenseRules.BeginDefyProtection(ref defense, playerID: 1);
        var tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 };

        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, ref verb, ref defense, in tuning);

        AssertThat(FighterDefenseRules.IsDefyProtected(in defense)).IsFalse();
        AssertThat(defense.AegisActive).IsEqual(0);
        AssertThat(runtime.AegisHits).IsEqual(0);
        AssertThat(defense.BarrierPoints).IsEqual(0);
        AssertThat(verb.DefyHistoryUsed)
            .OverrideFailureMessage("Defy is once per MATCH: a stock loss does not hand it back.")
            .IsEqual(1);
    }

    [TestCase]
    public void TheGrantedShieldExpiresAtExactlyFourHundredAndEightyActiveTicks() {
        // D02c, sim mirror: eight seconds of LIVE gameplay, and a suspended tick
        // consumes no lifetime.
        var defense = new FighterDefenseComponent {
            BarrierPoints = 10,
            BarrierCapacity = 10,
            BarrierEffectID = 1,
            BarrierRemainingFrames = FighterDefenseRules.GrantedShieldLifetimeTicks
        };

        for (int tick = 0; tick < 100; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: true, actionable: true);
        }
        AssertThat(defense.BarrierRemainingFrames)
            .OverrideFailureMessage("A frozen world consumes no shield lifetime.")
            .IsEqual(FighterDefenseRules.GrantedShieldLifetimeTicks);

        for (int tick = 0; tick < FighterDefenseRules.GrantedShieldLifetimeTicks - 1; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: false, actionable: true);
        }
        AssertThat(defense.BarrierPoints)
            .OverrideFailureMessage("At tick 479 the shield is still live.")
            .IsEqual(10);

        FighterDefenseRules.Tick(ref defense, suspended: false, actionable: true);
        AssertThat(defense.BarrierPoints)
            .OverrideFailureMessage("Tick 480 expires it, discarding the remaining absorption.")
            .IsEqual(0);
        AssertThat(defense.BarrierEffectID).IsEqual(0);
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

        public bool Hit(
            int damage,
            FP64 knockback = default,
            int hitstunFrames = 0,
            int statusType = (int)StatusType.None,
            int statusFrames = 0) {
            Attacker.Position = new FPVector2(Target.Position.x - FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                FighterDamageRules.BasicAttackClass, damage, knockback, hitstunFrames,
                statusType, statusFrames, FP64.One, Attacker.Position.x);
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
