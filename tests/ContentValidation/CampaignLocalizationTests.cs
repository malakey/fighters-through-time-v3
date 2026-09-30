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
    /// Authored campaign level scenes, derived from the content manifest rather
    /// than hardcoded; the manifest's own count is pinned independently by
    /// <c>ContentManifestValidator.ExactRequiredCounts</c>. Package 11 added one
    /// row per Level 4A variant; Package 13 W2 (S27) retired all nine, so this is
    /// the sixteen campaign levels again.
    /// </summary>
    private static int AuthoredLevelCount =>
        ContentManifest.LoadDefault().ForCategory(ContentCategory.StoryLevel).Count();

    /// <summary>Route slots: Levels 0–15 (S27 retired the Legacy Nexus slot).</summary>
    private const int CampaignRouteSlotCount = 16;
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
            // Every authored level + the hub (S27 deleted the nine 4A variant sets
            // and the shared 4A set).
            .IsEqual(AuthoredLevelCount + 1);
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
            ("*_level_title", key => key.EndsWith("_level_title"), AuthoredLevelCount),
            ("*_objective_*", key => key.Contains("_objective_"), 60),
            ("campaign_level_*", key => key.StartsWith("campaign_level_"), CampaignRouteSlotCount),
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
            .IsEqual(AuthoredLevelCount);

        var rendered = new HashSet<string>();
        foreach (string key in titles) {
            string text = TranslationServer.Translate(key).ToString();
            AssertThat(text != key).OverrideFailureMessage(
                $"Level title '{key}' does not resolve through the compiled translation.").IsTrue();
            AssertThat(rendered.Add(text)).OverrideFailureMessage(
                $"Two campaign levels render the same title: '{text}'.").IsTrue();
        }
    }

    /// <summary>
    /// Package 11 A6. The V7.5 narrative added two key families that no level
    /// controller references by literal, so the walks above cannot reach them: the
    /// N04 captive spoken names (resolved at render time from the roster minus the
    /// active hero) and the six N03 recognition speakers. Both render inside
    /// dialogue, so a missing compiled entry shows up as a raw key mid-scene.
    /// </summary>
    [TestCase]
    public void TheCaptiveNameAndRecognitionSpeakerFamiliesResolveThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        // Leonardo's spoken name is deliberately NOT his roster display name: N04
        // fixes it as "Da Vinci", which is why this family exists at all.
        string[] required = {
            "captive_name_leonardo", "captive_name_cleopatra", "captive_name_tesla",
            "speaker_apprentice", "speaker_captain", "speaker_engineer",
            "speaker_guard", "speaker_player_company", "speaker_union_officer",
            "speaker_okafor"
        };

        var issues = new List<string>();
        foreach (string key in required) {
            if (!csvKeys.Contains(key)) { issues.Add($"{key} is missing from en.csv"); continue; }
            if (TranslationServer.Translate(key).ToString() == key) {
                issues.Add($"{key} does not resolve through the compiled en.en.translation");
            }
        }
        AssertString(TranslationServer.Translate("captive_name_leonardo").ToString())
            .OverrideFailureMessage("N04 fixes Leonardo's spoken captive name as 'Da Vinci'.")
            .IsEqual("Da Vinci");

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Package 11 A6. Every hero-conditional sequence must use the
    /// <c>&lt;baseID&gt;@&lt;heroID&gt;</c> form, declare the matching
    /// <c>HeroConditionCharacterID</c>, and have a base sequence to fall back to.
    /// N03 is explicit that variant IDs are distinct strings so the seen/skip rules
    /// cannot substitute one branch for the other across save slots.
    /// </summary>
    [TestCase]
    public void EveryHeroVariantSequenceIsWellFormedAndHasABaseToFallBackTo() {
        var issues = new List<string>();
        int variants = 0;

        foreach (string path in DialogueSetPaths()) {
            var set = AuthoredResources.Load<DialogueSetData>(path);
            if (set?.Sequences == null) continue;

            var allIDs = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (DialogueSequenceData sequence in set.Sequences) {
                if (sequence != null) allIDs.Add(sequence.DialogueID);
            }

            foreach (DialogueSequenceData sequence in set.Sequences) {
                if (sequence == null) continue;
                int at = sequence.DialogueID.IndexOf('@');
                bool declares = !string.IsNullOrWhiteSpace(sequence.HeroConditionCharacterID);

                if (at < 0) {
                    if (declares) {
                        issues.Add($"{path}:{sequence.DialogueID} declares a hero condition " +
                                   "but its ID is not <baseID>@<heroID>");
                    }
                    continue;
                }

                variants++;
                string baseID = sequence.DialogueID[..at];
                string heroID = sequence.DialogueID[(at + 1)..];
                if (!declares) {
                    issues.Add($"{path}:{sequence.DialogueID} has no HeroConditionCharacterID");
                } else if (sequence.HeroConditionCharacterID != heroID) {
                    issues.Add($"{path}:{sequence.DialogueID} declares " +
                               $"'{sequence.HeroConditionCharacterID}', not '{heroID}'");
                }
                if (!allIDs.Contains(baseID)) {
                    issues.Add($"{path}:{sequence.DialogueID} has no base sequence '{baseID}' to fall back to");
                }
            }
        }

        AssertThat(variants).OverrideFailureMessage(
            $"Only {variants} hero-variant sequences were reached; the V7.5 Mystery Thread " +
            "authors the six N03 recognition branches plus the per-character Level 0 opening.")
            .IsGreaterEqual(12);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Package 12 W7 (M35): the hero name table. Every roster hero — read from the
    /// manifest, never a literal cast list — has its display, spoken-address,
    /// possessive and home-era rows, each resolving through the compiled
    /// translation, and every campaign dialogue line renders for every hero with
    /// no hero token left on screen.
    /// </summary>
    [TestCase]
    public void TheHeroNameTableResolvesForEveryRosterHeroAndEveryTokenRenders() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();
        var issues = new List<string>();
        var heroes = new List<string>(CharacterRoster.IDs) { DialogueTokens.FallbackHeroID };
        foreach (string hero in heroes) {
            foreach (string key in DialogueTokens.TableKeysFor(hero)) {
                if (!csvKeys.Contains(key)) { issues.Add($"{key} is missing from en.csv"); continue; }
                if (TranslationServer.Translate(key).ToString() == key) {
                    issues.Add($"{key} does not resolve through the compiled en.en.translation");
                }
            }
        }
        foreach (string key in new[] { "hub_bridge", "hub_npc_okafor" }) {
            if (TranslationServer.Translate(key).ToString() == key) issues.Add($"{key} does not resolve");
        }

        int tokenLines = 0;
        foreach (string path in DialogueSetPaths()) {
            var set = AuthoredResources.Load<DialogueSetData>(path);
            if (set?.Sequences == null) continue;
            foreach (DialogueSequenceData sequence in set.Sequences) {
                if (sequence?.LineKeys == null) continue;
                foreach (string lineKey in sequence.LineKeys) {
                    string english = TranslationServer.Translate(lineKey).ToString();
                    if (!DialogueTokens.HasHeroToken(english)) continue;
                    tokenLines++;
                    foreach (string hero in CharacterRoster.IDs) {
                        string rendered = DialogueTokens.SubstituteHeroTokens(
                            english, hero, key => TranslationServer.Translate(key).ToString());
                        if (DialogueTokens.HasHeroToken(rendered) || rendered.Contains("hero_")) {
                            issues.Add($"{lineKey} leaves a hero token or raw key for {hero}");
                        }
                    }
                }
            }
        }
        AssertThat(tokenLines).OverrideFailureMessage(
            $"Only {tokenLines} dialogue lines carry a hero token; the M35 rewrite is missing.")
            .IsGreaterEqual(20);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
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
