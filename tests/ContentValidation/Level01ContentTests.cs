using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Level 1 (Florence) content contract.
///
/// Florence's three budgeted Chronal Extractors were the one standing content
/// gap <c>docs/DUST_ECONOMY.md</c> §6 left open: Package 5 deliberately did
/// not retrofit the pre-existing Tutorial and Florence controllers, so the
/// ledger budgeted three machines that no scene actually authored. Package 11
/// A3 closes it.
///
/// Level 1 is <b>untimed</b> — it does not extend
/// <c>StoryLevelControllerBase</c>, so no F01 Integrity clock arms and no tier
/// bonus applies. These machines therefore carry presentation and dust only;
/// the per-machine dust allocation is A10's.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level01ContentTests {

    [TestCase]
    public void FlorenceAuthorsItsThreeBudgetedChronalExtractors() {
        var level = new Level01Controller { Name = "FlorenceContentFixture" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(level);
        try {
            IReadOnlyList<ChronalExtractor> extractors = level.Extractors;
            AssertThat(extractors.Count)
                .OverrideFailureMessage("DUST_ECONOMY budgets Florence exactly three Extractors.")
                .IsEqual(3);

            var issues = new List<string>();
            var seen = new HashSet<string>();
            foreach (ChronalExtractor extractor in extractors) {
                if (extractor == null || !GodotObject.IsInstanceValid(extractor)) {
                    issues.Add("an Extractor failed to instantiate from the shared template");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(extractor.ObjectID)) issues.Add("an Extractor has no stable ID");
                else if (!seen.Add(extractor.ObjectID)) issues.Add($"duplicate Extractor ID {extractor.ObjectID}");
                if (!extractor.ObjectID.StartsWith("florence_extractor_", System.StringComparison.Ordinal)) {
                    issues.Add($"{extractor.ObjectID} does not follow the florence_extractor_N convention");
                }
                // The dust value stays resource-owned: never overridden from
                // level code, per the standing rule in the base controller.
                if (extractor.DustReward != 3) {
                    issues.Add($"{extractor.ObjectID} overrides the resource-owned dust value");
                }
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

            // One machine per traversable room, off the critical path — never
            // stacked in one place, and never in the boss arena.
            var columns = new List<float>();
            foreach (ChronalExtractor extractor in extractors) columns.Add(extractor.Position.X);
            columns.Sort();
            AssertThat(columns[1] - columns[0])
                .OverrideFailureMessage("Florence's Extractors must be spread across its rooms.")
                .IsGreater(1500f);
            AssertThat(columns[2] - columns[1]).IsGreater(1500f);
        } finally {
            level.Free();
        }
    }
}
