using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B1. The Story HUD's boss bar draws a notch at each authored phase
/// threshold, and the notches are derived from the resource rather than restated
/// in the UI.
///
/// This is the failure worth guarding: a hardcoded "two notches at a third and
/// two thirds" would look right on the six-phase-threshold bosses and be silently
/// wrong on the eight that ship a single mid-fight threshold — and completely
/// wrong on the single-phase Mirror Paradox, which must draw no notches at all.
/// So the whole authored roster is walked rather than one representative boss.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossBarPhaseNotchTests {

    private const string BossDirectory = "res://resources/Bosses/";

    private static List<BossData> LoadAuthoredBosses() {
        var bosses = new List<BossData>();
        using DirAccess directory = DirAccess.Open(BossDirectory);
        AssertObject(directory).IsNotNull();
        foreach (string fileName in directory.GetFiles()) {
            if (!fileName.EndsWith(".tres")) continue;
            // AuthoredResources pins the graph for the process lifetime; loading a
            // .tres directly here would churn the cache (CLAUDE.md signature 2).
            BossData data = AuthoredResources.Load<BossData>(BossDirectory + fileName);
            if (data != null) bosses.Add(data);
        }
        return bosses;
    }

    [TestCase]
    public void EveryAuthoredBossNotchesItsBarFromItsOwnPhaseThresholds() {
        List<BossData> bosses = LoadAuthoredBosses();
        AssertThat(bosses.Count).IsEqual(15);

        var issues = new List<string>();
        foreach (BossData boss in bosses) {
            float[] thresholds = boss.PhaseThresholds ?? System.Array.Empty<float>();
            List<float> notches = BossBarPhaseNotches.Normalized(thresholds);

            // One notch per boundary between phases, never one per phase.
            if (notches.Count != boss.PhaseCount - 1) {
                issues.Add($"{boss.BossID}: {notches.Count} notches for {boss.PhaseCount} phases");
            }

            for (int index = 0; index < notches.Count; index++) {
                if (notches[index] <= 0f || notches[index] >= 1f) {
                    issues.Add($"{boss.BossID}: notch {index} at {notches[index]} is off the bar");
                }
                // The array must equal the authored data, not a re-derived guess.
                if (!Mathf.IsEqualApprox(notches[index], thresholds[index])) {
                    issues.Add(
                        $"{boss.BossID}: notch {index} is {notches[index]} " +
                        $"but the resource authors {thresholds[index]}");
                }
                if (index > 0 && notches[index] >= notches[index - 1]) {
                    issues.Add($"{boss.BossID}: notches are not ordered from full HP downwards");
                }
            }
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheSinglePhaseMirrorParadoxDrawsNoNotches() {
        // The Level 13 Mirror Paradox is the roster's one boss with no authored
        // thresholds. A notch drawn there would be pure invention.
        BossData mirror = AuthoredResources.Load<BossData>(BossDirectory + "mirror_paradox.tres");
        AssertObject(mirror).IsNotNull();
        AssertThat(mirror.PhaseCount).IsEqual(1);
        AssertThat(BossBarPhaseNotches.Normalized(mirror.PhaseThresholds).Count).IsEqual(0);
        AssertThat(BossBarPhaseNotches.VisiblePhaseCount(mirror.PhaseThresholds)).IsEqual(1);
        AssertThat(BossBarPhaseNotches.Offsets(mirror.PhaseThresholds, 600f).Count).IsEqual(0);
    }

    [TestCase]
    public void NotchPositionsAgreeWithThePhaseTheFightIsActuallyIn() {
        // BossController advances when the HP fraction drops to OR BELOW the next
        // threshold. A notch that sat on the other side of that comparison would
        // light up one hit late for the whole fight.
        BossData twoThreshold = AuthoredResources.Load<BossData>(BossDirectory + "archive_prime.tres");
        AssertObject(twoThreshold).IsNotNull();
        float[] thresholds = twoThreshold.PhaseThresholds;
        AssertThat(thresholds.Length).IsEqual(2);

        AssertThat(BossBarPhaseNotches.PhaseAt(thresholds, 1f)).IsEqual(0);
        AssertThat(BossBarPhaseNotches.PhaseAt(thresholds, thresholds[0] + 0.01f)).IsEqual(0);
        AssertThat(BossBarPhaseNotches.PhaseAt(thresholds, thresholds[0])).IsEqual(1);
        AssertThat(BossBarPhaseNotches.PhaseAt(thresholds, thresholds[1])).IsEqual(2);
        AssertThat(BossBarPhaseNotches.PhaseAt(thresholds, 0f)).IsEqual(2);
    }

    [TestCase]
    public void OffsetsScaleWithTheBarAndCollapseOnAnUnlaidOutBar() {
        BossData boss = AuthoredResources.Load<BossData>(BossDirectory + "gravity_overseer.tres");
        AssertObject(boss).IsNotNull();

        List<float> offsets = BossBarPhaseNotches.Offsets(boss.PhaseThresholds, 600f);
        AssertThat(offsets.Count).IsEqual(boss.PhaseThresholds.Length);
        for (int index = 0; index < offsets.Count; index++) {
            AssertFloat(offsets[index]).IsEqualApprox(boss.PhaseThresholds[index] * 600f, 0.001f);
        }

        // A bar that has not been laid out yet must not stack every notch at zero.
        AssertThat(BossBarPhaseNotches.Offsets(boss.PhaseThresholds, 0f).Count).IsEqual(0);
    }

    [TestCase]
    public void UnusableThresholdsAreDroppedRatherThanClampedOntoTheBarEnds() {
        // Not authored data today, but a hand-edited resource must degrade to
        // "fewer notches", never to a stripe hidden under the bar's own border.
        List<float> notches = BossBarPhaseNotches.Normalized(
            new[] { 1f, 0.75f, 0.75f, 0f, -0.2f, 1.4f, float.NaN, 0.25f });

        AssertThat(notches.Count).IsEqual(2);
        AssertFloat(notches[0]).IsEqualApprox(0.75f, 0.0001f);
        AssertFloat(notches[1]).IsEqualApprox(0.25f, 0.0001f);
        AssertThat(BossBarPhaseNotches.Normalized(null).Count).IsEqual(0);
    }
}
