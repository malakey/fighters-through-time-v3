using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// 2026-10-11 grid repairs. Guards that keep every bought Resonance node doing
/// something, and keep the grid out of the deterministic sim:
/// <list type="bullet">
/// <item>every <c>StatModifierKey</c>, <c>AbilityScope</c> and perk key
/// (<c>AbilityModifierKey</c>) authored across the roster's
/// <c>resources/Resonance/*_grid.tres</c> has a production reader under
/// <c>scripts/</c>. Three nodes shipped with none: Tesla's Minor Conductive Hold
/// (a perk key spelled differently from its check, and a scoped lane nothing
/// asked for), Tesla's Minor Pulse Radius (a fixed pulse radius) and, until the
/// meter rulebook step, Mozart's Minor Resonance;</item>
/// <item>every scoped cooldown minor resolves to a cooldown multiplier below 1.0
/// on a slot that has a cooldown. Joan's, Mozart's and Lincoln's resolved to
/// 1.1 and lengthened their Special;</item>
/// <item>nothing under <c>scripts/FighterSim/</c> names a Resonance or Story-perk
/// type (Story/Fighter isolation).</item>
/// </list>
/// The reader scans strip comments first, so prose never counts as a reader.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceReaderCoverageTests {
    private static readonly IReadOnlyList<string> CharacterIDs = CharacterRoster.IDs;

    /// <summary>Files that declare or resolve the lanes. A resolver is not a reader.</summary>
    private static readonly HashSet<string> ResolverFiles = new(StringComparer.Ordinal) {
        "ResonanceProgression.cs", "ResonanceGridData.cs", "ResonanceNodeData.cs"
    };

    /// <summary>
    /// The <see cref="PlayerController"/> member each character-wide lane lands
    /// on (<c>CharacterFactory</c> copies the resolved profile there). A lane is
    /// read when that member is read anywhere in production code.
    /// </summary>
    private static readonly Dictionary<string, string> CharacterWideCarriers = new(StringComparer.Ordinal) {
        [ResonanceStatKeys.MaxHP] = nameof(PlayerController.StoryMaxHPBonus),
        [ResonanceStatKeys.BlockCharges] = nameof(PlayerController.StoryBlockChargeBonus),
        [ResonanceStatKeys.MoveSpeed] = nameof(PlayerController.StoryMoveSpeedMultiplier),
        [ResonanceStatKeys.JumpForce] = nameof(PlayerController.StoryJumpForceMultiplier),
        [ResonanceStatKeys.BasicAttackDamage] = nameof(PlayerController.StoryBasicDamageMultiplier),
        [ResonanceStatKeys.SpecialDamage] = nameof(PlayerController.StorySpecialDamageMultiplier),
        [ResonanceStatKeys.CooldownReduction] = nameof(PlayerController.StoryCooldownMultiplier),
        [ResonanceStatKeys.AttackRange] = nameof(PlayerController.StoryAttackRangeMultiplier),
        [ResonanceStatKeys.ComboSpeed] = nameof(PlayerController.StoryComboSpeedMultiplier),
        [ResonanceStatKeys.BlockRecovery] = nameof(PlayerController.StoryBlockRecoveryMultiplier),
        [ResonanceStatKeys.KnockbackForce] = nameof(PlayerController.StoryKnockbackMultiplier),
        [ResonanceStatKeys.ProjectileSpeed] = nameof(PlayerController.StoryProjectileSpeedMultiplier),
        [ResonanceStatKeys.ProjectileDamage] = nameof(PlayerController.StoryProjectileDamageMultiplier),
        [ResonanceStatKeys.GlideSpeed] = nameof(PlayerController.StoryGlideSpeedMultiplier),
        [ResonanceStatKeys.GlideDuration] = nameof(PlayerController.StoryGlideDurationMultiplier),
        [ResonanceStatKeys.ZoneRadius] = nameof(PlayerController.StoryZoneRadiusMultiplier),
        [ResonanceStatKeys.ZoneDuration] = nameof(PlayerController.StoryZoneDurationMultiplier),
        [ResonanceStatKeys.PersistentDuration] = nameof(PlayerController.StoryPersistentDurationMultiplier),
        [ResonanceStatKeys.PersistentRange] = nameof(PlayerController.StoryPersistentRangeMultiplier),
        [ResonanceStatKeys.PersistentHealth] = nameof(PlayerController.StoryPersistentHealthMultiplier),
        [ResonanceStatKeys.StatusDuration] = nameof(PlayerController.StoryStatusDurationMultiplier),
        [ResonanceStatKeys.StatusDamage] = nameof(PlayerController.StoryStatusIntensityMultiplier),
        [ResonanceStatKeys.RallyEchoFraction] = nameof(PlayerController.StoryRallyEchoFractionMultiplier),
        [ResonanceStatKeys.UltimateBuildRate] = nameof(PlayerController.StoryUltimateBuildRateMultiplier),
        [ResonanceStatKeys.ExtractorDamage] = nameof(PlayerController.StoryExtractorDamageMultiplier)
    };

    /// <summary>The identifiers <c>scripts/FighterSim/</c> may never name.</summary>
    private static readonly Regex StoryProgressionToken = new(
        @"\b(ResonanceProgression|ResonanceGridData|ResonanceNodeData|ResonanceStatKeys|StoryStatProfile"
        + @"|ScopedStoryStats|ScopedStatKey|StoryScopedStats|StoryScoped|StoryAbilityPerks|HasStoryPerk"
        + @"|Story\w*Multiplier)\b");

    [TestCase]
    public void EveryAuthoredCharacterWideStatKeyHasAProductionReader() {
        Dictionary<string, string> sources = ProductionSources();
        var issues = new List<string>();
        int checkedNodes = 0;
        foreach ((string characterID, ResonanceNodeData node) in AuthoredNodes()) {
            if (string.IsNullOrWhiteSpace(node.StatModifierKey) || !string.IsNullOrWhiteSpace(node.AbilityScope)) continue;
            checkedNodes++;
            if (!CharacterWideCarriers.TryGetValue(node.StatModifierKey, out string carrier)) {
                issues.Add($"{node.NodeID}: lane '{node.StatModifierKey}' has no known PlayerController carrier");
                continue;
            }
            if (!IsMemberRead(sources, carrier)) {
                issues.Add($"{node.NodeID} ({characterID}): '{node.StatModifierKey}' lands on {carrier}, which nothing reads");
            }
        }
        AssertThat(checkedNodes > 0).IsTrue();
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>The file that holds the one Story ability-damage composition (PlayerController.StoryHitboxContactDamageScale).</summary>
    private const string SharedAbilityDamageComposition = "Characters/PlayerController.cs";

    [TestCase]
    public void EveryAuthoredAbilityScopeHasAProductionReader() {
        Dictionary<string, string> sources = ProductionSources();
        List<ScopedRead> reads = ScopedReads(sources);
        AssertThat(reads.Count > 0).IsTrue();
        var issues = new List<string>();
        int checkedNodes = 0;
        foreach ((string characterID, ResonanceNodeData node) in AuthoredNodes()) {
            if (string.IsNullOrWhiteSpace(node.AbilityScope)) continue;
            checkedNodes++;
            HashSet<string> abilityIDs = AbilityIDs(characterID);
            string kitPrefix = char.ToUpperInvariant(characterID[0]) + characterID.Substring(1);
            bool read = false;
            foreach (ScopedRead candidate in reads) {
                if (!string.Equals(candidate.StatKey, node.StatModifierKey, StringComparison.Ordinal)) continue;
                // A reader that names the scope outright.
                if (candidate.ScopeValues.Contains(node.AbilityScope)) {
                    read = true;
                    break;
                }
                // A reader keyed by the casting ability's ID: it serves this scope
                // only if the scope IS one of this character's ability IDs and the
                // reader is shared combat code or this character's own kit. Since
                // 2026-10-11 the shared AbilityDamage reader is the one Story
                // ability-damage composition on PlayerController (every kit hit and
                // every Hitbox payload goes through it; KitAlignmentContractTests
                // keeps the kits on it).
                if (candidate.KeyedByAbilityID
                    && abilityIDs.Contains(node.AbilityScope)
                    && (candidate.File.StartsWith("Combat/", StringComparison.Ordinal)
                        || candidate.File == SharedAbilityDamageComposition
                        || Path.GetFileName(candidate.File).StartsWith(kitPrefix, StringComparison.Ordinal))) {
                    read = true;
                    break;
                }
            }
            if (!read) {
                issues.Add($"{node.NodeID} ({characterID}): nothing reads StoryScoped({node.StatModifierKey}, {node.AbilityScope})");
            }
        }
        AssertThat(checkedNodes > 0).IsTrue();
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAuthoredPerkKeyHasAProductionReader() {
        Dictionary<string, string> sources = ProductionSources();
        var perkIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var perkLiterals = new HashSet<string>(StringComparer.Ordinal);
        var constantsByValue = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var perkCall = new Regex(@"(?:HasStoryPerk|StoryAbilityPerks\.Contains)\(([^()]*)\)");
        var constantDeclaration = new Regex(@"const\s+string\s+(\w+)\s*=\s*""([^""]*)""");
        foreach (string text in sources.Values) {
            foreach (Match match in perkCall.Matches(text)) {
                string argument = match.Groups[1].Value.Trim();
                Match literal = Regex.Match(argument, @"^""([^""]*)""$");
                if (literal.Success) {
                    perkLiterals.Add(literal.Groups[1].Value);
                } else {
                    int dot = argument.LastIndexOf('.');
                    perkIdentifiers.Add(dot >= 0 ? argument.Substring(dot + 1) : argument);
                }
            }
            foreach (Match match in constantDeclaration.Matches(text)) {
                if (!constantsByValue.TryGetValue(match.Groups[2].Value, out HashSet<string> names)) {
                    names = new HashSet<string>(StringComparer.Ordinal);
                    constantsByValue[match.Groups[2].Value] = names;
                }
                names.Add(match.Groups[1].Value);
            }
        }
        var issues = new List<string>();
        int checkedNodes = 0;
        foreach ((string characterID, ResonanceNodeData node) in AuthoredNodes()) {
            if (string.IsNullOrWhiteSpace(node.AbilityModifierKey)) continue;
            checkedNodes++;
            bool read = perkLiterals.Contains(node.AbilityModifierKey);
            if (!read && constantsByValue.TryGetValue(node.AbilityModifierKey, out HashSet<string> names)) {
                foreach (string name in names) {
                    if (perkIdentifiers.Contains(name)) {
                        read = true;
                        break;
                    }
                }
            }
            if (!read) issues.Add($"{node.NodeID} ({characterID}): nothing checks perk '{node.AbilityModifierKey}'");
        }
        // 27 Majors + 9 traversal flags at least.
        AssertThat(checkedNodes >= 36).IsTrue();
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryCooldownMinorResolvesBelowOneOnACooldownBearingSlot() {
        var issues = new List<string>();
        int scopedCooldownNodes = 0;
        foreach ((string characterID, ResonanceNodeData node) in AuthoredNodes()) {
            if (node.StatModifierKey != ResonanceStatKeys.CooldownReduction) continue;
            ResonanceGridData grid = ResonanceProgression.LoadGrid(characterID);
            var save = new StorySaveData { SelectedCharacterID = characterID };
            save.GridProgress[characterID] = new List<string> { node.NodeID };
            StoryStatProfile profile = ResonanceProgression.Resolve(grid, save);
            if (string.IsNullOrWhiteSpace(node.AbilityScope)) {
                if (!(profile.CooldownMultiplier < 1f)) {
                    issues.Add($"{node.NodeID}: character-wide cooldown resolves to {profile.CooldownMultiplier}");
                }
                continue;
            }
            scopedCooldownNodes++;
            float scoped = profile.GetScoped(ResonanceStatKeys.CooldownReduction, node.AbilityScope);
            if (!(scoped < 1f) || !(scoped >= ResonanceProgression.MinimumCooldownMultiplier)) {
                issues.Add($"{node.NodeID}: scoped cooldown resolves to {scoped}, not into [0.05, 1)");
            }
            if (!(profile.CooldownMultiplier == 1f)) {
                issues.Add($"{node.NodeID}: a scoped reduction leaked into the character-wide lane");
            }
            AbilityData ability = AbilityByID(characterID, node.AbilityScope);
            if (ability == null) {
                issues.Add($"{node.NodeID}: scope '{node.AbilityScope}' is not one of {characterID}'s abilities");
            } else if (ability.Slot == AbilitySlot.Ultimate) {
                issues.Add($"{node.NodeID}: '{node.AbilityScope}' is the Ultimate, which arms no cooldown");
            }
        }
        // Joan's Piercing, Mozart's Wave and Lincoln's Splitting minors.
        AssertThat(scopedCooldownNodes >= 3).IsTrue();
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Story/Fighter isolation, structurally: nothing in the deterministic sim
    /// may name the Resonance resolver, its data types, the scoped lanes, the
    /// Story perk set or any Story stat multiplier. Comments are stripped
    /// first, so prose that explains the boundary is allowed.
    /// </summary>
    [TestCase]
    public void NothingUnderTheFighterSimNamesAResonanceOrStoryPerkType() {
        string simRoot = ProjectSettings.GlobalizePath("res://scripts/FighterSim");
        string[] files = Directory.GetFiles(simRoot, "*.cs", SearchOption.AllDirectories);
        AssertThat(files.Length > 0).IsTrue();
        var offenders = new List<string>();
        foreach (string path in files) {
            string[] lines = StripComments(File.ReadAllText(path)).Split('\n');
            for (int index = 0; index < lines.Length; index++) {
                Match match = StoryProgressionToken.Match(lines[index]);
                if (match.Success) offenders.Add($"{Path.GetFileName(path)}:{index + 1} names {match.Value}");
            }
        }
        // The scan must be able to see what it forbids.
        AssertThat(StoryProgressionToken.IsMatch("x.StoryScoped(\"AbilityRange\", id)")).IsTrue();
        AssertThat(StoryProgressionToken.IsMatch("float y = StoryCooldownMultiplier;")).IsTrue();
        AssertThat(StoryProgressionToken.IsMatch(StripComments("// HasStoryPerk(key)"))).IsFalse();
        if (offenders.Count > 0) {
            AssertThat("scripts/FighterSim must never read Story progression: " + string.Join(", ", offenders))
                .IsEqual("");
        }
    }

    // ---- helpers ------------------------------------------------------------

    private readonly struct ScopedRead {
        public readonly string File;
        public readonly string StatKey;
        public readonly HashSet<string> ScopeValues;
        public readonly bool KeyedByAbilityID;

        public ScopedRead(string file, string statKey, HashSet<string> scopeValues, bool keyedByAbilityID) {
            File = file;
            StatKey = statKey;
            ScopeValues = scopeValues;
            KeyedByAbilityID = keyedByAbilityID;
        }
    }

    private static IEnumerable<(string CharacterID, ResonanceNodeData Node)> AuthoredNodes() {
        foreach (string characterID in CharacterIDs) {
            ResonanceGridData grid = ResonanceProgression.LoadGrid(characterID);
            AssertThat(grid).IsNotNull();
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null) yield return (characterID, node);
            }
        }
    }

    private static AbilityData[] Abilities(string characterID) {
        CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
        AssertThat(data).IsNotNull();
        return new AbilityData[] {
            data.SpecialAttackOne, data.SpecialAttackTwo, data.MovementAbility, data.UltimateAttack
        };
    }

    private static HashSet<string> AbilityIDs(string characterID) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (AbilityData ability in Abilities(characterID)) {
            if (ability != null && !string.IsNullOrWhiteSpace(ability.AbilityID)) ids.Add(ability.AbilityID);
        }
        return ids;
    }

    private static AbilityData AbilityByID(string characterID, string abilityID) {
        foreach (AbilityData ability in Abilities(characterID)) {
            if (ability != null && ability.AbilityID == abilityID) return ability;
        }
        return null;
    }

    /// <summary>
    /// Every production script, comment-stripped, keyed by its path relative to
    /// <c>scripts/</c>. The sim is excluded (it may never read a Story lane, and
    /// <see cref="NothingUnderTheFighterSimNamesAResonanceOrStoryPerkType"/>
    /// proves it does not), as are the playtest bots and the resolver files.
    /// </summary>
    private static Dictionary<string, string> ProductionSources() {
        string root = ProjectSettings.GlobalizePath("res://scripts");
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)) {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.StartsWith("FighterSim/", StringComparison.Ordinal)
                || relative.StartsWith("Diagnostics/", StringComparison.Ordinal)
                || ResolverFiles.Contains(Path.GetFileName(path))) continue;
            sources[relative] = StripComments(File.ReadAllText(path));
        }
        AssertThat(sources.Count > 0).IsTrue();
        return sources;
    }

    /// <summary>
    /// True when <paramref name="member"/> is read somewhere: a line naming it
    /// that is neither its declaration nor a plain or compound assignment to it.
    /// </summary>
    private static bool IsMemberRead(Dictionary<string, string> sources, string member) {
        var named = new Regex($@"\b{Regex.Escape(member)}\b");
        var declaredOrWritten = new Regex($@"\b{Regex.Escape(member)}\s*(\{{|=(?![=>])|\+=|-=|\*=|/=)");
        foreach (string text in sources.Values) {
            foreach (string line in text.Split('\n')) {
                if (!named.IsMatch(line)) continue;
                if (declaredOrWritten.IsMatch(line)) continue;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Every <c>StoryScoped(key, scope)</c> call: the key resolved from a string
    /// literal or a <see cref="ResonanceStatKeys"/> constant, the scope's literal
    /// and constant values, and whether the scope is the casting ability's ID.
    /// </summary>
    private static List<ScopedRead> ScopedReads(Dictionary<string, string> sources) {
        var reads = new List<ScopedRead>();
        var call = new Regex(@"\bStoryScoped\(");
        foreach (KeyValuePair<string, string> source in sources) {
            string text = source.Value;
            foreach (Match match in call.Matches(text)) {
                List<string> arguments = SplitArguments(text, match.Index + match.Length);
                if (arguments.Count < 2) continue;
                HashSet<string> keys = Values(arguments[0]);
                HashSet<string> scopes = Values(arguments[1]);
                bool keyedByAbilityID = Regex.IsMatch(arguments[1], @"\b(AbilityID|abilityID|AttackID)\b");
                foreach (string key in keys) {
                    reads.Add(new ScopedRead(source.Key, key, scopes, keyedByAbilityID));
                }
            }
        }
        return reads;
    }

    /// <summary>The top-level arguments of a call whose '(' ends just before <paramref name="start"/>.</summary>
    private static List<string> SplitArguments(string text, int start) {
        var arguments = new List<string>();
        var current = new StringBuilder();
        int depth = 1;
        bool inString = false;
        for (int index = start; index < text.Length; index++) {
            char c = text[index];
            if (inString) {
                current.Append(c);
                if (c == '\\' && index + 1 < text.Length) {
                    current.Append(text[++index]);
                } else if (c == '"') {
                    inString = false;
                }
                continue;
            }
            if (c == '"') {
                inString = true;
                current.Append(c);
                continue;
            }
            if (c == '(') depth++;
            if (c == ')') {
                depth--;
                if (depth == 0) break;
            }
            if (c == ',' && depth == 1) {
                arguments.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        arguments.Add(current.ToString());
        return arguments;
    }

    /// <summary>The string values an argument names: its literals and any ResonanceStatKeys constants.</summary>
    private static HashSet<string> Values(string argument) {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match literal in Regex.Matches(argument, @"""([^""\\]*)""")) {
            values.Add(literal.Groups[1].Value);
        }
        foreach (Match constant in Regex.Matches(argument, @"\bResonanceStatKeys\.(\w+)")) {
            FieldInfo field = typeof(ResonanceStatKeys).GetField(
                constant.Groups[1].Value, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.IsLiteral && field.GetRawConstantValue() is string value) values.Add(value);
        }
        return values;
    }

    /// <summary>
    /// Removes line, block and doc comments while leaving string and char
    /// literals intact (a <c>//</c> inside a string is not a comment). Newlines
    /// survive, so line numbers still match the file.
    /// </summary>
    private static string StripComments(string text) {
        var output = new StringBuilder(text.Length);
        int index = 0;
        while (index < text.Length) {
            char c = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (c == '/' && next == '/') {
                while (index < text.Length && text[index] != '\n') index++;
                continue;
            }
            if (c == '/' && next == '*') {
                index += 2;
                while (index < text.Length && !(text[index] == '*' && index + 1 < text.Length && text[index + 1] == '/')) {
                    if (text[index] == '\n') output.Append('\n');
                    index++;
                }
                index += 2;
                continue;
            }
            if (c == '"') {
                bool verbatim = index > 0 && (text[index - 1] == '@'
                    || (text[index - 1] == '$' && index > 1 && text[index - 2] == '@'));
                output.Append(c);
                index++;
                while (index < text.Length) {
                    char d = text[index];
                    output.Append(d);
                    index++;
                    if (verbatim) {
                        if (d != '"') continue;
                        if (index < text.Length && text[index] == '"') {
                            output.Append('"');
                            index++;
                            continue;
                        }
                        break;
                    }
                    if (d == '\\' && index < text.Length) {
                        output.Append(text[index]);
                        index++;
                        continue;
                    }
                    if (d == '"' || d == '\n') break;
                }
                continue;
            }
            if (c == '\'') {
                output.Append(c);
                index++;
                while (index < text.Length) {
                    char d = text[index];
                    output.Append(d);
                    index++;
                    if (d == '\\' && index < text.Length) {
                        output.Append(text[index]);
                        index++;
                        continue;
                    }
                    if (d == '\'' || d == '\n') break;
                }
                continue;
            }
            output.Append(c);
            index++;
        }
        return output.ToString();
    }
}
