using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// A configurable minimal level for the cases below that need a level other
/// than the shared Orleans fixture — the Act III gauntlet in particular, whose
/// levels chain into each other without the hub. Top level and partial for the
/// Godot generator, like <see cref="FrameworkTestLevelController"/>.
/// </summary>
internal partial class LevelStartResetTestLevelController : StoryLevelControllerBase {
    /// <summary>Set before the node enters the tree.</summary>
    public CampaignLevel TestLevel = CampaignLevel.NeoEarth;

    public override string LevelID => StoryManager.GetLevelID(TestLevel);
    public override CampaignLevel Level => TestLevel;
    public override string LevelTitleKey => "neo_earth_level_title";
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => new(200, 850);
    public override Rect2 LevelBounds => new(0, 0, 4000, 1080);

    protected override void BuildLevel() {
        BuildFloor(0, 900, 4000);
        BuildWall(0, 0, 1080);
        BuildCheckpoint(200, 850, $"{LevelID}_checkpoint_0", CheckpointRole.Entry);
        BuildCheckpoint(2000, 850, $"{LevelID}_checkpoint_1", CheckpointRole.Middle);
    }
}

/// <summary>
/// level-start-reset — the F10 fresh-entry initialization
/// (<c>docs/design-contracts/STORY_PERSISTENCE.md</c>): "Fresh level entry:
/// 100% HP, full difficulty rewind pool, full block charges, zero Ultimate
/// meter, abilities ready", and a full Restart Level is "the same
/// initialization". Ordinary save, quit/resume and crash recovery keep the
/// latest durable HP and meter, and a death rewind keeps the meter.
///
/// <para>Before this the save's root HP and meter rode from one attempt into
/// the next: a completed level left its last durable values on the save, the
/// next level's entry read them back through
/// <see cref="StoryLevelControllerBase.RestoreDurableResources"/>, and neither
/// Restart path zeroed the meter. The durable snapshot also captured whatever
/// Story hero was in the tree under the loading screen — the hub's full-HP,
/// zero-meter hero over a resumed attempt, or the previous Act III level's
/// hero over the next level's fresh entry.</para>
///
/// <para>The fix-up adds the two doors the first pass missed: a Game Over
/// reached by loading a Smothered save restarts the level it is parked on, and
/// Quit to Main Menu flushes the latest HP and meter and stops the run, so the
/// per-second snapshot cannot refund the exit fee afterwards.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LevelStartResourceResetTests {

    private const int ScratchSlot = 2;
    private const string Hero = "einstein";
    private const double Step = 1.0 / 60.0;

    private static readonly string OrleansMiddle = $"{FrameworkTestLevelController.TestLevelID}_checkpoint_1";

    [TestCase]
    public void CompletingAWoundedChargedLevelThenEnteringTheNextStartsAtFullHPAndZeroMeter() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.Florence);

        // Florence's hero, wounded and charged, made durable the ordinary way.
        PlayerController florenceHero = scope.AddHero();
        int wounded = florenceHero.MaximumHP - 37;
        florenceHero.RestoreStoryCheckpoint(florenceHero.GlobalPosition, wounded, 65f);
        story.CaptureDurableSnapshot();
        AssertThat(save.CurrentHP).IsEqual(wounded);
        AssertThat(save.CurrentUltimateMeter).IsEqualApprox(65f, 0.001f);

        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Florence));
        AssertThat(story.CurrentLevel).IsEqual(FrameworkTestLevelController.TestLevel);
        AssertThat(save.CurrentLevelID)
            .IsEqual(StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel));
        AssertThat(save.LastCheckpointID).IsEqual("");
        AssertThat(save.CurrentUltimateMeter)
            .OverrideFailureMessage("Fixture: the completed level's meter must still be on the root.")
            .IsEqualApprox(65f, 0.001f);
        scope.Free(florenceHero);   // the hub replaced Florence

        // The hub portal: the first entry into the next uncompleted level.
        story.LoadCurrentLevel();
        AssertThat(story.LastRequestedScenePath)
            .IsEqual(StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel));
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();
        AssertThat(save.CurrentUltimateMeter)
            .OverrideFailureMessage("Minting the fresh attempt must zero the root meter.")
            .IsEqual(0f);

        FrameworkTestLevelController level = scope.BuildOrleans();
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertFresh(level.Player, save, "next level after a completion");
        } finally {
            scope.Free(level);
        }
    }

    [TestCase]
    public void AFreshEntryAfterADrainedMomentumGaugeStartsAtTwoPips() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.Florence);

        PlayerController florenceHero = scope.AddHero();
        florenceHero.RestoreMomentumPips(0.25f);
        story.CaptureDurableSnapshot();
        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Florence));
        scope.Free(florenceHero);

        story.LoadCurrentLevel();
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();
        FrameworkTestLevelController level = scope.BuildOrleans();
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertThat(level.Player.MomentumPips)
                .OverrideFailureMessage("A fresh entry must not inherit the previous level's drained gauge.")
                .IsEqual(FTT.Combat.BasicComboRules.MomentumStartPips);
        } finally {
            scope.Free(level);
        }
    }

    [TestCase]
    public void FlorenceAfterTheTutorialStartsAtFullHPAndZeroMeter() {
        // Florence predates StoryLevelControllerBase and calls the shared
        // RestoreDurableResources itself; the Tutorial's calibration fills and
        // spends the meter, so a charged, wounded Level 0 hero is realistic.
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.Tutorial);

        PlayerController tutorialHero = scope.AddHero();
        tutorialHero.RestoreStoryCheckpoint(tutorialHero.GlobalPosition, tutorialHero.MaximumHP - 50, 50f);
        story.CaptureDurableSnapshot();
        AssertThat(save.CurrentUltimateMeter).IsEqualApprox(50f, 0.001f);

        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Tutorial));
        AssertThat(story.CurrentLevel).IsEqual(CampaignLevel.Florence);
        scope.Free(tutorialHero);

        story.LoadCurrentLevel();
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();

        Level01Controller florence = scope.Own(new Level01Controller { Name = "LevelStartResetFlorence" });
        AssertFresh(florence.GetNode<PlayerController>("Player"), save, "Florence after the Tutorial");
        scope.Free(florence);
    }

    [TestCase]
    public void TheActIIIGauntletsNextLevelStartsFreshWithThePreviousHeroStillUnderTheLoadingScreen() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.ChronalVoid);

        PlayerController voidHero = scope.AddHero();
        int wounded = voidHero.MaximumHP / 3;
        voidHero.RestoreStoryCheckpoint(voidHero.GlobalPosition, wounded, 80f);
        story.CaptureDurableSnapshot();
        AssertThat(save.CurrentUltimateMeter).IsEqualApprox(80f, 0.001f);

        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.ChronalVoid));
        AssertThat(story.CurrentLevel).IsEqual(CampaignLevel.NeoEarth);

        // The Act III results panel's Return: Level 14 loads in place, no hub.
        story.LoadCurrentLevel();
        AssertThat(story.LastRequestedScenePath)
            .IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.NeoEarth));
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();

        // Under the loading screen the run is armed while Level 13 — and its
        // wounded, charged hero — is still in the tree. The per-second durable
        // snapshot must not capture that hero for the new attempt.
        story.SceneTransitionInProgressOverrideForTesting = true;
        story.CaptureDurableSnapshot();
        AssertThat(save.CurrentUltimateMeter)
            .OverrideFailureMessage("The loading-screen snapshot captured the previous level's meter.")
            .IsEqual(0f);
        AssertThat(save.CurrentHP)
            .OverrideFailureMessage("The loading-screen snapshot captured the previous level's HP.")
            .IsEqual(voidHero.MaximumHP);
        AssertThat(story.CurrentAttempt.PlayerResourceTimers.BlockCharges)
            .OverrideFailureMessage("The loading-screen snapshot captured the previous hero's resources.")
            .IsEqual(-1);
        story.SceneTransitionInProgressOverrideForTesting = null;
        scope.Free(voidHero);   // the scene change lands

        LevelStartResetTestLevelController level = scope.BuildActIII(CampaignLevel.NeoEarth);
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertFresh(level.Player, save, "the next Act III level");
        } finally {
            scope.Free(level);
        }
    }

    [TestCase]
    public void AFullRestartLevelFromThePauseMenuStartsAtZeroMeterAndFullHP() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(FrameworkTestLevelController.TestLevel);

        FrameworkTestLevelController live = scope.BuildOrleans();
        Node pauseHost = scope.Own(new Node { Name = "LevelStartResetPauseHost" });
        try {
            PlayerController hero = live.Player;
            hero.RestoreStoryCheckpoint(hero.GlobalPosition, hero.MaximumHP / 2, 70f);
            SaveManager.Instance.SaveCheckpoint(OrleansMiddle);
            AssertThat(save.CurrentUltimateMeter).IsEqualApprox(70f, 0.001f);

            PauseMenu pause = PauseMenu.CreateDefault();
            pauseHost.AddChild(pause);
            pause.SetPaused(true);
            pause.GetNode<Button>("Root/Center/Panel/Layout/RestartButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            var modal = pause.FindChild("RestartConfirmModal", recursive: true, owned: false) as ConfirmModal;
            AssertObject(modal).IsNotNull();
            modal.Confirm();

            AssertThat(save.LastCheckpointID).IsEqual("");
            AssertThat(save.CurrentUltimateMeter)
                .OverrideFailureMessage("Restart Level kept the meter.")
                .IsEqual(0f);
            AssertThat(story.IsFreshAttemptHeroPending).IsTrue();
        } finally {
            ((SceneTree)Engine.GetMainLoop()).Paused = false;
            scope.Free(pauseHost);
            scope.Free(live);
        }

        FrameworkTestLevelController restarted = scope.BuildOrleans();
        try {
            AssertThat(restarted.ResumedMidLevel).IsFalse();
            AssertFresh(restarted.Player, save, "Restart Level");
        } finally {
            scope.Free(restarted);
        }
    }

    [TestCase]
    public void RestartFromTheSmotheredGameOverStartsAtZeroMeterAndFullHP() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.ChronalVoid);

        PlayerController hero = scope.AddHero();
        hero.RestoreStoryCheckpoint(hero.GlobalPosition, 1, 55f);
        story.CaptureDurableSnapshot();
        story.EnterSmothered($"{StoryManager.GetLevelID(CampaignLevel.ChronalVoid)}_checkpoint_1");
        AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.GameOverScenePath);
        AssertThat(save.CurrentUltimateMeter).IsEqualApprox(55f, 0.001f);
        scope.Free(hero);   // Game Over has no hero

        story.RestartFromGameOver();
        AssertThat(story.LastRequestedScenePath)
            .IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid));
        AssertThat(save.CurrentUltimateMeter)
            .OverrideFailureMessage("Restart from Game Over kept the meter.")
            .IsEqual(0f);
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();

        LevelStartResetTestLevelController level = scope.BuildActIII(CampaignLevel.ChronalVoid);
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertFresh(level.Player, save, "Restart from Game Over");
        } finally {
            scope.Free(level);
        }
    }

    [TestCase]
    public void RestartFromASmotheredGameOverLoadedFromTheSaveRestartsTheParkedLevel() {
        // The Game Over restarts StoryManager.CollapsedLevel, an in-memory field
        // only EnterSmothered used to set. A Game Over reached by LOADING a
        // Smothered save (a fresh boot here, so it still reads the Tutorial)
        // re-stamped the save to the Tutorial and loaded it instead of
        // restarting the Act III level.
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        story.PrepareDirectLevel(CampaignLevel.Tutorial, Hero, Difficulty.Normal);
        AssertThat(story.CollapsedLevel).IsEqual(CampaignLevel.Tutorial);

        string voidPath = StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid);
        string voidID = StoryManager.GetLevelID(CampaignLevel.ChronalVoid);
        var parked = new StorySaveData {
            SelectedCharacterID = Hero,
            Difficulty = Difficulty.Normal,
            CurrentLevelID = voidPath,
            LastCheckpointID = $"{voidID}_checkpoint_1",
            CurrentHP = 1,
            CurrentUltimateMeter = 55f,
            CurrentLives = 0
        };
        parked.CompletedLevels.Add(StoryManager.GetLevelID(CampaignLevel.Tutorial));
        parked.AttemptState = StoryAttemptState.CreateFresh(voidID, 0);
        parked.AttemptState.Status = StoryAttemptStatus.Smothered;
        StorySaveData reloaded = SaveSchemaMigrator.DeserializeStory(JsonConvert.SerializeObject(parked));
        scope.UseSlot(reloaded);
        story.ResumeCampaign(ScratchSlot, reloaded);
        AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.GameOverScenePath);
        AssertThat(story.CollapsedLevel)
            .OverrideFailureMessage("Loading a Smothered save must restore the level its Game Over restarts.")
            .IsEqual(CampaignLevel.ChronalVoid);

        story.RestartFromGameOver();
        AssertThat(story.LastRequestedScenePath)
            .OverrideFailureMessage("Restart from a reloaded Game Over loaded the wrong level.")
            .IsEqual(voidPath);
        AssertThat(story.CurrentLevel).IsEqual(CampaignLevel.ChronalVoid);
        AssertThat(reloaded.CurrentLevelID)
            .OverrideFailureMessage("Restart from a reloaded Game Over re-stamped the save to another level.")
            .IsEqual(voidPath);
        AssertThat(reloaded.LastCheckpointID).IsEqual("");
        AssertThat(reloaded.CurrentUltimateMeter).IsEqual(0f);
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();

        LevelStartResetTestLevelController level = scope.BuildActIII(CampaignLevel.ChronalVoid);
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertFresh(level.Player, reloaded, "Restart from a reloaded Game Over");
        } finally {
            scope.Free(level);
        }
    }

    [TestCase]
    public void AMidLevelQuitAndResumeKeepsTheDurableHPAndMeter() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(FrameworkTestLevelController.TestLevel);

        int wounded;
        FrameworkTestLevelController live = scope.BuildOrleans();
        try {
            PlayerController hero = live.Player;
            wounded = hero.MaximumHP - 29;
            hero.RestoreStoryCheckpoint(hero.GlobalPosition, wounded, 42f);
            SaveManager.Instance.SaveCheckpoint(OrleansMiddle);
        } finally {
            scope.Free(live);   // quit
        }

        // Load from the main menu: Acts I-II route to the hub, whose hero is
        // a fresh full-HP, zero-meter build — and it is still in the tree
        // under the loading screen when the portal starts the level.
        StorySaveData reloaded = SaveSchemaMigrator.DeserializeStory(JsonConvert.SerializeObject(save));
        scope.UseSlot(reloaded);
        story.ResumeCampaign(ScratchSlot, reloaded);
        AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.HubScenePath);
        PlayerController hubHero = scope.AddHero();
        story.LoadCurrentLevel();
        AssertThat(story.IsFreshAttemptHeroPending)
            .OverrideFailureMessage("A resume must never arm fresh-entry initialization.")
            .IsFalse();
        story.SceneTransitionInProgressOverrideForTesting = true;
        story.CaptureDurableSnapshot();
        AssertThat(reloaded.CurrentHP)
            .OverrideFailureMessage("The loading-screen snapshot wrote the hub hero's HP over the resume.")
            .IsEqual(wounded);
        AssertThat(reloaded.CurrentUltimateMeter)
            .OverrideFailureMessage("The loading-screen snapshot wrote the hub hero's meter over the resume.")
            .IsEqualApprox(42f, 0.001f);
        story.SceneTransitionInProgressOverrideForTesting = null;
        scope.Free(hubHero);

        FrameworkTestLevelController resumed = scope.BuildOrleans();
        try {
            AssertThat(resumed.ResumedMidLevel).IsTrue();
            AssertThat(resumed.Player.CurrentHP).IsEqual(wounded);
            AssertThat(resumed.Player.CurrentUltimateMeter).IsEqualApprox(42f, 0.001f);
            AssertThat(MeterNodeValue(resumed.Player)).IsEqualApprox(42f, 0.001f);
        } finally {
            scope.Free(resumed);
        }
    }

    [TestCase]
    public void QuitToTheMainMenuFlushesTheLatestHPAndMeterAndKeepsTheExitFee() {
        // F10: "An orderly Save/exit flushes the latest state." Quit to Main
        // Menu never stopped the level run, so the per-second snapshot kept
        // writing the unpenalized wallet over the 20% exit fee under the
        // loading screen and in the main menu. And with the snapshot skipping
        // the hero during a load, the quit-time HP and meter were never saved.
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(FrameworkTestLevelController.TestLevel);

        const int Wallet = 100;
        int retained = PauseMenu.CalculateExitWalletAfterPenalty(Wallet, Wallet);
        AssertThat(retained < Wallet)
            .OverrideFailureMessage("Fixture: the exit fee must take something.")
            .IsTrue();

        int wounded;
        FrameworkTestLevelController live = scope.BuildOrleans();
        Node pauseHost = scope.Own(new Node { Name = "LevelStartResetQuitHost" });
        try {
            PlayerController hero = live.Player;
            story.SetDust(Wallet);
            hero.RestoreStoryCheckpoint(hero.GlobalPosition, hero.MaximumHP - 10, 20f);
            SaveManager.Instance.SaveCheckpoint(OrleansMiddle);
            AssertThat(save.CurrentUltimateMeter).IsEqualApprox(20f, 0.001f);

            // Newer than anything durable: what the quit has to flush.
            wounded = hero.MaximumHP - 41;
            hero.RestoreStoryCheckpoint(hero.GlobalPosition, wounded, 64f);

            PauseMenu pause = PauseMenu.CreateDefault();
            pauseHost.AddChild(pause);
            pause.SetPaused(true);
            pause.GetNode<Button>("Root/Center/Panel/Layout/QuitButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            var modal = pause.FindChild("ConfirmModal", recursive: true, owned: false) as ConfirmModal;
            AssertObject(modal).IsNotNull();
            AssertThat(modal.IsOpen).IsTrue();
            modal.Confirm();

            AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.MainMenuScenePath);
            AssertThat(story.IsLevelTimerRunning)
                .OverrideFailureMessage("Quit to Main Menu left the level run live.")
                .IsFalse();
            AssertThat(save.CurrentHP)
                .OverrideFailureMessage("Quit to Main Menu did not flush the latest HP.")
                .IsEqual(wounded);
            AssertThat(save.CurrentUltimateMeter)
                .OverrideFailureMessage("Quit to Main Menu did not flush the latest meter.")
                .IsEqualApprox(64f, 0.001f);
            AssertThat(save.LevelChronalDust).IsEqual(retained);
            AssertThat(story.ChronalDustCollected).IsEqual(retained);

            // The autoload keeps ticking under the loading screen (the level and
            // its hero are still in the tree) and then in the main menu.
            story.SceneTransitionInProgressOverrideForTesting = true;
            for (int second = 0; second < 3; second++) story._PhysicsProcess(1.1);
            story.SceneTransitionInProgressOverrideForTesting = null;
            AssertThat(save.LevelChronalDust)
                .OverrideFailureMessage("The durable snapshot refunded the exit fee after the quit.")
                .IsEqual(retained);
            AssertThat(save.CurrentHP).IsEqual(wounded);
            AssertThat(save.CurrentUltimateMeter).IsEqualApprox(64f, 0.001f);
        } finally {
            ((SceneTree)Engine.GetMainLoop()).Paused = false;
            scope.Free(pauseHost);
            scope.Free(live);
        }

        // Load from the main menu, then the hub portal: the quit-time state comes back.
        StorySaveData reloaded = SaveSchemaMigrator.DeserializeStory(JsonConvert.SerializeObject(save));
        scope.UseSlot(reloaded);
        story.ResumeCampaign(ScratchSlot, reloaded);
        AssertThat(story.ChronalDustCollected).IsEqual(retained);
        story.LoadCurrentLevel();
        AssertThat(story.IsFreshAttemptHeroPending).IsFalse();

        FrameworkTestLevelController resumed = scope.BuildOrleans();
        try {
            AssertThat(resumed.ResumedMidLevel).IsTrue();
            AssertThat(resumed.Player.CurrentHP).IsEqual(wounded);
            AssertThat(resumed.Player.CurrentUltimateMeter).IsEqualApprox(64f, 0.001f);
        } finally {
            scope.Free(resumed);
        }
    }

    [TestCase]
    public void ADeathRewindKeepsTheMeter() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(FrameworkTestLevelController.TestLevel);

        FrameworkTestLevelController level = scope.BuildOrleans();
        try {
            PlayerController hero = level.Player;
            AssertThat(hero.CurrentUltimateMeter).IsEqual(0f);
            hero.RestoreStoryCheckpoint(hero.GlobalPosition, hero.MaximumHP, 45f);
            ChronalRewindManager manager = level.Services.RewindManager;
            AssertObject(manager).IsNotNull();
            for (int frame = 0; frame < 60; frame++) {
                hero.GlobalPosition = new Vector2(400f + frame * 4f, 850f);
                manager._PhysicsProcess(Step);
            }

            hero.ApplyDamage(hero.CurrentHP);
            AssertThat(manager.IsRewinding).IsTrue();
            for (int i = 0; i < 900 && manager.IsRewinding; i++) manager._PhysicsProcess(Step);
            AssertThat(manager.IsRewinding).IsFalse();

            AssertThat(hero.CurrentHP > 0).IsTrue();
            AssertThat(hero.CurrentUltimateMeter >= 45f)
                .OverrideFailureMessage($"The death rewind reset the meter to {hero.CurrentUltimateMeter}.")
                .IsTrue();
            AssertThat(story.IsFreshAttemptHeroPending).IsFalse();
            story.CaptureDurableSnapshot();
            AssertThat(save.CurrentUltimateMeter).IsEqualApprox(hero.CurrentUltimateMeter, 0.001f);
        } finally {
            scope.Free(level);
        }
    }

    [TestCase]
    public void ASaveParkedAtTheHubBeforeThisRuleStartsTheNextLevelFresh() {
        using var scope = new Scope();
        StoryManager story = StoryManager.Instance;
        // What the previous build wrote after completing Florence: the next
        // level stamped, no checkpoint, and Florence's last HP and meter still
        // on the root. No schema change: the reset is derived at mint.
        var parked = new StorySaveData {
            SelectedCharacterID = Hero,
            Difficulty = Difficulty.Normal,
            CurrentLevelID = StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel),
            LastCheckpointID = "",
            CurrentHP = 23,
            CurrentUltimateMeter = 75f,
            CurrentLives = 3
        };
        parked.CompletedLevels.Add(StoryManager.GetLevelID(CampaignLevel.Tutorial));
        parked.CompletedLevels.Add(StoryManager.GetLevelID(CampaignLevel.Florence));
        StorySaveData reloaded = SaveSchemaMigrator.DeserializeStory(JsonConvert.SerializeObject(parked));
        scope.UseSlot(reloaded);
        story.ResumeCampaign(ScratchSlot, reloaded);
        AssertThat(story.LastRequestedScenePath).IsEqual(StoryManager.HubScenePath);
        AssertThat(reloaded.CurrentUltimateMeter)
            .OverrideFailureMessage("Loading to the hub refills or drains nothing.")
            .IsEqualApprox(75f, 0.001f);

        story.LoadCurrentLevel();
        AssertThat(story.IsFreshAttemptHeroPending).IsTrue();
        AssertThat(reloaded.CurrentUltimateMeter).IsEqual(0f);

        FrameworkTestLevelController level = scope.BuildOrleans();
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertFresh(level.Player, reloaded, "an old save parked at the hub");
        } finally {
            scope.Free(level);
        }
    }

    // === Helpers ===========================================================

    private static float MeterNodeValue(PlayerController player) =>
        player.GetNodeOrNull<FTT.Combat.UltimateMeter>("UltimateMeter")?.CurrentValue ?? player.CurrentUltimateMeter;

    private static void AssertFresh(PlayerController player, StorySaveData save, string when) {
        AssertObject(player).IsNotNull();
        AssertThat(player.CurrentHP)
            .OverrideFailureMessage($"{when}: the hero did not start at 100% HP.")
            .IsEqual(player.MaximumHP);
        AssertThat(player.CurrentUltimateMeter)
            .OverrideFailureMessage($"{when}: the hero did not start at zero meter.")
            .IsEqual(0f);
        AssertThat(MeterNodeValue(player))
            .OverrideFailureMessage($"{when}: the meter node did not start at zero.")
            .IsEqual(0f);
        AssertThat(player.CurrentBlockCharges)
            .OverrideFailureMessage($"{when}: the hero did not start with full block charges.")
            .IsEqual(player.MaximumBlockCharges);
        AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
        AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
        AssertThat(player.MovementAbilityCooldownTimer).IsEqual(0f);
        AssertThat(player.MomentumPips)
            .OverrideFailureMessage($"{when}: the Momentum gauge did not start at two pips.")
            .IsEqual(FTT.Combat.BasicComboRules.MomentumStartPips);
        AssertThat(save.CurrentHP)
            .OverrideFailureMessage($"{when}: the save root HP was not reset.")
            .IsEqual(player.MaximumHP);
        AssertThat(save.CurrentUltimateMeter)
            .OverrideFailureMessage($"{when}: the save root meter was not reset.")
            .IsEqual(0f);
        AssertThat(StoryManager.Instance.IsFreshAttemptHeroPending)
            .OverrideFailureMessage($"{when}: the fresh-hero initialization was not consumed.")
            .IsFalse();
    }

    /// <summary>
    /// Owns every shared singleton a case touches and hands each one back:
    /// the scratch save slot, the session, the scene-load and loading-window
    /// seams, the run state and the pause flag. Every node it adds to the tree
    /// (heroes join the <c>StoryPlayer</c> group) is freed on dispose too, so a
    /// failing assertion can never leak a hero into later suites.
    /// </summary>
    private sealed class Scope : IDisposable {
        private readonly List<Node> _owned = new();
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly Difficulty _originalDifficulty;
        private readonly CampaignLevel _originalLevel;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public Scope() {
            _originalPaused = ((SceneTree)Engine.GetMainLoop()).Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalLevel = StoryManager.Instance.CurrentLevel;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            StoryManager.Instance.SuppressSceneLoadsForTesting = true;
        }

        /// <summary>A fresh entry into <paramref name="level"/> on the scratch slot.</summary>
        public StorySaveData Begin(CampaignLevel level) {
            StoryManager story = StoryManager.Instance;
            story.PrepareDirectLevel(level, Hero, Difficulty.Normal);
            var save = new StorySaveData {
                SelectedCharacterID = Hero,
                Difficulty = Difficulty.Normal,
                CurrentLevelID = StoryManager.GetLevelScenePath(level),
                CurrentHP = 100,
                CurrentLives = 3
            };
            UseSlot(save);
            story.BeginLevelRun();
            return save;
        }

        public void UseSlot(StorySaveData save) {
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = Hero;
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
        }

        /// <summary>A bare Story hero in the tree (the StoryPlayer group): a level's or the hub's.</summary>
        public PlayerController AddHero() {
            PlayerController hero = CharacterFactory.CreateCharacter(Hero, 0);
            hero.Name = "LevelStartResetHero";
            Own(hero);
            hero.GlobalPosition = new Vector2(400f, 850f);
            return hero;
        }

        public FrameworkTestLevelController BuildOrleans() {
            var level = new FrameworkTestLevelController { Name = "LevelStartResetOrleans" };
            Own(level);
            return level;
        }

        public LevelStartResetTestLevelController BuildActIII(CampaignLevel campaignLevel) {
            var level = new LevelStartResetTestLevelController {
                Name = "LevelStartResetActIII",
                TestLevel = campaignLevel
            };
            Own(level);
            return level;
        }

        /// <summary>Adds <paramref name="node"/> under the root and frees it on dispose.</summary>
        public T Own<T>(T node) where T : Node {
            _owned.Add(node);
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);
            return node;
        }

        public void Free(Node node) {
            _owned.Remove(node);
            if (!GodotObject.IsInstanceValid(node)) return;
            node.GetParent()?.RemoveChild(node);
            node.Free();
        }

        public void Dispose() {
            foreach (Node node in _owned.ToArray()) Free(node);
            StoryManager story = StoryManager.Instance;
            story.SuppressSceneLoadsForTesting = false;
            story.SceneTransitionInProgressOverrideForTesting = null;
            story.TryConsumeCompletionDepositNotice(out _);
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? Hero : _originalCharacter, _originalDifficulty);
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            LevelRewardDirectory.ResetAttempt();
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = _originalDifficulty;
            // ResumeCampaign and SaveCheckpoint refresh the session marker; a test is not a session.
            SessionExitGuard.ClearMarker();
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
