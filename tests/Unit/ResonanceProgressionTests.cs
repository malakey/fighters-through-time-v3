using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A4 rewrote this suite wholesale for the V7.6 Resonance grids.
///
/// <para>Every structural assumption the pre-V7.6 suite made is now false:
/// "Tier 1 has no prerequisites" breaks in four grids, "a Major has exactly one
/// prerequisite" breaks for Leonardo's capstone, all three Shakespeare Majors
/// and Pocahontas's Leaf Barrier, and "Tier 2 has exactly one prerequisite"
/// breaks for every Any-of and paired node. The grids are nine unique
/// topologies now, so the suite pins the explicit <c>Tier</c> field, the
/// 50/75/200 = 975 economy, prerequisite-mode validity, the per-character
/// topology signature, the cheapest-route table, the new resolver lanes and the
/// ability gate.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceProgressionTests {
    private static readonly string[] CharacterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    /// <summary>V7.6 locked costs: Tier 1 / Tier 2 / Major. 3 + 3 + 3 = 975.</summary>
    private const int TierOneCost = 50;
    private const int TierTwoCost = 75;
    private const int MajorCost = 200;
    private const int GridTotalCost = 975;

    /// <summary>
    /// The systems a Level 0 hero already has. A ROOT node (zero prerequisites)
    /// must modify one of these and must be ungated, or the first hub visit
    /// shows an empty grid under the V7.6 hidden-node rule.
    /// </summary>
    private static readonly HashSet<string> LevelZeroKeys = new() {
        ResonanceStatKeys.MaxHP,
        ResonanceStatKeys.MoveSpeed,
        ResonanceStatKeys.BasicAttackDamage,
        ResonanceStatKeys.AttackRange,
        ResonanceStatKeys.BlockCharges,
        ResonanceStatKeys.RallyEchoFraction,
        ResonanceStatKeys.UltimateBuildRate,
        ResonanceStatKeys.AbilityDamage
    };

    /// <summary>Recon F §6: the designed cheapest route to a first Major, per character.</summary>
    private static readonly Dictionary<string, int> CheapestFirstMajor = new() {
        ["einstein"] = 325,
        ["joan"] = 350,
        ["leonardo"] = 325,
        ["tesla"] = 325,
        ["shakespeare"] = 475,
        ["mozart"] = 350,
        ["cleopatra"] = 375,
        ["lincoln"] = 325,
        ["pocahontas"] = 325
    };

    // ======================================================================
    // Purchase and evaluation
    // ======================================================================

    [TestCase]
    public void UnlockRequiresDustAndAllPrerequisites() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(175);

        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_all_child"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(125);
        // Still missing the SECOND of the All-of pair.
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_all_child"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_b"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_all_child"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(0);
    }

    [TestCase]
    public void AnyOfPrerequisiteNeedsExactlyOneUnlockedNeighbour() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);

        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_any_child"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_b"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        // One of the two listed prerequisites is enough under Any.
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_any_child"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        // ...and the All-of sibling over the SAME pair is still blocked.
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_all_child"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
    }

    [TestCase]
    public void EvaluateUnlockReportsBlockingReasonWithoutMutatingSave() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(60);

        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_all_child"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "missing_node"))
            .IsEqual(ResonanceUnlockResult.MissingNode);

        // Evaluation is a pure read: nothing was purchased or deducted.
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(60);
        AssertThat(save.GridProgress["einstein"].Count).IsEqual(0);

        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.AlreadyUnlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_root_b"))
            .IsEqual(ResonanceUnlockResult.InsufficientDust);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(10);
    }

    [TestCase]
    public void AGatedNodeRefusesPurchaseUntilItsLegacyAbilityUnlocks() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.Unlocked);

        // No probe installed (A5's Legacy Unlock Schedule absent): gates are
        // transparent, which is the pre-V7.6 behaviour.
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_gated"))
            .IsEqual(ResonanceUnlockResult.Unlocked);

        try {
            ResonanceProgression.AbilityUnlockProbe = (_, slot) => slot != ResonanceAbilitySlots.Special2;
            // The gate reports its OWN reason, ahead of dust and prerequisites,
            // so the UI can draw a dormant star rather than a price tag.
            AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "test_gated"))
                .IsEqual(ResonanceUnlockResult.AbilityLocked);
            AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_gated"))
                .IsEqual(ResonanceUnlockResult.AbilityLocked);
            AssertThat(save.GridProgress["einstein"].Contains("test_gated")).IsFalse();
            AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(450);
        } finally {
            ResonanceProgression.AbilityUnlockProbe = null;
        }
    }

    [TestCase]
    public void GridCannotSpendAnotherCampaignCharactersDust() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);
        save.SelectedCharacterID = "joan";

        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.WrongCharacter);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(500);
    }

    [TestCase]
    public void UnlockedProgressSurvivesSaveEnvelopeRoundTrip() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(125);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.Unlocked);

        // Same persistence path as SaveManager.SaveStorySlot: normalized JSON
        // through the authenticated envelope and back.
        save.Normalize();
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);
        byte[] envelope = SaveEnvelopeCodec.Encode(
            "story", save.SaveVersion, Newtonsoft.Json.JsonConvert.SerializeObject(save), key, 1L);
        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, key, out DecodedSaveEnvelope decoded, out _))
            .IsTrue();
        StorySaveData restored = SaveSchemaMigrator.DeserializeStory(decoded.Json);

        AssertThat(restored.DepositedChronalDust["einstein"]).IsEqual(75);
        AssertThat(restored.GridProgress["einstein"].Contains("test_root_a")).IsTrue();
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, restored, "test_root_a"))
            .IsEqual(ResonanceUnlockResult.AlreadyUnlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, restored, "test_root_b"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
    }

    // ======================================================================
    // Resolver lanes
    // ======================================================================

    [TestCase]
    public void ResolvedModifiersAreStoryOnlyValues() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);
        save.GridProgress["einstein"] = new List<string> { "test_root_a", "test_root_b" };

        StoryStatProfile profile = ResonanceProgression.Resolve(grid, save);
        AssertThat(profile.MaxHPBonus).IsEqual(15);
        AssertThat(profile.MoveSpeedMultiplier).IsEqual(1.02f);
        AssertThat(profile.BasicDamageMultiplier).IsEqual(1f);
    }

    [TestCase]
    public void LockedAndUnauthoredLanesResolveNeutral() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);

        // Nothing unlocked: every lane is its identity value.
        StoryStatProfile empty = ResonanceProgression.Resolve(grid, save);
        AssertThat(empty.MaxHPBonus).IsEqual(0);
        AssertThat(empty.RallyEchoFractionMultiplier).IsEqual(1f);
        AssertThat(empty.UltimateBuildRateMultiplier).IsEqual(1f);
        AssertThat(empty.ExtractorDamageMultiplier).IsEqual(1f);
        AssertThat(empty.ScopedMultipliers.Count).IsEqual(0);
        AssertThat(empty.GetScoped(ResonanceStatKeys.AbilityRange, "relativity_rift")).IsEqual(1f);

        // A node whose stat key nothing routes contributes nothing either.
        save.GridProgress["einstein"] = new List<string> { "test_unknown_key" };
        StoryStatProfile unknown = ResonanceProgression.Resolve(grid, save);
        AssertThat(unknown.MaxHPBonus).IsEqual(0);
        AssertThat(unknown.MoveSpeedMultiplier).IsEqual(1f);
    }

    [TestCase]
    public void TheNewCharacterWideLanesResolveFromTheAuthoredGrids() {
        // RallyEchoFraction: Lincoln's Wrestler's Rally is the roster's largest at +15%.
        ResonanceGridData lincoln = LoadGrid("lincoln");
        StorySaveData lincolnSave = BuildSave(0, "lincoln");
        lincolnSave.GridProgress["lincoln"] = new List<string> { "lincoln_wrestlers_rally" };
        AssertFloat(ResonanceProgression.Resolve(lincoln, lincolnSave).RallyEchoFractionMultiplier)
            .IsEqualApprox(1.15f, 0.0001f);

        // UltimateBuildRate: Mozart's Minor Resonance is the roster's only use.
        ResonanceGridData mozart = LoadGrid("mozart");
        StorySaveData mozartSave = BuildSave(0, "mozart");
        mozartSave.GridProgress["mozart"] = new List<string> { "mozart_resonance" };
        AssertFloat(ResonanceProgression.Resolve(mozart, mozartSave).UltimateBuildRateMultiplier)
            .IsEqualApprox(1.10f, 0.0001f);
    }

    [TestCase]
    public void AbilityScopedLanesResolveOnlyForTheirOwnScope() {
        ResonanceGridData tesla = LoadGrid("tesla");
        StorySaveData save = BuildSave(0, "tesla");
        save.GridProgress["tesla"] = new List<string> { "tesla_coil_duration", "tesla_pulse_radius" };
        StoryStatProfile profile = ResonanceProgression.Resolve(tesla, save);

        // 30 s -> 40 s and +15% radius, each confined to its own ability.
        AssertFloat(profile.GetScoped(ResonanceStatKeys.AbilityDuration, "tesla_coil") * 30f)
            .IsEqualApprox(40f, 0.01f);
        AssertFloat(profile.GetScoped(ResonanceStatKeys.AbilityRange, "tesla_lorentz_pulse"))
            .IsEqualApprox(1.15f, 0.0001f);
        // A different ability, and the character-wide lanes, stay neutral.
        AssertThat(profile.GetScoped(ResonanceStatKeys.AbilityDuration, "tesla_lorentz_pulse")).IsEqual(1f);
        AssertThat(profile.GetScoped(ResonanceStatKeys.AbilityRange, "tesla_coil")).IsEqual(1f);
        AssertThat(profile.PersistentDurationMultiplier).IsEqual(1f);
        AssertThat(profile.StatusDurationMultiplier).IsEqual(1f);
    }

    [TestCase]
    public void TheConductiveHoldScopeNeverRoutesIntoStunDuration() {
        // F07 hygiene: the mark-duration node must not lengthen the Static
        // Charge interrupt. It is scoped, so it can only ever reach code that
        // asks for its exact (key, scope) pair.
        ResonanceGridData tesla = LoadGrid("tesla");
        StorySaveData save = BuildSave(0, "tesla");
        save.GridProgress["tesla"] = new List<string> { "tesla_conductive_hold" };
        StoryStatProfile profile = ResonanceProgression.Resolve(tesla, save);

        AssertFloat(profile.GetScoped(
                ResonanceStatKeys.AbilityDuration,
                ResonanceStatKeys.ConductiveFinisherMarkScope) * 1.5f)
            .IsEqualApprox(2.5f, 0.01f);
        AssertThat(profile.StatusDurationMultiplier).IsEqual(1f);
    }

    [TestCase]
    public void CollectUnlockedAbilityModifiersReturnsEveryUnlockedPerkKeyIncludingTraversalFlags() {
        ResonanceGridData einstein = LoadGrid("einstein");
        StorySaveData save = BuildSave(0);
        var collected = new HashSet<string>();

        // Nothing unlocked, nothing collected.
        ResonanceProgression.CollectUnlockedAbilityModifiers(einstein, save, collected);
        AssertThat(collected.Count).IsEqual(0);

        // A TRAVERSAL node is not a Major but carries an AbilityModifierKey,
        // which is exactly how the nine V7.6 traversal flags reach
        // PlayerController.StoryAbilityPerks with no new plumbing.
        save.GridProgress["einstein"] = new List<string> {
            "einstein_momentum", "einstein_extended_float", "einstein_event_horizon"
        };
        ResonanceProgression.CollectUnlockedAbilityModifiers(einstein, save, collected);
        AssertThat(collected.Contains("extended_float")).IsTrue();
        AssertThat(collected.Contains("event_horizon")).IsTrue();
        // einstein_momentum is a stat minor with no perk key.
        AssertThat(collected.Count).IsEqual(2);
    }

    [TestCase]
    public void PlayerPerkQueryIsEmptyUnlessStoryProgressionPopulatesIt() {
        var player = new FTT.Characters.PlayerController();
        try {
            AssertThat(player.StoryAbilityPerks.Count).IsEqual(0);
            AssertThat(player.HasStoryPerk("event_horizon")).IsFalse();
            AssertThat(player.HasStoryPerk("")).IsFalse();
            // The scoped bucket is null-safe by construction: a Fighter loadout
            // never populates it and every read is the neutral 1.0.
            AssertThat(player.StoryScoped(ResonanceStatKeys.AbilityRange, "relativity_rift")).IsEqual(1f);
            AssertThat(player.StoryRallyEchoFractionMultiplier).IsEqual(1f);
        } finally {
            player.Free();
        }
    }

    // ======================================================================
    // Authored content
    // ======================================================================

    [TestCase]
    public void EveryAuthoredGridHasNineNodesThreePerTierAndTotals975() {
        var issues = new List<string>();
        var allNodeIDs = new HashSet<string>();
        foreach (string characterID in CharacterIDs) {
            ResonanceGridData grid = LoadGrid(characterID);
            AssertObject(grid).IsNotNull();
            AssertString(grid.CharacterID).IsEqual(characterID);
            AssertThat(grid.Nodes.Length).IsEqual(9);

            var perTier = new int[4];
            int total = 0;
            foreach (ResonanceNodeData node in grid.Nodes) {
                if (node == null) {
                    issues.Add($"{characterID}: null node");
                    continue;
                }
                if (!allNodeIDs.Add(node.NodeID)) issues.Add($"duplicate node ID {node.NodeID}");
                if (node.Tier < 1 || node.Tier > 3) {
                    issues.Add($"{node.NodeID}: tier {node.Tier}");
                    continue;
                }
                perTier[node.Tier]++;
                total += node.UnlockCost;
                int expected = node.Tier switch {
                    1 => TierOneCost,
                    2 => TierTwoCost,
                    _ => MajorCost
                };
                if (node.UnlockCost != expected) {
                    issues.Add($"{node.NodeID}: tier {node.Tier} costs {node.UnlockCost}, expected {expected}");
                }
                // Tier 3 IS the Major tier; the traversal node is always Tier 2.
                bool major = node.Type == ResonanceNodeType.Major;
                if (major != (node.Tier == 3)) issues.Add($"{node.NodeID}: type/tier mismatch");
                if (node.Type == ResonanceNodeType.Traversal && node.Tier != 2) {
                    issues.Add($"{node.NodeID}: traversal node outside tier 2");
                }
            }
            if (perTier[1] != 3 || perTier[2] != 3 || perTier[3] != 3) {
                issues.Add($"{characterID}: tier counts {perTier[1]}/{perTier[2]}/{perTier[3]}");
            }
            if (total != GridTotalCost) issues.Add($"{characterID}: total {total}, expected {GridTotalCost}");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAuthoredGridValidatesWithNoDanglingPrerequisitesOrCycles() {
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            issues.AddRange(ResonanceProgression.ValidateGrid(LoadGrid(characterID)));
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheValidatorRejectsADanglingPrerequisiteIDAndACycle() {
        var dangling = new ResonanceGridData {
            GridID = "test_dangling",
            CharacterID = "einstein",
            Nodes = new[] {
                new ResonanceNodeData { NodeID = "a", PrerequisiteNodeIDs = new[] { "ghost" } }
            }
        };
        AssertThat(ResonanceProgression.ValidateGrid(dangling).Count > 0).IsTrue();

        var cyclic = new ResonanceGridData {
            GridID = "test_cycle",
            CharacterID = "einstein",
            Nodes = new[] {
                new ResonanceNodeData { NodeID = "a", PrerequisiteNodeIDs = new[] { "b" } },
                new ResonanceNodeData { NodeID = "b", PrerequisiteNodeIDs = new[] { "a" } }
            }
        };
        AssertThat(ResonanceProgression.ValidateGrid(cyclic).Count > 0).IsTrue();

        var scopeless = new ResonanceGridData {
            GridID = "test_scopeless",
            CharacterID = "einstein",
            Nodes = new[] {
                new ResonanceNodeData {
                    NodeID = "a",
                    PrerequisiteNodeIDs = System.Array.Empty<string>(),
                    StatModifierKey = ResonanceStatKeys.AbilityDamage
                }
            }
        };
        AssertThat(ResonanceProgression.ValidateGrid(scopeless).Count > 0).IsTrue();
    }

    [TestCase]
    public void EveryAuthoredNodeKeyIsAKnownResolverLaneAndEveryRetiredKeyIsGone() {
        // V7.6 retirements: a damage-reducing Armor stat is against the combat
        // pillar; Block Health, Block Recovery and Combo Speed name stats that
        // do not exist; jump-height minors are retired roster-wide; and a stun
        // -duration minor is barred by the hygiene rule.
        string[] retired = {
            "Armor", "BlockDurability", "BlockRecovery", "ComboSpeed", "JumpForce"
        };
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (string.IsNullOrWhiteSpace(node.StatModifierKey)) continue;
                if (System.Array.IndexOf(retired, node.StatModifierKey) >= 0) {
                    issues.Add($"{node.NodeID}: retired key {node.StatModifierKey}");
                }
                if (System.Array.IndexOf(ResonanceStatKeys.All, node.StatModifierKey) < 0) {
                    issues.Add($"{node.NodeID}: unknown key {node.StatModifierKey}");
                }
            }
        }
        // Tesla's one StatusDuration node was the roster's stun minor; V7.6
        // replaced it with a scoped mark-duration node, so no grid authors the
        // character-wide StatusDuration lane at all any more.
        foreach (string characterID in CharacterIDs) {
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.StatModifierKey == ResonanceStatKeys.StatusDuration) {
                    issues.Add($"{node.NodeID}: character-wide StatusDuration survives");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryRootNodeModifiesALevelZeroSystemAndIsUngated() {
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            ResonanceGridData grid = LoadGrid(characterID);
            int roots = 0;
            foreach (ResonanceNodeData node in grid.Nodes) {
                if ((node.PrerequisiteNodeIDs?.Length ?? 0) != 0) continue;
                roots++;
                if (node.Tier != 1) issues.Add($"{node.NodeID}: root outside tier 1");
                if (!string.IsNullOrEmpty(node.GatedAbilityID)) {
                    issues.Add($"{node.NodeID}: root gated on {node.GatedAbilityID}");
                }
                if (!LevelZeroKeys.Contains(node.StatModifierKey)) {
                    issues.Add($"{node.NodeID}: root modifies {node.StatModifierKey}");
                }
            }
            // Joan's spine, Mozart's scale and Lincoln's fence deliberately
            // have exactly one root; nobody may have zero.
            if (roots == 0) issues.Add($"{characterID}: no root node, the grid would open empty");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAuthoredGridCarriesExactlyOneTraversalNodeWithAPerkKeyAndNoStatLane() {
        var issues = new List<string>();
        var perkKeys = new HashSet<string>();
        foreach (string characterID in CharacterIDs) {
            int traversal = 0;
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.Type != ResonanceNodeType.Traversal) continue;
                traversal++;
                if (string.IsNullOrWhiteSpace(node.AbilityModifierKey)) {
                    issues.Add($"{node.NodeID}: traversal node with no AbilityModifierKey");
                }
                if (!string.IsNullOrWhiteSpace(node.StatModifierKey)) {
                    issues.Add($"{node.NodeID}: traversal node also carries a stat lane");
                }
                if (!perkKeys.Add(node.AbilityModifierKey)) {
                    issues.Add($"{node.NodeID}: duplicate traversal perk key");
                }
            }
            if (traversal != 1) issues.Add($"{characterID}: {traversal} traversal nodes");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryGridAuthorsExactlyThreeMajorPerkKeysAndAGateSlotTheScheduleKnows() {
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            int majors = 0;
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.Type == ResonanceNodeType.Major) {
                    majors++;
                    if (string.IsNullOrWhiteSpace(node.AbilityModifierKey)) {
                        issues.Add($"{node.NodeID}: Major with no perk key");
                    }
                }
                if (string.IsNullOrEmpty(node.GatedAbilityID)) continue;
                if (System.Array.IndexOf(ResonanceAbilitySlots.All, node.GatedAbilityID) < 0) {
                    issues.Add($"{node.NodeID}: gate slot '{node.GatedAbilityID}' is not a Legacy slot");
                }
            }
            if (majors != 3) issues.Add($"{characterID}: {majors} Majors");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryCharacterGridMatchesItsDesignedTopologySignature() {
        // The Any-of count is the cheapest single fingerprint of a topology:
        // Einstein's mesh gives every non-root node two routes in, Leonardo's
        // gear rings mesh at one tooth, Shakespeare's three Act II nodes each
        // accept any Act I node, Pocahontas's crossing accepts either current,
        // and the linear/paired grids use none.
        var expectedAnyOf = new Dictionary<string, int> {
            ["einstein"] = 6,
            ["joan"] = 0,
            ["leonardo"] = 1,
            ["tesla"] = 0,
            ["shakespeare"] = 3,
            ["mozart"] = 0,
            ["cleopatra"] = 0,
            ["lincoln"] = 0,
            ["pocahontas"] = 1
        };
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            int anyOf = 0;
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (node.PrerequisiteMode == PrerequisiteMode.Any
                    && (node.PrerequisiteNodeIDs?.Length ?? 0) > 0) anyOf++;
            }
            if (anyOf != expectedAnyOf[characterID]) {
                issues.Add($"{characterID}: {anyOf} Any-of nodes, expected {expectedAnyOf[characterID]}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ShakespeareThreeActsMakesHisCheapestFirstMajorCostExactly475() {
        ResonanceGridData grid = LoadGrid("shakespeare");
        // F08 Option B: every Act II node accepts ANY Act I node, and every
        // Act III Major requires ALL THREE Act II nodes. One Act I (50) plus
        // three Act II (225) plus the Major (200) = 475, the roster's peak.
        foreach (ResonanceNodeData node in grid.Nodes) {
            if (node.Tier == 2) {
                AssertThat(node.PrerequisiteMode).IsEqual(PrerequisiteMode.Any);
                AssertThat(node.PrerequisiteNodeIDs.Length).IsEqual(3);
            }
            if (node.Tier == 3) {
                AssertThat(node.PrerequisiteMode).IsEqual(PrerequisiteMode.All);
                AssertThat(node.PrerequisiteNodeIDs.Length).IsEqual(3);
            }
        }
        AssertThat(CheapestRouteToAnyMajor(grid)).IsEqual(475);
    }

    [TestCase]
    public void TheCheapestRouteToAFirstMajorMatchesTheDesignTableForEveryCharacter() {
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            int actual = CheapestRouteToAnyMajor(LoadGrid(characterID));
            if (actual != CheapestFirstMajor[characterID]) {
                issues.Add($"{characterID}: {actual}, expected {CheapestFirstMajor[characterID]}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheTwoMultiPrerequisiteMajorsAreAuthoredAsAllOf() {
        // Leonardo's capstone needs all three of his Tier 2 nodes; Pocahontas's
        // Leaf Barrier needs the Second Glide crossing AND Forest Vigor.
        ResonanceNodeData daedalus = FindNode(LoadGrid("leonardo"), "leonardo_daedalus_wings");
        AssertThat(daedalus.PrerequisiteMode).IsEqual(PrerequisiteMode.All);
        AssertThat(daedalus.PrerequisiteNodeIDs.Length).IsEqual(3);

        ResonanceNodeData leaf = FindNode(LoadGrid("pocahontas"), "pocahontas_leaf_barrier");
        AssertThat(leaf.PrerequisiteMode).IsEqual(PrerequisiteMode.All);
        AssertThat(leaf.PrerequisiteNodeIDs.Length).IsEqual(2);
        AssertThat(System.Array.IndexOf(leaf.PrerequisiteNodeIDs, "pocahontas_second_glide") >= 0).IsTrue();
        AssertThat(System.Array.IndexOf(leaf.PrerequisiteNodeIDs, "pocahontas_forest_vigor") >= 0).IsTrue();
    }

    [TestCase]
    public void EveryAuthoredNodeCarriesLocalizedCopyAndAUniqueLayoutPosition() {
        TranslationServer.SetLocale("en");
        var issues = new List<string>();
        foreach (string characterID in CharacterIDs) {
            var positions = new HashSet<(float, float)>();
            foreach (ResonanceNodeData node in LoadGrid(characterID).Nodes) {
                if (string.IsNullOrWhiteSpace(node.DisplayNameKey)
                    || string.IsNullOrWhiteSpace(node.DescriptionKey)) {
                    issues.Add($"{node.NodeID}: missing copy key");
                    continue;
                }
                if (TranslationServer.Translate(node.DisplayNameKey) == node.DisplayNameKey) {
                    issues.Add($"{node.DisplayNameKey}: unresolved");
                }
                if (TranslationServer.Translate(node.DescriptionKey) == node.DescriptionKey) {
                    issues.Add($"{node.DescriptionKey}: unresolved");
                }
                if (!positions.Add((node.LayoutPosition.X, node.LayoutPosition.Y))) {
                    issues.Add($"{node.NodeID}: duplicate LayoutPosition");
                }
                if (node.LayoutPosition.X < 0f || node.LayoutPosition.X > 1f
                    || node.LayoutPosition.Y < 0f || node.LayoutPosition.Y > 1f) {
                    issues.Add($"{node.NodeID}: LayoutPosition outside 0-1");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheV76MigrationDropsRetiredNodesAndRefundsThemExactlyOnce() {
        StorySaveData save = BuildSave(0);
        // A pre-V7.6 payload: three purchases whose IDs no longer exist
        // (50 + 75 + 200) plus one that still does.
        save.GridProgress["einstein"] = new List<string> {
            "einstein_u1", "einstein_u2", "einstein_u3", "einstein_momentum"
        };

        int refunded = ResonanceProgression.MigrateGridProgressToV76(save);
        AssertThat(refunded).IsEqual(325);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(325);
        AssertThat(save.GridProgress["einstein"].Count).IsEqual(1);
        AssertThat(save.GridProgress["einstein"][0]).IsEqual("einstein_momentum");

        // Idempotent: a second pass finds nothing to drop and pays nothing.
        AssertThat(ResonanceProgression.MigrateGridProgressToV76(save)).IsEqual(0);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(325);
    }

    // ---- helpers ------------------------------------------------------------

    private static ResonanceGridData LoadGrid(string characterID) =>
        AuthoredResources.Load<ResonanceGridData>(
            $"res://resources/Resonance/{characterID}_grid.tres");

    private static ResonanceNodeData FindNode(ResonanceGridData grid, string nodeID) {
        foreach (ResonanceNodeData node in grid.Nodes) {
            if (node.NodeID == nodeID) return node;
        }
        return null;
    }

    /// <summary>
    /// The cheapest total dust that buys ANY one Major on the grid, resolved by
    /// exhaustive closure over the nine nodes: a node is affordable once its
    /// prerequisite rule is satisfied by the set bought so far.
    /// </summary>
    private static int CheapestRouteToAnyMajor(ResonanceGridData grid) {
        int best = int.MaxValue;
        int nodeCount = grid.Nodes.Length;
        for (int mask = 0; mask < (1 << nodeCount); mask++) {
            var selected = new HashSet<string>();
            int cost = 0;
            bool hasMajor = false;
            for (int i = 0; i < nodeCount; i++) {
                if ((mask & (1 << i)) == 0) continue;
                selected.Add(grid.Nodes[i].NodeID);
                cost += grid.Nodes[i].UnlockCost;
                if (grid.Nodes[i].Type == ResonanceNodeType.Major) hasMajor = true;
            }
            if (!hasMajor || cost >= best) continue;
            bool reachable = true;
            foreach (ResonanceNodeData node in grid.Nodes) {
                if (!selected.Contains(node.NodeID)) continue;
                if (!ResonanceProgression.PrerequisitesSatisfied(node, new List<string>(selected))) {
                    reachable = false;
                    break;
                }
            }
            if (reachable) best = cost;
        }
        return best;
    }

    private static StorySaveData BuildSave(int dust, string characterID = "einstein") => new() {
        SelectedCharacterID = characterID,
        DepositedChronalDust = new Dictionary<string, int> { [characterID] = dust },
        GridProgress = new Dictionary<string, List<string>>()
    };

    /// <summary>
    /// A synthetic grid exercising the resolver's branches without depending on
    /// any character's authored topology: two roots, an All-of child over both,
    /// an Any-of child over both, a gated node and an unknown-key node.
    /// </summary>
    private static ResonanceGridData BuildGrid() => new() {
        GridID = "test_grid",
        CharacterID = "einstein",
        Nodes = new[] {
            new ResonanceNodeData {
                NodeID = "test_root_a",
                Tier = 1,
                UnlockCost = 50,
                PrerequisiteNodeIDs = System.Array.Empty<string>(),
                StatModifierKey = ResonanceStatKeys.MaxHP,
                StatModifierValue = 15
            },
            new ResonanceNodeData {
                NodeID = "test_root_b",
                Tier = 1,
                UnlockCost = 50,
                PrerequisiteNodeIDs = System.Array.Empty<string>(),
                StatModifierKey = ResonanceStatKeys.MoveSpeed,
                StatModifierValue = 0.02f,
                StatModifierIsPercent = true
            },
            new ResonanceNodeData {
                NodeID = "test_all_child",
                Tier = 2,
                UnlockCost = 75,
                PrerequisiteNodeIDs = new[] { "test_root_a", "test_root_b" },
                PrerequisiteMode = PrerequisiteMode.All
            },
            new ResonanceNodeData {
                NodeID = "test_any_child",
                Tier = 2,
                UnlockCost = 75,
                PrerequisiteNodeIDs = new[] { "test_root_a", "test_root_b" },
                PrerequisiteMode = PrerequisiteMode.Any
            },
            new ResonanceNodeData {
                NodeID = "test_gated",
                Tier = 2,
                UnlockCost = 75,
                PrerequisiteNodeIDs = new[] { "test_root_a" },
                GatedAbilityID = ResonanceAbilitySlots.Special2
            },
            new ResonanceNodeData {
                NodeID = "test_unknown_key",
                Tier = 1,
                UnlockCost = 50,
                PrerequisiteNodeIDs = System.Array.Empty<string>(),
                StatModifierKey = "NoSuchLane",
                StatModifierValue = 4f
            }
        }
    };
}
