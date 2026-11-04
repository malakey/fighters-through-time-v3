using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-CAP (gameplay review 2026-11-03, rulings 3a+++ and 3a++++): every hero's tenth Resonance node is a Major of
/// tier 4 at 375 dust behind two of that hero's Majors, for 1,350 in all. Its perk is Story only, the respec
/// refunds it, the dormant gate reads it and the tier mark reuses tier 3's art.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CapstoneContentTests {
    private const int CapstoneCost = 375;

    private static readonly Dictionary<string, string> PerkByHero = new(StringComparer.Ordinal) {
        ["joan"] = CapstoneRules.BannerOfOrleansPerkKey,
        ["einstein"] = CapstoneRules.UnifiedFieldPerkKey,
        ["leonardo"] = CapstoneRules.CodexAtlanticusPerkKey,
        ["lincoln"] = CapstoneRules.EmancipationPerkKey,
        ["cleopatra"] = CapstoneRules.DynastysCursePerkKey,
        ["tesla"] = CapstoneRules.WorldSystemPerkKey,
        ["shakespeare"] = CapstoneRules.CurtainCallPerkKey,
        ["mozart"] = CapstoneRules.SymphonyNo41PerkKey,
        ["tubman"] = CapstoneRules.FreedomLinePerkKey
    };

    private static ResonanceNodeData Capstone(ResonanceGridData grid) {
        foreach (ResonanceNodeData node in grid.Nodes) if (node.Tier == CapstoneRules.Tier) return node;
        return null;
    }

    [TestCase]
    public void EveryHeroHasOneTierFourMajorCosting375BehindTwoOfTheirMajors() {
        var issues = new List<string>();
        AssertThat(PerkByHero.Count).IsEqual(CharacterRoster.IDs.Count);
        foreach (string hero in CharacterRoster.IDs) {
            ResonanceGridData grid = ResonanceProgression.LoadGrid(hero);
            int total = 0, capstones = 0;
            foreach (ResonanceNodeData node in grid.Nodes) {
                total += node.UnlockCost;
                if (node.Tier == CapstoneRules.Tier) capstones++;
            }
            if (grid.Nodes.Length != 10 || capstones != 1 || total != 1350) {
                issues.Add($"{hero}: {grid.Nodes.Length} nodes, {capstones} capstones, {total} dust");
                continue;
            }
            ResonanceNodeData capstone = Capstone(grid);
            if (capstone.Type != ResonanceNodeType.Major) issues.Add($"{capstone.NodeID}: type {capstone.Type}");
            if (capstone.UnlockCost != CapstoneCost) issues.Add($"{capstone.NodeID}: costs {capstone.UnlockCost}");
            if (capstone.PrerequisiteMode != PrerequisiteMode.All) issues.Add($"{capstone.NodeID}: must be All-of");
            if (capstone.AbilityModifierKey != PerkByHero[hero]) issues.Add($"{capstone.NodeID}: perk {capstone.AbilityModifierKey}");
            if (capstone.PrerequisiteNodeIDs?.Length != 2) {
                issues.Add($"{capstone.NodeID}: {capstone.PrerequisiteNodeIDs?.Length} prerequisites");
                continue;
            }
            foreach (string id in capstone.PrerequisiteNodeIDs) {
                ResonanceNodeData prerequisite = Array.Find(grid.Nodes, node => node.NodeID == id);
                if (prerequisite?.Type != ResonanceNodeType.Major || prerequisite.Tier != 3) {
                    issues.Add($"{capstone.NodeID}: prerequisite {id} is not a tier 3 Major");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheTierMarkDrawsACapstoneWithTierThreesArt() {
        AssertThat(ResonanceTierMark.PointsFor(CapstoneRules.Tier)).IsEqual(ResonanceTierMark.PointsFor(3));
        AssertThat(ResonanceTierMark.FillFor(CapstoneRules.Tier)).IsEqual(ResonanceTierMark.FillFor(3));
    }

    [TestCase]
    public void ACapstoneNeedsBothItsMajorsAndItsPerkResolvesOnlyOnTheStoryPath() {
        var issues = new List<string>();
        foreach (string hero in CharacterRoster.IDs) {
            ResonanceGridData grid = ResonanceProgression.LoadGrid(hero);
            ResonanceNodeData capstone = Capstone(grid);
            if (ResonanceProgression.PrerequisitesSatisfied(capstone, new List<string> { capstone.PrerequisiteNodeIDs[0] })) {
                issues.Add($"{capstone.NodeID}: one Major was enough");
            }
            if (!ResonanceProgression.PrerequisitesSatisfied(capstone, new List<string>(capstone.PrerequisiteNodeIDs))) {
                issues.Add($"{capstone.NodeID}: both Majors were not enough");
            }
            var save = new StorySaveData { SelectedCharacterID = hero };
            save.GridProgress[hero] = new List<string> { capstone.NodeID };
            if (!ResonanceProgression.HasUnlockedAbilityModifier(grid, save, capstone.AbilityModifierKey)) {
                issues.Add($"{capstone.NodeID}: the perk does not resolve");
            }
            // Fighter's seam: a normalized build carries no perk at all.
            PlayerController fighter = CharacterFactory.CreateCharacter(hero, 0, applyStoryProgression: false);
            if (fighter.HasStoryPerk(capstone.AbilityModifierKey)) issues.Add($"{hero}: a normalized build holds the perk");
            fighter.Free();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheRespecRefundsTheCapstoneAndTheDormantGateRefusesItUntilItsSlotIsRestored() {
        Func<string, string, bool> originalProbe = ResonanceProgression.AbilityUnlockProbe;
        try {
            var issues = new List<string>();
            foreach (string hero in CharacterRoster.IDs) {
                ResonanceGridData grid = ResonanceProgression.LoadGrid(hero);
                ResonanceNodeData capstone = Capstone(grid);
                var save = new StorySaveData { SelectedCharacterID = hero };
                save.DepositedChronalDust[hero] = 1350;
                ResonanceProgression.AbilityUnlockProbe = (_, _) => true;
                // Buy everything through the production path, in dependency order, the capstone last.
                var bought = new List<string>();
                for (int pass = 0; pass < 12 && bought.Count < 9; pass++) {
                    foreach (ResonanceNodeData node in grid.Nodes) {
                        if (node == capstone || bought.Contains(node.NodeID)) continue;
                        if (ResonanceProgression.TryUnlock(grid, save, node.NodeID) == ResonanceUnlockResult.Unlocked) bought.Add(node.NodeID);
                    }
                }
                if (bought.Count != 9) issues.Add($"{hero}: bought {bought.Count} of the nine");
                if (!string.IsNullOrEmpty(capstone.GatedAbilityID)) {
                    ResonanceProgression.AbilityUnlockProbe = (_, slot) => slot != capstone.GatedAbilityID;
                    ResonanceUnlockResult dormant = ResonanceProgression.EvaluateUnlock(grid, save, capstone.NodeID);
                    if (dormant != ResonanceUnlockResult.AbilityLocked) issues.Add($"{capstone.NodeID}: dormant gate gave {dormant}");
                }
                ResonanceProgression.AbilityUnlockProbe = (_, _) => true;
                ResonanceUnlockResult result = ResonanceProgression.TryUnlock(grid, save, capstone.NodeID);
                if (result != ResonanceUnlockResult.Unlocked) issues.Add($"{capstone.NodeID}: {result}");
                if (save.DepositedChronalDust[hero] != 0) issues.Add($"{hero}: {save.DepositedChronalDust[hero]} left over");
                if (ResonanceProgression.CalculateSpentDust(grid, save) != 1350) issues.Add($"{hero}: spent is not 1350");
                if (ResonanceProgression.RespecAll(grid, save) != 1350) issues.Add($"{hero}: the respec did not refund 1350");
                if (save.DepositedChronalDust[hero] != 1350) issues.Add($"{hero}: {save.DepositedChronalDust[hero]} after the respec");
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        } finally {
            ResonanceProgression.AbilityUnlockProbe = originalProbe;
        }
    }

    /// <summary>Story/Fighter isolation, structurally. Comments are stripped, so prose may explain the boundary.</summary>
    [TestCase]
    public void NothingUnderTheFighterSimNamesACapstone() {
        var forbidden = new Regex(@"\b(CapstoneRules|SymphonyStack|NoteSymphonySpecial|RefundMomentumPip)\b");
        AssertThat(forbidden.IsMatch("CapstoneRules.Tier")).IsTrue();
        string root = ProjectSettings.GlobalizePath("res://scripts/FighterSim");
        var offenders = new List<string>();
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)) {
            string source = Regex.Replace(File.ReadAllText(path), @"//.*?$|/\*.*?\*/", "",
                RegexOptions.Singleline | RegexOptions.Multiline);
            bool named = forbidden.IsMatch(source);
            foreach (string perk in PerkByHero.Values) named |= source.Contains($"\"{perk}\"", StringComparison.Ordinal);
            if (named) offenders.Add(Path.GetFileName(path));
        }
        AssertThat(string.Join(", ", offenders)).IsEqual("");
    }
}
