using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 5 C1: the campaign-wide localization sweep.
///
/// <para><b>Why this suite exists.</b> Several Package 5 level agents hit the same
/// trap and it cost a cycle each time: <c>--headless --quit</c> does NOT reimport
/// <c>localization/en.csv</c>, only <c>--headless --import</c> (or the editor) does.
/// An agent that adds keys, runs the import <i>check</i>, and commits ships a stale
/// <c>localization/en.en.translation</c> — and because <c>Node.Tr()</c> reads the
/// compiled resource rather than the CSV, every new key then renders as its raw key
/// at runtime while the CSV looks perfectly correct in review. Asserting a key is
/// "in en.csv" cannot see that. Every assertion here goes through
/// <see cref="TranslationServer.Translate(string)"/>, which is what the game reads.</para>
///
/// <para>Duplicate-key detection lives in
/// <c>EnemyRosterContentTests.EnglishTranslationTableHasNoDuplicateKeys</c> and is
/// deliberately not repeated here — one canonical check that cannot drift out of
/// agreement with a second copy.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignLocalizationTests {
    /// <summary>
    /// Authored campaign levels. Package 11 A12 (V7.6) added a seventeenth — Level 4A,
    /// the per-character Legacy Level — as the Einstein exemplar; <b>B1-B3 raise this to
    /// 25</b> with the other eight heroes. It counts authored levels, not route length:
    /// one playthrough still visits sixteen levels plus one 4A variant.
    /// </summary>
    private const int CampaignLevelCount = 17;
    private const string DialogueDirectory = "res://resources/Dialogue/";

    /// <summary>
    /// Every line and speaker key actually referenced by the authored campaign
    /// dialogue sets (hub + levels 0-15) must be in the CSV and must resolve
    /// through the compiled translation.
    /// </summary>
    [TestCase]
    public void EveryDialogueLineAndSpeakerKeyResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        var missingFromCsv = new List<string>();
        var unresolved = new List<string>();
        int sets = 0;
        int sequences = 0;
        int lines = 0;

        foreach (string path in DialogueSetPaths()) {
            var set = AuthoredResources.Load<DialogueSetData>(path);
            AssertObject(set).OverrideFailureMessage($"{path} did not load as a DialogueSetData.")
                .IsNotNull();
            sets++;

            foreach (DialogueSequenceData sequence in set.Sequences ?? System.Array.Empty<DialogueSequenceData>()) {
                if (sequence == null) continue;
                sequences++;

                AssertThat(string.IsNullOrWhiteSpace(sequence.DialogueID)).OverrideFailureMessage(
                    $"{path} has a sequence with no DialogueID.").IsFalse();

                // A speaker list shorter than the line list leaves lines unattributed;
                // longer means a line was deleted and its speaker left behind.
                AssertThat(sequence.SpeakerNameKeys.Length).OverrideFailureMessage(
                    $"{path} sequence '{sequence.DialogueID}' has " +
                    $"{sequence.LineKeys.Length} lines but " +
                    $"{sequence.SpeakerNameKeys.Length} speakers.")
                    .IsEqual(sequence.LineKeys.Length);

                foreach (string key in sequence.LineKeys.Concat(sequence.SpeakerNameKeys)) {
                    lines++;
                    string where = $"{path}:{sequence.DialogueID} -> '{key}'";
                    if (string.IsNullOrWhiteSpace(key)) { missingFromCsv.Add(where + " (empty)"); continue; }
                    if (!csvKeys.Contains(key)) missingFromCsv.Add(where);
                    if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(where);
                }
            }
        }

        AssertThat(sets).OverrideFailureMessage(
            $"Only {sets} dialogue sets were reached; the directory walk is broken.")
            .IsEqual(CampaignLevelCount + 1); // the authored levels (incl. Level 4A) + the hub.
        AssertThat(sequences).OverrideFailureMessage(
            $"Only {sequences} dialogue sequences were reached.").IsGreaterEqual(48);
        AssertThat(lines).IsGreater(300);

        AssertThat(missingFromCsv.Count).OverrideFailureMessage(
            "Dialogue keys missing from localization/en.csv: " + string.Join(", ", missingFromCsv))
            .IsEqual(0);
        AssertThat(unresolved.Count).OverrideFailureMessage(
            "Dialogue keys that do not resolve through localization/en.en.translation " +
            "(the CSV is ahead of the compiled resource - run `--headless --import` and commit " +
            "the regenerated en.en.translation): " + string.Join(", ", unresolved))
            .IsEqual(0);
    }

    /// <summary>
    /// The CSV-side sweep. Walking the authored resources only reaches keys that
    /// something references; this walks every campaign-facing key family in the CSV
    /// itself, so a key added to en.csv without a reimport is caught even before a
    /// level starts using it.
    /// </summary>
    [TestCase]
    public void EveryCampaignKeyFamilyInTheCsvResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");

        // family prefix/suffix -> the minimum number of keys the campaign must have.
        // The floors stop a family from silently emptying out (a renamed prefix would
        // otherwise make this suite pass by checking nothing).
        var families = new (string Label, System.Func<string, bool> Match, int Minimum)[] {
            ("dlg_l* dialogue lines", key => key.StartsWith("dlg_l"), 160),
            ("*_level_title", key => key.EndsWith("_level_title"), CampaignLevelCount),
            ("*_objective_*", key => key.Contains("_objective_"), 60),
            ("campaign_level_*", key => key.StartsWith("campaign_level_"), CampaignLevelCount),
            ("speaker_*", key => key.StartsWith("speaker_"), 10)
        };

        string[] csvKeys = EnglishTranslationKeys().ToArray();
        var unresolved = new List<string>();

        foreach ((string label, System.Func<string, bool> match, int minimum) in families) {
            string[] matched = csvKeys.Where(match).ToArray();
            AssertThat(matched.Length).OverrideFailureMessage(
                $"Only {matched.Length} '{label}' keys found in localization/en.csv; " +
                $"expected at least {minimum}. Did the family get renamed?")
                .IsGreaterEqual(minimum);

            foreach (string key in matched) {
                if (TranslationServer.Translate(key).ToString() == key) unresolved.Add($"{label}: {key}");
            }
        }

        AssertThat(unresolved.Count).OverrideFailureMessage(
            "These localization/en.csv keys do not resolve through the compiled " +
            "localization/en.en.translation. The compiled resource is stale: run " +
            "`--headless --import` and commit the regenerated en.en.translation. Keys: " +
            string.Join(", ", unresolved))
            .IsEqual(0);
    }

    /// <summary>
    /// Every campaign level has exactly one title key and one hub mission key, and
    /// they all resolve. This is the per-level version of the family sweep above:
    /// a level whose title key was never authored would leave the family count
    /// intact if another level had two.
    /// </summary>
    [TestCase]
    public void EveryLevelHasAResolvingTitleKeyAndTheTitlesAreDistinct() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        string[] titles = csvKeys.Where(key => key.EndsWith("_level_title")).OrderBy(key => key).ToArray();
        AssertThat(titles.Length).OverrideFailureMessage(
            "Expected one *_level_title per campaign level; found: " + string.Join(", ", titles))
            .IsEqual(CampaignLevelCount);

        var rendered = new HashSet<string>();
        foreach (string key in titles) {
            string text = TranslationServer.Translate(key).ToString();
            AssertThat(text != key).OverrideFailureMessage(
                $"Level title '{key}' does not resolve through the compiled translation.").IsTrue();
            AssertThat(rendered.Add(text)).OverrideFailureMessage(
                $"Two campaign levels render the same title: '{text}'.").IsTrue();
        }
    }

    private static IEnumerable<string> DialogueSetPaths() {
        using DirAccess directory = DirAccess.Open(DialogueDirectory);
        if (directory == null) yield break;
        foreach (string file in directory.GetFiles().OrderBy(name => name)) {
            // Godot renames .tres to .tres.remap in exported builds; headless runs
            // against the source tree, so a plain suffix check is correct here.
            if (!file.EndsWith(".tres")) continue;
            yield return DialogueDirectory + file;
        }
    }

    private static HashSet<string> EnglishTranslationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        return keys;
    }
}
