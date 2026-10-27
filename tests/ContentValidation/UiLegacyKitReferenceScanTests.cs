using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// TG-CLOSE (Temporal Glass closeout, plan <c>docs/UI_GLASS_2026-10-26_PLAN.md</c> 2.13 item 3): the pulp kit is gone and
/// stays gone. Nothing under <c>scripts/</c>, <c>scenes/</c>, <c>resources/</c>, <c>tests/</c> or <c>tools/</c> names the
/// pulp kit's folders or catalog, a retired theme variation or Label role (plan 2.2), a retired <c>UIPalette</c> constant
/// or a retired icon name (the pulp-only names and the two transition aliases). This file is the one place that holds the
/// retired lists; other suites (UIThemeTests, PulpIconTests, MainMenuCoverTests) read them from here. The kit keeps its
/// legacy C# name "Pulp" (<c>PulpKit</c>, <c>PulpIcon</c>, the <c>PulpBar</c> variations): that name is not retired.
///
/// <para>Pure C# (System.IO only): no engine call, so it runs in the plain test host (CLAUDE.md signature 7).</para>
/// </summary>
[TestSuite]
public class UiLegacyKitReferenceScanTests {

    /// <summary>The pulp kit's folders and catalog (split so no other file need hold them whole).</summary>
    public static readonly string[] RetiredPaths = { "ui/" + "pulp/", "pulp" + "_kit", "asset_src/" + "pulp" };

    /// <summary>Theme variations the glass closeout retired (plan 2.2's closeout column).</summary>
    public static readonly string[] RetiredVariations = {
        "HudPlatePanel", "PaperPanel", "BannerPanel", "CoverBannerPanel", "BossNameplate", "BossPhaseTab", "SlotPlate",
        "SlotPlateReady", "KeyTagPlate", "CardPlate", "CardPlatePick", "BubblePlate", "StampPlate"
    };

    /// <summary>Label roles the glass closeout retired (T36, T39, T43).</summary>
    public static readonly string[] RetiredRoles = { "CardDates", "BubbleText", "StarBadgeText" };

    /// <summary>The pulp-only <c>UIPalette</c> constants (marked legacy by TG-THEME, deleted by the closeout).</summary>
    public static readonly string[] RetiredPaletteConstants = {
        "PlateFill", "PlateRaised", "Paper", "LabelDark", "PaperInk", "PaperInk2", "PaperInk3", "Track", "SlotFrame",
        "PromptCream", "DisabledFill", "DisabledLabel"
    };

    /// <summary>Icon names only the pulp kit drew, and the two transition aliases (<c>anchor</c>, <c>anchor_hollow</c>).</summary>
    public static readonly string[] RetiredIconNames = {
        "anchor", "anchor_hollow", "anchor_pip", "anchor_pip_cracked", "marker", "prompt_circle"
    };

    private static readonly string[] ScannedFolders = { "scripts", "scenes", "resources", "tests", "tools" };
    private static readonly string[] ScannedExtensions = { ".cs", ".tscn", ".tres", ".py", ".gd", ".json", ".ps1", ".md", ".gdshader" };

    // An icon name counts only on a line that hands it to the icon API (so a test's own "anchor" label is not one).
    private static readonly Regex IconContext = new(
        @"PulpIcon|IconName|GlyphName|TextureFor|ResolveGlass|GlassIconTexture|HasGlassIcon|icon_name", RegexOptions.Compiled);

