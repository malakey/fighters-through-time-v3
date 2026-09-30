using System;
using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W7 (M14, design §16 family 3, 2026-09-26): <i>any scripted list of,
/// or reference to, missing legends is built from non-active heroes only; the
/// active hero is never named as missing.</i>
///
/// <para><b>The rule, mechanised.</b> Every Mystery Thread absence report — the
/// Sarah lines that enumerate the eras and legends that came back empty — is
/// resolved exactly as the game resolves it for each roster hero
/// (<see cref="DialogueSetData.FindSequence"/>, preferring the hero's variant), and
/// the resolved English must not name that hero, or that hero's home era, as
/// missing. The markers are each hero's spoken name and home era as the copy uses
/// them. A hero with no entry in <see cref="MissingMarkers"/> is a roster addition
/// the absence reports have not been audited for, and fails the sweep until it
/// is.</para>
///
/// <para>Selection is by the saved hero ID, never a localized name, and new variant
/// keys use the D6(a) <c>__heroid</c> suffix while existing keys keep
/// <c>_heroid</c>.</para>
///
/// <para><b>Package 13 W4:</b> the Level 3 and Level 8 lists are now the
/// <c>{MissingSoFar}</c> / <c>{MissingPlaces}</c> tokens
/// (<see cref="MissingLegendTokens"/>), resolved per hero from
/// <c>missing_so_far_*</c> / <c>missing_places_*</c> rows with a fallback, so the
/// sweep resolves every line through the same token pass the dialogue box runs
/// before looking for markers. Package 12's four <c>__leonardo</c>/<c>__joan</c>
/// sequence variants are retired. The absence-list line of each rewritten exit is
/// the hero's line at index 1 (design §16, S23/S29/S35).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MysteryThreadAbsenceTests {

    /// <summary>
    /// The absence-list lines: (dialogue set, base sequence ID, the line index that
    /// enumerates missing legends).
    /// </summary>
    private static readonly (string SetPath, string BaseID, int LineIndex)[] AbsenceReports = {
        ("res://resources/Dialogue/level_02_dialogue.tres", "level_02.exit", 1),
        ("res://resources/Dialogue/level_03_dialogue.tres", "level_03.exit", 1),
        ("res://resources/Dialogue/level_08_dialogue.tres", "level_08.postboss", 1),
        ("res://resources/Dialogue/level_10_dialogue.tres", "level_10.exit", 1),
        ("res://resources/Dialogue/level_11_dialogue.tres", "level_11.exit", 1),
        // Package 13 W3 (S16/S35): Okafor's faint-shard report, both on the
        // Observation Deck and in the Act I->II boundary scene with Sarah present.
        ("res://resources/Dialogue/hub_dialogue.tres", "hub.okafor_act2", 1),
        ("res://resources/Dialogue/hub_dialogue.tres", "hub.act1_boundary", 1),
    };

    /// <summary>How the copy names each legend, and their era, when reporting them missing.</summary>
    private static readonly Dictionary<string, string[]> MissingMarkers = new(StringComparer.Ordinal) {
        ["leonardo"] = new[] { "Florence", "Da Vinci", "Leonardo" },
        ["joan"] = new[] { "Orléans", "Joan", "Maid" },
        ["tesla"] = new[] { "Chicago", "Tesla" },
        ["cleopatra"] = new[] { "Alexandria", "Cleopatra" },
        ["shakespeare"] = new[] { "Globe", "Shakespeare" },
        ["lincoln"] = new[] { "Gettysburg", "Lincoln" },
        ["mozart"] = new[] { "Vienna", "Prague", "Mozart" },
        ["einstein"] = new[] { "Princeton", "Einstein" },
        ["pocahontas"] = new[] { "Tidewater", "Tsenacommacah", "Pocahontas" },
    };

    [TestCase]
    public void NoAbsenceReportNamesTheActiveHeroAsMissing() {
        Dictionary<string, string> english = EnglishRows();
        var issues = new List<string>();
        int checkedLines = 0;
        foreach (string hero in CharacterRoster.IDs) {
            if (!MissingMarkers.TryGetValue(hero, out string[] markers)) {
                issues.Add($"{hero} has no missing-legend markers; audit the absence reports for it");
                continue;
            }
            foreach ((string setPath, string baseID, int lineIndex) in AbsenceReports) {
                var set = AuthoredResources.Load<DialogueSetData>(setPath);
                DialogueSequenceData sequence = set?.FindSequence(baseID, hero);
                if (sequence == null || lineIndex >= sequence.LineKeys.Length) {
                    issues.Add($"{baseID} did not resolve a line {lineIndex} for {hero}");
                    continue;
                }
                string key = sequence.LineKeys[lineIndex];
                string line = english.TryGetValue(key, out string value) ? value : "";
                // Resolve the list tokens exactly as the dialogue box does.
                line = MissingLegendTokens.Substitute(line, hero,
                    row => english.TryGetValue(row, out string resolved) ? resolved : row);
                checkedLines++;
                foreach (string marker in markers) {
                    if (line.Contains(marker, StringComparison.Ordinal)) {
                        issues.Add($"{hero}: {sequence.DialogueID} line {lineIndex} ({key}) names '{marker}' as missing");
                    }
                }
            }
        }
        AssertThat(checkedLines).IsGreaterEqual(AbsenceReports.Length * 9);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheM14ListsAreTokensNotSequenceVariantsAndDropOnlyTheActiveHero() {
        var level03 = AuthoredResources.Load<DialogueSetData>("res://resources/Dialogue/level_03_dialogue.tres");
        var level08 = AuthoredResources.Load<DialogueSetData>("res://resources/Dialogue/level_08_dialogue.tres");
        Dictionary<string, string> english = EnglishRows();
        string Rows(string row) => english.TryGetValue(row, out string value) ? value : row;

        // Leonardo and Joan read the base sequences now - the token does the work.
        foreach (string hero in new[] { "leonardo", "joan", "einstein" }) {
            AssertString(level03.FindSequence("level_03.exit", hero).DialogueID).IsEqual("level_03.exit");
        }
        foreach (string hero in new[] { "leonardo", "joan", "tesla", "mozart" }) {
            AssertString(level08.FindSequence("level_08.postboss", hero).DialogueID).IsEqual("level_08.postboss");
        }
        AssertString(Rows("dlg_l03_exit_2")).Contains(MissingLegendTokens.MissingSoFar);
        AssertString(Rows("dlg_l08_postboss_2")).Contains(MissingLegendTokens.MissingPlaces);

        // The design's lists, verbatim (design section 16, M14 notes).
        AssertString(MissingLegendTokens.Substitute("{MissingSoFar}", "einstein", Rows)).IsEqual("Da Vinci, Joan");
        AssertString(MissingLegendTokens.Substitute("{MissingSoFar}", "leonardo", Rows)).IsEqual("Joan");
        AssertString(MissingLegendTokens.Substitute("{MissingSoFar}", "joan", Rows)).IsEqual("Da Vinci");
        AssertString(MissingLegendTokens.Substitute("{MissingPlaces}", "mozart", Rows))
            .IsEqual("Florence. Like Orléans. Like Chicago");
        AssertString(MissingLegendTokens.Substitute("{MissingPlaces}", "leonardo", Rows)).IsEqual("Orléans. Like Chicago");
        AssertString(MissingLegendTokens.Substitute("{MissingPlaces}", "joan", Rows)).IsEqual("Florence. Like Chicago");
        AssertString(MissingLegendTokens.Substitute("{MissingPlaces}", "tesla", Rows)).IsEqual("Florence. Like Orléans");

        // The retired Package 12 variant keys are gone from the table.
        foreach (string retired in new[] {
            "dlg_l03_exit_4__leonardo", "dlg_l03_exit_4__joan",
            "dlg_l08_postboss_2__leonardo", "dlg_l08_postboss_2__joan" }) {
            AssertThat(english.ContainsKey(retired)).OverrideFailureMessage($"{retired} should be retired").IsFalse();
        }
    }

    private static Dictionary<string, string> EnglishRows() {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0 || line[0] == '#') continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string value = line[(comma + 1)..];
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
                value = value[1..^1].Replace("\"\"", "\"");
            }
            rows[line[..comma]] = value;
        }
        return rows;
    }
}
