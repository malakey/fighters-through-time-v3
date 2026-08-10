using System.Collections.Generic;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Guards a Godot 4.7.1 .NET heap-corruption landmine found while stabilising the
/// Package 4 B6 Mirror Paradox suite.
/// <para>
/// An <b>empty</b> typed array whose element type is declared by a C# Script
/// ext_resource — <c>Prop = Array[ExtResource("N")]([])</c> where resource N is a
/// <c>.cs</c> script — corrupts the process when the resource is marshalled to its
/// C# array export. The damage surfaces later at an unrelated site: a silent child
/// death (observed exit codes -1073741819 / -1073741795), a hang, or
/// <c>ResourceLoader.Load</c> returning null for a resource that is otherwise fine.
/// </para>
/// <para>
/// Non-empty Script-typed arrays are fine, and so are empty arrays of builtin types
/// (<c>Array[float]([])</c>, <c>Array[int]([])</c>). To express "no entries", omit
/// the property so the C# field default applies.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScriptTypedEmptyArrayTests {
    private static readonly string[] ScannedDirectories = {
        "res://resources/Bosses",
        "res://resources/Enemies",
        "res://resources/Characters",
        "res://resources/Abilities",
        "res://resources/Resonance",
        "res://resources/FighterStages",
        "res://resources/Pools",
        "res://resources/Dialogue"
    };

    /// <summary>Matches Prop = Array[ExtResource("id")]([]) with only whitespace inside.</summary>
    private static readonly Regex ScriptTypedEmptyArray =
        new(@"Array\[ExtResource\(""[^""]+""\)\]\(\s*\[\s*\]\s*\)", RegexOptions.Compiled);

    private static readonly Regex ExtResourceScript =
        new(@"\[ext_resource\s+type=""Script""\s+[^\]]*id=""([^""]+)""", RegexOptions.Compiled);

    private static int _scannedFiles;

    [TestCase]
    public void NoResourceDeclaresAnEmptyScriptTypedArray() {
        var offenders = new List<string>();
        _scannedFiles = 0;
        foreach (string directory in ScannedDirectories) {
            CollectOffenders(directory, offenders);
        }

        // A scanner that silently reaches nothing would pass vacuously.
        AssertThat(_scannedFiles).OverrideFailureMessage(
            $"Resource scan reached only {_scannedFiles} .tres files; the walk is broken.")
            .IsGreater(50);

        AssertThat(offenders.Count).OverrideFailureMessage(
            "Empty Script-typed arrays corrupt the Godot .NET heap. Omit the property " +
            "instead so the C# default applies. Offending resources:\n  " +
            string.Join("\n  ", offenders)).IsEqual(0);
    }

    private static void CollectOffenders(string directory, List<string> offenders) {
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) return;

        foreach (string subdirectory in dir.GetDirectories()) {
            CollectOffenders($"{directory}/{subdirectory}", offenders);
        }

        foreach (string file in dir.GetFiles()) {
            if (!file.EndsWith(".tres")) continue;
            _scannedFiles++;
            string path = $"{directory}/{file}";
            using FileAccess handle = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (handle == null) continue;
            string text = handle.GetAsText();

            // Only Script-typed element types are dangerous; an empty array of a
            // builtin type or of a non-script ext_resource marshals correctly.
            var scriptIDs = new HashSet<string>();
            foreach (Match declaration in ExtResourceScript.Matches(text)) {
                scriptIDs.Add(declaration.Groups[1].Value);
            }
            if (scriptIDs.Count == 0) continue;

            foreach (Match usage in ScriptTypedEmptyArray.Matches(text)) {
                Match id = Regex.Match(usage.Value, @"ExtResource\(""([^""]+)""\)");
                if (id.Success && scriptIDs.Contains(id.Groups[1].Value)) {
                    offenders.Add($"{path}  ->  {usage.Value}");
                }
            }
        }
    }

    [TestCase]
    public void TheMirrorParadoxResourceStillLoadsAndReportsNoAbilities() {
        var data = FTT.Core.AuthoredResources.Load<FTT.Enemies.BossData>(
            "res://resources/Bosses/mirror_paradox.tres");
        AssertObject(data).IsNotNull();
        // Omitted in the .tres, so the C# default (null) stands. Consumers already
        // treat null and empty identically; see BossController.SelectAbilityIndex.
        AssertThat(data.BossAbilities == null || data.BossAbilities.Length == 0).IsTrue();
        AssertThat(data.MaxHP).IsEqual(1000);
    }
}
