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
/// Echo Step, Medium introduces occasional use at a provisional 25% admission
/// rolled once per opportunity, and Hard scores both tactically including the
/// 30-meter Echo Step spend against Ultimate setup and an unused, eligible Defy.
/// </summary>
/// <remarks>
/// <para>
/// Pure C#: the decision table reads a <see cref="CpuDecisionObservation"/>, so
/// every scenario is a hand-built observation. Both verbs reach the simulation as
/// the <b>shared chords</b> a human presses — BasicAttack pressed while Block is
/// held for the grab, Block plus a Roll edge during the swing's recovery frames
/// for Echo Step — because the contract says to "generate ordinary player inputs
/// through the shared action resolver… never call an ability directly".
/// </para>
/// <para>
/// <b>Wire bits.</b> Package 11 A1c adds dedicated <c>gameplay_grab</c> and
/// <c>gameplay_echo_step</c> actions with their own protocol-v3 bits. The CPU
/// emits the logical verb, so when those land the chord constants below are the
/// only thing that has to move.
/// </para>
/// </remarks>
[TestSuite]
public class CpuVerbPolicyTests {
    private const int Ticks = 900;

    [TestCase]
    public void EasyEmitsNeitherGrabNorEchoStep() {
        // CPU_COMBAT_POLICY.md: "Easy | Disabled. | Disabled."
        var grabber = new FighterCpuController(CpuDifficulty.Easy, 4242);
        AssertThat(CountGrabChords(grabber, GrabOpportunity(), Ticks))
            .OverrideFailureMessage("Easy must never attempt a grab.")
            .IsEqual(0);

        var stepper = new FighterCpuController(CpuDifficulty.Easy, 4242);
        AssertThat(CountEchoStepChords(stepper, WhiffRecovery(), Ticks))
            .OverrideFailureMessage("Easy must never attempt an Echo Step.")
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
            CpuDifficulty.Normal, 77, null, null, VerbTuning(grab: 0, echoStep: 0));
        AssertThat(CountGrabChords(never, opportunity, Ticks)).IsEqual(0);
        AssertThat(never.GrabOpportunityID)
            .OverrideFailureMessage("A continuing opportunity must stay ONE opportunity.")
            .IsEqual(1);

        // A 100% band grabs, but the opportunity identity still advances only
        // when the conditions actually lapse and re-form.
        var always = new FighterCpuController(
            CpuDifficulty.Normal, 77, null, null, VerbTuning(grab: 100, echoStep: 0));
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
    public void HardWeighsTheThirtyMeterSpendAgainstUltimateSetupAndAnUnusedDefy() {
        // "Hard values a viable Ultimate setup and retaining full meter for an
        // unused, eligible Defy… This is a utility tradeoff, not a mandatory
        // meter floor: Hard may spend to avoid a credible punish."
        CpuDecisionObservation reserved = WhiffRecovery();
        reserved.InfluenceRaw = FP64.FromInt(100).RawValue;
        reserved.SelfDefyAvailable = 1;
        // Nothing threatening: the opponent is far away and pressing nothing, so
        // there is no punish worth 30 meter.
        reserved.TargetPositionXRaw = FP64.FromInt(7).RawValue;
        reserved.TargetPressedButtons = 0;
        // Admission is forced so the assertions read the GATE, not the dice —
        // Hard's scored 75% would otherwise make a once-per-execution roll the
        // subject of the test instead of the meter tradeoff.
        CpuBandTuning hard = CpuBandTuning.Hard with { EchoStepAdmissionPercent = 100 };
        var holding = new FighterCpuController(CpuDifficulty.Hard, 1234, null, null, hard);
        AssertThat(CountEchoStepChords(holding, reserved, Ticks))
            .OverrideFailureMessage("Hard must keep full meter behind an unused, eligible Defy.")
            .IsEqual(0);

        // A spent Defy has no reserve value, so the same meter is available.
        CpuDecisionObservation spentDefy = reserved;
        spentDefy.SelfDefyAvailable = 0;
        var spending = new FighterCpuController(CpuDifficulty.Hard, 1234, null, null, hard);
        AssertThat(CountEchoStepChords(spending, spentDefy, Ticks) > 0)
            .OverrideFailureMessage("A spent Defy carries no reserve value.")
            .IsTrue();

        // A credible punish buys the escape even with the Defy still in hand.
        CpuDecisionObservation punished = reserved;
        punished.TargetPositionXRaw = FP64.One.RawValue;
        punished.TargetPressedButtons = (int)GameplayButtons.BasicAttack;
        var escaping = new FighterCpuController(CpuDifficulty.Hard, 1234, null, null, hard);
        AssertThat(CountEchoStepChords(escaping, punished, Ticks) > 0)
            .OverrideFailureMessage("Hard may spend to avoid a credible punish.")
            .IsTrue();
    }

