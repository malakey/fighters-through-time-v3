using System.Collections.Generic;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A1. Loading-variant selection and the campaign title mapping.
///
/// Variant selection is pure string logic on purpose: <c>GameManager.LoadScene</c>
/// passes a destination path and gets a treatment back, with no caller anywhere
/// having to state what kind of transition it is. That makes the rule testable
/// without a scene load, and it makes a new destination directory a one-line
/// change here rather than an audit of every call site.
///
/// The title mapping is the fragile half: the sixteen level title keys were
/// authored per level with an era slug rather than an index, so the derivation
/// has to be pinned against every real campaign scene path or a level will
/// silently load with no title.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LoadingScreenTests {

    /// <summary>Every campaign scene path, matching StoryManager's routing table.</summary>
    private static readonly string[] CampaignScenes = {
        "res://scenes/campaign/Level_00_Tutorial.tscn",
        "res://scenes/campaign/Level_01_Florence.tscn",
        "res://scenes/campaign/Level_02_Orleans.tscn",
        "res://scenes/campaign/Level_03_Chicago.tscn",
        "res://scenes/campaign/Level_04_Paris.tscn",
        "res://scenes/campaign/Level_05_Titanic.tscn",
        "res://scenes/campaign/Level_06_Pompeii.tscn",
        "res://scenes/campaign/Level_07_Nassau.tscn",
        "res://scenes/campaign/Level_08_Egypt.tscn",
        "res://scenes/campaign/Level_09_Berlin.tscn",
        "res://scenes/campaign/Level_10_Globe.tscn",
        "res://scenes/campaign/Level_11_Gettysburg.tscn",
        "res://scenes/campaign/Level_12_Lunar.tscn",
        "res://scenes/campaign/Level_13_ChronalVoid.tscn",
        "res://scenes/campaign/Level_14_NeoEarth.tscn",
        "res://scenes/campaign/Level_15_Alexandria.tscn"
    };

    [TestCase]
    public void FighterDestinationsSelectTheVersusVariant() {
        foreach (string path in new[] {
            "res://scenes/fighter/FighterStage_Florence.tscn",
            "res://scenes/fighter/FighterStage_Gettysburg.tscn",
            // The Test Arena lives under scenes/arenas/ for historical reasons but
            // is a Fighter destination to the player, so it is named explicitly.
            "res://scenes/arenas/TestArena.tscn"
        }) {
            AssertThat(LoadingScreen.SelectVariant(path))
                .OverrideFailureMessage($"Expected a VS treatment for {path}")
                .IsEqual(LoadingVariant.FighterVersus);
        }
    }

    [TestCase]
    public void CampaignDestinationsSelectThePortalVariant() {
        foreach (string path in CampaignScenes) {
            AssertThat(LoadingScreen.SelectVariant(path))
                .OverrideFailureMessage($"Expected a portal treatment for {path}")
                .IsEqual(LoadingVariant.StoryPortal);
        }
        AssertThat(LoadingScreen.SelectVariant("res://scenes/campaign/HubWorld.tscn"))
            .IsEqual(LoadingVariant.StoryPortal);
    }

    [TestCase]
    public void EverythingElseFallsBackToTheGenericVariant() {
        AssertThat(LoadingScreen.SelectVariant("res://scenes/menus/MainMenu.tscn"))
            .IsEqual(LoadingVariant.Generic);
        AssertThat(LoadingScreen.SelectVariant("res://scenes/menus/CharacterSelect.tscn"))
            .IsEqual(LoadingVariant.Generic);

        // A null or blank destination must not throw on the way to a scene change.
        AssertThat(LoadingScreen.SelectVariant("")).IsEqual(LoadingVariant.Generic);
        AssertThat(LoadingScreen.SelectVariant(null)).IsEqual(LoadingVariant.Generic);
    }

    [TestCase]
    public void EveryVariantResolvesToAnAuthoredScene() {
        var missing = new List<string>();
        foreach (LoadingVariant variant in new[] {
            LoadingVariant.Generic, LoadingVariant.StoryPortal, LoadingVariant.FighterVersus }) {

            string path = LoadingScreen.ScenePathForVariant(variant);
            if (!ResourceLoader.Exists(path)) missing.Add($"{variant} -> {path}");
        }
        if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
    }

    [TestCase]
    public void EveryCampaignDestinationResolvesATitleKeyThatExists() {
        var problems = new List<string>();

        foreach (string path in CampaignScenes) {
            string key = LoadingScreen.LevelTitleKeyForScene(path);
            if (string.IsNullOrEmpty(key)) {
                problems.Add($"{path} produced no title key");
                continue;
            }
            // The key must resolve through the compiled translation, not merely
            // look plausible: a portal screen showing a raw key is the exact
            // failure this mapping exists to prevent.
            if (TranslationServer.Translate(key) == key) {
                problems.Add($"{path} -> '{key}' does not resolve to any translation");
            }
        }

        string hubKey = LoadingScreen.LevelTitleKeyForScene("res://scenes/campaign/HubWorld.tscn");
        if (hubKey != LoadingScreen.HubTitleKey) problems.Add($"hub produced '{hubKey}'");

        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public void MultiWordEraTokensBecomeSnakeCaseKeys() {
        // The two compound era names are the only cases the naive lowercase
        // derivation would get wrong.
        AssertThat(LoadingScreen.LevelTitleKeyForScene("res://scenes/campaign/Level_13_ChronalVoid.tscn"))
            .IsEqual("chronal_void_level_title");
        AssertThat(LoadingScreen.LevelTitleKeyForScene("res://scenes/campaign/Level_14_NeoEarth.tscn"))
            .IsEqual("neo_earth_level_title");

        // A non-campaign or malformed path yields no key rather than a bad one.
        AssertThat(LoadingScreen.LevelTitleKeyForScene("res://scenes/menus/MainMenu.tscn")).IsEqual("");
        AssertThat(LoadingScreen.LevelTitleKeyForScene("")).IsEqual("");
    }

    [TestCase]
    public void EachVariantInstantiatesAndCarriesItsOwnTreatmentNodes() {
        AssertVariantNodes(LoadingVariant.Generic, new[] { "StatusLabel" });
        AssertVariantNodes(LoadingVariant.StoryPortal, new[] { "StatusLabel", "TitleLabel" });
        AssertVariantNodes(
            LoadingVariant.FighterVersus,
            new[] { "StatusLabel", "PlayerPortrait", "OpponentPortrait", "StageNameLabel", "StagePreview" });
    }

    private static void AssertVariantNodes(LoadingVariant variant, string[] requiredNodes) {
        LoadingScreen screen = LoadingScreen.Create(variant);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(screen);
        try {
            AssertThat(screen.Layer).IsEqual(100);
            // Always: a paused tree must not stop the loading screen from drawing.
            AssertThat(screen.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);

            var missing = new List<string>();
            foreach (string nodeName in requiredNodes) {
                if (screen.FindChild(nodeName, recursive: true, owned: false) == null) {
                    missing.Add($"{variant} is missing {nodeName}");
                }
            }
            if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
        } finally {
            tree.Root.RemoveChild(screen);
            screen.Free();
        }
    }
}
