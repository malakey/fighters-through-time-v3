using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W2 — H02 (2026-09-26): Chronal Dust banks <b>only</b> in the
/// level-completion transaction.
///
/// <para>Before this the hub deposited the carried wallet on arrival and again
/// whenever the Repository was opened, so a Collapse or a voluntary exit
/// quietly banked the surviving 80% of an unfinished level. Now every level —
/// not just the Act III gauntlet — deposits once at completion; a hub return
/// deposits nothing; the open attempt keeps its held dust until that level is
/// completed; and a full Restart Level clears it.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompletionOnlyBankingTests {

    private const int ScratchSlot = 2;
    private const string Hero = "einstein";

    [TestCase]
    public void AnActsOneToTwoCompletionBanksTheWholeWalletOnceInItsTransaction() {
        using var scope = new StoryScope();
        StorySaveData save = scope.Begin(CampaignLevel.Orleans, deposited: 100);
        StoryManager story = StoryManager.Instance;
        story.CollectDust(120);

        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Orleans));

        int bonus = story.LastLevelTierBonusDust + story.LastLevelRankBonusDust;
        AssertThat(save.DepositedChronalDust[Hero])
            .OverrideFailureMessage("Acts I-II must bank at completion, not wait for the hub.")
            .IsEqual(100 + 120 + bonus);
        AssertThat(story.ChronalDustCollected).IsEqual(0);
        AssertThat(save.LevelChronalDust).IsEqual(0);
        AssertThat(story.LastCompletionDepositDust).IsEqual(120 + bonus);
        AssertThat(story.TryConsumeCompletionDepositNotice(out int announced)).IsTrue();
        AssertThat(announced).IsEqual(120 + bonus);
        AssertThat(story.TryConsumeCompletionDepositNotice(out _))
            .OverrideFailureMessage("The arrival notice is shown once.")
            .IsFalse();
    }

    [TestCase]
    public void ACollapseAndTheHubReturnDepositNothingAndTheAttemptKeepsItsHeldDust() {
        using var scope = new StoryScope();
        StorySaveData save = scope.Begin(CampaignLevel.Orleans, deposited: 50);
        StoryManager story = StoryManager.Instance;
        story.CollectDust(100);

        story.BeginTimelineCollapse($"{StoryManager.GetLevelID(CampaignLevel.Orleans)}_checkpoint_1");

        AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.HubScenePath);
        AssertThat(save.DepositedChronalDust[Hero])
            .OverrideFailureMessage("A Collapse must never bank.")
            .IsEqual(50);
        AssertThat(save.LevelChronalDust).IsEqual(80);
        AssertThat(story.AttemptHeldDust).IsEqual(80);

        // The hub itself: arriving and opening its surfaces banks nothing.
        var tree = (SceneTree)Engine.GetMainLoop();
        Node hub = ResourceLoader.Load<PackedScene>(StoryManager.HubScenePath).Instantiate();
        tree.Root.AddChild(hub);
        try {
            AssertThat(save.DepositedChronalDust[Hero])
                .OverrideFailureMessage("The hub's arrival deposit is retired by H02.")
                .IsEqual(50);
            AssertThat(story.ChronalDustCollected).IsEqual(80);
            var ledger = hub.GetNodeOrNull<Label>("HubHUD/RepositoryLedger");
            AssertObject(ledger).IsNotNull();
            AssertThat(ledger.Visible).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(hub);
            hub.GetParent()?.RemoveChild(hub);
            hub.Free();
        }

        // Resuming from the anchor keeps the held dust; completing banks it.
        AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: true)).IsTrue();
        AssertThat(story.ChronalDustCollected).IsEqual(80);
        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Orleans));
        AssertThat(save.DepositedChronalDust[Hero])
            .IsEqual(50 + 80 + story.LastLevelTierBonusDust + story.LastLevelRankBonusDust);
    }

    [TestCase]
    public void AFullRestartAfterACollapseClearsTheHeldDustAndLeavesDepositsAlone() {
        using var scope = new StoryScope();
        StorySaveData save = scope.Begin(CampaignLevel.Orleans, deposited: 50);
        StoryManager story = StoryManager.Instance;
        story.CollectDust(100);
        story.BeginTimelineCollapse($"{StoryManager.GetLevelID(CampaignLevel.Orleans)}_checkpoint_1");
        AssertThat(story.AttemptHeldDust).IsEqual(80);

        AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: false)).IsTrue();
        AssertThat(story.ChronalDustCollected)
            .OverrideFailureMessage("Restart Level clears the open attempt's wallet (F02/F10).")
            .IsEqual(0);
        AssertThat(save.LevelChronalDust).IsEqual(0);
        AssertThat(save.DepositedChronalDust[Hero]).IsEqual(50);
    }

    /// <summary>
    /// RESULTS-DUST-LINES-AFTER-RESET (D12 review fix-up): an Anchor Snap's fee
    /// and a later voluntary exit fee are both itemised on the results of the
    /// attempt that paid them. Each is followed by a reload of that attempt
    /// (the Snap's in-place reconstruction, then a slot load from the main
    /// menu), and each reload's reset used to zero the in-memory tally; the
    /// exit fee was never recorded at all. The carried-in figure makes the
    /// itemisation add up to the bank: 10 earned after the last load + 100
    /// carried + bonus − 36 lost = 74 + bonus.
    /// </summary>
    [TestCase]
    public void AnAnchorSnapAndAnExitFeeBothReachTheResumedAttemptsResults() {
        using var scope = new StoryScope();
        StorySaveData save = scope.Begin(CampaignLevel.ChronalVoid, deposited: 0);
        StoryManager story = StoryManager.Instance;
        string levelID = StoryManager.GetLevelID(CampaignLevel.ChronalVoid);
        story.CollectDust(100);

        // The Snap: 20% fee, then the in-place reload of the same attempt.
        story.BeginAnchorSnap($"{levelID}_checkpoint_1");
        AssertThat(story.ChronalDustCollected).IsEqual(80);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt)
            .OverrideFailureMessage("The Snap's reload must not zero the fee it just charged.")
            .IsEqual(20);
        AssertThat(story.LevelDustCarriedIn).IsEqual(100);

        // Quit to Main Menu: the pause menu charges 20% of the 80 held...
        int retained = SessionExitGuard.CalculateExitWalletAfterPenalty(80, 80);
        AssertThat(retained).IsEqual(64);
        story.CommitOrderlyExit(retained);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt)
            .OverrideFailureMessage("The exit fee is a loss on the attempt that paid it.")
            .IsEqual(36);
        // ...and loading the slot again routes an Act III attempt straight back
        // into the level, a resume of the same attempt.
        story.ResumeCampaign(ScratchSlot, save);
        AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid));
        AssertThat(story.ChronalDustCollected).IsEqual(64);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt).IsEqual(36);
        AssertThat(story.LevelDustCarriedIn).IsEqual(100);

        story.CollectDust(10);
        EventBus.Instance.RaiseLevelComplete(levelID);

        int bonus = story.LastLevelTierBonusDust + story.LastLevelRankBonusDust;
        AssertThat(story.LastLevelDustLost).IsEqual(36);
        AssertThat(story.LastLevelDustCarriedIn).IsEqual(100);
        AssertThat(story.LastCompletionDepositDust).IsEqual(74 + bonus);
        AssertThat(10 + story.LastLevelDustCarriedIn + bonus - story.LastLevelDustLost)
            .OverrideFailureMessage("The itemisation must add up to what the completion banked.")
            .IsEqual(story.LastCompletionDepositDust);
        AssertThat(save.DepositedChronalDust[Hero]).IsEqual(74 + bonus);
    }

    /// <summary>
    /// The loss tally is keyed by the attempt's GUID: a reload of any other
    /// attempt (another slot loaded in the same session) never inherits it,
    /// and a fee charged to another attempt starts the tally over instead of
    /// adding to it.
    /// </summary>
    [TestCase]
    public void AnotherAttemptNeverInheritsTheLossTally() {
        using var scope = new StoryScope();
        StorySaveData save = scope.Begin(CampaignLevel.Orleans, deposited: 0);
        StoryManager story = StoryManager.Instance;
        string levelID = StoryManager.GetLevelID(CampaignLevel.Orleans);
        story.CollectDust(100);
        story.BeginTimelineCollapse($"{levelID}_checkpoint_1");
        AssertThat(LevelRewardDirectory.DustLostThisAttempt).IsEqual(20);

        // A different attempt's parked save: the reset zeroes the tally.
        var otherAttempt = new StorySaveData {
            SelectedCharacterID = Hero,
            Difficulty = Difficulty.Normal,
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Orleans),
            LastCheckpointID = $"{levelID}_checkpoint_1",
            AttemptState = StoryAttemptState.CreateFresh(levelID, 0)
        };
        AssertThat(otherAttempt.AttemptState.AttemptID).IsNotEqual(save.AttemptState.AttemptID);
        story.RestoreAttemptStateFromSave(otherAttempt);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt)
            .OverrideFailureMessage("A reload of another attempt must not inherit this attempt's loss.")
            .IsEqual(0);

        // Positive control: the attempt that paid carries its own loss. The
        // slot is still parked on it, so this load is a resume of it, and its
        // exit fee is recorded against it.
        story.SetDust(100);
        story.BeginLevelRun();
        AssertThat(LevelRewardDirectory.DustLostThisAttempt).IsEqual(0);
        story.CommitOrderlyExit(80);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt).IsEqual(20);
        string payingAttempt = save.AttemptState.AttemptID;
        story.RestoreAttemptStateFromSave(save);
        AssertThat(save.AttemptState.AttemptID).IsEqual(payingAttempt);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt).IsEqual(20);

        // A fee charged to a different attempt while the tally still holds
        // this one's (a hub exit after loading another slot) starts it over.
        story.ResumeCampaign(ScratchSlot, otherAttempt);
        story.SetDust(50);
        story.CommitOrderlyExit(40);
        AssertThat(LevelRewardDirectory.DustLostThisAttempt)
            .OverrideFailureMessage("Two attempts' losses must never be added together.")
            .IsEqual(10);
    }

    [TestCase]
    public void TheRepositoryLedgerShowsTheLastBankAndAnyHeldDustAsSeparateLines() {
        AssertThat(HubWorldController.RepositoryLedgerLines(0, 0).Count).IsEqual(0);
        var both = HubWorldController.RepositoryLedgerLines(143, 64);
        AssertThat(both.Count).IsEqual(2);
        AssertThat(both[0].Key).IsEqual(HubWorldController.RepositoryLastBankedKey);
        AssertThat(both[0].Amount).IsEqual(143);
        AssertThat(both[1].Key).IsEqual(HubWorldController.RepositoryAttemptHeldKey);
        AssertThat(both[1].Amount).IsEqual(64);
        var heldOnly = HubWorldController.RepositoryLedgerLines(0, 64);
        AssertThat(heldOnly.Count).IsEqual(1);
        AssertThat(heldOnly[0].Key).IsEqual(HubWorldController.RepositoryAttemptHeldKey);
        foreach (string key in new[] {
                     HubWorldController.RepositoryLastBankedKey,
                     HubWorldController.RepositoryAttemptHeldKey,
                     "hub_dust_deposited_toast" }) {
            AssertThat(TranslationServer.Translate(key).ToString())
                .OverrideFailureMessage($"'{key}' does not resolve through the compiled translation.")
                .IsNotEqual(key);
        }
    }

    /// <summary>Scratch slot + session + story state, all handed back on dispose.</summary>
    private sealed class StoryScope : IDisposable {
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly Difficulty _originalDifficulty;
        private readonly CampaignLevel _originalLevel;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public StoryScope() {
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalLevel = StoryManager.Instance.CurrentLevel;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            _originalPaused = ((SceneTree)Engine.GetMainLoop()).Paused;
            StoryManager.Instance.SuppressSceneLoadsForTesting = true;
        }

        public StorySaveData Begin(CampaignLevel level, int deposited) {
            StoryManager story = StoryManager.Instance;
            story.PrepareDirectLevel(level, Hero, Difficulty.Normal);
            var save = new StorySaveData {
                SelectedCharacterID = Hero,
                Difficulty = Difficulty.Normal,
                CurrentLevelID = StoryManager.GetLevelScenePath(level),
                CurrentHP = 100
            };
            save.DepositedChronalDust[Hero] = deposited;
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            story.BeginLevelRun();
            return save;
        }

        public void Dispose() {
            StoryManager story = StoryManager.Instance;
            story.SuppressSceneLoadsForTesting = false;
            story.TryConsumeCompletionDepositNotice(out _);
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? Hero : _originalCharacter, _originalDifficulty);
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = _originalDifficulty;
            SessionExitGuard.ClearMarker();
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
