using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — V7.6 anchor charges, the Act III gauntlet's stakes.
///
/// <para>By Act III the player has a build but the required route cannot buy the
/// whole grid, and the 20% dust fee alone has uneven bite — the charges are what
/// make a death in the finale cost something. So the initialization rule is
/// narrow and load-bearing: <b>Easy 3 / Normal 2 / Hard 1 per Act III level,
/// initialized only on fresh level entry or an explicit full Restart Level</b>,
/// and never on a scene reload, a hub return, a checkpoint activation or an
/// Anchor Snap.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AnchorChargeTests {

    [TestCase]
    public void TheDifficultyTableIsEasyThreeNormalTwoHardOne() {
        AssertThat(StoryManager.AnchorChargesFor(Difficulty.Easy)).IsEqual(3);
        AssertThat(StoryManager.AnchorChargesFor(Difficulty.Normal)).IsEqual(2);
        AssertThat(StoryManager.AnchorChargesFor(Difficulty.Hard)).IsEqual(1);
    }

    [TestCase]
    public void ChargesInitializeOnFreshActIIIEntryAndAreZeroOutsideActIII() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;

            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            story.BeginLevelRun();
            AssertThat(story.AnchorChargesRemaining)
                .OverrideFailureMessage("Acts I-II have no Beacon and no anchors.")
                .IsEqual(0);

            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Normal);
            story.BeginLevelRun();
            AssertThat(story.AnchorChargesRemaining).IsEqual(2);
            AssertThat(story.AnchorChargeMaximum).IsEqual(2);

            // Level 4A is enum value 16 — numerically past ChronalVoid — and is
            // played between Levels 4 and 5. It is not Act III.
            AssertThat(StoryManager.IsActIIILevel(CampaignLevel.LegacyNexus))
                .OverrideFailureMessage("Level 4A must never be classified as Act III.")
                .IsFalse();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            story.PrepareDirectLevel(originalLevel, "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void NeitherAReloadNorACheckpointNorAHubReturnRefreshesCharges() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.NeoEarth, "einstein", Difficulty.Easy);
            story.BeginLevelRun();
            AssertThat(story.AnchorChargesRemaining).IsEqual(3);
            story.SpendAnchorCharge();
            AssertThat(story.AnchorChargesRemaining).IsEqual(2);

            // A checkpoint activation is not a refill.
            story.TryActivateCheckpoint("level_14_neo_earth_checkpoint_1");
            AssertThat(story.AnchorChargesRemaining).IsEqual(2);

            // Neither is a scene reload: the parked attempt comes back as it was.
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            AssertThat(save.AnchorCharges).IsEqual(2);
            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.AnchorChargesRemaining)
                .OverrideFailureMessage("A reload restores the spent count; it never refills it.")
                .IsEqual(2);

            // Nor is a hub return.
            story.ReturnToHub();
            AssertThat(story.AnchorChargesRemaining).IsEqual(2);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            story.PrepareDirectLevel(originalLevel, "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ASnapSpendsExactlyOneChargeIncludingTheLast() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Hard);
            story.BeginLevelRun();
            AssertThat(story.AnchorChargesRemaining).IsEqual(1);

            story.BeginAnchorSnap("level_13_chronal_void_checkpoint_1");
            AssertThat(story.AnchorChargesRemaining)
                .OverrideFailureMessage("Spending the last charge 1 -> 0 still completes the Snap.")
                .IsEqual(0);
            AssertThat(story.LastRequestedScenePath)
                .OverrideFailureMessage("A Snap stays in the level; it never routes to the hub.")
                .IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid));
            AssertThat(story.AttemptStatus)
                .OverrideFailureMessage("Zero remaining is a valid LIVING state after a successful Snap.")
                .IsEqual(StoryAttemptStatus.Active);

            // And a Snap is not itself a refill.
            AssertThat(story.SpendAnchorCharge())
                .OverrideFailureMessage("With none left there is nothing to spend — that is Smothered.")
                .IsFalse();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            story.PrepareDirectLevel(originalLevel, "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AnExplicitRestartLevelRefillsThemAndMintsANewAttempt() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.Alexandria, "einstein", Difficulty.Easy);
            story.BeginLevelRun();
            string attempt = story.CurrentAttempt.AttemptID;
            story.SpendAnchorCharge();
            story.SpendAnchorCharge();
            AssertThat(story.AnchorChargesRemaining).IsEqual(1);

            story.RestartLevelAttempt();
            AssertThat(story.AnchorChargesRemaining)
                .OverrideFailureMessage("Restart Level is the cost-bearing way to replenish anchors.")
                .IsEqual(3);
            AssertThat(story.CurrentAttempt.AttemptID).IsNotEqual(attempt);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            story.PrepareDirectLevel(originalLevel, "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }
}
