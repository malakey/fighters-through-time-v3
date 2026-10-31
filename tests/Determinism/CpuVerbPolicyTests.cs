using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A9b — the F19 CPU verb policy from
/// <c>docs/design-contracts/CPU_COMBAT_POLICY.md</c>: Easy uses neither grab nor
/// grab, Medium introduces occasional use at a provisional 25% admission
/// rolled once per opportunity, and Hard scores it tactically. (Echo Step, the
/// second F19 verb, was removed on 2026-10-25.)
/// </summary>
/// <remarks>
/// <para>
/// Pure C#: the decision table reads a <see cref="CpuDecisionObservation"/>, so
/// every scenario is a hand-built observation. The grab reaches the simulation
/// as the <b>shared chord</b> a human presses — BasicAttack pressed while Block
/// is held — because the contract says to "generate ordinary player inputs
/// through the shared action resolver… never call an ability directly".
/// </para>
/// <para>
/// <b>Wire bits.</b> Package 11 A1c added a dedicated <c>gameplay_grab</c>
/// action with its own protocol bit. The CPU emits the chord, so the chord
/// constants below are the only thing that would have to move.
/// </para>
/// </remarks>
[TestSuite]
public class CpuVerbPolicyTests {
    private const int Ticks = 900;

    [TestCase]
    public void EasyEmitsNoGrab() {
        // CPU_COMBAT_POLICY.md: "Easy | Disabled."
        var grabber = new FighterCpuController(CpuDifficulty.Easy, 4242);
        AssertThat(CountGrabChords(grabber, GrabOpportunity(), Ticks))
            .OverrideFailureMessage("Easy must never attempt a grab.")
            .IsEqual(0);
    }

    [TestCase]
    public void MediumRollsGrabAdmissionOncePerOpportunityAndAFailedRollNeverReopensIt() {
        // "Roll admission once per opportunity using the seeded match PRNG, never
        // once per three-frame evaluation… Do not reopen the same continuing
        // opportunity just because the admission roll failed."
        CpuDecisionObservation opportunity = GrabOpportunity();

        // A 0% band proves the failed roll never reopens: the conditions hold for
        // 900 straight ticks and not one chord escapes.
        var never = new FighterCpuController(
            CpuDifficulty.Normal, 77, null, null, VerbTuning(grab: 0));
        AssertThat(CountGrabChords(never, opportunity, Ticks)).IsEqual(0);
        AssertThat(never.GrabOpportunityID)
            .OverrideFailureMessage("A continuing opportunity must stay ONE opportunity.")
            .IsEqual(1);

        // A 100% band grabs, but the opportunity identity still advances only
        // when the conditions actually lapse and re-form.
        var always = new FighterCpuController(
            CpuDifficulty.Normal, 77, null, null, VerbTuning(grab: 100));
        AssertThat(CountGrabChords(always, opportunity, 60) > 0).IsTrue();
        int afterFirst = always.GrabOpportunityID;
        CpuDecisionObservation lapsed = opportunity;
        lapsed.TargetBlockStance = 0;
        RunFrames(always, lapsed, 60);
        AssertThat(always.GrabOpportunityID).IsEqual(afterFirst);
        RunFrames(always, opportunity, 30);
        AssertThat(always.GrabOpportunityID)
            .OverrideFailureMessage("A lapsed-and-reformed stance is a NEW opportunity.")
            .IsEqual(afterFirst + 1);

        // And the shipped Medium rate is the design's provisional 25%.
        AssertThat(CpuBandTuning.Normal.GrabAdmissionPercent).IsEqual(25);
    }

