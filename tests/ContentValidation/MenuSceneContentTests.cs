using System.Collections.Generic;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B4 hygiene. Three long-standing defects the menus workstream closed,
/// each of which reads as content rather than as code:
///
/// <list type="bullet">
/// <item>the unreferenced <c>scenes/TestScene.tscn</c> scratch scene, which
/// carried an English literal in a visible Label and was reachable by anyone
/// browsing the project;</item>
/// <item><c>scenes/characters/Player.tscn</c>'s <c>DebugLabel</c>, which rendered
/// the untranslated word "Player" above every Story-mode character;</item>
/// <item><c>MainMenu.CharacterName()</c>'s raw <c>GD.Load</c> of a
/// <c>CharacterData</c>, which drops the pinned instance and rebuilds the whole
/// scripted resource graph on the next call.</item>
/// </list>
///
/// <para>These are the two visible-<c>text</c> offenders C1's planned <c>.tscn</c>
/// scanner is meant to prove itself against, so they are pinned here rather than
/// left to that later sweep.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MenuSceneContentTests {

    /// <summary>An actual <c>GD.Load&lt;T&gt;(...)</c> call, not a prose mention.</summary>
    private static readonly Regex GenericEngineLoad =
        new(@"GD\.Load<[^>]+>\s*\(", RegexOptions.Compiled);

    [TestCase]
    public void TheUnreferencedTestSceneIsGone() {
        AssertThat(FileAccess.FileExists("res://scenes/TestScene.tscn"))
            .OverrideFailureMessage("scenes/TestScene.tscn was reintroduced; nothing references it.")
            .IsFalse();
        AssertThat(ResourceLoader.Exists("res://scenes/TestScene.tscn")).IsFalse();
    }

    [TestCase]
    public void ThePlayerSceneCarriesNoVisibleDebugText() {
        string source = ReadText("res://scenes/characters/Player.tscn");
        AssertThat(source.Length > 0).IsTrue();
        AssertThat(source.Contains("DebugLabel"))
            .OverrideFailureMessage("Player.tscn still carries the DebugLabel node.")
            .IsFalse();
        AssertThat(source.Contains("text = \"Player\""))
            .OverrideFailureMessage("Player.tscn still renders the literal \"Player\".")
            .IsFalse();

        // The scene must still instantiate with its collision and placeholder body.
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/characters/Player.tscn");
        Node player = packed.Instantiate();
        try {
            AssertThat(player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") != null).IsTrue();
            AssertThat(player.GetNodeOrNull<ColorRect>("PlaceholderBody") != null).IsTrue();
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void TheMenuScriptsLoadAuthoredCharacterDataThroughThePinningCache() {
        foreach (string script in new[] {
                     "res://scripts/UI/MainMenu.cs",
                     "res://scripts/UI/CharacterSelectScreen.cs" }) {
            string source = ReadText(script);
            AssertThat(source.Length > 0)
                .OverrideFailureMessage($"Could not read {script}")
                .IsTrue();
            // A real call, not a doc-comment mention of the API being retired.
            AssertThat(GenericEngineLoad.IsMatch(source))
                .OverrideFailureMessage(
                    $"{script} loads a resource with GD.Load; authored data must go " +
                    "through FTT.Core.AuthoredResources.Load<T>().")
                .IsFalse();
            AssertThat(source.Contains("AuthoredResources.Load<"))
                .OverrideFailureMessage($"{script} no longer uses the pinning cache")
                .IsTrue();
        }
    }

    [TestCase]
    public void EveryMenuSceneAdoptsTheSharedThemeAndStoresCopyAsTranslationKeys() {
        var offenders = new List<string>();
        foreach (string scene in new[] {
                     "res://scenes/menus/MainMenu.tscn",
                     "res://scenes/menus/CharacterSelect.tscn",
                     "res://scenes/ui/ResonanceGrid.tscn" }) {
            string source = ReadText(scene);
            if (!source.Contains("res://resources/UI/ftt_theme.tres")) {
                offenders.Add($"{scene} does not adopt the shared theme");
            }
            foreach (string english in new[] {
                         "text = \"Back\"", "text = \"Settings\"", "text = \"Quit",
                         "text = \"Story", "text = \"FIGHT", "text = \"Delete" }) {
                if (source.Contains(english)) offenders.Add($"{scene} contains literal {english}");
            }
        }
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }

    [TestCase]
    public void TheB4TranslationKeysResolveThroughTheCompiledTable() {
        TranslationServer.SetLocale("en");
        // Package 8 B4 added exactly one key. Per plan §2.8 the compiled
        // translation is regenerated by the orchestrator at the wave boundary; run
        // `--headless --import` locally before this suite in a lone worktree.
        AssertThat(TranslationServer.Translate("fighter_cpu_difficulty").ToString())
            .OverrideFailureMessage(
                "fighter_cpu_difficulty does not resolve; run --headless --import.")
            .IsNotEqual("fighter_cpu_difficulty");
    }

    private static string ReadText(string path) {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file == null ? "" : file.GetAsText();
    }
}
