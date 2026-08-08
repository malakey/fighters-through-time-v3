using System.Collections.Generic;
using System.IO;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 A2. Contracts for the two authored match-flow UI scenes and the
/// localization families the flow introduces.
///
/// Every localization assertion goes through
/// <see cref="TranslationServer.Translate(string)"/> rather than the CSV, because
/// <c>--headless --quit</c> does not recompile <c>en.en.translation</c> — a key
/// present in the CSV but absent from the compiled resource renders as its raw
/// key at runtime while the CSV reviews clean.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMatchFlowContentTests {
    private const string PauseScenePath = "res://scenes/ui/LocalFighterPause.tscn";
    private const string ResultsScenePath = "res://scenes/ui/MatchResults.tscn";

    /// <summary>Keys the countdown, KO sequence, pause, and results flow render.</summary>
    private static readonly string[] FlowKeys = {
        "match_ko",
        "match_draw",
        "fighter_countdown_digit",
        "fighter_countdown_go",
        "fighter_winner_pose",
        "fighter_state_countdown",
        "fighter_state_respawn_platform",
        "fighter_frequency_off",
        "fighter_frequency_low",
        "fighter_frequency_medium",
        "fighter_frequency_high",
        "fighter_pause_exit",
        "fighter_pause_exit_confirm",
        "fighter_disconnect_modal",
        "fighter_disconnect_rebind",
        "fighter_disconnect_forfeit",
        "fighter_results_title",
        "fighter_results_winner",
        "fighter_results_draw",
        "fighter_rematch",
        "fighter_change_fighters",
        "fighter_main_menu",
        "common_confirm",
        "common_cancel",
        "menu_paused",
        "menu_resume"
    };

    [TestCase]
    public void TheAuthoredPauseSceneInstantiatesAtItsReservedPath() {
        AssertThat(ResourceLoader.Exists(PauseScenePath)).OverrideFailureMessage(
            $"{PauseScenePath} is the path the local_pause manifest row reserves.").IsTrue();
        var packed = ResourceLoader.Load<PackedScene>(PauseScenePath);
        AssertObject(packed).IsNotNull();
        Node root = AutoFree(packed.Instantiate());
        AssertThat(root is LocalFighterPause).OverrideFailureMessage(
            $"{PauseScenePath} root is {root.GetType().Name}, not LocalFighterPause.").IsTrue();
    }

    [TestCase]
    public void TheAuthoredResultsSceneInstantiatesAtItsReservedPath() {
        AssertThat(ResourceLoader.Exists(ResultsScenePath)).OverrideFailureMessage(
            $"{ResultsScenePath} is the path the match_results manifest row reserves.").IsTrue();
        var packed = ResourceLoader.Load<PackedScene>(ResultsScenePath);
        AssertObject(packed).IsNotNull();
        Node root = AutoFree(packed.Instantiate());
        AssertThat(root is MatchResults).OverrideFailureMessage(
            $"{ResultsScenePath} root is {root.GetType().Name}, not MatchResults.").IsTrue();
    }

    [TestCase]
    public void RematchResolvesToARealSceneRatherThanTheHardcodedTestArena() {
        // The pre-Package-6 results panel always sent a rematch back to
        // TestArena.tscn, silently discarding the selected stage.
        string path = MatchResults.RematchScenePath();
        AssertThat(string.IsNullOrWhiteSpace(path)).IsFalse();
        AssertThat(ResourceLoader.Exists(path)).OverrideFailureMessage(
            $"Rematch destination '{path}' does not exist.").IsTrue();
        AssertThat(ResourceLoader.Exists(MatchResults.ExitScenePath())).IsTrue();
    }

    [TestCase]
    public void EveryMatchFlowKeyIsInTheCsvAndResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        var missingFromCsv = new List<string>();
        var unresolved = new List<string>();
        foreach (string key in FlowKeys) {
            if (!csvKeys.Contains(key)) missingFromCsv.Add(key);
            if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(key);
        }

        AssertThat(missingFromCsv.Count).OverrideFailureMessage(
            $"Missing from localization/en.csv: {string.Join(", ", missingFromCsv)}").IsEqual(0);
        AssertThat(unresolved.Count).OverrideFailureMessage(
            "These keys are in en.csv but do not resolve through the compiled "
            + "en.en.translation — run `--headless --import` (not `--quit`) and commit "
            + $"the regenerated translation: {string.Join(", ", unresolved)}").IsEqual(0);
    }

    [TestCase]
    public void FormattedFlowStringsKeepTheirPlaceholders() {
        TranslationServer.SetLocale("en");
        AssertThat(TranslationServer.Translate("fighter_disconnect_modal").ToString()).Contains("{0}");
        AssertThat(TranslationServer.Translate("fighter_winner_pose").ToString()).Contains("{0}");
        AssertThat(TranslationServer.Translate("fighter_results_winner").ToString()).Contains("{0}");
        AssertThat(TranslationServer.Translate("fighter_countdown_digit").ToString()).Contains("{0}");
    }

    [TestCase]
    public void MatchFlowTimingsMatchTheDesignDocument() {
        // design-godot.md ~1565-1571: 5.0 s platform, 3.0 s invulnerability from
        // the drop; Section 11: a three-second pre-match countdown.
        AssertThat(FighterMatchFlowRules.CountdownFrames).IsEqual(3 * FighterSimulation.TickRate);
        AssertThat(FighterMatchFlowRules.CountdownFramesPerDigit).IsEqual(FighterSimulation.TickRate);
        AssertThat(FighterMatchFlowRules.GoBannerFrames).IsEqual(30);
        AssertThat(FighterMatchFlowRules.RespawnPlatformFrames).IsEqual(5 * FighterSimulation.TickRate);
        AssertThat(FighterMatchFlowRules.RespawnInvulnerabilityFrames).IsEqual(3 * FighterSimulation.TickRate);
        AssertThat(FighterMatchFlowRules.RespawnPlatformGraceFrames > 0).IsTrue();
        AssertThat(FighterMatchFlowRules.RespawnPlatformGraceFrames
            < FighterMatchFlowRules.RespawnPlatformFrames).IsTrue();
        AssertThat(FighterMatchFlowRules.RespawnPlatformPosition.x.ToFloat()).IsEqual(0f);
        AssertThat(FighterMatchFlowRules.RespawnPlatformPosition.y.ToFloat()).IsEqual(3f);
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