    [TestCase]
    public void TheGrabNeverTargetsAVictimTheSharedRulesWouldWhiffOn() {
        // "Do not intentionally target victims known to be airborne, rolling,
        // invulnerable, in hitstun, daze or shieldstun."
        var always = VerbTuning(grab: 100);
        CpuDecisionObservation baseline = GrabOpportunity();
        AssertThat(CountGrabChords(NewNormal(always), baseline, 120) > 0)
            .OverrideFailureMessage("The control case must actually grab.")
            .IsTrue();

        AssertIllegalVictim(always, baseline, "airborne", o => o.Value.TargetIsGrounded = 0);
        AssertIllegalVictim(always, baseline, "rolling", o => o.Value.TargetRolling = 1);
        AssertIllegalVictim(always, baseline, "invulnerable", o => o.Value.TargetInvulnerabilityFrames = 30);
        AssertIllegalVictim(always, baseline, "in hitstun", o => o.Value.TargetHitstunFrames = 20);
        AssertIllegalVictim(always, baseline, "dazed", o => o.Value.TargetDazeFrames = 60);
        AssertIllegalVictim(always, baseline, "shieldstunned", o => o.Value.TargetShieldStunFrames = 8);
        AssertIllegalVictim(always, baseline, "throw-immune", o => o.Value.TargetThrowImmune = 1);
        AssertIllegalVictim(always, baseline, "not blocking", o => o.Value.TargetBlockStance = 0);
        AssertIllegalVictim(always, baseline, "out of reach",
            o => o.Value.TargetPositionXRaw = FP64.FromInt(3).RawValue);
        AssertIllegalVictim(always, baseline, "behind the grabber",
            o => o.Value.TargetPositionXRaw = FP64.FromDouble(-0.5).RawValue);
        // Self-side legality mirrors FighterGrabRules.CanStartGrab.
        AssertIllegalVictim(always, baseline, "grabber airborne", o => o.Value.IsGrounded = 0);
        AssertIllegalVictim(always, baseline, "grabber mid-swing",
            o => o.Value.SelfAttackPhase = FighterBasicAttackRules.PhaseActive);
        AssertIllegalVictim(always, baseline, "grabber shieldstunned", o => o.Value.SelfShieldStunFrames = 8);
    }

    [TestCase]
    public void TheExclusiveHitstunBlockHoldSurvivesTheNewVerbs() {
        // V7.4's hold is EXCLUSIVE precisely so a scheduled attack edge cannot
        // read as the grab chord and drop the shield. Adding CPU grabs must not
        // regress that: with a 100% defense roll and a 100% grab rate, every
        // hitstun frame holds Block and nothing else.
        CpuBandTuning tuning = VerbTuning(grab: 100) with {
            HitstunDefensePercent = 100,
            DiPercent = 100
        };
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 909, null, null, tuning);
        CpuDecisionObservation stunned = GrabOpportunity();
        stunned.HitstunFrames = 40;

