using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Pulp UI redesign (2026-10-26) standing decision 4: the interface wears the
/// Wardens' gold, and cold cyan is reserved for player 1's ownership colour
/// (<c>UIPalette.P1</c> / <c>GlowPalette.SlotColor</c>) and the Severed
/// (<c>UIPalette.UnboundCold*</c>). This is the closeout's guard against chrome
/// cyan creeping back into UI code: it fails on a read of the legacy
/// <c>UIPalette.Cyan</c> / <c>CyanDim</c> constants, or on a cyan colour literal,
/// anywhere under <c>scripts/UI/</c> or in the UI and menu scenes, unless the
/// file is on <see cref="Allowed"/> with a reason. Pure C#: the sources are read
/// with <c>System.IO</c> from the path this file was compiled from.
/// </summary>
[TestSuite]
public class UiChromeCyanSourceScanTests {

    /// <summary>Files that may name cyan, each with the one-line reason.</summary>
    private static readonly Dictionary<string, string> Allowed = new() {
        ["scripts/UI/UIPalette.cs"] = "defines the legacy Cyan / CyanDim constants and the grammar colours",
        ["scripts/UI/ResonanceEdgeLayer.cs"] = "the Resonance grid's semantic 'satisfied' edge colour (kept by the redesign ruling)",
        ["scripts/UI/RewindPresentationOverlay.cs"] = "the rewind/collapse world-state tint, a temporal-effect presentation and not interface chrome"
    };

    private static readonly Regex NamedRead = new(@"\bUIPalette\s*\.\s*(Cyan|CyanDim)\b", RegexOptions.Compiled);
    private const string Num = @"(-?\d*\.?\d+)f?";
    private static readonly Regex Literal = new(
        @"(?:new\s*(?:Color)?|Color)\s*\(\s*" + Num + @"\s*,\s*" + Num + @"\s*,\s*" + Num + @"\s*(?:,\s*" + Num + @"\s*)?\)",
        RegexOptions.Compiled);

    [TestCase]
    public void NoUiCodeOrSceneReadsChromeCyanOutsideTheAllowList() {
        string root = RepositoryRoot();
        var files = new List<string>();
        files.AddRange(Directory.GetFiles(Path.Combine(root, "scripts", "UI"), "*.cs", SearchOption.AllDirectories));
        foreach (string dir in new[] { "scenes/ui", "scenes/menus" }) {
            files.AddRange(Directory.GetFiles(Path.Combine(root, dir), "*.tscn", SearchOption.AllDirectories));
        }
        AssertThat(files.Count > 40).OverrideFailureMessage("Harness: found only " + files.Count + " UI files.").IsTrue();

        var offenders = new List<string>();
        foreach (string path in files) {
            string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (Allowed.ContainsKey(rel)) continue;
            int line = 0;
            foreach (string raw in File.ReadAllLines(path)) {
                line++;
                string text = path.EndsWith(".cs") ? StripLineComment(raw) : raw;
                if (FindsCyan(text)) offenders.Add(rel + ":" + line + " " + text.Trim());
            }
        }
        if (offenders.Count > 0) {
            AssertThat("chrome cyan sites: " + string.Join(" | ", offenders)).IsEqual("chrome cyan sites: ");
        }

        foreach (string allowed in Allowed.Keys) {
            AssertThat(File.Exists(Path.Combine(root, allowed)))
                .OverrideFailureMessage("The allow-list names a missing file: " + allowed).IsTrue();
        }
    }

    [TestCase]
    public void TheDetectorSeesCyanReadsAndLiteralsAndLeavesGoldAlone() {
        AssertThat(FindsCyan("var c = UIPalette.Cyan;")).IsTrue();
        AssertThat(FindsCyan("var c = UIPalette.CyanDim;")).IsTrue();
        AssertThat(FindsCyan("label.AddThemeColorOverride(\"font_color\", new Color(0f, 0.9f, 0.9f));")).IsTrue();
        AssertThat(FindsCyan("color = Color(0.2, 0.9, 1, 0.4)")).IsTrue();
        AssertThat(FindsCyan("var c = UIPalette.Accent;")).IsFalse();
        AssertThat(FindsCyan("var c = UIPalette.P1;")).IsFalse();
        AssertThat(FindsCyan("var c = new Color(0.953f, 0.788f, 0.357f);")).IsFalse();
        AssertThat(FindsCyan("color = Color(0.95, 0.2, 0.2, 1)")).IsFalse();
    }

    private static bool FindsCyan(string text) {
        if (NamedRead.IsMatch(text)) return true;
        foreach (Match m in Literal.Matches(text)) {
            double r = Parse(m.Groups[1].Value), g = Parse(m.Groups[2].Value), b = Parse(m.Groups[3].Value);
            if (r <= 0.30 && g >= 0.50 && b >= 0.50 && g - r >= 0.35 && System.Math.Abs(g - b) <= 0.25) return true;
        }
        return false;
    }

    private static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static string StripLineComment(string line) {
        int at = line.IndexOf("//", System.StringComparison.Ordinal);
        return at >= 0 ? line.Substring(0, at) : line;
    }

    private static string RepositoryRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here) ?? ".", "..", ".."));
}
