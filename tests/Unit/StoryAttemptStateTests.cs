using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — the F10 attempt record
/// (<c>docs/design-contracts/STORY_PERSISTENCE.md</c>).
///
/// <para>What these pin is the <i>identity and ordering</i> half of F10: an
/// attempt is minted exactly twice in its life (fresh entry, full Restart Level),
/// its status commits before the presentation it causes, its revision only ever
/// goes up, an older asynchronous write can never land on a newer one, a failed
/// write keeps the last good save and refuses to pretend otherwise, ordinary
/// loading restores the latest durable resources rather than the values a
/// fracture happened to bank, and a legacy payload that cannot be reconstructed
/// is preserved and flagged instead of being handed invented anchors.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAttemptStateTests {

    private const int ScratchSlot = 2;

    [TestCase]
    public void AnAttemptIDIsMintedOnlyOnFreshEntryAndRestart() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            string first = story.CurrentAttempt.AttemptID;
            AssertThat(string.IsNullOrWhiteSpace(first))
                .OverrideFailureMessage("Fresh entry must mint an attempt ID.")
                .IsFalse();

            // A checkpoint, a secret and a broken machine are all ordinary play:
            // none of them is a new attempt.
            story.TryActivateCheckpoint("mint_checkpoint_0");
            story.RecordExtractorDestroyed("mint_extractor");
            story.RegisterSecretFound("mint_secret", isSpecialSecret: false);
            AssertThat(story.CurrentAttempt.AttemptID).IsEqual(first);

            // A resume is the same attempt coming back, never a new one.
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.CurrentAttempt.AttemptID)
                .OverrideFailureMessage("A mid-level resume must not mint a new attempt.")
                .IsEqual(first);

            // The two events that do mint.
            story.BeginLevelRun();
            string second = story.CurrentAttempt.AttemptID;
            AssertThat(second).IsNotEqual(first);
            story.RestartLevelAttempt();
            AssertThat(story.CurrentAttempt.AttemptID).IsNotEqual(second);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ARevisionIsMonotonicAndAnObsoleteQueuedWriteIsIgnored() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        try {
            story.BeginLevelRun();
            long start = story.CurrentAttempt.Revision;
            story.TryActivateCheckpoint("revision_checkpoint_0");
            long afterCheckpoint = story.CurrentAttempt.Revision;
            AssertThat(afterCheckpoint > start)
                .OverrideFailureMessage("A committed change must move the revision.")
                .IsTrue();
            story.CurrentAttempt.Bump();
            AssertThat(story.CurrentAttempt.Revision > afterCheckpoint).IsTrue();

            // The ordered writer refuses a revision it has already queued: an
            // older asynchronous write must never replace a newer one.
            saves.SaveSlots[ScratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            AssertThat(saves.QueueStorySlotWrite(ScratchSlot, 10))
                .OverrideFailureMessage("A newer revision must be accepted.")
                .IsTrue();
            AssertThat(saves.QueueStorySlotWrite(ScratchSlot, 9))
                .OverrideFailureMessage("An obsolete revision must be dropped, not written.")
                .IsFalse();
            AssertThat(saves.QueueStorySlotWrite(ScratchSlot, 10))
                .OverrideFailureMessage("Re-queuing the same revision is also obsolete.")
                .IsFalse();
            saves.FlushStorySlotWrites(ScratchSlot);
        } finally {
            saves.FlushStorySlotWrites();
            saves.SaveSlots[ScratchSlot] = original;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AStatusTransitionCommitsBeforeItsPresentation() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            saves.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            story.BeginLevelRun();

            story.EnterSmothered("commit_checkpoint_1");
            // The record is in the payload, not merely in memory, and the route
            // to the presentation happened after it.
            AssertThat(save.AttemptState.Status)
                .OverrideFailureMessage("Smothered must be persisted before its presentation.")
                .IsEqual(StoryAttemptStatus.Smothered);
            AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.GameOverScenePath);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[ScratchSlot] = original;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AFailedWriteRetainsThePriorRevisionAndReportsTheFailure() {
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        try {
            // A null slot is the simplest write refusal the manager exposes:
            // nothing is written, and nothing may claim it was.
            long before = saves.WrittenRevision(ScratchSlot);
            saves.SaveSlots[ScratchSlot] = null;
            AssertThat(saves.SaveStorySlot(ScratchSlot))
                .OverrideFailureMessage("A slot with no data cannot report a successful save.")
                .IsFalse();
            AssertThat(saves.WrittenRevision(ScratchSlot))
                .OverrideFailureMessage("A refused write must retain the prior valid revision.")
                .IsEqual(before);
            AssertThat(SaveManager.WriteFailedNoticeKey).IsEqual("save_notice_write_failed");
        } finally {
            saves.SaveSlots[ScratchSlot] = original;
        }
    }

    [TestCase]
    public void ThePerSecondSnapshotMakesContinuousResourcesDurable() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        int originalRewinds = story.ChronalRewindsRemaining;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein", CurrentLives = 99 };
            saves.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            story.BeginLevelRun();
            story.SetRewinds(2);
            story.CollectDust(17);

            long before = story.CurrentAttempt.Revision;
            story.CaptureDurableSnapshot();
            AssertThat(save.CurrentLives)
                .OverrideFailureMessage("The snapshot must make the live rewind pool durable.")
                .IsEqual(2);
            AssertThat(save.LevelChronalDust >= 17)
                .OverrideFailureMessage("The snapshot must make the live wallet durable.")
                .IsTrue();
            AssertThat(story.CurrentAttempt.Revision > before)
                .OverrideFailureMessage("A snapshot is a committed change and moves the revision.")
                .IsTrue();
            saves.FlushStorySlotWrites();
        } finally {
            saves.FlushStorySlotWrites();
            story.SetRewinds(originalRewinds);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[ScratchSlot] = original;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void OrdinaryLoadingRestoresLatestDurableIntegrityNotTheCheckpointAllowance() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            story.DrainTimelineIntegrityAmount(20f);
            story.BankCheckpointIntegrity();            // allowance banked at 80
            story.DrainTimelineIntegrityAmount(15f);    // live gauge keeps draining to 65
            float live = story.TimelineIntegrityPercent;
            float allowance = story.CheckpointIntegrityPercent;
            AssertThat(allowance).IsNotEqual(live);

            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            story.ClearLevelAttemptState();
            story.RestoreAttemptStateFromSave(save);

            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage(
                    "F10 supersedes the V7.3 rule: ordinary loading restores the latest durable " +
                    "gauge, never the checkpoint allowance.")
                .IsEqual(live);
            AssertThat(story.CheckpointIntegrityPercent)
                .OverrideFailureMessage("The allowance survives as the separate paid-recovery figure.")
                .IsEqual(allowance);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ALegacyMidLevelPayloadIsPreservedAndFlaggedRatherThanReconstructed() {
        // A v5 payload parked mid-level: its anchors, Defy use, healing and
        // reward claims are unknowable. F10 forbids guessing them.
        var legacy = new StorySaveData {
            SelectedCharacterID = "einstein",
            LastCheckpointID = "legacy_checkpoint_1",
            LevelIntegrityPercent = 42.5f,
            LevelChronalDust = 140,
            CurrentHP = 37
        };
        legacy.ActivatedCheckpointIDs.Add("legacy_checkpoint_0");
        legacy.ActivatedCheckpointIDs.Add("legacy_checkpoint_1");
        legacy.DestroyedExtractorIDs.Add("legacy_extractor_0");
        legacy.DepositedChronalDust["einstein"] = 610;

        AssertThat(StoryAttemptState.MigrateFromLegacyRoot(legacy)).IsTrue();

        AssertThat(legacy.AttemptState.Status)
            .OverrideFailureMessage("An unreconstructable active attempt must say so, not pretend.")
            .IsEqual(StoryAttemptStatus.LegacyRecoveryRequired);
        // Every balance and record is preserved exactly.
        AssertThat(legacy.LevelChronalDust).IsEqual(140);
        AssertThat(legacy.CurrentHP).IsEqual(37);
        AssertThat(legacy.DepositedChronalDust["einstein"]).IsEqual(610);
        AssertThat(legacy.AttemptState.CurrentIntegrity).IsEqual(42.5f);
        AssertThat(legacy.AttemptState.DestroyedExtractorIDs).Contains("legacy_extractor_0");
        AssertThat(legacy.AttemptState.CheckpointRecord.ActivatedCheckpointIDs)
            .Contains("legacy_checkpoint_1");
        // Migration runs once.
        AssertThat(StoryAttemptState.MigrateFromLegacyRoot(legacy))
            .OverrideFailureMessage("Migration must be idempotent.")
            .IsFalse();
    }

    [TestCase]
    public void MigrationNeverDefaultsAnActiveLegacyAttemptToFullAnchorsOrUnusedDefy() {
        var midLevel = new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = "res://scenes/campaign/Level_13_ChronalVoid.tscn",
            LastCheckpointID = "level_13_chronal_void_checkpoint_1"
        };
        StoryAttemptState.MigrateFromLegacyRoot(midLevel);
        AssertThat(midLevel.AttemptState.AnchorChargesRemaining)
            .OverrideFailureMessage("A legacy Act III attempt must not be handed free anchors.")
            .IsEqual(0);
        AssertThat(midLevel.AttemptState.DefyHistoryUsed)
            .OverrideFailureMessage("Defy must be assumed spent, never assumed available.")
            .IsTrue();

        // A payload that is NOT mid-level has no attempt history to lose, so it
        // migrates cleanly and plays on.
        var betweenLevels = new StorySaveData { SelectedCharacterID = "einstein" };
        StoryAttemptState.MigrateFromLegacyRoot(betweenLevels);
        AssertThat(betweenLevels.AttemptState.Status).IsEqual(StoryAttemptStatus.Active);
        AssertThat(betweenLevels.AttemptState.DefyHistoryUsed).IsFalse();
    }
}