        PlayerInputFrame previous = default;
        for (uint tick = 0; tick < 240; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in stunned, in previous);
            previous = frame;
            AssertThat(frame.IsHeld(GameplayButtons.Block))
                .OverrideFailureMessage("The committed escape stance must hold Block for the whole hitstun.")
                .IsTrue();
            AssertThat(frame.IsHeld(GameplayButtons.BasicAttack))
                .OverrideFailureMessage("A grab chord must never form out of the hitstun escape stance.")
                .IsFalse();
            AssertThat(frame.IsHeld(GameplayButtons.Roll))
                .OverrideFailureMessage("A Roll must never form out of the hitstun escape stance.")
                .IsFalse();
        }
    }

    [TestCase]
    public void ASpecialNeedsAPipAndAClearReuseDelayWhenTheGaugeIsKnown() {
        // Meter and Defy update (G4.4): the four cooldown tests are one predicate, a whole pip
        // (7200 units) AND the slot's reuse delay run out by delivery. A real gauge of 7199 starves
        // the press that 7200 pays for; a 30-frame reuse delay (beyond Medium's 15-frame reaction
        // floor) holds it back whatever the gauge.
        CpuBandTuning tuning = SpecialTuning();
        int cost = BasicComboRules.MomentumSpecialCostUnits;
        AssertThat(CountSpecialOnePresses(tuning, SpecialRange(known: true, units: cost - 1, reuse: 0))).IsEqual(0);
        AssertThat(CountSpecialOnePresses(tuning, SpecialRange(known: true, units: cost, reuse: 0)) > 0)
            .OverrideFailureMessage("A whole pip with a clear reuse delay must press Special 1.")
            .IsTrue();
        AssertThat(CountSpecialOnePresses(tuning, SpecialRange(known: true, units: BasicComboRules.MomentumMaxUnits, reuse: 30)))
            .OverrideFailureMessage("A reuse delay still running at delivery holds the press back.")
            .IsEqual(0);
        AssertThat(CountSpecialOnePresses(tuning, SpecialRange(known: true, units: BasicComboRules.MomentumMaxUnits, reuse: 10)) > 0)
            .OverrideFailureMessage("A delay that will have run out by delivery does not.")
            .IsTrue();
    }

    [TestCase]
    public void AHandBuiltObservationIgnoresTheGaugeAndOnlyTheReuseDelayGates() {
        // P24: MomentumKnown defaults to 0, so a gauge of 0 units cannot be told from "not filled in".
        // Every pre-update observation therefore behaves as it did.
        CpuDecisionObservation handBuilt = SpecialRange(known: false, units: 0, reuse: 0);
        AssertThat(handBuilt.MomentumKnown).IsEqual(0);
        AssertThat(CountSpecialOnePresses(SpecialTuning(), handBuilt) > 0)
            .OverrideFailureMessage("An unknown gauge must not starve the Special.")
            .IsTrue();
        AssertThat(CountSpecialOnePresses(SpecialTuning(), SpecialRange(known: false, units: 0, reuse: 30)))
            .OverrideFailureMessage("The reuse delay still gates a hand-built observation.")
            .IsEqual(0);
    }

    // === Helpers ===

    private static CpuBandTuning SpecialTuning() => CpuBandTuning.Normal with {
        SpecialOneClosePercent = 100,
        SpecialTwoPercent = 0,
        UltimatePercent = 0,
        GrabAdmissionPercent = 0,
        EvasiveRollPercent = 0,
        BlockPercent = 0
    };

    private static CpuDecisionObservation SpecialRange(bool known, int units, int reuse) => new() {
        Stocks = 3,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        IsGrounded = 1,
        MomentumKnown = known ? 1 : 0,
        SelfMomentumUnits = units,
        SpecialOneReuseFrames = reuse,
        SpecialTwoReuseFrames = reuse,
        TargetPositionXRaw = FP64.FromDouble(1.0).RawValue,
        TargetCurrentHP = 100,
        TargetMaxHP = 100
    };

    private static int CountSpecialOnePresses(CpuBandTuning tuning, CpuDecisionObservation observation) {
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 612, null, null, tuning);
        PlayerInputFrame previous = default;
        int presses = 0;
        for (uint tick = 0; tick < 240; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Special1)) presses++;
        }
        return presses;
    }

    private static FighterCpuController NewNormal(CpuBandTuning tuning) =>
        new(CpuDifficulty.Normal, 555, null, null, tuning);

    /// <summary>
    /// Medium's band with the grab admission forced, so the gates rather than
    /// the dice are what the assertions read.
    /// </summary>
    private static CpuBandTuning VerbTuning(int grab) =>
        CpuBandTuning.Normal with {
            GrabAdmissionPercent = grab,
            // Keep the Ultimate out of the way: these scenarios are about the
            // grab.
            UltimatePercent = 0
        };

    private static void AssertIllegalVictim(
        CpuBandTuning tuning,
        CpuDecisionObservation baseline,
        string label,
        System.Action<Mutator> mutate) {
        CpuDecisionObservation observation = baseline;
        var mutator = new Mutator(observation);
        mutate(mutator);
        observation = mutator.Value;
        AssertThat(CountGrabChords(NewNormal(tuning), observation, Ticks))
            .OverrideFailureMessage($"The CPU grabbed a victim that is {label}.")
            .IsEqual(0);
    }

    /// <summary>Boxes the observation so the scenario mutators can be lambdas.</summary>
    private sealed class Mutator {
        public Mutator(CpuDecisionObservation value) => Value = value;
        public CpuDecisionObservation Value;
    }

    private static bool IsGrabChord(in PlayerInputFrame frame) =>
        frame.IsPressed(GameplayButtons.BasicAttack) && frame.IsHeld(GameplayButtons.Block);

    private static int CountGrabChords(
        FighterCpuController cpu, CpuDecisionObservation observation, int ticks) {
        PlayerInputFrame previous = default;
        int chords = 0;
        for (uint tick = 0; tick < ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (IsGrabChord(in frame)) chords++;
        }
        return chords;
    }

    private static void RunFrames(
        FighterCpuController cpu, CpuDecisionObservation observation, int ticks) {
        PlayerInputFrame previous = default;
        for (uint tick = 0; tick < ticks; tick++) {
            previous = cpu.Sample(tick, in observation, in previous);
        }
    }

    /// <summary>
    /// A blocking, grounded opponent half a unit in front of a grounded, idle CPU
    /// — inside the canonical 0.8-unit grab reach.
    /// </summary>
    private static CpuDecisionObservation GrabOpportunity() => new() {
        SelfPositionXRaw = FP64.Zero.RawValue,
        TargetPositionXRaw = FP64.FromDouble(0.5).RawValue,
        Stocks = 3,
        IsGrounded = 1,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        TargetCurrentHP = 100,
        TargetMaxHP = 100,
        HasVerbState = 1,
        SelfFacingRight = 1,
        HasTargetVerbState = 1,
        TargetIsGrounded = 1,
        TargetBlockStance = 1,
        HasStageBounds = 1,
        LeftWallRaw = FP64.FromInt(-9).RawValue,
        RightWallRaw = FP64.FromInt(9).RawValue,
        CeilingRaw = FP64.FromInt(9).RawValue,
        BottomBlastZoneRaw = FP64.FromInt(-5).RawValue
    };
}
