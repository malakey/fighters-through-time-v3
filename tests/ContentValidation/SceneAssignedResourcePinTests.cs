using System.Collections.Generic;
using System.Text.RegularExpressions;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Guards the third source of CLAUDE.md failure signature 2 (found 2026-10-04 in
/// the playtest feel pass): a C#-scripted <c>.tres</c> that a <b>scene file</b>
/// assigns to an export — <c>ContentTemplateMarker.Contract</c> on every enemy,
/// checkpoint, construct and template scene, <c>ChronalDustPickup.VisualTiers</c>
/// — reached C# without <see cref="AuthoredResources.Load{T}"/>, so nothing
/// pinned it. When a level teardown freed the last instance its wrapper became
/// collectable, and a scene reload that took the still-cached native resource
/// back before the finalizer ran left a released handle for the late finalizer
/// to trip over (<c>gchandle.is_released()</c>, exit <c>0xC000001D</c>, right
/// after <c>LevelDeathRewindDiagnosticTests</c>' Level 2 teardown).
/// <para>
/// The fix pins such resources from the export's setter
/// (<see cref="AuthoredResources.Pin{T}"/>). This suite fails if a scene starts
/// assigning a scripted resource through any other property, and proves the
/// setters actually pin.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SceneAssignedResourcePinTests {
    /// <summary>The exports whose setters pin through <see cref="AuthoredResources.Pin{T}"/>.</summary>
    private static readonly HashSet<string> PinningProperties = new() { "Contract", "PoolConfig", "VisualTiers" };

    private static readonly Regex ResourceExt =
        new(@"\[ext_resource\s+type=""Resource""[^\]]*path=""([^""]+)""[^\]]*id=""([^""]+)""", RegexOptions.Compiled);

    [TestCase]
    public void EveryScriptedResourceASceneAssignsGoesThroughAPinningExport() {
        var offenders = new List<string>();
        int assignments = 0;
        var scenes = new List<string>();
        CollectScenes("res://scenes", scenes);
        foreach (string scene in scenes) {
            string text = FileAccess.GetFileAsString(scene);
            foreach (Match ext in ResourceExt.Matches(text)) {
                string path = ext.Groups[1].Value;
                string id = ext.Groups[2].Value;
                if (!IsScripted(path)) continue;
                var assignment = new Regex(@"^(\w+)\s*=.*ExtResource\(""" + Regex.Escape(id) + @"""\)", RegexOptions.Multiline);
                foreach (Match use in assignment.Matches(text)) {
                    assignments++;
                    string property = use.Groups[1].Value;
                    if (!PinningProperties.Contains(property)) {
                        offenders.Add($"{scene}: {property} = {path} (assign it through an export whose setter calls AuthoredResources.Pin)");
                    }
                }
            }
        }

        // A walk that reaches nothing would pass vacuously.
        AssertThat(scenes.Count).OverrideFailureMessage($"Scene scan reached only {scenes.Count} scenes.").IsGreater(50);
        AssertThat(assignments).OverrideFailureMessage("No scene assigns a scripted resource; the scan is broken.").IsGreater(10);
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }

    [TestCase]
    public void InstancingAnEnemyAndADustPickupPinsTheirSceneAssignedData() {
        Node enemy = ResourceLoader.Load<PackedScene>("res://scenes/enemies/StandardEnemy.tscn").Instantiate();
        Node pickup = ResourceLoader.Load<PackedScene>("res://scenes/templates/ChronalDustPickupTemplate.tscn").Instantiate();
        try {
            var marker = enemy.GetNode<ContentTemplateMarker>("ContentContract");
            AssertObject(marker.Contract).IsNotNull();
            AssertThat(AuthoredResources.IsPinned(marker.Contract.ResourcePath))
                .OverrideFailureMessage("The enemy scene's contract must be pinned the moment an instance receives it.")
                .IsTrue();

            var dust = pickup as ChronalDustPickup;
            AssertObject(dust).OverrideFailureMessage("The dust template's root is the pickup.").IsNotNull();
            AssertObject(dust.VisualTiers).IsNotNull();
            AssertThat(AuthoredResources.IsPinned(dust.VisualTiers.ResourcePath)).IsTrue();
        } finally {
            enemy.Free();
            pickup.Free();
        }
    }

    private static bool IsScripted(string resourcePath) =>
        FileAccess.FileExists(resourcePath)
        && FileAccess.GetFileAsString(resourcePath).Contains("script = ExtResource(");

    private static void CollectScenes(string directory, List<string> scenes) {
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) return;
        foreach (string sub in dir.GetDirectories()) CollectScenes($"{directory}/{sub}", scenes);
        foreach (string file in dir.GetFiles()) {
            if (file.EndsWith(".tscn")) scenes.Add($"{directory}/{file}");
        }
    }
}
