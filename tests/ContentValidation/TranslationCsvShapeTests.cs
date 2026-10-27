using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Guards the shape of <c>localization/en.csv</c>. Godot's CSV translation importer splits an unquoted value on its commas
/// and silently drops the extra columns, so a value such as "Nassau, 1715 ..." shows as "Nassau". Every data row must parse
/// (quote-aware) to exactly the header's column count. A line starting with <c>#</c> is the file's comment convention and
/// blank lines are fine; neither is a data row.
///
/// <para>Pure C# (System.IO only): no engine call, so it runs in the plain test host (CLAUDE.md signature 7).</para>
/// </summary>
[TestSuite]
public class TranslationCsvShapeTests {

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

    /// <summary>Splits one logical CSV record into fields: commas separate, a double-quoted field may hold commas and "" escapes.</summary>
    public static List<string> SplitRecord(string line) {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++) {
            char c = line[i];
            if (quoted) {
                if (c == '"') {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                } else sb.Append(c);
            } else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    /// <summary>Returns one message per data row whose column count differs from the header's.</summary>
    public static List<string> FindShapeProblems(string[] lines) {
        var problems = new List<string>();
        int expected = -1;
        for (int n = 0; n < lines.Length; n++) {
            string line = lines[n].TrimEnd('\r');
            if (n == 0) line = line.TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var fields = SplitRecord(line);
            if (expected < 0) { expected = fields.Count; continue; }
            if (fields.Count != expected)
                problems.Add($"line {n + 1} key '{fields[0]}' has {fields.Count} columns, expected {expected}");
        }
        return problems;
    }

    [TestCase]
    public void EveryDataRowHasTheHeadersColumnCount() {
        string[] lines = File.ReadAllLines(Path.Combine(RepoRoot(), "localization", "en.csv"), Encoding.UTF8);
        AssertThat(lines.Length > 100).IsTrue();
        var problems = FindShapeProblems(lines);
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public void TheReaderSkipsCommentLinesBlankLinesAndHonoursQuotes() {
        string[] lines = {
            "key,en",
            "# a comment, with, many, commas",
            "",
            "a,\"Nassau, 1715\"",
            "b,\"say \"\"hi\"\", ok\"",
            "c,plain"
        };
        AssertThat(FindShapeProblems(lines).Count).IsEqual(0);
        var bad = FindShapeProblems(new[] { "key,en", "a,Nassau, 1715" });
        AssertThat(bad.Count).IsEqual(1);
        AssertThat(bad[0]).Contains("line 2");
    }
}
