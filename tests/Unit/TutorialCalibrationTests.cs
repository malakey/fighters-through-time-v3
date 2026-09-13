using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit M-3: the tutorial calibration step machine. The tutorial previously
/// taught BasicHits → any-cooldown → rewind, never onboarding Block or the
/// Ultimate anywhere in the campaign, and any single cooldown event (movement
/// ability included) passed the specials step. These tests pin the designed
/// order — attacks, block, specials, forced-meter ultimate, scripted rewind —
/// and the phase gating that stops an event from the wrong lesson from
/// skipping a step. Pure C#: the machine is engine-free by design.
/// </summary>
[TestSuite]
public class TutorialCalibrationTests {

    private static TutorialCalibrationScript AdvanceToStep(TutorialCalibrationStep target) {
        var script = new TutorialCalibrationScript();
        if (target == TutorialCalibrationStep.BasicHits) return script;
        for (int hit = 0; hit < TutorialCalibrationScript.RequiredBasicHits; hit++) script.RegisterBasicHit();
        if (target == TutorialCalibrationStep.RallyReclaim) return script;
        script.RegisterRallyReclaimHit();
        if (target == TutorialCalibrationStep.Block) return script;
        for (int hit = 0; hit < TutorialCalibrationScript.RequiredBlockedHits; hit++) script.RegisterBlockedHit();
        if (target == TutorialCalibrationStep.UseSpecial) return script;
        script.RegisterSpecialUsed(AbilitySlot.Special1);
        if (target == TutorialCalibrationStep.UseUltimate) return script;
        script.RegisterUltimateUsed();
        if (target == TutorialCalibrationStep.UseRewind) return script;
        script.RegisterRewindComplete();
        if (target == TutorialCalibrationStep.UseTimeFreeze) return script;
        script.RegisterTimeFreezeComplete();
        return script;
    }

    [TestCase]
    public void TheDesignedStepOrderRunsAttackRallyBlockSpecialUltimateRewindTimeFreeze() {
        var script = new TutorialCalibrationScript();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.BasicHits);

