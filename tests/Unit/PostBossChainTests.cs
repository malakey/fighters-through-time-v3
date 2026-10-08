using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// A minimal boss level whose dialogue source behaves like the real
/// <see cref="DialogueManager"/>: one sequence at a time, and a request made while
/// another is on screen is dropped — yet still reported as started, exactly as the
/// manager's ID overload does, so the chain cannot lean on the return value. No
/// beat delay, so the chain steps synchronously.
/// </summary>
internal partial class SealChainOrderTestLevelController : StoryLevelControllerBase {
    public const string TestLevelID = "level_02_orleans";
    public const string PreBossID = "level_02.preboss_chain_test";
    public const string PostBossID = "level_02.postboss_chain_test";
    public const string ExitID = "level_02.exit";
    public const string PreBossCheckpointID = TestLevelID + "_checkpoint_2";

    public override string LevelID => TestLevelID;
    public override CampaignLevel Level => CampaignLevel.Orleans;
    public override string LevelTitleKey => "orleans_level_title";
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => new(200f, 850f);
    public override Rect2 LevelBounds => new(0, 0, 6000, 1080);
    protected override float ExitDialogueDelaySeconds => 0f;
    protected override Vector2? SealingAnchorPosition => new(3200f, 850f);

    private static readonly string[] PostBossBeats = { PostBossID };
    protected override IReadOnlyList<string> PostBossDialogueIDs => PostBossBeats;

    public readonly List<string> StartedDialogues = new();
    public string ActiveSequence { get; private set; }
    public CheckpointTrigger PreBossCheckpoint { get; private set; }

    protected override void BuildLevel() {
        BuildFloor(0, 900, 6000);
        BuildCheckpoint(200, 850, $"{TestLevelID}_checkpoint_0", CheckpointRole.Entry);
        BuildCheckpoint(1500, 850, $"{TestLevelID}_checkpoint_1", CheckpointRole.Middle);
        PreBossCheckpoint = BuildCheckpoint(2800, 850, PreBossCheckpointID, CheckpointRole.PreBoss);
        BuildBossEncounter(Level02Controller.BossResourcePath, new Vector2(3600f, 850f),
            "SealChainOrderTestBoss", revealDistance: 0f);
    }

    protected override bool StartDialogue(string dialogueID) {
        // The real manager drops a sequence requested over another and reports true.
        if (ActiveSequence != null) return true;
        ActiveSequence = dialogueID;
        StartedDialogues.Add(dialogueID);
        return true;
    }

    protected override bool IsDialogueSequenceActive => ActiveSequence != null;

    /// <summary>Ends the sequence on screen the way the manager does: inactive, then the completion event.</summary>
    public void CompleteActiveSequence() {
        string completed = ActiveSequence;
        ActiveSequence = null;
        EventBus.Instance.RaiseDialogueComplete(completed);
    }

    public bool StartForeignSequence(string dialogueID) => StartDialogue(dialogueID);

    public bool StartPreBossBeatForTest() => StartPreBossDialogue(PreBossID);

    public void DefeatBossForTest() =>
        OnBossDefeated(null, new BossDefeatedPayload { BossID = "siegemaster_duke", ChronalDustDrop = 25 });
}

