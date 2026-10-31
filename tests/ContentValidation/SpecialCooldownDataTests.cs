using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Meter and Defy update (2026-10-31, P1): a Special no longer carries a cooldown; the Momentum gauge and the shared 2 s reuse
/// delay pay for it. <c>AbilityData.CooldownDuration</c>'s class default is 10 s, so a Special that simply omitted the line would
/// quietly regain one: every Special-slot ability resource must author <c>CooldownDuration = 0</c> explicitly. The movement
/// ability and the Ultimate keep their own values and are not read here.
///
/// <para>Pure C# (System.IO only): no engine call, so it runs in the plain test host (CLAUDE.md signature 7).</para>
/// </summary>
[TestSuite]
public class SpecialCooldownDataTests {

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
    public void EverySpecialSlotAbilityAuthorsAZeroCooldownExplicitly() {
        string abilities = Path.Combine(RepoRoot(), "resources", "Abilities");
        var cooldown = new Regex(@"^CooldownDuration\s*=\s*(-?[0-9.]+)\s*$", RegexOptions.Multiline);
        var problems = new List<string>();
        int checkedFiles = 0;
        foreach (string character in Directory.GetDirectories(abilities)) {
            foreach (string name in new[] { "special_1.tres", "special_2.tres" }) {
                string path = Path.Combine(character, name);
                if (!File.Exists(path)) continue;
                checkedFiles++;
                string label = Path.GetFileName(character) + "/" + name;
                MatchCollection lines = cooldown.Matches(File.ReadAllText(path));
                if (lines.Count != 1) {
                    problems.Add($"{label} must author CooldownDuration exactly once (the class default is 10 s); found {lines.Count}");
                } else if (double.Parse(lines[0].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) != 0.0) {
                    problems.Add($"{label} authors CooldownDuration = {lines[0].Groups[1].Value}");
                }
            }
        }
        AssertThat(checkedFiles >= 18)
            .OverrideFailureMessage($"Harness: only {checkedFiles} Special resources were checked (nine heroes have 18).")
            .IsTrue();
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }
}
