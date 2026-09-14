using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — F10 reward-source claims.
///
/// <para>Checkpoint reconstruction rebuilds ordinary encounters from an authored
/// baseline, so a respawned enemy may fight again — and that is exactly why the
/// claim ledger has to be separate from the encounter. "A respawned enemy may
/// fight again but cannot reissue a claimed reward." A claim survives a death
/// rewind, a checkpoint resume, an Anchor Snap, a quit and a crash; an
/// uncollected pickup restores once rather than duplicating; and a randomized
/// drop's outcome is fixed at issue so a reload cannot reroll it.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RewardSourcePersistenceTests {

    [TestCase]
    public void ASourceIssuesOnceSoARespawnedEncounterAwardsNothingAgain() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            AssertThat(story.TryIssueReward("l05_mob_03", 4, "chronal_dust")).IsTrue();
            AssertThat(story.TryCollectReward("l05_mob_03")).IsTrue();
            AssertThat(story.IsRewardClaimed("l05_mob_03")).IsTrue();

            // The baseline respawns the encounter; the ledger refuses it.
            AssertThat(story.TryIssueReward("l05_mob_03", 4, "chronal_dust"))
                .OverrideFailureMessage("A claimed source must never reissue.")
                .IsFalse();
            AssertThat(story.TryCollectReward("l05_mob_03"))
                .OverrideFailureMessage("A duplicate collection callback must pay nothing.")
                .IsFalse();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ClaimsSurviveACheckpointResumeAndACrashReload() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            story.TryIssueReward("l06_extractor_1", 5, "chronal_dust");
            story.TryCollectReward("l06_extractor_1");
            story.TryIssueReward("l06_secret_0", 10, "chronal_dust");   // spawned, not collected

            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);

            // A crash: the live manager is gone, the payload is what is left.
            story.ClearLevelAttemptState();
            AssertThat(story.IsRewardClaimed("l06_extractor_1")).IsFalse();

            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.IsRewardClaimed("l06_extractor_1"))
                .OverrideFailureMessage("A collected claim is durable across a reload.")
                .IsTrue();
            AssertThat(story.IsRewardPendingPickup("l06_secret_0"))
                .OverrideFailureMessage("A spawned-uncollected pickup restores once, still pending.")
                .IsTrue();
            AssertThat(story.TryIssueReward("l06_secret_0", 10, "chronal_dust"))
                .OverrideFailureMessage("Restoring a pending pickup must not duplicate it.")
                .IsFalse();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ClaimsSurviveAnAnchorSnapBecauseASnapIsTheSameAttempt() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            saves.SaveSlots[scratchSlot] = save;
            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Easy);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();

            story.TryIssueReward("l13_conduit_0", 5, "chronal_dust");
            story.TryCollectReward("l13_conduit_0");
            string attempt = story.CurrentAttempt.AttemptID;

            story.BeginAnchorSnap("level_13_chronal_void_checkpoint_1");
            AssertThat(story.CurrentAttempt.AttemptID)
                .OverrideFailureMessage("A Snap is a paid recovery inside the same attempt.")
                .IsEqual(attempt);
            AssertThat(story.IsRewardClaimed("l13_conduit_0"))
                .OverrideFailureMessage("Claims stay spent even when dust is lost to the fee.")
                .IsTrue();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AFixedRandomDropOutcomeIsRetainedAcrossAReloadRatherThanRerolled() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            story.TryIssueReward("l07_elite_2", 5, "chronal_dust", new Vector2(120f, -40f), fixedDropOutcome: 3);

            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            story.ClearLevelAttemptState();
            story.RestoreAttemptStateFromSave(save);

            StoryRewardSourceRecord record = story.RewardSource("l07_elite_2");
            AssertThat(record.FixedDropOutcome)
                .OverrideFailureMessage("An issued random outcome is fixed; a reload cannot reroll it.")
                .IsEqual(3);
            AssertThat(record.Quantity).IsEqual(5);
            AssertThat(record.PickupType).IsEqual("chronal_dust");
            AssertThat(record.PositionX).IsEqual(120f);
            AssertThat(record.PositionY).IsEqual(-40f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AFreshEntryAndAnExplicitRestartBothClearTheLedger() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            story.TryIssueReward("l08_mob_00", 2, "chronal_dust");
            story.TryCollectReward("l08_mob_00");
            AssertThat(story.IsRewardClaimed("l08_mob_00")).IsTrue();

            story.BeginLevelRun();      // fresh entry
            AssertThat(story.IsRewardClaimed("l08_mob_00"))
                .OverrideFailureMessage("A new attempt has nothing claimed.")
                .IsFalse();

            story.TryIssueReward("l08_mob_00", 2, "chronal_dust");
            story.TryCollectReward("l08_mob_00");
            story.RestartLevelAttempt();
            AssertThat(story.IsRewardClaimed("l08_mob_00"))
                .OverrideFailureMessage("Restart Level discards the attempt's claims.")
                .IsFalse();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }
}