/// <summary>
/// G4 (playtest bots, 2026-10-04): Level 5 could not be finished when the hero
/// struck the PreBoss fracture only after the Tidal Overseer fell. Crossing the
/// pre-boss trigger then started <c>level_05.preboss</c>, and the post-boss beat
/// came due 1.5 s later over it. <see cref="DialogueManager.StartSequence(string)"/>
/// refused the second sequence but still returned true, so the chain recorded the
/// post-boss beat as playing, waited for a completion that never came, and never
/// armed the sealing anchor. In the luckier ordering the pre-boss lead-in played
/// after the post-boss exchange.
///
/// <para>The fix, pinned here and kept inside the level base so no other dialogue
/// flow moves: a post-boss or exit beat due while another sequence is on screen
/// waits for it instead of being requested over it, and a pre-boss beat never
/// plays once the boss is down. Striking the PreBoss fracture after the defeat
/// leaves the AwaitingSeal chain alone.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PostBossChainTests {

    private const int ScratchSlot = 2;

    [TestCase]
    public void APostBossBeatDueOverAnotherSequenceWaitsForItAndTheAnchorStillArms() {
        using var fixture = new ChainFixture();
        SealChainOrderTestLevelController level = fixture.Level;

        // A late pre-boss (or absence, or bark) sequence is on screen as the boss falls.
        AssertThat(level.StartForeignSequence("level_02.absence_chain_test")).IsTrue();
        level.DefeatBossForTest();
        AssertThat(level.BeatsAwaitingDialogue).IsEqual(1);
        AssertThat(level.StartedDialogues).ContainsExactly("level_02.absence_chain_test");
        AssertThat(level.SealingAnchor.IsArmed)
            .OverrideFailureMessage("The anchor arms after the post-boss beat, not over a refused one.")
            .IsFalse();

        // The sequence on screen ends: the post-boss beat plays, it does not vanish.
        level.CompleteActiveSequence();
        AssertThat(level.StartedDialogues)
            .ContainsExactly("level_02.absence_chain_test", SealChainOrderTestLevelController.PostBossID);
        AssertString(level.ActivePostBossDialogueID).IsEqual(SealChainOrderTestLevelController.PostBossID);
        AssertThat(level.SealingAnchor.IsArmed).IsFalse();

        level.CompleteActiveSequence();
        AssertThat(level.BeatsAwaitingDialogue).IsEqual(0);
        AssertThat(level.SealingAnchor.IsArmed).IsTrue();
        AssertThat(level.IsAwaitingSeal).IsTrue();
        AssertThat(StoryManager.Instance.CurrentAttempt.SealReadiness.BossDefeated).IsTrue();
    }

    [TestCase]
    public void APreBossStrikeAfterTheBossFallsPlaysNoPreBossBeatAndLeavesTheSealChainAlone() {
        using var fixture = new ChainFixture();
        SealChainOrderTestLevelController level = fixture.Level;

        level.DefeatBossForTest();
        AssertThat(level.StartedDialogues).ContainsExactly(SealChainOrderTestLevelController.PostBossID);

        // The hero walked past the fracture into the fight and comes back to strike it.
        AssertThat(level.StartPreBossBeatForTest())
            .OverrideFailureMessage("The pre-boss lead-in must never play once the boss is down.")
            .IsFalse();
        level.PreBossCheckpoint.Activate();
        AssertThat(level.PreBossCheckpoint.IsActivated).IsTrue();
        AssertThat(level.StartedDialogues).ContainsExactly(SealChainOrderTestLevelController.PostBossID);
        AssertString(level.ActivePostBossDialogueID).IsEqual(SealChainOrderTestLevelController.PostBossID);
        AssertThat(StoryManager.Instance.CurrentAttempt.SealReadiness.BossDefeated).IsTrue();

        level.CompleteActiveSequence();
        AssertThat(level.IsAwaitingSeal).IsTrue();

        // A second strike once the anchor waits changes nothing either.
        level.PreBossCheckpoint.Activate();
        AssertThat(level.StartPreBossBeatForTest()).IsFalse();
        AssertThat(level.IsAwaitingSeal).IsTrue();
        AssertThat(level.StartedDialogues).ContainsExactly(SealChainOrderTestLevelController.PostBossID);

        // ...and the seal still completes the level exactly as before.
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        AssertThat(level.AcceptSeal()).IsTrue();
        AssertThat(level.LevelComplete).IsTrue();
        AssertThat(level.StartedDialogues)
            .ContainsExactly(SealChainOrderTestLevelController.PostBossID, SealChainOrderTestLevelController.ExitID);
    }

    [TestCase]
    public void ThePreBossBeatStillPlaysBeforeTheFight() {
        using var fixture = new ChainFixture();
        SealChainOrderTestLevelController level = fixture.Level;
        AssertThat(level.StartPreBossBeatForTest()).IsTrue();
        AssertThat(level.StartedDialogues).ContainsExactly(SealChainOrderTestLevelController.PreBossID);
    }

    [TestCase]
    public void TheExitBeatWaitsForASequenceOnScreenInsteadOfBeingDropped() {
        using var fixture = new ChainFixture();
        SealChainOrderTestLevelController level = fixture.Level;
        level.DefeatBossForTest();
        level.CompleteActiveSequence();
        AssertThat(level.IsAwaitingSeal).IsTrue();

        AssertThat(level.StartForeignSequence("level_02.bark_chain_test")).IsTrue();
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        AssertThat(level.AcceptSeal()).IsTrue();
        AssertThat(level.BeatsAwaitingDialogue).IsEqual(1);
        AssertThat(level.StartedDialogues).NotContains(SealChainOrderTestLevelController.ExitID);

        level.CompleteActiveSequence();
        AssertThat(level.BeatsAwaitingDialogue).IsEqual(0);
        AssertString(level.ActiveSequence).IsEqual(SealChainOrderTestLevelController.ExitID);
    }

    /// <summary>
    /// The release of a waiting beat runs from the completion event, so it relies on
    /// the manager being free by the time it raises that event. Pinned on the real
    /// manager with real Titanic copy.
    /// </summary>
    [TestCase]
    public void TheDialogueManagerIsFreeWhenItRaisesACompletion() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        HashSet<string> originalSeen = new(
            SaveManager.Instance.GlobalData.SeenDialogueIDs ?? new HashSet<string>(), StringComparer.Ordinal);
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        // Slotless: the completion's effect ledger must not write to a real slot.
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        DialogueManager dialogue = DialogueManager.CreateDefault();
        tree.Root.AddChild(dialogue);
        bool? activeAtCompletion = null;
        void OnComplete(string id) {
            if (DialogueSequenceData.BaseIDOf(id) == "level_05.preboss") activeAtCompletion = dialogue.IsSequenceActive;
        }
        EventBus.Instance.OnDialogueComplete += OnComplete;
        try {
            AssertThat(dialogue.RegisterSetFromPath("res://resources/Dialogue/level_05_dialogue.tres")).IsTrue();
            AssertThat(dialogue.StartSequence("level_05.preboss")).IsTrue();
            AssertThat(dialogue.IsSequenceActive).IsTrue();
            for (int step = 0; step < 80 && dialogue.IsSequenceActive; step++) dialogue.AdvanceLine();
            AssertThat(activeAtCompletion.HasValue)
                .OverrideFailureMessage("The pre-boss sequence never raised its completion.").IsTrue();
            AssertThat(activeAtCompletion.Value)
                .OverrideFailureMessage("The manager still reported a sequence on screen as it raised the completion.")
                .IsFalse();
        } finally {
            EventBus.Instance.OnDialogueComplete -= OnComplete;
            dialogue.GetParent()?.RemoveChild(dialogue);
            dialogue.Free();
            SaveManager.Instance.GlobalData.SeenDialogueIDs = originalSeen;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// The reported run on the real Titanic scene: the Overseer falls before the hero
    /// has crossed the pre-boss trigger or struck the PreBoss fracture; both happen
    /// afterwards. No pre-boss beat plays, the post-boss exchange plays when due,
    /// and the sealing anchor arms.
    /// </summary>
    [TestCase]
    public async Task OnTheTitanicAPreBossApproachAfterTheOverseerFallsStillArmsTheAnchor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new TitanicFixture();
        Level05Controller level = fixture.Level;
        DialogueManager dialogue = level.Services?.Dialogue;
        AssertObject(dialogue).IsNotNull();
        AssertThat(level.BossEncounters.Count).IsEqual(1);
        BossController overseer = level.BossEncounters[0].Boss;
        AssertObject(overseer).IsNotNull();

        overseer.TakeDamage(99999);
        AssertThat(level.IsBossDefeated).IsTrue();
        // A hit on the dormant Overseer reveals it on the way down, so its intro
        // sequence may be on screen now; read it through (the post-boss beat that
        // comes due meanwhile waits for it rather than being dropped).
        for (int step = 0; step < 80 && dialogue.IsSequenceActive; step++) dialogue.AdvanceLine();
        AssertThat(dialogue.IsSequenceActive).IsFalse();

        // Only now does the hero cross the pre-boss trigger and strike the fracture.
        var trigger = level.GetNodeOrNull<Area2D>("PreBossTrigger");
        AssertObject(trigger).IsNotNull();
        trigger.EmitSignal(Area2D.SignalName.BodyEntered, level.Player);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(level.PreBossTriggerCrossed).IsTrue();
        AssertThat(dialogue.IsSequenceActive)
            .OverrideFailureMessage($"'{dialogue.ActiveDialogueID}' started after the Overseer fell.")
            .IsFalse();
        var preBoss = level.GetNodeOrNull<CheckpointTrigger>($"Checkpoint_{Level05Controller.Checkpoint2}");
        AssertObject(preBoss).IsNotNull();
        preBoss.Activate();

        // The post-boss exchange comes due after the beat delay (~90 frames).
        for (int frame = 0; frame < 300 && !dialogue.IsSequenceActive; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        AssertString(DialogueSequenceData.BaseIDOf(dialogue.ActiveDialogueID)).IsEqual(level.PostBossDialogueID);
        // Read it through synchronously: the sequence pauses the tree, so no frame is awaited meanwhile.
        for (int step = 0; step < 80 && dialogue.IsSequenceActive; step++) dialogue.AdvanceLine();
        AssertThat(dialogue.IsSequenceActive).IsFalse();
        AssertThat(level.SealingAnchor?.IsArmed ?? false)
            .OverrideFailureMessage("The sealing anchor never armed: the level cannot be finished.")
            .IsTrue();
        AssertThat(level.IsAwaitingSeal).IsTrue();
    }

    // ---- Harness -------------------------------------------------------------

    private sealed class ChainFixture : IDisposable {
        public readonly SealChainOrderTestLevelController Level;
        private readonly CampaignLevel _originalLevel;
        private readonly Difficulty _originalDifficulty;
        private readonly string _originalCharacter;
        private readonly int _originalSlot;
        private readonly bool _originalPaused;

        public ChainFixture() {
            StoryManager story = StoryManager.Instance;
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalLevel = story.CurrentLevel;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;

            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            LevelRewardDirectory.ResetAttempt();
            story.BeginLevelRun();
            Level = new SealChainOrderTestLevelController { Name = "SealChainOrderTestLevel" };
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Paused = _originalPaused;
            StoryManager story = StoryManager.Instance;
            story.StopLevelRun();
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? "einstein" : _originalCharacter, _originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
    }

    /// <summary>
    /// The real Titanic scene resumed at the Middle anchor (a fresh entry defers the
    /// tree-pausing entrance dialogue), handing every shared singleton and the pause
    /// flag back on dispose.
    /// </summary>
    private sealed class TitanicFixture : IDisposable {
        public readonly Level05Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;
        private readonly HashSet<string> _originalSeen;

        public TitanicFixture() {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            // The sequences this case reads through enter the global seen-set.
            _originalSeen = new HashSet<string>(
                SaveManager.Instance.GlobalData.SeenDialogueIDs ?? new HashSet<string>(), StringComparer.Ordinal);
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Titanic),
                LastCheckpointID = Level05Controller.Checkpoint1,
                CurrentHP = 100,
                CurrentUltimateMeter = 25f
            };

            Level = ResourceLoader.Load<PackedScene>(StoryManager.GetLevelScenePath(CampaignLevel.Titanic))
                .Instantiate<Level05Controller>();
            Level.Name = "Level05PostBossChainFixture";
            tree.Root.AddChild(Level);
            // The resume has been read. The case runs real frames and strikes a
            // checkpoint, so go slotless: no write may reach a real slot file.
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            SaveManager.Instance.GlobalData.SeenDialogueIDs = _originalSeen;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
