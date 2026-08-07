using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class ResonanceProgressionTests {
    private static readonly string[] CharacterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    [TestCase]
    public void UnlockRequiresDustAndAllPrerequisites() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(125);

        AssertThat(ResonanceProgression.TryUnlock(grid, save, "einstein_d2"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(75);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "einstein_d2"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(0);
    }

    [TestCase]
    public void ResolvedModifiersAreStoryOnlyValues() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);
        save.GridProgress["einstein"] = new List<string> { "einstein_d1", "einstein_u1" };

        StoryStatProfile profile = ResonanceProgression.Resolve(grid, save);
        AssertThat(profile.MaxHPBonus).IsEqual(15);
        AssertThat(profile.MoveSpeedMultiplier).IsEqual(1.02f);
        AssertThat(profile.BasicDamageMultiplier).IsEqual(1f);
    }

    [TestCase]
    public void GridCannotSpendAnotherCampaignCharactersDust() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(500);
        save.SelectedCharacterID = "joan";

        AssertThat(ResonanceProgression.TryUnlock(grid, save, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.WrongCharacter);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(500);
    }

    [TestCase]
    public void AllCharacterGridsHaveThreeValidThreeNodeBranchesAndLocalizedCopy() {
        TranslationServer.SetLocale("en");
        var allNodeIDs = new HashSet<string>();
        foreach (string characterID in CharacterIDs) {
            ResonanceGridData grid = ResourceLoader.Load<ResonanceGridData>(
                $"res://resources/Resonance/{characterID}_grid.tres");
            AssertObject(grid).IsNotNull();
            AssertString(grid.CharacterID).IsEqual(characterID);
            AssertThat(grid.Nodes.Length).IsEqual(9);

            int tierOne = 0;
            int tierTwo = 0;
            int majors = 0;
            var gridIDs = new HashSet<string>();
            foreach (ResonanceNodeData node in grid.Nodes) {
                AssertObject(node).IsNotNull();
                AssertThat(gridIDs.Add(node.NodeID)).IsTrue();
                AssertThat(allNodeIDs.Add(node.NodeID)).IsTrue();
                AssertThat(string.IsNullOrWhiteSpace(node.DisplayNameKey)).IsFalse();
                AssertThat(string.IsNullOrWhiteSpace(node.DescriptionKey)).IsFalse();
                AssertThat(TranslationServer.Translate(node.DisplayNameKey) != node.DisplayNameKey).IsTrue();
                AssertThat(TranslationServer.Translate(node.DescriptionKey) != node.DescriptionKey).IsTrue();
                if (node.Type == ResonanceNodeType.Major) {
                    majors++;
                    AssertThat(node.UnlockCost).IsEqual(200);
                    AssertThat(node.PrerequisiteNodeIDs.Length).IsEqual(1);
                    AssertThat(string.IsNullOrWhiteSpace(node.AbilityModifierKey)).IsFalse();
                } else if (node.PrerequisiteNodeIDs.Length == 0) {
                    tierOne++;
                    AssertThat(node.UnlockCost).IsEqual(50);
                } else {
                    tierTwo++;
                    AssertThat(node.UnlockCost).IsEqual(75);
                    AssertThat(node.PrerequisiteNodeIDs.Length).IsEqual(1);
                }
            }
            AssertThat(tierOne).IsEqual(3);
            AssertThat(tierTwo).IsEqual(3);
            AssertThat(majors).IsEqual(3);
            foreach (ResonanceNodeData node in grid.Nodes) {
                foreach (string prerequisite in node.PrerequisiteNodeIDs) {
                    AssertThat(gridIDs.Contains(prerequisite)).IsTrue();
                }
            }
        }
    }

    [TestCase]
    public void UnlockedGenericAndMajorModifiersRemainCharacterScoped() {
        ResonanceGridData grid = ResourceLoader.Load<ResonanceGridData>(
            "res://resources/Resonance/tesla_grid.tres");
        StorySaveData save = new() {
            SelectedCharacterID = "tesla",
            DepositedChronalDust = new Dictionary<string, int>(),
            GridProgress = new Dictionary<string, List<string>> {
                ["tesla"] = new() { "tesla_c1", "tesla_c2", "tesla_c3" }
            }
        };

        AssertThat(ResonanceProgression.GetUnlockedStatTotal(grid, save, "PersistentDuration"))
            .IsEqual(0.1f);
        AssertThat(ResonanceProgression.GetUnlockedStatTotal(grid, save, "PersistentRange"))
            .IsEqual(0.15f);
        AssertThat(ResonanceProgression.HasUnlockedAbilityModifier(grid, save, "resonant_overdrive"))
            .IsTrue();
        save.SelectedCharacterID = "einstein";
        AssertThat(ResonanceProgression.HasUnlockedAbilityModifier(grid, save, "resonant_overdrive"))
            .IsFalse();
    }

    [TestCase]
    public void CollectUnlockedAbilityModifiersReturnsOnlyUnlockedMajorPerkKeys() {
        ResonanceGridData grid = ResourceLoader.Load<ResonanceGridData>(
            "res://resources/Resonance/einstein_grid.tres");
        StorySaveData save = new() {
            SelectedCharacterID = "einstein",
            DepositedChronalDust = new Dictionary<string, int>(),
            GridProgress = new Dictionary<string, List<string>> {
                ["einstein"] = new() { "einstein_u1", "einstein_u2", "einstein_u3", "einstein_o1" }
            }
        };

        var perks = new HashSet<string>();
        ResonanceProgression.CollectUnlockedAbilityModifiers(grid, save, perks);
        AssertThat(perks.Count).IsEqual(1);
        AssertThat(perks.Contains("event_horizon")).IsTrue();

        // Perk keys stay character-scoped: a different campaign character collects nothing.
        save.SelectedCharacterID = "joan";
        ResonanceProgression.CollectUnlockedAbilityModifiers(grid, save, perks);
        AssertThat(perks.Count).IsEqual(0);
    }

    [TestCase]
    public void PlayerPerkQueryIsEmptyUnlessStoryProgressionPopulatesIt() {
        var player = new FTT.Characters.PlayerController();
        AssertThat(player.HasStoryPerk("event_horizon")).IsFalse();
        AssertThat(player.HasStoryPerk("")).IsFalse();

        player.StoryAbilityPerks.Add("event_horizon");
        AssertThat(player.HasStoryPerk("event_horizon")).IsTrue();
        AssertThat(player.HasStoryPerk("critical_mass")).IsFalse();
        player.Free();
    }

    [TestCase]
    public void EvaluateUnlockReportsBlockingReasonWithoutMutatingSave() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(60);

        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "einstein_d2"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "missing_node"))
            .IsEqual(ResonanceUnlockResult.MissingNode);

        // Evaluation is a pure read: nothing was purchased or deducted.
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(60);
        AssertThat(save.GridProgress["einstein"].Count).IsEqual(0);

        AssertThat(ResonanceProgression.TryUnlock(grid, save, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.AlreadyUnlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "einstein_d2"))
            .IsEqual(ResonanceUnlockResult.InsufficientDust);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(10);
    }

    [TestCase]
    public void UnlockedProgressSurvivesSaveEnvelopeRoundTrip() {
        ResonanceGridData grid = BuildGrid();
        StorySaveData save = BuildSave(125);
        AssertThat(ResonanceProgression.TryUnlock(grid, save, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.Unlocked);

        // Same persistence path as SaveManager.SaveStorySlot: normalized JSON
        // through the authenticated schema-v3 envelope and back.
        save.Normalize();
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);
        byte[] envelope = SaveEnvelopeCodec.Encode(
            "story", save.SaveVersion, Newtonsoft.Json.JsonConvert.SerializeObject(save), key, 1L);
        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, key, out DecodedSaveEnvelope decoded, out _))
            .IsTrue();
        StorySaveData restored = SaveSchemaMigrator.DeserializeStory(decoded.Json);

        AssertThat(restored.DepositedChronalDust["einstein"]).IsEqual(75);
        AssertThat(restored.GridProgress["einstein"].Contains("einstein_d1")).IsTrue();
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, restored, "einstein_d1"))
            .IsEqual(ResonanceUnlockResult.AlreadyUnlocked);
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, restored, "einstein_d2"))
            .IsEqual(ResonanceUnlockResult.Unlocked);
    }

    private static StorySaveData BuildSave(int dust) => new() {
        SelectedCharacterID = "einstein",
        DepositedChronalDust = new Dictionary<string, int> { ["einstein"] = dust },
        GridProgress = new Dictionary<string, List<string>>()
    };

    private static ResonanceGridData BuildGrid() => new() {
        CharacterID = "einstein",
        Nodes = new[] {
            new ResonanceNodeData {
                NodeID = "einstein_d1",
                UnlockCost = 50,
                PrerequisiteNodeIDs = System.Array.Empty<string>(),
                StatModifierKey = "MaxHP",
                StatModifierValue = 15
            },
            new ResonanceNodeData {
                NodeID = "einstein_d2",
                UnlockCost = 75,
                PrerequisiteNodeIDs = new[] { "einstein_d1" },
                StatModifierKey = "BlockDurability",
                StatModifierValue = 0.1f,
                StatModifierIsPercent = true
            },
            new ResonanceNodeData {
                NodeID = "einstein_u1",
                UnlockCost = 50,
                PrerequisiteNodeIDs = System.Array.Empty<string>(),
                StatModifierKey = "MoveSpeed",
                StatModifierValue = 0.02f,
                StatModifierIsPercent = true
            }
        }
    };
}
