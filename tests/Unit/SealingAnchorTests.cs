using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// A minimal boss level for the N01 sealing stage: three anchors, a real boss
/// encounter (Orleans' Siegemaster), one Extractor, and a recorded dialogue
/// source with no beat delay so a case can step the chain synchronously.
/// </summary>
internal partial class SealingTestLevelController : StoryLevelControllerBase {
    public const string TestLevelID = "level_02_orleans";
    public static readonly Vector2 AnchorPosition = new(3200f, 850f);

    public override string LevelID => TestLevelID;
    public override CampaignLevel Level => CampaignLevel.Orleans;
    public override string LevelTitleKey => "orleans_level_title";
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => new(200f, 850f);
    public override Rect2 LevelBounds => new(0, 0, 6000, 1080);
    protected override float ExitDialogueDelaySeconds => 0f;
    protected override Vector2? SealingAnchorPosition => AnchorPosition;

    public readonly List<string> StartedDialogues = new();
    public readonly List<bool> CompleteWhenDialogueStarted = new();
    public BossEncounterController Encounter;
    public ChronalExtractor Extractor;

    protected override void BuildLevel() {
        BuildFloor(0, 900, 6000);
        BuildCheckpoint(200, 850, $"{TestLevelID}_checkpoint_0", CheckpointRole.Entry);
        BuildCheckpoint(1500, 850, $"{TestLevelID}_checkpoint_1", CheckpointRole.Middle);
        BuildCheckpoint(2800, 850, $"{TestLevelID}_checkpoint_2", CheckpointRole.PreBoss);
        Extractor = BuildExtractor("level_02.extractor_courtyard", new Vector2(1000f, 830f));
        Encounter = BuildBossEncounter(Level02Controller.BossResourcePath, new Vector2(3600f, 850f),
            "SealingTestBoss", revealDistance: 0f);
    }

    protected override bool StartDialogue(string dialogueID) {
        StartedDialogues.Add(dialogueID);
        CompleteWhenDialogueStarted.Add(LevelComplete);
        return true;
    }

    public void DefeatBossForTest() =>
        OnBossDefeated(null, new BossDefeatedPayload { BossID = "siegemaster_duke", ChronalDustDrop = 25 });
}

