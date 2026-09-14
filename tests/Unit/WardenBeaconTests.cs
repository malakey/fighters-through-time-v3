using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — the V7.6 Warden Beacon (F02 resolution, Option A).
///
/// <para>Sarah hands it over at Level 13's entry: <i>data crosses the field,
/// matter doesn't</i>. It opens the <b>existing</b> Repository screen at any
/// <b>activated</b> Act III checkpoint for grid purchases and the free respec —
/// and its whole design constraint is what it cannot do. It cannot deposit or
/// spend the active level's earnings; those bank once at level completion,
/// "sealed through the Beacon", which is what makes Level 13's dust spendable at
/// Level 14's Beacon and stops a repeated level from banking repeat rewards.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WardenBeaconTests {

    [TestCase]
    public void TheSpendableBalanceExcludesTheActiveLevelsUndepositedEarnings() {
        var save = new StorySaveData { SelectedCharacterID = "einstein", LevelChronalDust = 240 };
        save.DepositedChronalDust["einstein"] = 310;

        AssertThat(ResonanceProgression.SpendableBalance(save, "einstein"))
            .OverrideFailureMessage(
                "F02: the Beacon spends previously deposited dust only. The 240 collected in " +
                "the level the player is standing in is not spendable there.")
            .IsEqual(310);

        // Completing the level seals it through the Beacon; only then is it spendable.
        ResonanceProgression.DepositActiveDust(save, "einstein", save.LevelChronalDust);
        save.LevelChronalDust = 0;
        AssertThat(ResonanceProgression.SpendableBalance(save, "einstein")).IsEqual(550);
    }

    [TestCase]
    public void TheBeaconIsLiveOnlyAtAnActivatedActIIICheckpoint() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        var tree = (SceneTree)Engine.GetMainLoop();
        FTT.Characters.PlayerController player = FTT.Characters.CharacterFactory.CreateCharacter("einstein");
        WardenBeacon beacon = WardenBeacon.Create("level_13_chronal_void_checkpoint_1", Vector2.Zero);
        tree.Root.AddChild(player);
        tree.Root.AddChild(beacon);
        story.SuppressSceneLoadsForTesting = true;
        try {
            // Acts I-II: there is no Beacon, so an authored one is inert.
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            story.BeginLevelRun();
            story.TryActivateCheckpoint("level_13_chronal_void_checkpoint_1");
            AssertThat(beacon.CanInteract(player))
                .OverrideFailureMessage("The Beacon is an Act III affordance and nothing else.")
                .IsFalse();

            // Act III, anchor not yet struck: still nothing. An anchor the player
            // walked past is not a Repository terminal.
            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Normal);
            story.BeginLevelRun();
            AssertThat(beacon.CanInteract(player))
                .OverrideFailureMessage("An unactivated anchor carries no live Beacon.")
                .IsFalse();

            // Struck: live.
            story.TryActivateCheckpoint("level_13_chronal_void_checkpoint_1");
            AssertThat(beacon.CanInteract(player))
                .OverrideFailureMessage("Any ACTIVATED Act III checkpoint opens the Repository.")
                .IsTrue();
            AssertThat(beacon.PromptKey).IsEqual("beacon_interaction_open_repository");
            AssertThat(beacon.IsInGroup(WardenBeacon.GroupName)).IsTrue();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            beacon.Free();
            player.Free();
        }
    }

    [TestCase]
    public void OpeningTheBeaconNeverDepositsTheActiveLevelsDust() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        var tree = (SceneTree)Engine.GetMainLoop();
        FTT.Characters.PlayerController player = FTT.Characters.CharacterFactory.CreateCharacter("einstein");
        WardenBeacon beacon = WardenBeacon.Create("level_13_chronal_void_checkpoint_0", Vector2.Zero);
        tree.Root.AddChild(player);
        tree.Root.AddChild(beacon);
        story.SuppressSceneLoadsForTesting = true;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            save.DepositedChronalDust["einstein"] = 120;
            saves.SaveSlots[scratchSlot] = save;

            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();
            story.CollectDust(90);
            story.TryActivateCheckpoint("level_13_chronal_void_checkpoint_0");

            beacon.Interact(player);
            AssertThat(beacon.OpenCount)
                .OverrideFailureMessage("The Beacon opens the existing Repository screen.")
                .IsEqual(1);
            AssertThat(save.DepositedChronalDust["einstein"])
                .OverrideFailureMessage(
                    "The hub Repository deposits before it opens; the Beacon must not. " +
                    "Its checkpoint UI offers no Deposit action at all.")
                .IsEqual(120);
            AssertThat(story.ChronalDustCollected)
                .OverrideFailureMessage("The level's earnings stay undeposited until completion.")
                .IsEqual(90);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            beacon.Free();
            player.Free();
        }
    }

    [TestCase]
    public void TheFreeRespecRefundsOnlyPreviouslySpentDepositedDust() {
        var save = new StorySaveData { SelectedCharacterID = "einstein", LevelChronalDust = 300 };
        save.DepositedChronalDust["einstein"] = 0;
        ResonanceGridData grid = ResonanceProgression.LoadGrid("einstein");
        AssertThat(grid).IsNotNull();

        // Nothing was ever purchased, so there is nothing to refund — and the
        // 300 sitting undeposited in the active level is not a refund source.
        int refunded = ResonanceProgression.RespecAll(grid, save);
        AssertThat(refunded).IsEqual(0);
        AssertThat(ResonanceProgression.SpendableBalance(save, "einstein"))
            .OverrideFailureMessage("A respec cannot conjure spendable dust out of level earnings.")
            .IsEqual(0);
        AssertThat(save.LevelChronalDust)
            .OverrideFailureMessage("The undeposited wallet is untouched by a respec.")
            .IsEqual(300);

        // With a real purchase behind it, the refund is exactly what was spent.
        save.DepositedChronalDust["einstein"] = 1000;
        ResonanceNodeData first = null;
        foreach (ResonanceNodeData node in grid.Nodes) {
            if (node != null && (node.PrerequisiteNodeIDs == null || node.PrerequisiteNodeIDs.Length == 0)) {
                first = node;
                break;
            }
        }
        AssertThat(first).IsNotNull();
        ResonanceProgression.TryUnlock(grid, save, first.NodeID);
        int spent = 1000 - save.DepositedChronalDust["einstein"];
        AssertThat(ResonanceProgression.RespecAll(grid, save)).IsEqual(spent);
        AssertThat(ResonanceProgression.SpendableBalance(save, "einstein")).IsEqual(1000);
    }
}
