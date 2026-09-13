using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The tutorial calibration step machine (audit M-3, rebuilt for V7.6 by
/// Package 11 A5). The order the machine pins is
/// <c>BasicHits → RallyReclaim → Block → Grab → HitstunDI → LandingTech →
/// MeterAndDefy → UseRewind → UseTimeFreeze → Done</c>: the Special and
/// Ultimate lessons are gone, because under the V7.5 Legacy Unlock Schedule
/// neither ability exists at Level 0 and a step demanding one was an
/// unclearable wall.
///
/// <para>These cases cover the ordering and the phase gating. The V7.6 beats'
/// own rules — the persistent dummy shield, the free repeating launch, the
/// Defy proc — are pinned in <c>TutorialCalibrationV76Tests</c>. Pure C#: the
/// machine is engine-free by design.</para>
/// </summary>
[TestSuite]
public class TutorialCalibrationTests {

    internal static TutorialCalibrationScript AdvanceToStep(TutorialCalibrationStep target) {
        var script = new TutorialCalibrationScript();
        if (target == TutorialCalibrationStep.BasicHits) return script;
        for (int hit = 0; hit < TutorialCalibrationScript.RequiredBasicHits; hit++) script.RegisterBasicHit();
        if (target == TutorialCalibrationStep.RallyReclaim) return script;
        script.RegisterRallyReclaimHit();
        if (target == TutorialCalibrationStep.Block) return script;
        for (int hit = 0; hit < TutorialCalibrationScript.RequiredBlockedHits; hit++) script.RegisterBlockedHit();
        if (target == TutorialCalibrationStep.Grab) return script;
        script.RegisterGrabThrow();
        if (target == TutorialCalibrationStep.HitstunDI) return script;
        script.RegisterDirectionalInfluenceBeat();
        if (target == TutorialCalibrationStep.LandingTech) return script;
        script.RegisterLandingTech();
        if (target == TutorialCalibrationStep.MeterAndDefy) return script;
        script.RegisterDefyProc();
        if (target == TutorialCalibrationStep.UseRewind) return script;
        script.RegisterRewindComplete();
        if (target == TutorialCalibrationStep.UseTimeFreeze) return script;
        script.RegisterTimeFreezeComplete();
        return script;
    }

    [TestCase]
    public void TheV76StepOrderRunsAttackRallyBlockGrabDiTechDefyRewindManual() {
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
        // V7.6: Block hands off to the Grab calibration, not to a Special the
        // hero has not earned yet.
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Grab);

        AssertThat(script.RegisterGrabThrow()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.HitstunDI);

        AssertThat(script.RegisterDirectionalInfluenceBeat()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.LandingTech);

        AssertThat(script.RegisterLandingTech()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.MeterAndDefy);

        AssertThat(script.RegisterDefyProc()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);

        // V7.2: the scripted death-rewind demo hands off to the player's own
        // rewind lesson (A2's Time Freeze drill replaces its body).
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
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Grab);
    }

    [TestCase]
    public void AGuardBreakCompletesTheBlockLessonImmediately() {
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.Block);

        // The full depletion arc (all charges spent, guard broken) is the whole
        // lesson in one stroke; the step must not wait for more absorbs.
        AssertThat(script.RegisterGuardBreak()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Grab);
    }

    [TestCase]
    public void NoStepEverAsksForASpecialOrTheUltimateCast() {
        // The V7.5 regression this rebuild exists to prevent: Level 0 must never
        // require an ability the Legacy Unlock Schedule has not granted. Walking
        // the machine end to end must never pass through a step naming a locked
        // slot.
        var script = new TutorialCalibrationScript();
        var visited = new System.Collections.Generic.List<TutorialCalibrationStep> { script.Step };
        for (int hit = 0; hit < TutorialCalibrationScript.RequiredBasicHits; hit++) script.RegisterBasicHit();
        visited.Add(script.Step);
        script.RegisterRallyReclaimHit();
        visited.Add(script.Step);
        for (int hit = 0; hit < TutorialCalibrationScript.RequiredBlockedHits; hit++) script.RegisterBlockedHit();
        visited.Add(script.Step);
        script.RegisterGrabThrow();
        visited.Add(script.Step);
        script.RegisterDirectionalInfluenceBeat();
        visited.Add(script.Step);
        script.RegisterLandingTech();
        visited.Add(script.Step);
        script.RegisterDefyProc();
        visited.Add(script.Step);
        script.RegisterRewindComplete();
        visited.Add(script.Step);
        script.RegisterTimeFreezeComplete();
        visited.Add(script.Step);

        var issues = new System.Collections.Generic.List<string>();
        foreach (TutorialCalibrationStep step in visited) {
            string name = step.ToString();
            if (name.Contains("Special") || name.Contains("Ultimate")) {
                issues.Add($"Level 0 still calibrates a locked slot: {name}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(visited[^1]).IsEqual(TutorialCalibrationStep.Done);
    }

    [TestCase]
    public void TheDefyStepAdvancesOnlyOnADefyProc() {
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.MeterAndDefy);

        // Nothing else moves the step forward.
        AssertThat(script.RegisterBasicHit()).IsFalse();
        AssertThat(script.RegisterBlockedHit()).IsFalse();
        AssertThat(script.RegisterGrabThrow()).IsFalse();
        AssertThat(script.RegisterLandingTech()).IsFalse();
        AssertThat(script.RegisterRewindComplete()).IsFalse();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.MeterAndDefy);

        AssertThat(script.RegisterDefyProc()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);
    }

    [TestCase]
    public void EventsFromTheWrongStepNeverAdvanceAnything() {
        var script = new TutorialCalibrationScript();

        // Blocking, grabbing, the DI beat, the tech, Defy and the rewinds during
        // the basic-hit lesson are all inert.
        AssertThat(script.RegisterRallyReclaimHit()).IsFalse();
        AssertThat(script.RegisterBlockedHit()).IsFalse();
        AssertThat(script.RegisterGuardBreak()).IsFalse();
        AssertThat(script.RegisterGrabLessonSwingAbsorbed()).IsFalse();
        AssertThat(script.RegisterGrabThrow()).IsFalse();
        AssertThat(script.RegisterDirectionalInfluenceBeat()).IsFalse();
        AssertThat(script.RegisterLandingTech()).IsFalse();
        AssertThat(script.RegisterLandingTechMissed()).IsFalse();
        AssertThat(script.RegisterDefyProc()).IsFalse();
        AssertThat(script.RegisterRewindComplete()).IsFalse();
        AssertThat(script.RegisterTimeFreezeComplete()).IsFalse();
        AssertThat(script.SkipDefyLesson()).IsFalse();
        AssertThat(script.SkipRewindDemonstration()).IsFalse();
        AssertThat(script.SkipTimeFreezeLesson()).IsFalse();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.BasicHits);
        AssertThat(script.HitsBlocked).IsEqual(0);
    }

    [TestCase]
    public void TheRewindStepsCompleteInSequenceWithNeverStrandFallbacks() {
        // The scripted rewind demonstration (ScriptedRewindTests) is preserved
        // and hands off to the player's own rewind lesson; both carry a
        // never-strand skip, and so does the V7.6 Defy beat.
        TutorialCalibrationScript script = AdvanceToStep(TutorialCalibrationStep.UseRewind);
        AssertThat(script.RegisterRewindComplete()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseTimeFreeze);
        AssertThat(script.RegisterTimeFreezeComplete()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Done);

        // The demonstration could not run at all: skip both rewind lessons.
        TutorialCalibrationScript stranded = AdvanceToStep(TutorialCalibrationStep.UseRewind);
        AssertThat(stranded.SkipRewindDemonstration()).IsTrue();
        AssertThat(stranded.Step).IsEqual(TutorialCalibrationStep.Done);

        // The manual lesson alone could not run: its own skip closes it.
        TutorialCalibrationScript manualStranded = AdvanceToStep(TutorialCalibrationStep.UseTimeFreeze);
        AssertThat(manualStranded.SkipTimeFreezeLesson()).IsTrue();
        AssertThat(manualStranded.Step).IsEqual(TutorialCalibrationStep.Done);

        // A resumed attempt that already spent Defy History cannot show the
        // proc; the lesson closes on its coaching text instead.
        TutorialCalibrationScript spentDefy = AdvanceToStep(TutorialCalibrationStep.MeterAndDefy);
        AssertThat(spentDefy.SkipDefyLesson()).IsTrue();
        AssertThat(spentDefy.Step).IsEqual(TutorialCalibrationStep.UseRewind);
    }

    [TestCase]
    public void MovementGateRuleSurvivesForTheLaterWrenDrill() {
        // V7.6 deleted Level 0's movement corridor (the ability unlocks after
        // Level 1), but the rule itself is the shape the optional Wren drill
        // will reuse, so it stays pinned.

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