    private static string RepoRoot() {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir != null; i++) {
            if (File.Exists(Path.Combine(dir, "project.godot"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 12 && dir != null; i++) {
            if (File.Exists(Path.Combine(dir, "project.godot"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>
    /// Every retired reference on <paramref name="text"/>, a file of extension <paramref name="extension"/>: one entry per
    /// line and kind ("line N: what"). Public, so the planted case can drive it.
    /// </summary>
    public static List<string> Find(string text, string extension) {
        var found = new List<string>();
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        bool code = extension is ".cs" or ".py" or ".gd";
        bool godotText = extension is ".tscn" or ".tres";
        for (int i = 0; i < lines.Length; i++) {
            string line = lines[i];
            foreach (string path in RetiredPaths) {
                if (line.Contains(path, StringComparison.Ordinal)) found.Add($"line {i + 1}: '{path}'");
            }
            foreach (string name in RetiredVariations.Concat(RetiredRoles)) {
                if (!line.Contains(name, StringComparison.Ordinal)) continue;
                bool hit = Regex.IsMatch(line, $@"\b(Variations|LabelRoles)\.{name}\b")
                    || (code && line.Contains($"\"{name}\"", StringComparison.Ordinal))
                    || (godotText && (line.Contains($"&\"{name}\"", StringComparison.Ordinal)
                                      || line.StartsWith(name + "/", StringComparison.Ordinal)));
                if (hit) found.Add($"line {i + 1}: the retired theme name {name}");
            }
            foreach (string name in RetiredPaletteConstants) {
                if (!line.Contains(name, StringComparison.Ordinal)) continue;
                bool hit = Regex.IsMatch(line, $@"\bUIPalette\.{name}\b")
                    || (extension == ".py" && line.Contains($"pal(\"{name}\"", StringComparison.Ordinal));
                if (hit) found.Add($"line {i + 1}: the retired palette constant {name}");
            }
            if ((code || godotText) && IconContext.IsMatch(line)) {
                foreach (string name in RetiredIconNames) {
                    if (line.Contains($"\"{name}\"", StringComparison.Ordinal)) found.Add($"line {i + 1}: the retired icon name {name}");
                }
            }
        }
        return found;
    }

    [TestCase]
    public void NothingInTheCodeContentOrToolsNamesThePulpKitOrARetiredThemeNamePaletteConstantOrIcon() {
        string root = RepoRoot();
        AssertThat(root != null).OverrideFailureMessage("the repository root (project.godot) was not found").IsTrue();
        string self = Path.GetFullPath(Path.Combine(root, "tests", "ContentValidation", nameof(UiLegacyKitReferenceScanTests) + ".cs"));
        var issues = new List<string>();
        int scanned = 0;
        foreach (string folder in ScannedFolders) {
            string dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(ScannedExtensions, ext) < 0) continue;
                string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (rel.Contains("/raw/") || rel.Contains("/__pycache__/")) continue;   // the art chain's git-ignored scratch
                if (string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase)) continue;
                scanned++;
                foreach (string hit in Find(File.ReadAllText(file), ext)) issues.Add($"{rel} {hit}");
            }
        }
        AssertThat(scanned > 500).OverrideFailureMessage($"only {scanned} files scanned").IsTrue();
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheDetectorSeesAPlantedReferenceOfEveryKindAndLeavesTheGlassNamesAlone() {
        // Planted, one per kind; each must be found.
        var planted = new (string Text, string Extension)[] {
            ("var t = GD.Load<Texture2D>(\"res://assets/sprites/" + "ui/pulp/panel_dark.png\");", ".cs"),
            ("PULP = json.load(open(\"pulp" + "_kit.json\"))", ".py"),
            ("py tools/" + "asset_src/pulp/build_all.ps1", ".ps1"),
            ("panel.ThemeTypeVariation = UIPalette.Variations." + "PaperPanel;", ".cs"),
            ("label.ThemeTypeVariation = UIPalette.LabelRoles." + "CardDates;", ".cs"),
            ("new PanelContainer { ThemeTypeVariation = \"" + "BossNameplate\" };", ".cs"),
            ("theme_type_variation = &\"" + "StampPlate\"", ".tscn"),
            ("BubblePlate" + "/styles/panel = SubResource(\"Bubble_1\")", ".tres"),
            ("label.AddThemeColorOverride(\"font_color\", UIPalette." + "LabelDark);", ".cs"),
            ("bar.Color = UIPalette." + "Track;", ".cs"),
            ("pal(\"" + "PromptCream\", \"FFF3D8\")", ".py"),
            ("legacy.AddChild(PulpIcon.Create(\"" + "anchor\", UIPalette.Accent2, 46f));", ".cs"),
            ("icon.IconName = \"" + "prompt_circle\";", ".cs"),
        };
        var missed = new List<string>();
        foreach ((string text, string ext) in planted) {
            if (Find(text, ext).Count == 0) missed.Add(text);
        }
        if (missed.Count > 0) AssertThat("not found: " + string.Join(" | ", missed)).IsEqual("");

        // The glass names and ordinary words that merely contain a retired one are left alone.
        var clean = new (string Text, string Extension)[] {
            ("panel.ThemeTypeVariation = UIPalette.Variations.GlassPanelPause;", ".cs"),
            ("var bar = new ProgressBar { ThemeTypeVariation = UIPalette.Variations.PulpBarH14 };", ".cs"),
            ("track.Color = UIPalette.TubeTrackTop; slider.Color = UIPalette.SliderTrack; UIPalette.TrackBackground;", ".cs"),
            ("PulpIcon icon = PulpIcon.Create(\"beacon_anchor\", UIPalette.Accent2, 46f); icon.IconName = \"beacon_anchor_hollow\";", ".cs"),
            ("case \"anchor\": return BuildTagAnchor();", ".cs"),
            ("var phase = hud.GetNode<Label>(\"SafeArea/BossPanel/BossPhaseTab\");", ".cs"),
            ("[node name=\"BossPhaseTab\" type=\"Label\" parent=\"SafeArea/BossPanel\"]", ".tscn"),
            ("PulpKit.Texture(\"glass_panel\"); // the kit's legacy name is Pulp", ".cs"),
            ("res://assets/sprites/ui/glass/glass_kit.json", ".cs"),
            ("res://assets/sprites/ui/ability_icons/einstein_special_1.png", ".cs"),
        };
        var flagged = new List<string>();
        foreach ((string text, string ext) in clean) {
            List<string> hits = Find(text, ext);
            if (hits.Count > 0) flagged.Add($"{text} -> {string.Join(", ", hits)}");
        }
        if (flagged.Count > 0) AssertThat("wrongly flagged: " + string.Join(" | ", flagged)).IsEqual("");
    }
}
