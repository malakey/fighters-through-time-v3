using System.Collections.Generic;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Guards the authored-resource load rule (Package 5 Phase A close).
/// <para>
/// Authored <c>.tres</c> data resources carry a C# script, and several of them reach
/// scripted sub-resources through an <c>Array[ExtResource(script)]</c> export. Loading
/// one, reading it, and dropping the only reference destroys that whole graph and its
/// script instances, and rebuilds them on the next call. The .NET finalizer thread
/// reaps the discarded wrappers at unpredictable times, racing the main thread that is
/// destroying the same native objects, and Godot trips
/// <c>CRASH_COND(gchandle.is_released())</c> in <c>mono_object_disposed_baseref</c> -
/// which is <c>ud2</c> on this build, so the child dies with <c>0xC000001D</c> and a
/// large partial <c>Total:</c>. The damage always surfaces somewhere unrelated.
/// </para>
/// <para>
/// So: authored data resources load through <see cref="FTT.Core.AuthoredResources"/>,
/// which pins each one for the process lifetime. Scenes, textures, audio, and anything
/// pooled or streamed are deliberately NOT covered - those are meant to be released.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AuthoredResourceLoadRuleTests {

    private static readonly string[] ScannedDirectories = { "res://scripts", "res://tests" };

    /// <summary>The C#-scripted authored data resource types that must be pinned.</summary>
    private const string PinnedTypes =
        "CharacterData|AbilityData|ChronalOrbData|ContentSceneContract|PoolDefinition|" +
        "ScenePoolCatalog|ScenePoolCatalogEntry|ScenePoolConfig|BossData|EnemyAbilityData|" +
        "EnemyData|DustVisualTierSet|FighterStageCatalog|FighterStageData|ResonanceGridData|" +
        "ResonanceNodeData|StoryDropProfile|StoryDropTable|DialogueSequenceData|DialogueSetData";

    private static readonly Regex DirectLoad =
        new(@"\b(?:GD|ResourceLoader)\.Load<\s*(" + PinnedTypes + @")\s*>\s*\(", RegexOptions.Compiled);

    /// <summary>AuthoredResources itself is the one place allowed to call ResourceLoader.</summary>
    private const string CacheImplementation = "res://scripts/Core/AuthoredResources.cs";

    private static int _scannedFiles;

    [TestCase]
    public void AuthoredDataResourcesAreOnlyLoadedThroughTheAuthoredResourcesCache() {
        var offenders = new List<string>();
        _scannedFiles = 0;
        foreach (string directory in ScannedDirectories) CollectOffenders(directory, offenders);

        // A walk that silently reaches nothing would pass vacuously.
        AssertThat(_scannedFiles).OverrideFailureMessage(
            $"Source scan reached only {_scannedFiles} .cs files; the walk is broken.")
            .IsGreater(200);

        AssertThat(offenders.Count).OverrideFailureMessage(
            "Authored data resources must load through FTT.Core.AuthoredResources.Load<T>(), " +
            "not ResourceLoader.Load/GD.Load: repeatedly loading and releasing a scripted .tres " +
            "graph corrupts the .NET heap (see CLAUDE.md failure signature 2). Offenders:\n  " +
            string.Join("\n  ", offenders)).IsEqual(0);
    }

    private static void CollectOffenders(string directory, List<string> offenders) {
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) return;

        foreach (string subdirectory in dir.GetDirectories()) {
            CollectOffenders($"{directory}/{subdirectory}", offenders);
        }

        foreach (string file in dir.GetFiles()) {
            if (!file.EndsWith(".cs")) continue;
            string path = $"{directory}/{file}";
            if (path == CacheImplementation) continue;
            _scannedFiles++;

            using FileAccess handle = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (handle == null) continue;
            string text = handle.GetAsText();
            if (!DirectLoad.IsMatch(text)) continue;

            string[] lines = text.Split('\n');
            for (int index = 0; index < lines.Length; index++) {
                if (DirectLoad.IsMatch(lines[index])) offenders.Add($"{path}:{index + 1}");
            }
        }
    }
}
