using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Combat and movement update 2026-10-16, decision 11.1 (plan §2.11.1,
/// CM-CANCEL): D3 is one rule in both modes, so its table lives once, in
/// <see cref="BasicComboRules"/>' D3 section — recovery frame 0 for a roll or
/// jump, 6 for a basic or the movement ability, a 6-frame buffer — Story's
/// <see cref="StoryCombatRules"/> names forward to it, and nothing under
/// <c>scripts/FighterSim/</c> names a Story rulebook (plan §1.2). Pure C#: the
/// sources are read with <c>System.IO</c> from the path this file was compiled
/// from; no engine API is touched.
/// </summary>
[TestSuite]
public class SpecialRecoveryCancelTableTests {

    [TestCase]
    public void TheD3TableLivesOnceInBasicComboRulesStoryForwardsToItAndTheSimNamesNoStoryRulebook() {
        // The user's numbers and the agent's buffer length.
        AssertThat(BasicComboRules.SpecialRecoveryEvadeCancelFrame).IsEqual(0);
        AssertThat(BasicComboRules.SpecialRecoveryActionCancelFrame).IsEqual(6);
        AssertThat(BasicComboRules.SpecialCancelBufferFrames).IsEqual(6);

        // Story forwards: the same values and the same answers on every frame.
        AssertThat(StoryCombatRules.SpecialRecoveryEvadeCancelFrame).IsEqual(BasicComboRules.SpecialRecoveryEvadeCancelFrame);
        AssertThat(StoryCombatRules.SpecialRecoveryActionCancelFrame).IsEqual(BasicComboRules.SpecialRecoveryActionCancelFrame);
        AssertThat(StoryCombatRules.SpecialCancelBufferFrames).IsEqual(BasicComboRules.SpecialCancelBufferFrames);
        var mismatches = new List<string>();
        for (int frame = -2; frame <= 12; frame++) {
            foreach (bool landed in new[] { false, true }) {
                if (StoryCombatRules.SpecialRecoveryAllowsEvade(landed, frame)
                    != BasicComboRules.SpecialRecoveryAllowsEvade(landed, frame)) {
                    mismatches.Add($"evade landed={landed} frame={frame}");
                }
                if (StoryCombatRules.SpecialRecoveryAllowsAction(landed, frame)
                    != BasicComboRules.SpecialRecoveryAllowsAction(landed, frame)) {
                    mismatches.Add($"action landed={landed} frame={frame}");
                }
            }
            if (StoryCombatRules.SpecialCancelBufferHolds(frame) != BasicComboRules.SpecialCancelBufferHolds(frame)) {
                mismatches.Add($"buffer age={frame}");
            }
        }
        if (mismatches.Count > 0) AssertThat(string.Join(" | ", mismatches)).IsEqual("");
        AssertThat(BasicComboRules.SpecialRecoveryAllowsEvade(true, 0)).IsTrue();
        AssertThat(BasicComboRules.SpecialRecoveryAllowsAction(true, 5)).IsFalse();
        AssertThat(BasicComboRules.SpecialRecoveryAllowsAction(true, 6)).IsTrue();
        AssertThat(BasicComboRules.SpecialRecoveryAllowsEvade(false, 6)).IsFalse();
        AssertThat(BasicComboRules.SpecialCancelBufferHolds(6)).IsTrue();
        AssertThat(BasicComboRules.SpecialCancelBufferHolds(7)).IsFalse();

        // Forwarders, not copies: Story's D3 constants are assigned from the shared table.
        string root = RepositoryRoot();
        string story = File.ReadAllText(Path.Combine(root, "scripts", "Combat", "StoryCombatRules.cs"));
        foreach (string name in new[] {
                     "SpecialRecoveryEvadeCancelFrame", "SpecialRecoveryActionCancelFrame", "SpecialCancelBufferFrames" }) {
            AssertThat(story.Contains($"public const int {name} = BasicComboRules.{name};"))
                .OverrideFailureMessage($"StoryCombatRules.{name} must forward to BasicComboRules.{name}.")
                .IsTrue();
        }

        // Plan §1.2: no Story-named rulebook reaches the deterministic sim.
        string simRoot = Path.Combine(root, "scripts", "FighterSim");
        AssertThat(Directory.Exists(simRoot)).OverrideFailureMessage($"Harness: {simRoot} not found.").IsTrue();
        string[] forbidden = {
            "StoryCombatRules", "StoryAirPhysics", "StoryEnemyCombatRules", "StoryEngagement", "EncounterLockRules"
        };
        var leaks = new List<string>();
        foreach (string file in Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories)) {
            string text = File.ReadAllText(file);
            foreach (string name in forbidden) {
                if (text.Contains(name)) leaks.Add($"{Path.GetFileName(file)} names {name}");
            }
        }
        if (leaks.Count > 0) AssertThat(string.Join(" | ", leaks)).IsEqual("");
    }

    /// <summary>The repository root: two directories above this source file.</summary>
    private static string RepositoryRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here) ?? ".", "..", ".."));
}
