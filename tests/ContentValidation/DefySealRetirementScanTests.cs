using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// The meter and Defy update (2026-10-31) retired the passive Defy seal, the per-hit Momentum refund, the Defy kit icons,
/// the theme's Defy role and the passive-protection members (plan 1.9, two-stage retirement). This scan makes the retired
/// names unusable anywhere in the source trees, test names included (the TimeFreeze retirement precedent): a reintroduced
/// identifier, key or asset name fails here. The names are assembled from halves so this file does not match itself, and
/// C# comments are stripped first so a retirement note in prose is fine.
///
/// <para>Whole-word matching from the start of the name: <c>tutorial_step_defy_spent</c> (a live Level 0 key) does not
/// match the retired icon name. The save payload's dead <c>StoryDefyHistoryUsed</c> / <c>DefyHistoryUsed</c> fields are
/// deliberately not retired names: they stay deserializable (STORY_PERSISTENCE).</para>
///
/// <para>Pure C# (System.IO only): no engine call, so it runs in the plain test host (CLAUDE.md signature 7).</para>
/// </summary>
[TestSuite]
public class DefySealRetirementScanTests {

    private static readonly string[] Roots = { "scripts", "scenes", "resources", "tests", "tools" };
    private static readonly string[] Extensions = { ".cs", ".tscn", ".tres", ".gd", ".py", ".ps1" };

    private static readonly string[] RetiredNames = {
        "Defy" + "Seal",                       // DefySealModel / State / Payload, OnDefySealChanged, RaiseDefySealChanged, PublishDefySealIfChanged
        "Momentum" + "Refund",                 // the D2 per-hit refund region and the 310 counters
        "Momentum" + "Grant",
        "Defy" + "IconName", "Defy" + "IconTint",
        "Requiem" + "FortissimoShave",
        "Defy" + "Label", "Defy" + "ReadyGlow",
        "hud_" + "defy_",                      // the four seal captions
        "defy_" + "ready", "defy_" + "notready", "defy_" + "spent", "defy_" + "barred",
        "Begin" + "DefyProtection", "End" + "OnOffence", "Defy" + "ProtectionTicks", "Clear" + "DefyProtection",
        "Defy" + "ProtectionFrames", "IsDefy" + "UnspentAndUnbarred",
        "SetStory" + "DefyHistoryUsed",
        "SeedFighterDefy" + "ProtectionForTest", "SeedFighterSpecial" + "CooldownsForTest"
    };

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
        throw new InvalidOperationException("project.godot not found above the test assembly or the working directory");
    }

    [TestCase]
    public void NoRetiredDefySealOrMomentumRefundNameSurvivesInTheSourceTrees() {
        string root = RepoRoot();
        var patterns = new List<(string name, Regex regex)>();
        foreach (string name in RetiredNames) patterns.Add((name, new Regex(@"\b" + Regex.Escape(name), RegexOptions.Compiled)));

        string self = nameof(DefySealRetirementScanTests) + ".cs";
        var problems = new List<string>();
        int scanned = 0;
        foreach (string top in Roots) {
            string dir = Path.Combine(root, top);
            if (!Directory.Exists(dir)) continue;
            foreach (string path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
                string ext = Path.GetExtension(path);
                if (Array.IndexOf(Extensions, ext) < 0) continue;
                if (Path.GetFileName(path) == self) continue;
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                scanned++;
                string text = File.ReadAllText(path);
                if (ext == ".cs") text = StripComments(text);
                foreach ((string name, Regex regex) in patterns) {
                    if (regex.IsMatch(text)) problems.Add($"{relative} names the retired {name}");
                }
            }
        }
        AssertThat(scanned > 500)
            .OverrideFailureMessage($"Harness: the scan found only {scanned} files.")
            .IsTrue();
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    /// <summary>Removes <c>//</c> and <c>/* */</c> comments, leaving string and character literals (which may hold <c>//</c>) intact.</summary>
    public static string StripComments(string source) {
        var sb = new StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length) {
            char c = source[i];
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/') {
                while (i < source.Length && source[i] != '\n') i++;
            } else if (c == '/' && i + 1 < source.Length && source[i + 1] == '*') {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i += 2;
            } else if (c == '"' || c == '\'') {
                bool verbatim = c == '"' && ((i > 0 && source[i - 1] == '@') || (i > 1 && source[i - 1] == '$' && source[i - 2] == '@'));
                sb.Append(c);
                i++;
                while (i < source.Length) {
                    char d = source[i];
                    sb.Append(d);
                    i++;
                    if (!verbatim && d == '\\' && i < source.Length) { sb.Append(source[i]); i++; continue; }
                    if (verbatim && d == '"' && i < source.Length && source[i] == '"') { sb.Append('"'); i++; continue; }
                    if (d == c) break;
                    if (d == '\n' && c == '\'') break;
                }
            } else {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }
}
