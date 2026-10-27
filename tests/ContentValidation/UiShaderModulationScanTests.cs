using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Closeout review fix (2026-10-26). A canvas-item shader that assigns <c>COLOR</c> from scratch drops the
/// modulate the engine put in it (HUD opacity from <c>HudOpacityBinder</c>, panel pop-in and fade). Every UI shader
/// must either adjust <c>COLOR</c> in place (<c>COLOR.rgb = ...</c>, <c>COLOR.a *= ...</c>) or fold the incoming
/// value into a full assignment. TG-WIDGETS: the scan discovers every shader under <c>assets/shaders/</c> instead of
/// naming them, so a new UI shader cannot slip past it; only the combat outline is left out (<see cref="NotUi"/>).
/// Headless Godot has no renderer to read pixels from, so this is a source pin. Pure C#.
/// </summary>
[TestSuite]
public class UiShaderModulationScanTests {
    /// <summary>Shaders that are not UI: the combat outline and glow, which owns its sprite's colour by design.</summary>
    private static readonly string[] NotUi = { "outline_glow.gdshader" };
    private static readonly Regex FullAssign = new(@"^\s*COLOR\s*=\s*(?<rhs>.*);", RegexOptions.Compiled);

    [TestCase]
    public void EveryUiShaderKeepsTheIncomingModulation() {
        string dir = Path.Combine(RepositoryRoot(), "assets", "shaders");
        var offenders = new List<string>();
        var scanned = new List<string>();
        foreach (string path in Directory.GetFiles(dir, "*.gdshader")) {
            string name = Path.GetFileName(path);
            if (System.Array.IndexOf(NotUi, name) >= 0) continue;
            scanned.Add(name);
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
        // Harness: the discovery found the UI shaders (the title gradient and the desaturate at least).
        AssertThat(scanned.Contains("ui_title_gradient.gdshader") && scanned.Contains("ui_desaturate.gdshader"))
            .OverrideFailureMessage("Harness: the scan found " + string.Join(", ", scanned)).IsTrue();
        if (offenders.Count > 0) AssertThat("shader assignments dropping the modulate: " + string.Join(" | ", offenders)).IsEqual("shader assignments dropping the modulate: ");
    }

    private static string RepositoryRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here) ?? ".", "..", ".."));
}
