using System;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The level rank through <see cref="StoryManager"/> (gameplay review §1.9): a dust bonus
/// on top of F05, applied after the tier bonus, banked by the completion transaction, and a
/// tally that rides the attempt through every resume and resets only on a Restart.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryLevelRankTests {
    private const int ScratchSlot = 2;
    private const string Hero = "einstein";
    private const float AuthoredPar = 300f;

    [TestCase]
    public void TheBonusFollowsTheTierBonusWithoutCompoundingAndBanksWithTheWallet() {
        using var scope = new RankScope();
        StorySaveData save = scope.Begin(CampaignLevel.Orleans, Difficulty.Normal);
        StoryManager story = StoryManager.Instance;
        string levelID = StoryManager.GetLevelID(CampaignLevel.Orleans);
        story.BeginIntegrityClock(AuthoredPar, startingExtractors: 0);
        story.CollectDust(100);

        EventBus.Instance.RaiseLevelComplete(levelID);

        AssertThat(story.LastLevelTierBonusDust)
            .OverrideFailureMessage("The tier bonus must read the wallet before the rank joins it (F05, no compounding).")
            .IsEqual(10);
        AssertThat(story.LastLevelRankBonusDust).IsEqual(50);
        AssertThat(story.LastLevelRank.HasValue).IsTrue();
        AssertThat(story.LastLevelRank.Value.Letter).IsEqual(LevelRankLetter.S);
        AssertThat(save.DepositedChronalDust[Hero]).IsEqual(160);
        AssertThat(story.LastCompletionDepositDust).IsEqual(160);
        AssertThat(story.ChronalDustCollected).IsEqual(0);
        AssertThat(save.AttemptState.CompletionTransaction.RankBonusDust).IsEqual(50);
        AssertThat(save.AttemptState.CompletionTransaction.TierBonusDust).IsEqual(10);
        AssertThat(save.RankByLevel[levelID]).IsEqual("S");
        AssertThat(save.RankDustPaidByLevel[levelID]).IsEqual(50);
    }

    [TestCase]
    public void EasyPaysHalfAndHardPaysInFullForTheSameCleanRun() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;
        scope.Begin(CampaignLevel.Orleans, Difficulty.Easy);
        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Orleans));
        AssertThat(story.LastLevelRank.Value.Letter).IsEqual(LevelRankLetter.S);
        AssertThat(story.LastLevelRankBonusDust).IsEqual(25);

        scope.Begin(CampaignLevel.Orleans, Difficulty.Hard);
        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Orleans));
        AssertThat(story.LastLevelRank.Value.Letter).IsEqual(LevelRankLetter.S);
        AssertThat(story.LastLevelRankBonusDust).IsEqual(50);
    }

    [TestCase]
    public void TheTallyIsAuthoredDamageAndGrowsOnlyDuringTheLiveRun() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;
        // The same authored 30 HP hit on each difficulty's scale.
        scope.Begin(CampaignLevel.Orleans, Difficulty.Easy);
        story.AddRankDamage(15);
        AssertThat(story.RankBaseDamage).IsEqual(30f);
        scope.Begin(CampaignLevel.Orleans, Difficulty.Hard);
        story.AddRankDamage(45);
        AssertThat(story.RankBaseDamage).IsEqual(30f);
        AssertThat(story.RankHits).IsEqual(1);

        // The outgoing scene's hero under the loading screen is not this attempt's.
        story.SceneTransitionInProgressOverrideForTesting = true;
        try {
            story.AddRankDamage(45);
        } finally {
            story.SceneTransitionInProgressOverrideForTesting = null;
        }
        AssertThat(story.RankHits)
            .OverrideFailureMessage("A hit during a scene load must not feed the new attempt.")
            .IsEqual(1);

        story.StopLevelRun();
        story.AddRankDamage(45);
        AssertThat(story.RankHits)
            .OverrideFailureMessage("A hit after the run must not feed the tally.")
            .IsEqual(1);
    }

    [TestCase]
    public void ALevelThatHasAlreadyPaidPaysOnlyTheDifference() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;
        string levelID = StoryManager.GetLevelID(CampaignLevel.Orleans);

        StorySaveData save = scope.Begin(CampaignLevel.Orleans, Difficulty.Normal);
        save.RankDustPaidByLevel[levelID] = 35;
        EventBus.Instance.RaiseLevelComplete(levelID);
        AssertThat(story.LastLevelRankBonusDust).IsEqual(15);
        AssertThat(save.RankDustPaidByLevel[levelID]).IsEqual(50);
        AssertThat(save.DepositedChronalDust[Hero]).IsEqual(15);

        save = scope.Begin(CampaignLevel.Orleans, Difficulty.Normal);
        save.RankDustPaidByLevel[levelID] = 50;
        // Half the budget lost on an untimed run reads a C, worth less than was paid.
        story.AddRankDamage(story.GetSelectedCharacterMaximumHP() * 3 / 4);
        EventBus.Instance.RaiseLevelComplete(levelID);
        AssertThat(story.LastLevelRank.Value.Letter).IsEqual(LevelRankLetter.C);
        AssertThat(story.LastLevelRankBonusDust).IsEqual(0);
        AssertThat(save.RankDustPaidByLevel[levelID])
            .OverrideFailureMessage("A worse run never lowers what the level has paid.")
            .IsEqual(50);
    }

    [TestCase]
    public void LevelZeroEarnsNoRankAndNoDust() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.Tutorial, Difficulty.Normal);

        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Tutorial));

        AssertThat(story.LastLevelRank.HasValue).IsFalse();
        AssertThat(story.LastLevelRankBonusDust).IsEqual(0);
        AssertThat(save.RankByLevel.Count).IsEqual(0);
        AssertThat(save.RankDustPaidByLevel.Count).IsEqual(0);
    }

    [TestCase]
    public void FlorenceHasNoClockAndRanksOnDamageAlone() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;
        StorySaveData save = scope.Begin(CampaignLevel.Florence, Difficulty.Normal);
        // A fifth of a bar: damage 86.7 is an A alone, and would be an S with a full speed half.
        story.AddRankDamage(story.GetSelectedCharacterMaximumHP() / 5);

        EventBus.Instance.RaiseLevelComplete(StoryManager.GetLevelID(CampaignLevel.Florence));

        AssertThat(story.LastLevelRank.Value.Letter).IsEqual(LevelRankLetter.A);
        AssertThat(story.LastLevelRank.Value.SpeedPercent).IsEqual(LevelRankRules.SpeedPercentUntimed);
        AssertThat(story.LastLevelRankBonusDust).IsEqual(35);
        AssertThat(story.LastLevelTierBonusDust).IsEqual(0);
        AssertThat(save.DepositedChronalDust[Hero]).IsEqual(35);
    }

    [TestCase]
    public void TheSpeedReadsTheGaugeAtThePreBossGateNotAfterTheBoss() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;
        string levelID = StoryManager.GetLevelID(CampaignLevel.Orleans);
        StorySaveData save = scope.Begin(CampaignLevel.Orleans, Difficulty.Normal);
        // At par on Normal the gauge reads 55.6 and parUse is 1.0: speed 13.9.
        story.BeginIntegrityClock(AuthoredPar, startingExtractors: 0);
        story.TickIntegrityClock(AuthoredPar);
        float gate = story.TimelineIntegrityPercent;
        story.LockIntegrityAtPreBoss();
        story.TickIntegrityClock(AuthoredPar);
        AssertThat(story.TimelineIntegrityPercent).OverrideFailureMessage("The gauge stops at the gate.").IsEqual(gate);

        EventBus.Instance.RaiseLevelComplete(levelID);

        AssertThat(story.LastLevelRank.Value.SpeedPercent)
            .IsEqual((int)Math.Round(LevelRankRules.SpeedScore(gate, Difficulty.Normal)));
        AssertThat(story.LastLevelRank.Value.SpeedPercent).IsEqual(14);
        AssertThat(story.LastLevelRank.Value.Letter).IsEqual(LevelRankLetter.B);
        AssertThat(save.IntegrityByLevel[levelID])
            .OverrideFailureMessage("The rank must not touch the N05 record.")
            .IsEqual(gate);
    }

    [TestCase]
    public void TheTallySurvivesACollapseASnapAndAQuitAndOnlyARestartResetsIt() {
        using var scope = new RankScope();
        StoryManager story = StoryManager.Instance;

        // Collapse, then the hub's resume from the anchor.
        scope.Begin(CampaignLevel.Orleans, Difficulty.Normal);
        string orleans = StoryManager.GetLevelID(CampaignLevel.Orleans);
        story.AddRankDamage(20);
        story.BeginTimelineCollapse($"{orleans}_checkpoint_1");
        AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: true)).IsTrue();
        AssertThat(story.RankBaseDamage).OverrideFailureMessage("A Collapse resume keeps the tally.").IsEqual(20f);
        AssertThat(story.RankHits).IsEqual(1);

        // ...and the hub's restart from the beginning.
        story.BeginTimelineCollapse($"{orleans}_checkpoint_1");
        AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: false)).IsTrue();
        AssertThat(story.RankBaseDamage).OverrideFailureMessage("A Restart resets the tally.").IsEqual(0f);
        AssertThat(story.RankHits).IsEqual(0);

        // An Anchor Snap, a quit and the load back into the level.
        StorySaveData save = scope.Begin(CampaignLevel.ChronalVoid, Difficulty.Normal);
        string voidLevel = StoryManager.GetLevelID(CampaignLevel.ChronalVoid);
        story.AddRankDamage(30);
        story.BeginAnchorSnap($"{voidLevel}_checkpoint_1");
        AssertThat(story.RankBaseDamage).OverrideFailureMessage("An Anchor Snap keeps the tally.").IsEqual(30f);
        story.AddRankDamage(10);
        story.CommitOrderlyExit(0);
        story.ResumeCampaign(ScratchSlot, save);
        AssertThat(story.RankBaseDamage).OverrideFailureMessage("A quit and resume keeps the tally.").IsEqual(40f);
        AssertThat(story.RankHits).IsEqual(2);

        story.RestartLevelAttempt();
        AssertThat(story.RankBaseDamage).OverrideFailureMessage("A full Restart Level resets the tally.").IsEqual(0f);
    }

    private sealed class RankScope : IDisposable {
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly Difficulty _originalDifficulty;
        private readonly CampaignLevel _originalLevel;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public RankScope() {
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalLevel = StoryManager.Instance.CurrentLevel;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            _originalPaused = ((SceneTree)Engine.GetMainLoop()).Paused;
            StoryManager.Instance.SuppressSceneLoadsForTesting = true;
        }

        public StorySaveData Begin(CampaignLevel level, Difficulty difficulty) {
            StoryManager story = StoryManager.Instance;
            story.PrepareDirectLevel(level, Hero, difficulty);
            var save = new StorySaveData {
                SelectedCharacterID = Hero,
                Difficulty = difficulty,
                CurrentLevelID = StoryManager.GetLevelScenePath(level),
                CurrentHP = 100
            };
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
