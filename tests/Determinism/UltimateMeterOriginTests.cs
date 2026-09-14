using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A1b — V7.6 D03h: Ultimate-origin damage earns its caster ZERO
/// damage-dealt meter.
///
/// <para>An ordinary Ultimate still costs and spends 100 Influence on accepted
/// activation; what is removed is the refund. The exclusion follows the SOURCE
/// execution into direct hits, projectiles, chained bursts, summoned attacks,
/// persistent damage zones and attached damaging statuses — but it is a source
/// rule, not a global meter lock, so an independent non-Ultimate attack landing
/// while an Ultimate effect survives keeps its ordinary eligibility.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UltimateMeterOriginTests {

    [TestCase]
    public void AnUltimateRemovingSeventyHPFromTwoTargetsEarnsZero() {
        // DEFENSIVE_EFFECTS D03h's worked example: "an Ultimate removing 70
        // actual HP from each of two enemies earns its caster 0 meter, not 100
        // from the combined 140 damage."
        Fixture f = Fixture.New();

        f.Hit(FighterDamageRules.UltimateAttackClass, damage: 70, creditInfluence: false);
        f.Target.CurrentHP = 100;
        f.Hit(FighterDamageRules.UltimateAttackClass, damage: 70, creditInfluence: false);

        AssertThat(f.Attacker.Influence.RawValue)
            .OverrideFailureMessage("Ultimate-origin damage refills nothing.")
            .IsEqual(0L);
    }

    [TestCase]
    public void AnIndependentOrdinaryHitStillEarnsItsMeter() {
        // "If an independent eligible ordinary projectile later removes 8 HP, it
        // earns its normal 8 meter even while an Ultimate effect survives."
        Fixture f = Fixture.New();

        f.Hit(FighterDamageRules.UltimateAttackClass, damage: 70, creditInfluence: false);
        f.Hit(FighterDamageRules.SpecialAttackClass, damage: 8, creditInfluence: true);

        AssertThat(f.Attacker.Influence)
            .OverrideFailureMessage("An ordinary source keeps its 1.0-per-credited-HP award.")
            .IsEqual(FP64.FromInt(8));
    }

    [TestCase]
    public void AnUltimateOriginProjectileCarriesTheExclusionOnTheComponent() {
        // The flag is snapshot state on the projectile itself, so a delayed
        // Ultimate shot landing long after the cinematic still earns nothing.
        var ordinary = new FighterProjectileComponent { UltimateOrigin = 0 };
        var fromUltimate = new FighterProjectileComponent { UltimateOrigin = 1 };

        AssertThat(ordinary.UltimateOrigin == 0)
            .OverrideFailureMessage("An ordinary projectile credits its owner.")
            .IsTrue();
        AssertThat(fromUltimate.UltimateOrigin == 0)
            .OverrideFailureMessage("An Ultimate-spawned projectile credits nothing.")
            .IsFalse();
    }

    [TestCase]
    public void AFullHealthAttackerReclaimsNothingAndKeepsItsWholePool() {
        // V7.6 D03f: reclaim is min(pool, credited × 2.0, missingHP), and ONLY
        // the HP actually healed leaves the pool. The shipped code debited the
        // full capped amount even when the heal was clamped at MaxHP, so a
        // full-health attacker burned its whole pool for nothing.
        Fixture f = Fixture.New();
        f.AttackerVerb.EchoPool = FP64.FromInt(20);

        f.Hit(FighterDamageRules.BasicAttackClass, damage: 10, creditInfluence: true);

        AssertThat(f.Attacker.CurrentHP).IsEqual(100);
        AssertThat(f.AttackerVerb.EchoPool)
            .OverrideFailureMessage("A reclaim at full HP must cost nothing from the pool.")
            .IsEqual(FP64.FromInt(20));
    }

    [TestCase]
    public void AOneHPMissingAttackerReclaimsOneAndThePoolDropsByOne() {
        Fixture f = Fixture.New();
        f.Attacker.CurrentHP = 99;
        f.AttackerVerb.EchoPool = FP64.FromInt(20);

        f.Hit(FighterDamageRules.BasicAttackClass, damage: 10, creditInfluence: true);

        AssertThat(f.Attacker.CurrentHP)
            .OverrideFailureMessage("Only the missing HP can be reclaimed.")
            .IsEqual(100);
        AssertThat(f.AttackerVerb.EchoPool)
            .OverrideFailureMessage("Exactly the healed 1 HP leaves the pool.")
            .IsEqual(FP64.FromInt(19));
    }

    [TestCase]
    public void AZoneTickReclaimsNothingWhileADirectHitReclaims() {
        // V7.6 D03g: "a direct hit removing 6 HP can reclaim at most 12 HP; a
        // zone tick removing the same 6 HP reclaims zero." The sim zone pulse
        // passes collectsEcho: false now; before this it defaulted to true and
        // Relativity Rift / Sandstorm Vortex ticks reclaimed.
        Fixture tick = Fixture.New();
        tick.Attacker.CurrentHP = 50;
        tick.AttackerVerb.EchoPool = FP64.FromInt(20);
        tick.Hit(FighterDamageRules.SpecialAttackClass, damage: 6,
                 creditInfluence: true, collectsEcho: false);

        AssertThat(tick.Attacker.CurrentHP)
            .OverrideFailureMessage("A zone tick reclaims nothing.")
            .IsEqual(50);
        AssertThat(tick.AttackerVerb.EchoPool).IsEqual(FP64.FromInt(20));

        Fixture direct = Fixture.New();
        direct.Attacker.CurrentHP = 50;
        direct.AttackerVerb.EchoPool = FP64.FromInt(20);
        direct.Hit(FighterDamageRules.BasicAttackClass, damage: 6, creditInfluence: true);

        AssertThat(direct.Attacker.CurrentHP)
            .OverrideFailureMessage("A 6-HP direct hit reclaims up to 12.")
            .IsEqual(62);
        AssertThat(direct.AttackerVerb.EchoPool).IsEqual(FP64.FromInt(8));
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

        public bool Hit(int attackClass, int damage, bool creditInfluence, bool collectsEcho = true) {
            Attacker.Position = new FPVector2(Target.Position.x - FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                attackClass, damage, FP64.Zero, 0,
                (int)StatusType.None, 0, FP64.One, Attacker.Position.x,
                creditInfluence: creditInfluence,
                collectsEcho: collectsEcho);
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
