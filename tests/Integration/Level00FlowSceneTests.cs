using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

/// <summary>
/// Package 13 W3 (S02/S09/S10, D16): Level 0's new Part 1 and Part 2 on the real
/// scene. The hold is unlosable and spawns the hero's era locals when the voice
/// finishes; the beam break shows the ~2 s Eraser watcher and opens the Warden
/// rift; the Translation lands the hero in the bay, healed, for the arrival
/// dialogue; Wren's offer is the two-option prompt, and Full runs the
/// calibration.
///
/// <para>Driven through the controller's public beats and the EventBus, never
/// by stepping frames: a sync test never lets the deferred hold start run, and
/// dialogue completion is raised the way <c>DialogueManager</c> raises it. Every
/// case restores <c>SceneTree.Paused</c> and frees the level (and with it the
/// grouped <c>StoryPlayer</c>) in <c>finally</c>. No save slot is active, so
/// nothing here writes a save or advances the campaign.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level00FlowSceneTests {

    private const string ScenePath = "res://scenes/campaign/Level_00_Tutorial.tscn";

    [TestCase]
    public void TheHoldIsUnlosableSpawnsTheErasLocalsAndBreaksIntoTheWardenRift() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Hold);

            level.StartHoldFight();
            var player = level.GetNode<PlayerController>("Player");
            AssertThat(player.ScriptedHPFloor).IsEqual(Level00HoldFight.HeroHPFloor);

            // The Rally seam the hold uses every physics frame: a landed blow leaves
            // an echo, and the hold takes it back without healing.
            player.ApplyEnvironmentalDamage(10);
            int afterBlow = player.CurrentHP;
            player.ClearRallyEcho();
            AssertThat(player.EchoPool).IsEqual(0f);
            AssertThat(player.CurrentHP).IsEqual(afterBlow);

            // Unlosable: no blow can take the hero below 1 HP, so nothing dies,
            // no rewind charge is spent and Defy never has to fire.
            player.ApplyDamage(player.MaximumHP * 5);
            AssertThat(player.CurrentHP).IsEqual(1);
            AssertThat(player.CurrentState == CharacterState.Dead).IsFalse();

            // The voice finishes: the locals walk out of the tear.
            EventBus.Instance.RaiseDialogueComplete("level_00.hold_start");
            AssertThat(level.HoldStep).IsEqual(HoldFightStep.FightOff);
            int locals = 0;
            foreach (Node child in level.GetChildren()) {
                if (child is EnemyController enemy && enemy.IsInsideTree()) {
                    locals++;
                    AssertThat(enemy.IsSummoned)
                        .OverrideFailureMessage("Hold locals must draw no dust award.").IsTrue();
                }
            }
            AssertThat(locals).IsEqual(Level00HoldFight.LocalCount);

            for (int index = 0; index < Level00HoldFight.LocalsBeforeGuard; index++) {
                EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload { EnemyID = "hold_local" });
            }
            AssertThat(level.HoldStep).IsEqual(HoldFightStep.Guard);

            // Beat 6: the beam breaks, the watcher stands at the tear, the rift opens.
            level.BreakTheBeam();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Rift);
            AssertObject(level.GetNodeOrNull<Node2D>("EraserWatcher"))
                .OverrideFailureMessage("S10: the Eraser watcher must appear as the beam breaks.").IsNotNull();
            AssertThat(Level00Controller.WatcherSeconds).IsEqual(2f);
            AssertObject(level.GetNodeOrNull<Area2D>("WardenRiftGate")).IsNotNull();
            AssertThat(player.ScriptedHPFloor)
                .OverrideFailureMessage("The HP floor must end with the hold.").IsEqual(0);
        } finally {
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    [TestCase]
    public void TheArrivalEndsOnWrensOfferAndFullCalibrationRunsTheLessons() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            var player = level.GetNode<PlayerController>("Player");

            level.BreakTheBeam();
            player.ApplyDamage(10);
            level.TranslateToBay();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Arrival);
            AssertThat(player.GlobalPosition).IsEqual(Level00Controller.BayArrivalPosition);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("\"You're whole. Mostly.\" The Translation heals the hero.")
                .IsEqual(player.MaximumHP);
            AssertThat(level.GetNodeOrNull<Node2D>("ChronalFracture").Visible).IsFalse();

            // Arrival -> Wren's line -> the two-option prompt (D16).
            EventBus.Instance.RaiseDialogueComplete("level_00.intro");
            AssertObject(level.CalibrationPrompt)
                .OverrideFailureMessage("The prompt waits for Wren's offer to finish.").IsNull();
            EventBus.Instance.RaiseDialogueComplete("level_00.wren_offer");
            TwoOptionPrompt prompt = level.CalibrationPrompt;
            AssertObject(prompt).IsNotNull();
            AssertThat(prompt.IsOpen).IsTrue();
            AssertString(prompt.Modal.CancelButton.Text).IsEqual("level00_choice_full");
            AssertString(prompt.Modal.ConfirmButton.Text).IsEqual("level00_choice_skip");
            AssertThat(player.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);

            prompt.ChooseFirst();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Calibration);
            AssertThat(player.ProcessMode).IsEqual(Node.ProcessModeEnum.Inherit);
            AssertObject(level.GetNodeOrNull<Node>("CalibrationDummy"))
                .OverrideFailureMessage("Full calibration starts with the dummy.").IsNotNull();
        } finally {
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// MM-SDEFY (plan 2.14): the Meter &amp; Defy lesson sets the bar to exactly the Defy cost, a
    /// drone swarm comes with the hero's HP floored at 1, and the step completes only when the
    /// hero presses Defy and the blast fires.
    /// </summary>
    [TestCase]
    public void TheDefyLessonNeedsTheButtonAndTheSwarmCannotKillTheHero() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            var player = level.GetNode<PlayerController>("Player");
            level.BreakTheBeam();
            level.TranslateToBay();
            EventBus.Instance.RaiseDialogueComplete("level_00.intro");
            EventBus.Instance.RaiseDialogueComplete("level_00.wren_offer");
            level.CalibrationPrompt.ChooseFirst();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Calibration);
            TutorialCalibrationScript script = level.CalibrationScript;
            while (script.Step != TutorialCalibrationStep.MeterAndDefy) {
                script.RegisterBasicHit();
                script.RegisterRallyReclaimHit();
                script.RegisterBlockedHit();
                script.RegisterGrabThrow();
                script.RegisterDirectionalInfluenceBeat();
                script.RegisterLandingTech();
            }
            player.SetUltimateMeter(0f);
            EventBus.Instance.RaiseDialogueComplete("level_00.ultimate_intro");
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("The lesson sets the bar to exactly the Defy cost.")
                .IsEqual(FTT.Combat.BasicComboRules.DefyInfluenceCost);

            // The swarm comes after the objective has been read; the hero cannot be lost.
            for (int frame = 0; frame < 125; frame++) level._PhysicsProcess(1.0 / 60.0);
            AssertThat(player.ScriptedHPFloor).IsEqual(Level00HoldFight.HeroHPFloor);
            player.ApplyDamage(player.MaximumHP * 5);
            AssertThat(player.CurrentHP).IsEqual(1);
            AssertThat(script.Step)
                .OverrideFailureMessage("Nothing but the Defy blast completes the step.")
                .IsEqual(TutorialCalibrationStep.MeterAndDefy);

            // Press Defy: the blast completes the lesson and gives the floor back.
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Defy));
            InputManager.Instance.SetInputSource(player.PlayerIndex, source);
            for (int frame = 0; frame < 10 && script.Step == TutorialCalibrationStep.MeterAndDefy; frame++) {
                player._PhysicsProcess(1.0 / 60.0);
                source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            }
            AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseRewind);
            AssertThat(player.ScriptedHPFloor).IsEqual(0);
        } finally {
            if (level != null) InputManager.Instance?.ClearInputSource(0);
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// D13 (PROVISIONAL-FONT-PLACEMENT): the Tutorial's Restoration Font is built
    /// only when the Combat Trial starts, past the trial anchor — never during the
    /// Part 1 hold, so no Warden wellspring stands in the hero's own era.
    /// </summary>
    [TestCase]
    public void TheTutorialFontAppearsOnlyWithTheCombatTrialPastItsAnchor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Hold);
            AssertObject(level.PlacedRestorationFont).IsNull();
            // A recursive type check: FindChildren's type filter matches only native
            // and global classes, and RestorationFont is neither.
            AssertThat(CountFontsUnder(level))
                .OverrideFailureMessage("No Warden wellspring may stand in the hero's own era during the hold.")
                .IsEqual(0);

            level.BeginCombatTrialForTesting();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.CombatTrial);
            RestorationFont font = level.PlacedRestorationFont;
            AssertObject(font).IsNotNull();
            AssertObject(font.GetParent()).IsEqual(level);
            AssertString(font.FontID).IsEqual(Level00Controller.RestorationFontID);
            AssertString(font.FontID).IsEqual("level_00.restoration_font");
            AssertThat(font.Position).IsEqual(Level00Controller.RestorationFontPosition);
            AssertObject(font.GetNodeOrNull<InteractionArea>("InteractionArea")).IsNotNull();
            AssertObject(font.GetNodeOrNull<Node2D>("Glow")).IsNotNull();
            AssertObject(font.ChannelRadial).IsNotNull();
            AssertThat(font.RefusesWhile != null).IsTrue();
            AssertThat(font.RefusesWhile()).IsFalse();

            Vector2 position = font.Position;
            AssertThat(position.X >= Level00Controller.TrialCheckpointPosition.X + 110f)
                .OverrideFailureMessage($"The font at {position} is not 110 px past the trial anchor.")
                .IsTrue();
            AssertThat(position.X <= Level00Controller.CalibrationBayEastWallX - 120f)
                .OverrideFailureMessage($"The font at {position} crowds the bay's east wall.")
                .IsTrue();
            AssertFloat(position.Y).IsEqual(Level00Controller.TutorialFloorTopY);
            AssertThat(position.DistanceTo(Level00Controller.TrialCheckpointPosition) >= 110f).IsTrue();
            AssertThat(position.DistanceTo(Level00Controller.MobilityCheckpointPosition) >= 110f).IsTrue();
            AssertThat(CountFontsUnder(level))
                .OverrideFailureMessage("The Combat Trial builds exactly one font.")
                .IsEqual(1);
            // GU-L0 (2026-10-13): the font and the trial anchor stand west of the
            // trial's lock (footprints ±30 px), where the hero is when the trial
            // begins, so neither is shut away behind its entry barrier.
            EncounterLock trial = level.TrialLock;
            AssertObject(trial).IsNotNull();
            foreach (Vector2 anchor in new[] { position, Level00Controller.TrialCheckpointPosition }) {
                AssertThat(anchor.X + 30f <= trial.EntryBarrier.Footprint.Position.X)
                    .OverrideFailureMessage($"{anchor} stands inside or against the trial lock {trial.Area}.")
                    .IsTrue();
            }
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// GU-L0 (2026-10-13): the Combat Trial is one encounter lock over the bay's
    /// east end, built when the trial begins: its entry barrier hugs the area from
    /// the west (the bay's east wall is the far side), its two waves — five drones,
    /// then three drones and a slasher — spawn inside it as the hero walks in and
    /// once the first is down, and its clear, not a global kill count, ends the
    /// tutorial. In real physics frames on the real scene.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheCombatTrialIsOneLockOverTheBayWhoseClearEndsTheTutorial() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        StoryManager story = StoryManager.Instance;
        SessionData originalSession = GameManager.Instance.CurrentSession;
        CampaignLevel originalLevel = story.CurrentLevel;
        bool originalTutorial = story.TutorialComplete;
        int originalWallet = story.ChronalDustCollected;
        story.PrepareDirectLevel(CampaignLevel.Tutorial, "einstein", Difficulty.Normal);
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            // Straight to the trial, before the deferred hold start runs (it then
            // finds the phase moved on and does nothing).
            level.BeginCombatTrialForTesting();
            var player = level.GetNode<PlayerController>("Player");
            EncounterLock trial = level.TrialLock;
            AssertObject(trial).IsNotNull();
            AssertString(trial.LockID).IsEqual(Level00Controller.TrialLockID);
            AssertThat(trial.IsInGroup(EncounterLock.Group)).IsTrue();

            // The layout: 2580 to the east wall, one entry barrier hugging it from
            // the west with floor under it, no exit barrier, between one view and
            // one room wide; both waves spawn inside it.
            AssertThat(trial.Area.Position.X).IsEqual(Level00Controller.TrialLockMinX);
            AssertThat(trial.Area.End.X).IsEqual(Level00Controller.CalibrationBayEastWallX);
            AssertThat(trial.Area.Size.X >= EncounterLockRules.MinAreaWidthPixels
                && trial.Area.Size.X <= EncounterLockRules.MaxAreaWidthPixels).IsTrue();
            AssertObject(trial.ExitBarrier).IsNull();
            AssertObject(trial.EntryBarrier).IsNotNull();
            AssertThat(trial.EntryBarrier.Footprint.End.X).IsEqual(trial.Area.Position.X);
            AssertThat(trial.EntryBarrier.BottomY).IsEqual(Level00Controller.TutorialFloorTopY);
            AssertThat(trial.State).IsEqual(EncounterLockState.Armed);
            AssertThat(trial.EntryBarrier.IsClosed).IsFalse();
            AssertThat(trial.Spec.Waves.Length).IsEqual(2);
            AssertThat(trial.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnEngage);
            AssertThat(trial.Spec.Waves[1].Trigger).IsEqual(EncounterWaveTrigger.AfterPreviousCleared);
            foreach (EncounterLockWave wave in trial.Spec.Waves) {
                foreach (EncounterLockSpawn spawn in wave.Spawns) {
                    AssertThat(spawn.Position.X > trial.Area.Position.X + 20f && spawn.Position.X < trial.Area.End.X - 20f)
                        .OverrideFailureMessage($"{spawn.EnemyID} at {spawn.Position} spawns outside the trial lock.")
                        .IsTrue();
                }
            }
            AssertThat(trial.RegisteredCount)
                .OverrideFailureMessage("Nothing spawns before the hero walks into the bay.").IsEqual(0);
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            int wave1 = StoryDifficultyTuning.ScaleEncounterCount(5, difficulty);
            int wave2 = StoryDifficultyTuning.ScaleEncounterCount(4, difficulty);
            AssertThat(trial.PlannedTotal).IsEqual(wave1 + wave2);

            // The hero walks in: the entry shuts and wave 1 materializes, awake.
            player.GlobalPosition = new Vector2(3000f, 850f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(trial.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(trial.EntryBarrier.IsClosed).IsTrue();
            AssertThat(trial.LivingMemberCount).IsEqual(wave1);
            foreach (FTT.Enemies.EnemyController member in trial.LivingMembers) {
                AssertString(member.Data.EnemyID).IsEqual("hologram_drone");
                AssertThat(member.IsEncounterDormant).IsFalse();
            }

            foreach (FTT.Enemies.EnemyController member in trial.LivingMembers) member.TakeDamage(99999);
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.CombatTrial);
            AssertThat(trial.AllWavesSpawned).IsTrue();
            AssertThat(trial.LivingMemberCount).IsEqual(wave2);

            foreach (FTT.Enemies.EnemyController member in trial.LivingMembers) member.TakeDamage(99999);
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(trial.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(trial.EntryBarrier.IsClosed).IsFalse();
            AssertThat(level.Phase)
                .OverrideFailureMessage("The trial lock's clear must end the tutorial.")
                .IsEqual(Level00Controller.TutorialPhase.Complete);
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            FreeLevel(level);
            story.StopLevelRun();
            story.PrepareDirectLevel(originalLevel,
                string.IsNullOrEmpty(originalSession.SelectedCharacterID) ? "einstein" : originalSession.SelectedCharacterID,
                originalSession.Difficulty);
            story.TutorialComplete = originalTutorial;
            story.SetDust(originalWallet);
            story.ClearLevelAttemptState();
            GameManager.Instance.CurrentSession = originalSession;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// GU-L0 (2026-10-13): the grab lesson's drone leaves with its lesson. It used
    /// to stay behind as a live hostile once its throw resolved, and the trial's
    /// kill counter counted it.
    /// </summary>
    [TestCase]
    public void TheGrabLessonsDroneIsReleasedAfterItsLesson() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            level.BeginGrabLessonForTesting();
            FTT.Enemies.EnemyController drone = level.GrabLessonDrone;
            AssertObject(drone).IsNotNull();
            AssertObject(drone.GetParent()).IsEqual(level);

            level.CompleteGrabLessonForTesting();
            // A drone not in a thrown flight leaves on the next physics tick.
            level._PhysicsProcess(1.0 / 60.0);
            AssertObject(level.GrabLessonDrone).IsNull();
            AssertThat(GodotObject.IsInstanceValid(drone) && drone.GetParent() == level)
                .OverrideFailureMessage("The grab lesson's drone stayed in the bay after its lesson.")
                .IsFalse();
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    private static int CountFontsUnder(Node node) {
        int count = node is RestorationFont ? 1 : 0;
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) count += CountFontsUnder(child);
        return count;
    }

    private static void FreeLevel(Node level) {
        if (level == null || !GodotObject.IsInstanceValid(level)) return;
        level.GetParent()?.RemoveChild(level);
        level.Free();
    }
}
