using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W2 — S27 on the load path. A v7 save parked on a deleted
/// <c>Level_04A_&lt;hero&gt;.tscn</c> used to fall through
/// <c>StoryManager.ResumeCampaign</c>'s path lookup to the Tutorial. It now goes
/// through the declared v8 derivation (<see cref="StoryAttemptState.RetireLegacyLevelV8"/>,
/// plan D2) and resumes at Level 5 on a fresh attempt. This suite also pins the
/// derivation's engine-free literals against the live route.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RetireLegacyLevelResumeTests {

    [TestCase]
    public void TheDerivationsLiteralsNameTheLevelThatFollowsParisOnTheRoute() {
        AssertString(StoryAttemptState.LegacyRetirementScenePath)
            .IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.Titanic));
        AssertString(StoryAttemptState.LegacyRetirementLevelID)
            .IsEqual(StoryManager.GetLevelID(CampaignLevel.Titanic));
        AssertThat(StoryManager.RouteIndexOf(CampaignLevel.Titanic))
            .IsEqual(StoryManager.RouteIndexOf(CampaignLevel.Paris) + 1);
        // No routed level resolves to the retired prefix.
        foreach (CampaignLevel level in StoryManager.CampaignRoute) {
            AssertThat(StoryAttemptState.IsRetiredLegacyLevelScene(StoryManager.GetLevelScenePath(level))).IsFalse();
        }
    }

    [TestCase]
    public void ResumingASaveParkedOnARetiredLegacySceneLandsOnTheTitanicNotTheTutorial() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            var parked = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = "res://scenes/campaign/Level_04A_einstein.tscn",
                LastCheckpointID = "level_04a_einstein_checkpoint_1",
                LevelChronalDust = 6
            };
            parked.CompletedLevels.Add("level_04_paris");
            parked.AttemptState = StoryAttemptState.CreateFresh("level_04a_einstein", 0);
            saves.SaveSlots[scratchSlot] = parked;

            story.ResumeCampaign(scratchSlot, parked);

            AssertThat(story.CurrentLevel)
                .OverrideFailureMessage("A retired 4A path must never fall back to the Tutorial.")
                .IsEqual(CampaignLevel.Titanic);
            // Acts I-II resume through the Time-Ship; the portal then starts
            // Level 5 as a fresh entry.
            AssertString(story.LastRequestedScenePath).IsEqual(StoryManager.HubScenePath);
            AssertString(parked.CurrentLevelID).IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.Titanic));
            AssertString(parked.LastCheckpointID).IsEqual("");
            AssertThat(parked.LevelChronalDust).IsEqual(0);
            AssertThat(parked.DepositedChronalDust["einstein"]).IsEqual(6);
            AssertThat(story.ChronalDustCollected).IsEqual(0);
            AssertString(story.CurrentAttempt.LevelID).IsEqual("level_05_titanic");
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }
}
