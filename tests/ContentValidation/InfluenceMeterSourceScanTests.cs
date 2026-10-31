using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// The Influence meter has one rulebook, <c>FTT.Combat.InfluenceMeterRules</c>,
/// and the Fighter sim reads it only through <c>FTT.FighterSim.FighterMeterRules</c>.
/// Before it, the cap and the two rates were re-typed in three places — the
/// Story <c>UltimateMeter</c> node and literals across the sim, the CPU and the
/// driver — and a bare <c>100</c> clamped the meter in the save payload, the HUD
/// models, an orb pickup and the Ultimate press. These are source pins, because
/// a re-typed constant behaves identically until someone retunes the other one.
///
/// <para><b>What counts as a stray literal.</b> In a statement that names the
/// meter (an identifier containing <c>Meter</c> or <c>Influence</c>), a
/// <c>100</c>, a <c>0.25</c> or a <c>0.75</c> in any numeric form, or an
/// <c>FP64.FromInt(100 | 4 | 3)</c>; in a file whose whole subject is the meter
/// (<see cref="MeterOnlyFiles"/>) any of those, named or not. Colour literals
/// and <c>…Percent = N</c> tuning rows are not meter values and are ignored.
/// Comments and string contents are stripped first. The scan covers the files
/// that held a copy before the rulebook, and every file under
/// <c>scripts/FighterSim/</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class InfluenceMeterSourceScanTests {

    /// <summary>The Story, UI and diagnostics files that held a meter copy.</summary>
    private static readonly string[] TouchedStoryFiles = {
        "scripts/Characters/PlayerController.cs",
        "scripts/Combat/UltimateMeter.cs",
        "scripts/Combat/ChronalOrbItem.cs",
        "scripts/Core/SaveManager.cs",
        "scripts/Environment/Level00Controller.cs",
        "scripts/Environment/StoryLevelControllerBase.cs",
        "scripts/UI/FighterHudModel.cs",
        "scripts/UI/HudAbilityIndicatorModel.cs",
        "scripts/UI/StoryHUD.cs",
        "scripts/Diagnostics/PlaytestBots.cs"
    };

    /// <summary>Files about nothing but the meter: every literal in them is a meter value.</summary>
    private static readonly string[] MeterOnlyFiles = {
        "scripts/Combat/UltimateMeter.cs",
        "scripts/FighterSim/FighterMeterRules.cs"
    };

    private const string RulebookFile = "scripts/Combat/InfluenceMeterRules.cs";
    private const string SimViewFile = "scripts/FighterSim/FighterMeterRules.cs";

    private static readonly Regex MeterIdentifier = new(
        @"\b(\w*Meter\w*|meter\w*|_meter\w*|\w*Influence\w*|influence\w*)\b", RegexOptions.Compiled);
    private static readonly Regex MeterLiteral = new(
        @"(?<![\w.])(100(\.0+)?[fFdDmM]?|0?\.25[fFdDmM]?|0?\.75[fFdDmM]?)(?![\w.])"
        + @"|FromInt\(\s*(100|4|3)\s*\)|FromDouble\(\s*(100(\.0+)?|0?\.25|0?\.75)\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex ColorCall = new(@"\bColor\s*\([^()]*\)", RegexOptions.Compiled);
    private static readonly Regex PercentRow = new(@"\b\w+Percent\s*=\s*-?\d+", RegexOptions.Compiled);

    [TestCase]
    public void NoTouchedFileOrSimFileCarriesAStrayMeterLiteral() {
        var files = new SortedSet<string>(TouchedStoryFiles);
        foreach (string file in Directory.GetFiles(Absolute("scripts/FighterSim"), "*.cs", SearchOption.AllDirectories)) {
            files.Add("scripts/FighterSim/" + Path.GetRelativePath(Absolute("scripts/FighterSim"), file).Replace('\\', '/'));
        }
        AssertThat(files.Contains(SimViewFile))
            .OverrideFailureMessage("The sim's meter view must exist and be scanned.")
            .IsTrue();
        AssertThat(files.Contains(RulebookFile))
            .OverrideFailureMessage("The rulebook itself is where the numbers live; it is never scanned.")
            .IsFalse();

        var issues = new List<string>();
        foreach (string file in files) {
            bool meterOnly = MeterOnlyFiles.Contains(file);
            foreach (string statement in Statements(Code(file))) {
                string candidate = PercentRow.Replace(ColorCall.Replace(statement, " "), " ");
                if (!MeterLiteral.IsMatch(candidate)) continue;
                if (!meterOnly && !MeterIdentifier.IsMatch(candidate)) continue;
                issues.Add($"{file}: {Collapse(statement)}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheSimReadsOnlyTheFixedPointViewAndTheBuildRateLaneIsReadOnce() {
        var issues = new List<string>();

        // The sim: no Story gain factor or Story gain formula anywhere, and the
        // rulebook reached only through FighterMeterRules, which reads its
        // integer rationals and never a float view.
        string simRoot = Absolute("scripts/FighterSim");
        foreach (string path in Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories)) {
            string file = "scripts/FighterSim/" + Path.GetRelativePath(simRoot, path).Replace('\\', '/');
            string code = Code(file);
            foreach (string forbidden in new[] {
                         "StoryUltimateBuildRateMultiplier", "StoryMeterGainFactor",
                         "GainFromDamageDealt", "GainFromDamageTaken" }) {
                if (code.Contains(forbidden, System.StringComparison.Ordinal)) {
                    issues.Add($"{file} reads the Story-only {forbidden}");
                }
            }
            if (file != SimViewFile && code.Contains("InfluenceMeterRules", System.StringComparison.Ordinal)) {
                issues.Add($"{file} reads InfluenceMeterRules directly instead of FighterMeterRules");
            }
        }
        string simView = Code(SimViewFile);
        if (Regex.IsMatch(simView,
                @"InfluenceMeterRules\.(MaxValue|PointsPerDamageDealt|PointsPerDamageTaken|StockLossRetention)\b")) {
            issues.Add($"{SimViewFile} reads a float view of the rulebook");
        }
        if (Regex.IsMatch(simView, @"\b(float|double|FromDouble|FromFloat)\b")) {
            issues.Add($"{SimViewFile} touches floating point");
        }

        // Story: Mozart's lane is written by CharacterFactory and read in exactly
        // one expression, StoryMeterGainFactor, which the meter's gain paths read.
        var reads = new List<string>();
        foreach (string path in Directory.GetFiles(Absolute("scripts"), "*.cs", SearchOption.AllDirectories)) {
            string file = "scripts/" + Path.GetRelativePath(Absolute("scripts"), path).Replace('\\', '/');
            foreach (string statement in Statements(Code(file))) {
                string rest = Regex.Replace(statement, @"\bStoryUltimateBuildRateMultiplier\s*=(?!=)", " ");
                rest = Regex.Replace(rest, @"\bfloat\s+StoryUltimateBuildRateMultiplier\b", " ");
                if (rest.Contains("StoryUltimateBuildRateMultiplier", System.StringComparison.Ordinal)) {
                    reads.Add($"{file}: {Collapse(statement)}");
                }
            }
        }
        if (reads.Count != 1
            || !reads[0].StartsWith("scripts/Characters/PlayerController.cs:", System.StringComparison.Ordinal)
            || !reads[0].Contains("StoryMeterGainFactor =>", System.StringComparison.Ordinal)) {
            issues.Add("StoryUltimateBuildRateMultiplier must be read once, in PlayerController.StoryMeterGainFactor; reads: "
                + string.Join(" ; ", reads));
        }
        string meterNode = Code("scripts/Combat/UltimateMeter.cs");
        if (!meterNode.Contains("StoryMeterGainFactor", System.StringComparison.Ordinal)
            || !meterNode.Contains("InfluenceMeterRules.GainFromDamageDealt", System.StringComparison.Ordinal)
            || !meterNode.Contains("InfluenceMeterRules.GainFromDamageTaken", System.StringComparison.Ordinal)) {
            issues.Add("UltimateMeter's two gain paths must apply StoryMeterGainFactor through the rulebook's gain functions");
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// D5 (user direction 2026-10-11, <c>USER-STORY-METER-SCALE</c>): the Story
    /// meter-gain scale is stated once, in the Story-only, Godot-free
    /// <c>FTT.Combat.StoryCombatRules</c>, reaches the meter only through
    /// <c>PlayerController.StoryMeterGainFactor</c>, and never enters the shared
    /// rulebook or the Fighter sim. Only the campaign-hero branch of
    /// <c>CharacterFactory</c> may switch it on.
    /// </summary>
    [TestCase]
    public void TheStoryMeterScaleLivesInStoryCombatRulesAndNeverReachesTheSimOrTheSharedRulebook() {
        var issues = new List<string>();

        string simRoot = Absolute("scripts/FighterSim");
        string[] simFiles = Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories);
        if (simFiles.Length <= 10) issues.Add($"the FighterSim walk found only {simFiles.Length} files");
        foreach (string path in simFiles) {
            string file = "scripts/FighterSim/" + Path.GetRelativePath(simRoot, path).Replace('\\', '/');
            string code = Code(file);
            foreach (string forbidden in new[] {
                         "StoryCombatRules", "MeterGainScale", "MeterGainFactor", "AppliesStoryMeterGainScale",
                         "StoryMeterGainFactor", "StoryUltimateBuildRateMultiplier", "FlatEffectScale", "FlatMeterScale" }) {
                if (code.Contains(forbidden, System.StringComparison.Ordinal)) {
                    issues.Add($"{file} names the Story-only {forbidden}");
                }
            }
        }

        string rulebook = Code(RulebookFile);
        foreach (string forbidden in new[] {
                     "StoryCombatRules", "MeterGainScale", "MeterGainFactor", "AppliesStoryMeterGainScale" }) {
            if (rulebook.Contains(forbidden, System.StringComparison.Ordinal)) {
                issues.Add($"{RulebookFile} names the Story-only {forbidden}");
            }
        }

        List<string> factor = Statements(Code("scripts/Characters/PlayerController.cs"))
            .Where(statement => statement.Contains("StoryMeterGainFactor =>", System.StringComparison.Ordinal))
            .ToList();
        if (factor.Count != 1
            || !factor[0].Contains("StoryCombatRules.MeterGainFactor(", System.StringComparison.Ordinal)
            || !factor[0].Contains("AppliesStoryMeterGainScale", System.StringComparison.Ordinal)) {
            issues.Add("PlayerController.StoryMeterGainFactor must be StoryCombatRules.MeterGainFactor(AppliesStoryMeterGainScale, …); found: "
                + string.Join(" ; ", factor.Select(Collapse)));
        }

        var writers = new List<string>();
        foreach (string path in Directory.GetFiles(Absolute("scripts"), "*.cs", SearchOption.AllDirectories)) {
            string file = "scripts/" + Path.GetRelativePath(Absolute("scripts"), path).Replace('\\', '/');
            foreach (string statement in Statements(Code(file))) {
                if (Regex.IsMatch(statement, @"\bAppliesStoryMeterGainScale\s*=\s*true\b")) {
                    writers.Add($"{file}: {Collapse(statement)}");
                }
            }
        }
        if (writers.Count != 1
            || !writers[0].StartsWith("scripts/Characters/CharacterFactory.cs:", System.StringComparison.Ordinal)) {
            issues.Add("Only CharacterFactory's campaign-hero branch may switch the D5 scale on; writers: "
                + string.Join(" ; ", writers));
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Meter and Defy update (plan 1.2): every Story-only identifier of the Momentum gauge and the
    /// flat-effect scaling stays out of the Fighter simulation, which has its own integer views.
    /// </summary>
    [TestCase]
    public void NothingUnderTheFighterSimNamesAStoryOnlyMomentumOrScaleIdentifier() {
        var issues = new List<string>();
        string simRoot = Absolute("scripts/FighterSim");
        string[] simFiles = Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories);
        if (simFiles.Length <= 10) issues.Add($"the FighterSim walk found only {simFiles.Length} files");
        foreach (string path in simFiles) {
            string file = "scripts/FighterSim/" + Path.GetRelativePath(simRoot, path).Replace('\\', '/');
            string code = Code(file);
            foreach (string forbidden in new[] {
                         "StoryCombatRules", "MeterGainScale", "FlatEffectScale", "FlatMeterScale", "ScaleStoryFlatMeter",
                         "StoryMeterGainFactor", "MomentumRegen", "MomentumRegenMultiplier", "LegacyUnlockSchedule" }) {
                // Whole identifiers: the shared BasicComboRules.MomentumRegenUnitsPerFrame is not the Story multiplier.
                if (Regex.IsMatch(code, @"\b" + forbidden + @"\b")) {
                    issues.Add($"{file} names the Story-only {forbidden}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // === Source helpers =================================================

    private static string Absolute(string relative) => ProjectSettings.GlobalizePath("res://" + relative);

    private static string Code(string relative) => StripCommentsAndStrings(File.ReadAllText(Absolute(relative)));

    private static IEnumerable<string> Statements(string code) =>
        code.Split(new[] { ';', '{', '}' }).Where(statement => !string.IsNullOrWhiteSpace(statement));

    private static string Collapse(string statement) {
        string collapsed = Regex.Replace(statement, @"\s+", " ").Trim();
        return collapsed.Length > 200 ? collapsed[..200] : collapsed;
    }

    /// <summary>
    /// Drops <c>//</c> and <c>/* */</c> comments and empties string and char
    /// literals (regular, verbatim and interpolated), so a scan reads code, not
    /// prose or paths such as <c>"res://…"</c>.
    /// </summary>
    private static string StripCommentsAndStrings(string source) {
        var output = new StringBuilder(source.Length);
        int index = 0;
        int length = source.Length;
        while (index < length) {
            char c = source[index];
            if (c == '/' && index + 1 < length && source[index + 1] == '/') {
                int end = source.IndexOf('\n', index);
                index = end < 0 ? length : end;
                continue;
            }
            if (c == '/' && index + 1 < length && source[index + 1] == '*') {
                int end = source.IndexOf("*/", index + 2, System.StringComparison.Ordinal);
                index = end < 0 ? length : end + 2;
                output.Append(' ');
                continue;
            }
            if (c == '\'') {
                int end = index + 1;
                while (end < length && source[end] != '\'') end += source[end] == '\\' ? 2 : 1;
                output.Append("' '");
                index = end + 1;
                continue;
            }
            if (c == '"' || ((c == '@' || c == '$') && index + 1 < length && "\"@$".IndexOf(source[index + 1]) >= 0)) {
                int start = index;
                bool verbatim = false;
                while (start < length && (source[start] == '@' || source[start] == '$')) {
                    if (source[start] == '@') verbatim = true;
                    start++;
                }
                int end = start + 1;
                while (end < length) {
                    if (verbatim) {
                        if (source[end] == '"') {
                            if (end + 1 < length && source[end + 1] == '"') { end += 2; continue; }
                            break;
                        }
                        end++;
                    } else {
                        if (source[end] == '\\') { end += 2; continue; }
                        if (source[end] == '"' || source[end] == '\n') break;
                        end++;
                    }
                }
                output.Append("\"\"");
                index = end + 1;
                continue;
            }
            output.Append(c);
            index++;
        }
        return output.ToString();
    }
}
