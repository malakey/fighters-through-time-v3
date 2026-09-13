using System.Collections.Generic;
using System.IO;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B2. Localization and scene contracts for the production Fighter HUD.
///
/// Every assertion goes through <see cref="TranslationServer"/> as well as the
/// CSV, because <c>--headless --quit</c> does not recompile
/// <c>en.en.translation</c>: a key present in the CSV but absent from the
/// compiled resource renders as its raw key on a live HUD while the CSV reviews
/// perfectly clean.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHudContentTests {

    /// <summary>Every key the production HUD renders.</summary>
    private static readonly string[] HudKeys = {
        "fighter_hud_clock",
        "fighter_hud_stocks",
        "fighter_hud_shield",
        "fighter_hud_player",
        "fighter_hud_cooldown_special1",
        "fighter_hud_cooldown_special2",
        "fighter_hud_cooldown_movement",
        "fighter_hud_cooldown_ultimate",
        // Shared with the debug layer and the pre-existing flow surfaces.
        "common_seconds_short",
        "status_none",
        "status_venom",
        "status_root",
        "status_timedilation",
        "status_staticcharge",
        "status_radiantburn"
    };

    [TestCase]
    public void EveryHudKeyIsInTheCsvAndResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        var missingFromCsv = new List<string>();
        var unresolved = new List<string>();
        foreach (string key in HudKeys) {
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
    public void TheFormattedHudStringsKeepTheirPlaceholders() {
        TranslationServer.SetLocale("en");
        // The clock is assembled from two arguments rather than a hardcoded
        // colon, so a locale that separates time differently can express it.
        string clock = TranslationServer.Translate("fighter_hud_clock").ToString();
        AssertThat(clock).Contains("{0}");
        AssertThat(clock).Contains("{1}");
        AssertThat(TranslationServer.Translate("fighter_hud_player").ToString()).Contains("{0}");
        // The shared cooldown string carries a format specifier ("{0:F1}s"), so
        // only the argument index is asserted.
        AssertThat(TranslationServer.Translate("common_seconds_short").ToString()).Contains("{0");
    }

    [TestCase]
    public void TheAuthoredSceneCarriesEveryNodeTheScriptBinds() {
        // The script resolves its widgets by path. A renamed or moved node would
        // silently produce a HUD that renders nothing at all rather than failing.
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        AssertObject(packed).IsNotNull();
        Node root = AutoFree(packed.Instantiate());

        // Package 11 A8 renamed the tree to HUD_CONTRACT's names (Root ->
        // SafeArea, PlayerOne/Two -> P1_Status/P2_Status) and replaced the single
        // Status label with F24's two fixed 24 x 24 slots.
        var required = new List<string> { "SafeArea", "SafeArea/MatchClock" };
        foreach (string player in new[] { "P1_Status", "P2_Status" }) {
            required.Add($"SafeArea/{player}/Body/Portrait");
            required.Add($"SafeArea/{player}/Body/Column/TopRow/Name");
            required.Add($"SafeArea/{player}/Body/Column/HPBar");
            required.Add($"SafeArea/{player}/Body/Column/MeterBar");
            required.Add($"SafeArea/{player}/Body/Column/DefySeal");
            required.Add($"SafeArea/{player}/Body/Column/PipRow/Stocks");
            required.Add($"SafeArea/{player}/Body/Column/PipRow/StocksLost");
            required.Add($"SafeArea/{player}/Body/Column/PipRow/Shields");
            required.Add($"SafeArea/{player}/Body/Column/Cooldowns");
            required.Add(
                $"SafeArea/{player}/Body/Column/TopRow/StatusEffects_Panel/DamageStatus_Indicator");
            required.Add(
                $"SafeArea/{player}/Body/Column/TopRow/StatusEffects_Panel/ControlStatus_Indicator");
        }

        var missing = new List<string>();
        foreach (string path in required) {
            if (root.GetNodeOrNull(path) == null) missing.Add(path);
        }
        AssertThat(missing.Count).OverrideFailureMessage(
            $"{FighterHUD.ScenePath} is missing nodes the script binds: {string.Join(", ", missing)}")
            .IsEqual(0);
    }

    [TestCase]
    public void TheHudRootAdoptsTheSharedTheme() {
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        Node root = AutoFree(packed.Instantiate());
        var hudRoot = root.GetNodeOrNull<Control>("SafeArea");
        AssertObject(hudRoot).IsNotNull();
        // A missing theme resource must not take the HUD down, so this only
        // requires the adoption when the theme actually exists.
        if (UIPalette.LoadTheme() != null) {
            AssertObject(hudRoot.Theme).OverrideFailureMessage(
                "The HUD root does not carry the shared theme, so nothing under it "
                + "inherits the palette or type scale.").IsNotNull();
        }
    }

    [TestCase]
    public void TheRetiredLegacyHudControllerIsGone() {
        // The orphaned bars HUD (an empty UpdateStocks stub, zero references)
        // was retired with the production HUD. If it comes back, it comes back
        // as a second uncoordinated Fighter HUD.
        AssertThat(File.Exists("scripts/UI/HUDController.cs")).OverrideFailureMessage(
            "scripts/UI/HUDController.cs was retired by Package 8 B2.").IsFalse();
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
