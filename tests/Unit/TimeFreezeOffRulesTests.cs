using System;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay review 2026-11-03 (decision 10b++, plan §1.3), the engine-free half: <see cref="TimeFreezeRules"/> is off,
/// the calibration is eight lessons without the escape drill, and a saved cooldown stays dead, unclamped data. The
/// scene-side half (no controller, no tooltip, no input, no indicator, the Post-Landing Hold still working) is
/// <c>TimeFreezeOffSceneTests</c>. Pure C#, so it touches no engine API.
/// </summary>
[TestSuite]
public class TimeFreezeOffRulesTests {

    [TestCase]
    public void TheShippedRuleIsOffAndTheSeamOverridesItBothWaysAndRestores() {
        AssertThat(TimeFreezeRules.Default).IsFalse();
        bool? previous = TimeFreezeRules.EnabledOverrideForTesting;
        try {
            TimeFreezeRules.EnabledOverrideForTesting = null;
            AssertThat(TimeFreezeRules.Enabled).OverrideFailureMessage("Time Freeze ships off.").IsFalse();
            TimeFreezeRules.EnabledOverrideForTesting = true;
            AssertThat(TimeFreezeRules.Enabled).IsTrue();
            TimeFreezeRules.EnabledOverrideForTesting = false;
            AssertThat(TimeFreezeRules.Enabled).IsFalse();
        } finally {
            TimeFreezeRules.EnabledOverrideForTesting = previous;
        }
    }

    [TestCase]
    public void WithTheVerbOffTheCalibrationIsEightLessonsAndTheRewindDemonstrationIsTheLast() {
        bool? previous = TimeFreezeRules.EnabledOverrideForTesting;
        try {
            TimeFreezeRules.EnabledOverrideForTesting = false;
            var script = new TutorialCalibrationScript();
            for (int i = 0; i < TutorialCalibrationScript.RequiredBasicHits; i++) script.RegisterBasicHit();
            script.RegisterRallyReclaimHit();
            for (int i = 0; i < TutorialCalibrationScript.RequiredBlockedHits; i++) script.RegisterBlockedHit();
            script.RegisterGrabThrow();
            script.RegisterDirectionalInfluenceBeat();
            script.RegisterLandingTech();
            script.RegisterDefyExecuted();
            AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);

            AssertThat(script.RegisterRewindComplete()).IsTrue();
            AssertThat(script.Step)
                .OverrideFailureMessage("With the verb off the rewind demonstration ends the calibration.")
                .IsEqual(TutorialCalibrationStep.Done);
            // The lesson's methods stay for the on state but have nothing to advance.
            AssertThat(script.RegisterTimeFreezeComplete()).IsFalse();
            AssertThat(script.SkipTimeFreezeLesson()).IsFalse();

            // Lessons = every step before Done except the dormant UseTimeFreeze: nine members, eight lessons.
            int lessons = Enum.GetValues<TutorialCalibrationStep>()
                .Count(step => step != TutorialCalibrationStep.Done && step != TutorialCalibrationStep.UseTimeFreeze);
            AssertThat(lessons).IsEqual(8);
            // The enum member stays so a re-enable is one edit.
            AssertThat(Enum.IsDefined(TutorialCalibrationStep.UseTimeFreeze)).IsTrue();
        } finally {
            TimeFreezeRules.EnabledOverrideForTesting = previous;
        }
    }

    [TestCase]
    public void ForcedOnTheLegacyDrillStillFollowsTheRewindDemonstration() {
        bool? previous = TimeFreezeRules.EnabledOverrideForTesting;
        try {
            TimeFreezeRules.EnabledOverrideForTesting = true;
            var script = new TutorialCalibrationScript();
            for (int i = 0; i < TutorialCalibrationScript.RequiredBasicHits; i++) script.RegisterBasicHit();
            script.RegisterRallyReclaimHit();
            for (int i = 0; i < TutorialCalibrationScript.RequiredBlockedHits; i++) script.RegisterBlockedHit();
            script.RegisterGrabThrow();
            script.RegisterDirectionalInfluenceBeat();
            script.RegisterLandingTech();
            script.RegisterDefyExecuted();
            AssertThat(script.RegisterRewindComplete()).IsTrue();
            AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseTimeFreeze);
            AssertThat(script.RegisterTimeFreezeComplete()).IsTrue();
            AssertThat(script.Step).IsEqual(TutorialCalibrationStep.Done);
        } finally {
            TimeFreezeRules.EnabledOverrideForTesting = previous;
        }
    }

    [TestCase]
    public void ASavedCooldownIsDeadDataKeptUnclampedSoAReEnableRestoresIt() {
        // 0-45 s are the valid values; nothing clamps above, so a save written while the verb was on still
        // holds exactly what it held. Only a negative is raised to zero (the field's long-standing rule).
        foreach (float seconds in new[] { 0f, 12.5f, 45f, 90f }) {
            var timers = new StoryPlayerResourceTimers { TimeFreezeCooldownSeconds = seconds };
            timers.Normalize();
            AssertFloat(timers.TimeFreezeCooldownSeconds).IsEqual(seconds);
        }
        var negative = new StoryPlayerResourceTimers { TimeFreezeCooldownSeconds = -3f };
        negative.Normalize();
        AssertFloat(negative.TimeFreezeCooldownSeconds).IsEqual(0f);
    }
}
