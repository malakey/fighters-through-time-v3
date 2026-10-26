using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Closeout review fix (2026-10-26). A canvas-item shader that assigns <c>COLOR</c> from scratch drops the
/// modulate the engine put in it (HUD opacity from <c>HudOpacityBinder</c>, panel pop-in and fade). Every pulp UI
/// shader must either adjust <c>COLOR</c> in place (<c>COLOR.rgb = ...</c>, <c>COLOR.a *= ...</c>) or fold the
/// incoming value into a full assignment. Headless Godot has no renderer to read pixels from, so this is a
/// source pin. Pure C#.
/// </summary>
[TestSuite]
public class UiShaderModulationScanTests {
    private static readonly string[] UiShaders = {
        "pulp_bar_layers.gdshader", "halftone_corner.gdshader", "ui_desaturate.gdshader", "ui_circle_mask.gdshader"
    };
    private static readonly Regex FullAssign = new(@"^\s*COLOR\s*=\s*(?<rhs>.*);", RegexOptions.Compiled);

    [TestCase]
    public void EveryPulpUiShaderKeepsTheIncomingModulation() {
        string dir = Path.Combine(RepositoryRoot(), "assets", "shaders");
        var offenders = new List<string>();
        foreach (string name in UiShaders) {
            string path = Path.Combine(dir, name);
            AssertThat(File.Exists(path)).OverrideFailureMessage("Harness: missing " + name).IsTrue();
            int line = 0;
            bool assignedAny = false;
            foreach (string raw in File.ReadAllLines(path)) {
                line++;
                string text = raw.Contains("//") ? raw.Substring(0, raw.IndexOf("//", System.StringComparison.Ordinal)) : raw;
                Match m = FullAssign.Match(text);
                if (!m.Success) continue;
                assignedAny = true;
                string rhs = m.Groups["rhs"].Value;
                if (!rhs.Contains("COLOR.rgb") || !rhs.Contains("COLOR.a")) offenders.Add(name + ":" + line + " " + text.Trim());
            }
            // A shader with no full assignment adjusts COLOR in place, which keeps the modulation.
            if (!assignedAny) AssertThat(File.ReadAllText(path).Contains("COLOR")).IsTrue();
        }
        if (offenders.Count > 0) AssertThat("shader assignments dropping the modulate: " + string.Join(" | ", offenders)).IsEqual("shader assignments dropping the modulate: ");
    }

    private static string RepositoryRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here) ?? ".", "..", ".."));
}