    [TestCase]
    public void TheGrabNeverTargetsAVictimTheSharedRulesWouldWhiffOn() {
        // "Do not intentionally target victims known to be airborne, rolling,
        // invulnerable, in hitstun, daze or shieldstun."
        var always = VerbTuning(grab: 100, echoStep: 0);
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
    public void TheEchoStepGateHonoursMeterCooldownWindUpAndDestinationValidity() {
        var always = VerbTuning(grab: 0, echoStep: 100);
        CpuDecisionObservation baseline = WhiffRecovery();
        AssertThat(CountEchoStepChords(NewNormal(always), baseline, 120) > 0)
            .OverrideFailureMessage("The control case must actually Echo Step.")
            .IsTrue();

        // 29 meter is below the canonical 30-point cost; 30 exactly is enough.
        CpuDecisionObservation justShort = baseline;
        justShort.InfluenceRaw = FP64.FromInt(BasicComboRules.EchoStepMeterCost - 1).RawValue;
        AssertThat(CountEchoStepChords(NewNormal(always), justShort, Ticks)).IsEqual(0);
        CpuDecisionObservation exactlyEnough = baseline;
        exactlyEnough.InfluenceRaw = FP64.FromInt(BasicComboRules.EchoStepMeterCost).RawValue;
        AssertThat(CountEchoStepChords(NewNormal(always), exactlyEnough, Ticks) > 0).IsTrue();

        AssertEchoStepRefused(always, baseline, "cooldown running",
            o => o.Value.SelfEchoStepCooldownFrames = BasicComboRules.EchoStepCooldownFrames);
        AssertEchoStepRefused(always, baseline, "wind-up already armed", o => o.Value.SelfEchoStepWindupFrames = 8);
        AssertEchoStepRefused(always, baseline, "not in attack recovery",
            o => o.Value.SelfAttackPhase = FighterBasicAttackRules.PhaseNone);
        AssertEchoStepRefused(always, baseline, "the swing connected", o => o.Value.SelfAttackMadeContact = 1);
        AssertEchoStepRefused(always, baseline, "no resolvable destination",
            o => o.Value.HasEchoStepDestination = 0);
        AssertEchoStepRefused(always, baseline, "destination past the blast zone",
            o => o.Value.EchoStepDestinationYRaw = FP64.FromInt(-6).RawValue);
        AssertEchoStepRefused(always, baseline, "destination outside the walls",
            o => o.Value.EchoStepDestinationXRaw = FP64.FromInt(12).RawValue);
        // "including pit risk; a teleport toward a worse or unsupported location
        // is not a defensive improvement."
        AssertEchoStepRefused(always, baseline, "destination over a pit", o => {
            o.Value.HasFloorSegments = 1;
            o.Value.HasFloorSupportUnderSelf = 0;
            o.Value.HasFloorEdgeLeft = 1;
            o.Value.NearestFloorEdgeLeftXRaw = FP64.FromDouble(-2.5).RawValue;
            o.Value.HasFloorEdgeRight = 1;
            o.Value.NearestFloorEdgeRightXRaw = FP64.FromDouble(2.5).RawValue;
            o.Value.EchoStepDestinationXRaw = FP64.Zero.RawValue;
        });
        // A Story-shaped observation has no verb layer at all.
        AssertEchoStepRefused(always, baseline, "a Story observation", o => o.Value.HasVerbState = 0);
    }

    [TestCase]
    public void MediumsImmediateUltimatePolicyTakesPriorityOverEchoStep() {
        // "Medium retains its existing immediate-Ultimate policy at the first
        // legal opportunity when meter is full. If both actions are legal in one
        // decision, that policy takes priority."
        CpuDecisionObservation both = WhiffRecovery();
        both.InfluenceRaw = FP64.FromInt(100).RawValue;
        both.TargetPositionXRaw = FP64.One.RawValue;

        // Medium's own Ultimate policy, restored on top of the verb seam: 100%
        // at the first legal opportunity, no setup requirement.
        CpuBandTuning tuning = VerbTuning(grab: 0, echoStep: 100) with {
            UltimatePercent = 100,
            RequiresUltimateSetup = false
        };
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 31337, null, null, tuning);
        PlayerInputFrame previous = default;
        int ultimateEdges = 0;
        int echoChords = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in both, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Ultimate)) ultimateEdges++;
            if (IsEchoStepChord(frame)) echoChords++;
        }
        AssertThat(echoChords)
            .OverrideFailureMessage("Medium must not Echo Step while a legal Ultimate is available.")
            .IsEqual(0);
        AssertThat(ultimateEdges > 0)
            .OverrideFailureMessage("Medium must still fire the Ultimate at its first legal opportunity.")
            .IsTrue();
        AssertThat(CpuBandTuning.Normal.UltimateBeatsEchoStep).IsTrue();
    }

    [TestCase]
    public void TheExclusiveHitstunBlockHoldSurvivesTheNewVerbs() {
        // V7.4's hold is EXCLUSIVE precisely so a scheduled attack edge cannot
        // read as the grab chord and drop the shield. Adding CPU grabs must not
        // regress that: with a 100% defense roll and a 100% grab rate, every
        // hitstun frame holds Block and nothing else.
        CpuBandTuning tuning = VerbTuning(grab: 100, echoStep: 100) with {
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
                .OverrideFailureMessage("An Echo Step chord must never form out of the hitstun escape stance.")
                .IsFalse();
        }
    }

    // === Helpers ===

    private static FighterCpuController NewNormal(CpuBandTuning tuning) =>
        new(CpuDifficulty.Normal, 555, null, null, tuning);

    /// <summary>
    /// Medium's band with the two verb admissions forced, so the gates rather
    /// than the dice are what the assertions read.
    /// </summary>
    private static CpuBandTuning VerbTuning(int grab, int echoStep) =>
        CpuBandTuning.Normal with {
            GrabAdmissionPercent = grab,
            EchoStepAdmissionPercent = echoStep,
            // Keep the Ultimate out of the way: these scenarios are about the
            // verbs, and Medium's Ultimate policy has its own case above.
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

    private static void AssertEchoStepRefused(
        CpuBandTuning tuning,
        CpuDecisionObservation baseline,
        string label,
        System.Action<Mutator> mutate) {
        CpuDecisionObservation observation = baseline;
        var mutator = new Mutator(observation);
        mutate(mutator);
        observation = mutator.Value;
        AssertThat(CountEchoStepChords(NewNormal(tuning), observation, Ticks))
            .OverrideFailureMessage($"The CPU Echo Stepped with {label}.")
            .IsEqual(0);
    }

    /// <summary>Boxes the observation so the scenario mutators can be lambdas.</summary>
    private sealed class Mutator {
        public Mutator(CpuDecisionObservation value) => Value = value;
        public CpuDecisionObservation Value;
    }

    private static bool IsGrabChord(in PlayerInputFrame frame) =>
        frame.IsPressed(GameplayButtons.BasicAttack) && frame.IsHeld(GameplayButtons.Block);

    private static bool IsEchoStepChord(in PlayerInputFrame frame) =>
        frame.IsHeld(GameplayButtons.Block) && frame.IsHeld(GameplayButtons.Roll);

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

    private static int CountEchoStepChords(
        FighterCpuController cpu, CpuDecisionObservation observation, int ticks) {
        PlayerInputFrame previous = default;
        int chords = 0;
        for (uint tick = 0; tick < ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (IsEchoStepChord(in frame)) chords++;
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

    /// <summary>
    /// The CPU in the recovery frames of its own whiffed swing, with the meter,
    /// cooldown and destination Echo Step needs.
    /// </summary>
    private static CpuDecisionObservation WhiffRecovery() {
        CpuDecisionObservation observation = GrabOpportunity();
        observation.TargetBlockStance = 0;
        observation.TargetPositionXRaw = FP64.FromInt(3).RawValue;
        observation.SelfAttackPhase = FighterBasicAttackRules.PhaseRecovery;
        observation.SelfAttackMadeContact = 0;
        observation.SelfEchoStepCooldownFrames = 0;
        observation.SelfEchoStepWindupFrames = 0;
        observation.InfluenceRaw = FP64.FromInt(60).RawValue;
        observation.HasEchoStepDestination = 1;
        observation.EchoStepDestinationXRaw = FP64.FromInt(-2).RawValue;
        observation.EchoStepDestinationYRaw = FP64.Zero.RawValue;
        return observation;
    }
}
