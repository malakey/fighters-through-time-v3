using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — V7.6 <b>Smothered</b>, the game's only Game Over.
///
/// <para>An Act III collapse with no anchor charge left is the real thing: the
/// field finishes the cradle. F10 makes it a genuinely terminal state rather
/// than a pause — it persists <b>before</b> its presentation, quitting and
/// loading return to the same screen without refilling HP or charges and without
/// replaying the failure fee, and only an explicit Restart Level starts a fresh
/// attempt. Deposited dust is always safe.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SmotheredGameOverTests {

    private const int ScratchSlot = 2;

    [TestCase]
    public void ACollapseWithNoChargeLeftPersistsSmotheredBeforeItsPresentation() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Hard);
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            save.DepositedChronalDust["einstein"] = 500;
            saves.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            story.BeginLevelRun();
            story.CollectDust(100);

            // Hard gives one charge: spend it on a Snap, then fail again.
            story.ResolveActIIIFailure("level_13_chronal_void_checkpoint_1", TimelineCollapseCause.Death);
            AssertThat(story.AnchorChargesRemaining).IsEqual(0);
            AssertThat(story.AttemptStatus).IsEqual(StoryAttemptStatus.Active);

            story.ResolveActIIIFailure("level_13_chronal_void_checkpoint_1", TimelineCollapseCause.Death);
            AssertThat(save.AttemptState.Status)
                .OverrideFailureMessage("The terminal state must be in the payload before the beat plays.")
                .IsEqual(StoryAttemptStatus.Smothered);
            AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.GameOverScenePath);
            AssertThat(save.DepositedChronalDust["einstein"])
                .OverrideFailureMessage("Deposited dust is always safe, including through Smothered.")
                .IsEqual(500);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[ScratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ReopeningASettledGameOverNeverChargesTheSameFailureAgain() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.NeoEarth, "einstein", Difficulty.Hard);
            saves.SaveSlots[ScratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            story.BeginLevelRun();
            story.SpendAnchorCharge();      // zero charges, no Snap available
            story.CollectDust(100);

            story.EnterSmothered("level_14_neo_earth_checkpoint_1");
            int afterFirstFee = story.ChronalDustCollected;
            AssertThat(afterFirstFee)
                .OverrideFailureMessage("The triggering collapse charges its 20% once.")
                .IsEqual(80);

            // Quit, load, land back here: the settled event is not re-charged.
            story.EnterSmothered("level_14_neo_earth_checkpoint_1");
            story.EnterSmothered("level_14_neo_earth_checkpoint_1");
            AssertThat(story.ChronalDustCollected)
                .OverrideFailureMessage("Reopening a settled failure screen must not repeat the fee.")
                .IsEqual(afterFirstFee);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[ScratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ASmotheredSaveReloadsToGameOverAndNeverToTheHub() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            var parked = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid),
                LastCheckpointID = "level_13_chronal_void_checkpoint_1",
                CurrentHP = 1,
                CurrentLives = 0
            };
            parked.AttemptState = StoryAttemptState.CreateFresh("level_13_chronal_void", 0);
            parked.AttemptState.Status = StoryAttemptStatus.Smothered;
            saves.SaveSlots[ScratchSlot] = parked;

            story.ResumeCampaign(ScratchSlot, parked);
            AssertThat(story.LastRequestedScenePath)
                .OverrideFailureMessage("Smothered loads Game Over, never the hub and never the level.")
                .IsEqual(StoryManager.GameOverScenePath);
            AssertThat(parked.CurrentHP)
                .OverrideFailureMessage("Loading a Smothered attempt refills nothing.")
                .IsEqual(1);
            AssertThat(story.AnchorChargesRemaining).IsEqual(0);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[ScratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void TheGameOverSurfaceOffersExactlyRestartLevelAndQuitOnAnAuthoredFocusChain() {
        GameOverScreen screen = GameOverScreen.CreateDefault();
        AssertThat(screen)
            .OverrideFailureMessage("scenes/ui/GameOver.tscn must exist and carry GameOverScreen.")
            .IsNotNull();
        screen.PerformNavigation = false;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(screen);
        int restarts = 0;
        int quits = 0;
        screen.RestartChosen += () => restarts++;
        screen.QuitChosen += () => quits++;
        try {
            AssertThat(screen.RestartButton).IsNotNull();
            AssertThat(screen.QuitButton).IsNotNull();
            // Raw keys in the .tscn, resolved by control auto-translation.
            AssertThat(screen.RestartButton.Text).IsEqual("game_over_restart_level");
            AssertThat(screen.QuitButton.Text).IsEqual("game_over_quit_to_menu");
            AssertThat(screen.FocusChain.Count)
                .OverrideFailureMessage("Two options, both reachable by controller.")
                .IsEqual(2);

            screen.RestartButton.EmitSignal(BaseButton.SignalName.Pressed);
            screen.QuitButton.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(restarts).IsEqual(1);
            AssertThat(quits).IsEqual(1);
        } finally {
            screen.Free();
        }
    }
}