/// <summary>
/// Package 12 W8 — GAP-05 / N01 with D12(a): boss defeat commits a persisted
/// AwaitingSeal (never completion, deposit or unlock); the defeat beats play;
/// control returns beside a marked anchor; a single Interact seals — the
/// completion transaction commits BEFORE the restoration vignette — and a load
/// before the seal rebuilds the defeated boss (never respawned), the ready
/// anchor and at most one pending boss pickup.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SealingAnchorTests {

    private const int ScratchSlot = 2;
    private const string BossSourceID = "level_02.boss";

    [TestCase]
    public void DefeatCommitsAwaitingSealAndArmsTheAnchorWithoutCompletingTheLevel() {
        using var fixture = new SealFixture(preSeal: false);
        SealingTestLevelController level = fixture.Level;
        AssertObject(level.SealingAnchor).IsNotNull();
        AssertThat(level.SealingAnchor.IsArmed).OverrideFailureMessage("Dormant until the boss falls.").IsFalse();
        AssertString(level.SealingAnchor.AnchorID).IsEqual("level_02.sealing_anchor");
        AssertString(level.SealingAnchor.PromptKey).IsEqual(StoryLevelControllerBase.SealPromptKey);

        level.DefeatBossForTest();
        StorySealReadiness readiness = StoryManager.Instance.CurrentAttempt.SealReadiness;
        AssertThat(readiness.BossDefeated).IsTrue();
        AssertString(readiness.SealingAnchorID).IsEqual("level_02.sealing_anchor");
        AssertThat(readiness.PendingBossPickup).IsTrue();
        AssertThat(level.LevelComplete).OverrideFailureMessage("Defeat never completes the level.").IsFalse();
        AssertThat(level.SealingAnchor.IsArmed).IsTrue();
        AssertThat(level.IsAwaitingSeal).IsTrue();
        AssertThat(level.StartedDialogues)
            .OverrideFailureMessage("The exit beat waits for the seal.")
            .NotContains("level_02.exit");

        // Collecting the boss pickup clears the pending flag, not the readiness.
        EventBus.Instance.RaiseDustAwardCollected(new DustAwardCollectedPayload {
            Amount = 25, Source = DustAwardSource.Boss
        });
        AssertThat(readiness.PendingBossPickup).IsFalse();
        AssertThat(readiness.BossDefeated).IsTrue();
    }

    [TestCase]
    public void TheSealCommitsCompletionBeforeTheVignetteExactlyOnce() {
        using var fixture = new SealFixture(preSeal: false);
        SealingTestLevelController level = fixture.Level;
        int completions = 0;
        void OnComplete(string id) => completions++;
        EventBus.Instance.OnLevelComplete += OnComplete;
        try {
            level.DefeatBossForTest();
            AssertThat(level.SealingAnchor.CanInteract(level.Player)).IsTrue();
            level.SealingAnchor.Interact(level.Player);

            AssertThat(level.SealAccepted).IsTrue();
            AssertThat(level.LevelComplete).IsTrue();
            AssertThat(completions).IsEqual(1);
            int exitIndex = level.StartedDialogues.IndexOf("level_02.exit");
            AssertThat(exitIndex >= 0).OverrideFailureMessage("The exit beat is the restoration vignette.").IsTrue();
            AssertThat(level.CompleteWhenDialogueStarted[exitIndex])
                .OverrideFailureMessage("N01: the transaction commits BEFORE the vignette starts.")
                .IsTrue();
            AssertThat(StoryManager.Instance.CurrentAttempt.SealReadiness.BossDefeated)
                .OverrideFailureMessage("AwaitingSeal ends inside the completion commit.")
                .IsFalse();

            // Duplicate presses, callbacks and the vignette's end cannot settle it again.
            AssertThat(level.AcceptSeal()).IsFalse();
            level.SealingAnchor.InsertCore();
            EventBus.Instance.RaiseDialogueComplete("level_02.exit");
            AssertThat(completions).IsEqual(1);
        } finally {
            EventBus.Instance.OnLevelComplete -= OnComplete;
        }
    }

    [TestCase]
    public void TheAnchorRefusesDuringTimeFreezeTheHoldAndDeath() {
        using var fixture = new SealFixture(preSeal: false);
        SealingTestLevelController level = fixture.Level;
        PlayerController player = level.Player;
        level.DefeatBossForTest();

        player.TimeFrozen = true;
        try {
            AssertThat(level.SealingAnchor.CanInteract(player)).IsFalse();
        } finally {
            player.TimeFrozen = false;
        }
        player.BeginRecoveryHold(StoryRecoveryHoldCause.DeathRewind, 60);
        try {
            AssertThat(level.SealingAnchor.CanInteract(player)).IsFalse();
            level.SealingAnchor.Interact(player);
            AssertThat(level.LevelComplete).OverrideFailureMessage("Nothing queues during the hold.").IsFalse();
        } finally {
            player.CancelRecoveryHold();
        }
        AssertThat(level.SealingAnchor.CanInteract(player)).IsTrue();
    }

    [TestCase]
    public void SealingShutsRemainingExtractorsDownWithoutADestruction() {
        using var fixture = new SealFixture(preSeal: false);
        SealingTestLevelController level = fixture.Level;
        int livingBefore = StoryManager.Instance.LivingExtractorCount;
        level.DefeatBossForTest();
        level.AcceptSeal();

        AssertThat(level.Extractor.IsSealedShutdown).IsTrue();
        AssertThat(level.Extractor.IsDestroyed).IsFalse();
        AssertThat(StoryManager.Instance.IsExtractorDestroyed(level.Extractor.ObjectID)).IsFalse();
        AssertThat(StoryManager.Instance.LivingExtractorCount).IsEqual(livingBefore);
        AssertThat(level.Extractor.TakeEnvironmentDamage(9999f))
            .OverrideFailureMessage("A sealed machine is inert, not breakable for a late pickup.")
            .IsEqual(0);
        for (int frame = 0; frame < 900; frame++) level.Extractor._PhysicsProcess(1.0 / 60.0);
        AssertThat(level.Extractor.DischargeCount).OverrideFailureMessage("A sealed machine never discharges.").IsEqual(0);
    }

    [TestCase]
    public void APreSealLoadRebuildsTheDefeatedBossTheReadyAnchorAndOnePendingPickup() {
        using var fixture = new SealFixture(preSeal: true);
        SealingTestLevelController level = fixture.Level;
        AssertThat(level.ResumedAwaitingSeal).IsTrue();
        AssertThat(level.IsBossDefeated).IsTrue();
        AssertObject(level.Encounter.Boss)
            .OverrideFailureMessage("A pre-seal load never respawns the boss.")
            .IsNull();
        AssertThat(level.SealingAnchor.IsArmed).IsTrue();
        AssertThat(level.IsAwaitingSeal).IsTrue();

        ChronalDustPickup pickup = level.RestoredBossPickup;
        AssertObject(pickup).OverrideFailureMessage("The uncollected boss reward respawns once.").IsNotNull();
        AssertString(pickup.SourceID).IsEqual(BossSourceID);
        AssertThat(pickup.DustAmount).IsEqual(25);
        AssertThat(pickup.Source).IsEqual(DustAwardSource.Boss);

        // Sealing from the restored state still commits exactly once (slotless,
        // so the completion transaction writes nothing to the scratch slot).
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        level.SealingAnchor.Interact(level.Player);
        AssertThat(level.LevelComplete).IsTrue();
    }

    [TestCase]
    public void APreSealLoadAfterTheBossRewardWasCollectedSpawnsNothing() {
        using var fixture = new SealFixture(preSeal: true, bossRewardClaimed: true);
        SealingTestLevelController level = fixture.Level;
        AssertThat(level.ResumedAwaitingSeal).IsTrue();
        AssertObject(level.RestoredBossPickup).IsNull();
        AssertThat(StoryManager.Instance.CurrentAttempt.SealReadiness.PendingBossPickup).IsFalse();
        AssertThat(level.SealingAnchor.IsArmed).IsTrue();
    }

    // ---- Harness -------------------------------------------------------------

    private sealed class SealFixture : System.IDisposable {
        public readonly SealingTestLevelController Level;
        private readonly CampaignLevel _originalLevel;
        private readonly Difficulty _originalDifficulty;
        private readonly string _originalCharacter;
        private readonly int _originalSlot;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public SealFixture(bool preSeal, bool bossRewardClaimed = false) {
            StoryManager story = StoryManager.Instance;
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalLevel = story.CurrentLevel;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            LevelRewardDirectory.ResetAttempt();
            story.BeginLevelRun();
            if (preSeal) {
                StorySealReadiness readiness = story.CurrentAttempt.SealReadiness;
                readiness.BossDefeated = true;
                readiness.SealingAnchorID = "level_02.sealing_anchor";
                readiness.PendingBossPickup = true;
                if (bossRewardClaimed) LevelRewardDirectory.CommitClaim(BossSourceID);
                var save = new StorySaveData {
                    SelectedCharacterID = "einstein",
                    CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Orleans),
                    LastCheckpointID = $"{SealingTestLevelController.TestLevelID}_checkpoint_2",
                    CurrentHP = 100
                };
                SaveManager.Instance.SaveSlots[ScratchSlot] = save;
                GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            }
            Level = new SealingTestLevelController { Name = "SealingTestLevel" };
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
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
    }
}