        AssertThat(script.RegisterBasicHit()).IsFalse();
        AssertThat(script.RegisterBasicHit()).IsFalse();
        AssertThat(script.RegisterBasicHit()).IsTrue();
        // V7.1: the dummy's scripted counter-hit teaches Rally, closed by
        // striking back to reclaim the echo.
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.RallyReclaim);
        AssertThat(script.RegisterRallyReclaimHit()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Block);

        AssertThat(script.RegisterBlockedHit()).IsFalse();
        AssertThat(script.RegisterBlockedHit()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseSpecial);

        AssertThat(script.RegisterSpecialUsed(AbilitySlot.Special2)).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseUltimate);

        AssertThat(script.RegisterUltimateUsed()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);

        // V7.6: the scripted death-rewind demo hands off to the Time Freeze
        // escape drill, which is the last calibration beat.
        AssertThat(script.RegisterRewindComplete()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseTimeFreeze);
        AssertThat(script.RegisterTimeFreezeComplete()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Done);
    }

    [TestCase]
    public void BlockStepAdvancesOnlyAfterTheRequiredAbsorbedHits() {
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.Block);

        AssertThat(script.RegisterBlockedHit()).IsFalse();
        AssertThat(script.HitsBlocked).IsEqual(1);
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Block);

        AssertThat(script.RegisterBlockedHit()).IsTrue();
        AssertThat(script.HitsBlocked).IsEqual(TutorialCalibrationScript.RequiredBlockedHits);
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseSpecial);
    }

    [TestCase]
    public void AGuardBreakCompletesTheBlockLessonImmediately() {
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.Block);

        // The full depletion arc (all charges spent, guard broken) is the whole
        // lesson in one stroke; the step must not wait for more absorbs.
        AssertThat(script.RegisterGuardBreak()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseSpecial);
    }

    [TestCase]
    public void OnlyTheTwoSpecialSlotsSatisfyTheSpecialsStep() {
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.UseSpecial);

        // The old controller advanced on ANY cooldown event (audit M-3); the
        // movement ability and the ultimate chord must not skip the lesson.
        AssertThat(script.RegisterSpecialUsed(AbilitySlot.MovementAbility)).IsFalse();
        AssertThat(script.RegisterSpecialUsed(AbilitySlot.Ultimate)).IsFalse();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseSpecial);

        AssertThat(script.RegisterSpecialUsed(AbilitySlot.Special1)).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseUltimate);
    }

    [TestCase]
    public void UltimateStepAdvancesOnlyOnAnUltimateActivation() {
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.UseUltimate);

        // Nothing else moves the step forward.
        AssertThat(script.RegisterBasicHit()).IsFalse();
        AssertThat(script.RegisterBlockedHit()).IsFalse();
        AssertThat(script.RegisterSpecialUsed(AbilitySlot.Special1)).IsFalse();
        AssertThat(script.RegisterRewindComplete()).IsFalse();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseUltimate);

        AssertThat(script.RegisterUltimateUsed()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);
    }

    [TestCase]
    public void EventsFromTheWrongStepNeverAdvanceAnything() {
        var script = new TutorialCalibrationScript();

        // Blocking, specials, the ultimate and the rewind during the basic-hit
        // lesson are all inert.
        AssertThat(script.RegisterRallyReclaimHit()).IsFalse();
        AssertThat(script.RegisterBlockedHit()).IsFalse();
        AssertThat(script.RegisterGuardBreak()).IsFalse();
        AssertThat(script.RegisterSpecialUsed(AbilitySlot.Special1)).IsFalse();
        AssertThat(script.RegisterUltimateUsed()).IsFalse();
        AssertThat(script.RegisterRewindComplete()).IsFalse();
        AssertThat(script.RegisterTimeFreezeComplete()).IsFalse();
        AssertThat(script.SkipRewindDemonstration()).IsFalse();
        AssertThat(script.SkipTimeFreezeLesson()).IsFalse();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.BasicHits);
        AssertThat(script.HitsBlocked).IsEqual(0);
    }

    [TestCase]
    public void TheRewindStepsCompleteInSequenceWithNeverStrandFallbacks() {
        // V7.6: the scripted death-rewind demonstration (ScriptedRewindTests) is
        // preserved and hands off to the Time Freeze escape drill; both carry a
        // never-strand skip.
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.UseRewind);
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);
        AssertThat(script.RegisterRewindComplete()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseTimeFreeze);
        AssertThat(script.RegisterTimeFreezeComplete()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Done);

        // The demonstration could not run at all: skip both time lessons.
        TutorialCalibrationScript stranded = AdvanceToStep(TutorialCalibrationStep.UseRewind);
        AssertThat(stranded.SkipRewindDemonstration()).IsTrue();
        AssertThat(stranded.Step).IsEqual(TutorialCalibrationStep.Done);

        // The drill alone could not run (no TimeFreezeController): its own skip
        // closes it rather than stranding the tutorial.
        TutorialCalibrationScript drillStranded = AdvanceToStep(TutorialCalibrationStep.UseTimeFreeze);
        AssertThat(drillStranded.SkipTimeFreezeLesson()).IsTrue();
        AssertThat(drillStranded.Step).IsEqual(TutorialCalibrationStep.Done);
    }

    [TestCase]
    public void MovementGateRequiresTheZoneAndAbilityUseTogether() {
        // In the zone while the ability is active: satisfied.
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(true, true, -1)).IsTrue();

        // In the zone within the freshness window (instant teleports land the
        // player before the state can be observed): satisfied.
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(true, false, 0)).IsTrue();
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(
            true, false, TutorialMobilityRules.MovementAbilityFreshnessFrames)).IsTrue();

        // In the zone but the ability was never used, or used too long ago: no.
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(true, false, -1)).IsFalse();
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(
            true, false, TutorialMobilityRules.MovementAbilityFreshnessFrames + 1)).IsFalse();

        // Ability active or fresh but outside the zone: no.
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(false, true, 0)).IsFalse();
        AssertThat(TutorialMobilityRules.MovementGateSatisfied(false, false, 0)).IsFalse();
    }
}
