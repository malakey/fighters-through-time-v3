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

    /// <summary>
    /// Returns one message per bad record. Parses logical records as Godot's get_csv_line does: a record continues onto the
    /// next physical line while a double quote is open. Blank and <c>#</c> lines are skipped only between records.
    /// </summary>
    public static List<string> FindShapeProblems(string[] lines) {
        var problems = new List<string>();
        int expected = -1;
        bool quoted = false;
        int startLine = 0;
        var fields = new List<string>();
        var sb = new StringBuilder();
        for (int n = 0; n < lines.Length; n++) {
            string line = lines[n].TrimEnd('\r');
            if (n == 0) line = line.TrimStart('\uFEFF');
            if (!quoted) {
                if (line.Length == 0 || line.StartsWith("#")) continue;
                startLine = n + 1;
                fields.Clear();
                sb.Clear();
            }
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
            if (quoted) { sb.Append('\n'); continue; }
            fields.Add(sb.ToString());
            if (expected < 0) expected = fields.Count;
            else if (fields.Count != expected)
                problems.Add($"line {startLine} key '{fields[0]}' has {fields.Count} columns, expected {expected}");
        }
        if (quoted)
            problems.Add($"line {startLine} key '{(fields.Count > 0 ? fields[0] : "(none completed)")}' has an unterminated quote that runs to the end of the file");
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

    [TestCase]
    public void AQuotedValueMayCrossALineBreak() {
        string[] lines = { "key,en", "a,\"hello", "world\"", "b,ok" };
        AssertThat(string.Join(" | ", FindShapeProblems(lines))).IsEqual("");
    }

    [TestCase]
    public void AnUnterminatedQuoteThatSwallowsTheNextRowIsFlagged() {
        string[] lines = { "key,en", "a,\"hello", "b,world" };
        var swallowed = FindShapeProblems(lines);
        AssertThat(swallowed.Count).IsEqual(1);
        AssertThat(swallowed[0]).Contains("line 2");
        var open = FindShapeProblems(new[] { "key,en", "a,ok", "b,\"never closed", "c,x" });
        AssertThat(open.Count).IsEqual(1);
        AssertThat(open[0]).Contains("line 3");
        AssertThat(open[0]).Contains("unterminated");
    }

    [TestCase]
    public void ACommentLineInsideAQuotedValueIsPartOfTheValue() {
        string[] lines = { "key,en", "a,\"one", "# closes here\"", "b,ok", "# real comment, with, commas" };
        AssertThat(string.Join(" | ", FindShapeProblems(lines))).IsEqual("");
    }

    [TestCase]
    public void AnUnterminatedQuoteBeforeAnyCompletedFieldIsReportedNotThrown() {
        var key = FindShapeProblems(new[] { "key,en", "\"unfinished", "b,world" });
        AssertThat(key.Count).IsEqual(1);
        AssertThat(key[0]).Contains("line 2");
        AssertThat(key[0]).Contains("unterminated");
        var header = FindShapeProblems(new[] { "\"key,en", "a,ok" });
        AssertThat(header.Count).IsEqual(1);
        AssertThat(header[0]).Contains("line 1");
        AssertThat(header[0]).Contains("unterminated");
    }
}
