using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 C1: the Fighter-side localization sweep, mirroring
/// <see cref="CampaignLocalizationTests"/>.
///
/// <para><b>Why every assertion goes through the compiled translation.</b>
/// <c>--headless --quit</c> does NOT reimport <c>localization/en.csv</c> into
/// <c>localization/en.en.translation</c>; only <c>--headless --import</c> (or the
/// editor) does. <see cref="Node.Tr(string, StringName)"/> reads the compiled
/// resource, so an agent who adds keys, runs the import <i>check</i>, and commits
/// ships a stale artifact — every new key renders as its raw key at runtime while
/// the CSV looks perfectly correct in review. A test that only asserts CSV
/// membership cannot see that, so every check here calls
/// <see cref="TranslationServer.Translate(string)"/>.</para>
///
/// <para>Duplicate-key detection lives in
/// <c>EnemyRosterContentTests.EnglishTranslationTableHasNoDuplicateKeys</c> and is
/// deliberately not repeated here.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterLocalizationTests {
    private const int StageCount = 10;

    /// <summary>
    /// Family floors. The minimums stop a family from silently emptying out: a
    /// renamed prefix would otherwise make the whole suite pass by checking
    /// nothing. The A2 match-flow families are listed individually rather than
    /// folded into <c>fighter_*</c> because that umbrella is large enough to hide
    /// a whole missing feature's worth of keys.
    /// </summary>
    private static readonly (string Label, string Prefix, int Minimum)[] Families = {
        // Four keys per stage (name, layout, hazard name, hazard description) x 10.
        ("stage_*", "stage_", 40),
        ("fighter_*", "fighter_", 25),
        ("match_*", "match_", 3),
        // Package 6 A2's new match-flow copy.
        ("fighter_countdown_*", "fighter_countdown_", 2),
        ("fighter_frequency_*", "fighter_frequency_", 4),
        ("fighter_disconnect_*", "fighter_disconnect_", 3),
        ("fighter_pause_*", "fighter_pause_", 2),
        // 10 since the universal dash's removal retired fighter_state_dashing
        // (2026-08-09 user directive).
        ("fighter_state_*", "fighter_state_", 10),
        ("fighter_results_*", "fighter_results_", 3)
    };

    [TestCase]
    public void EveryFighterKeyFamilyInTheCsvResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        string[] csvKeys = EnglishTranslationKeys().ToArray();
        var unresolved = new List<string>();

        foreach ((string label, string prefix, int minimum) in Families) {
            string[] matched = csvKeys.Where(key => key.StartsWith(prefix)).ToArray();
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
    /// The per-stage version of the family sweep. A stage that never authored its
    /// layout key would leave the <c>stage_*</c> family count intact if another
    /// stage had a spare key, so each of the four keys is checked against the
    /// catalog resource that actually references it.
    /// </summary>
    [TestCase]
    public void EveryCatalogStageKeyResolvesAndTheStageNamesAreDistinct() {
        TranslationServer.SetLocale("en");
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();
        AssertThat(catalog.Stages.Length).IsEqual(StageCount);

        HashSet<string> csvKeys = EnglishTranslationKeys();
        var unresolved = new List<string>();
        var renderedNames = new HashSet<string>();

        foreach (FighterStageData stage in catalog.Stages) {
            foreach (string key in new[] {
                stage.DisplayNameKey, stage.LayoutDescriptionKey,
                stage.HazardNameKey, stage.HazardDescriptionKey
            }) {
                string where = $"{stage.StageID} -> '{key}'";
                if (string.IsNullOrWhiteSpace(key)) { unresolved.Add(where + " (empty)"); continue; }
                if (!csvKeys.Contains(key)) unresolved.Add(where + " (not in en.csv)");
                if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(where + " (not compiled)");
            }

            string name = TranslationServer.Translate(stage.DisplayNameKey).ToString();
            AssertThat(renderedNames.Add(name)).OverrideFailureMessage(
                $"Two catalog stages render the same display name: '{name}'.").IsTrue();
        }

        AssertThat(unresolved.Count).OverrideFailureMessage(
            "Stage keys that are missing or do not resolve through the compiled " +
            "translation: " + string.Join(", ", unresolved))
            .IsEqual(0);
    }

    /// <summary>
    /// The stage-select tooltip's prototype line is now gated on
    /// <c>ProductionReady</c>, so the key must still exist and resolve even though
    /// all ten stages are production-contract as of the Package 6 closeout — a
    /// future unfinished stage re-enables it with no code change.
    /// </summary>
    [TestCase]
    public void TheMatchFlowAndPrototypeCopyUsedByRuntimeCodeResolves() {
        TranslationServer.SetLocale("en");
        var unresolved = new List<string>();

        foreach (string key in new[] {
            "fighter_stage_prototype", "fighter_stage", "fighter_start_match",
            "fighter_countdown_digit", "fighter_countdown_go", "fighter_winner_pose",
            "fighter_state_countdown", "fighter_state_respawn_platform",
            "fighter_pause_exit", "fighter_pause_exit_confirm",
            "fighter_disconnect_modal", "fighter_disconnect_rebind", "fighter_disconnect_forfeit",
            "fighter_frequency_off", "fighter_frequency_low",
            "fighter_frequency_medium", "fighter_frequency_high",
            "fighter_rematch", "fighter_change_fighters", "fighter_main_menu",
            "fighter_return_to_ship", "fighter_results_title", "fighter_results_winner",
            "fighter_results_draw", "match_ko", "match_draw", "match_player_wins",
            "common_confirm", "common_cancel"
        }) {
            if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(key);
        }

        AssertThat(unresolved.Count).OverrideFailureMessage(
            "Fighter runtime copy that does not resolve through the compiled " +
            "localization/en.en.translation: " + string.Join(", ", unresolved))
            .IsEqual(0);
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
