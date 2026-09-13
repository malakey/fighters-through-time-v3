using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A4. The V7.6 grid HYGIENE rules, enforced against the authored
/// content rather than against prose.
///
/// <para>A Resonance minor may never touch stun or hitstun duration (the V7.4
/// Stagger Discipline owns those numbers), may never grant rewind charges, and
/// may never modify the Timeline Integrity drain rate. Grids interact with the
/// V7.6 level timer only INDIRECTLY — bonus damage to a Chronal Extractor is
/// the one sanctioned channel, and all three such effects are Majors.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceHygieneTests {
    private static readonly string[] CharacterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    [TestCase]
    public void NoMinorTouchesStunOrHitstunDuration() {
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.Type == ResonanceNodeType.Major) continue;
                string key = node.StatModifierKey ?? "";
                if (key.Contains("Stun") || key.Contains("Hitstun")) {
                    issues.Add($"{node.NodeID}: {key}");
                }
                // The character-wide StatusDuration lane was the roster's stun
                // minor (the retired tesla_p2 "Minor Stun Duration +15%") and
                // is barred from minors outright; the replacement is the
                // ability-scoped conductive-mark node, which can only ever
                // reach code asking for its exact (key, scope) pair.
                if (key == ResonanceStatKeys.StatusDuration) {
                    issues.Add($"{node.NodeID}: character-wide StatusDuration");
                }
                string scope = node.AbilityScope ?? "";
                if (scope.Contains("stun") || scope.Contains("static_charge")) {
                    issues.Add($"{node.NodeID}: scope {scope}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void NoMinorGrantsRewindChargesOrModifiesTheIntegrityDrainRate() {
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.Type == ResonanceNodeType.Major) continue;
                foreach (string barred in ResonanceStatKeys.BarredFromMinors) {
                    if (node.StatModifierKey == barred) issues.Add($"{node.NodeID}: {barred}");
                }
                string key = node.StatModifierKey ?? "";
                if (key.Contains("Rewind") || key.Contains("Integrity") || key.Contains("Drain")) {
                    issues.Add($"{node.NodeID}: {key}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ExtractorDamageIsDeclaredButUnusedByAnyMinorAndTheThreeRidersAreMajors() {
        // The design declares the lane; recon F §12.6 confirms it is
        // future-proofing, not a missing node. Inventing a minor for it would
        // hand a grid a direct lever on the V7.6 level timer.
        AssertThat(System.Array.IndexOf(ResonanceStatKeys.All, ResonanceStatKeys.ExtractorDamage) >= 0)
            .IsTrue();

        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.StatModifierKey == ResonanceStatKeys.ExtractorDamage) {
                    issues.Add($"{node.NodeID} authors ExtractorDamage");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // The three Extractor effects ride Major perk keys instead, and they
        // all resolve through the one Story chokepoint.
        AssertThat(StoryExtractorDamage.ClockworkOverdriveExtractorMultiplier).IsEqual(2.0f);
        AssertThat(StoryExtractorDamage.KineticSplittingStructureMultiplier).IsEqual(1.5f);
        // A null attacker (an enemy, a hazard, a Fighter-mode body) is neutral:
        // the isolation boundary is a no-op by default.
        AssertThat(StoryExtractorDamage.ExtractorMultiplier(null, "anything")).IsEqual(1f);
        AssertThat(StoryExtractorDamage.ConstructMultiplier(null, "anything")).IsEqual(1f);
    }

    private static ResonanceGridData LoadGrid(string characterID) =>
        AuthoredResources.Load<ResonanceGridData>(
            $"res://resources/Resonance/{characterID}_grid.tres");
}
