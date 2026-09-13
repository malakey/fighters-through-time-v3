using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — the V7.6 Act III Gauntlet: three levels, one run, no ship.
///
/// <para>"We can hear you. We can't reach you. If you fall in there, you get
/// up." Past the Void the Wardens' rift cannot follow, so Levels 13–15 play back
/// to back: the results overlay and the post-boss dialogue still play, there is
/// simply no portal trip, and the hub stays unreachable until the campaign is
/// complete. Hard's middle checkpoint — inert everywhere else — is active for the
/// gauntlet. The Resonance Hold keeps the same clock with different drain
/// sources: severed conduits, cradle intake valves, firing-channel anchor pylons,
/// each carrying an Extractor's weight exactly.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ActThreeChainingTests {

    [TestCase]
    public void CompletingAnActIIILevelRoutesToTheNextLevelAndNeverToTheHub() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            // Level 13 -> 14 -> 15 along the authored route, with no hub between.
            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Normal);
            story.AdvanceToNextLevel();
            AssertThat(story.CurrentLevel).IsEqual(CampaignLevel.NeoEarth);
            story.AdvanceToNextLevel();
            AssertThat(story.CurrentLevel).IsEqual(CampaignLevel.Alexandria);
            story.AdvanceToNextLevel();
            AssertThat(story.CurrentLevel)
                .OverrideFailureMessage("Alexandria is still the campaign's hard cap.")
                .IsEqual(CampaignLevel.Alexandria);

            // And the three levels are exactly the gauntlet — Level 4A, appended
            // at enum value 16, is emphatically not part of it.
            AssertThat(StoryManager.IsActIIILevel(CampaignLevel.ChronalVoid)).IsTrue();
            AssertThat(StoryManager.IsActIIILevel(CampaignLevel.NeoEarth)).IsTrue();
            AssertThat(StoryManager.IsActIIILevel(CampaignLevel.Alexandria)).IsTrue();
            AssertThat(StoryManager.IsActIIILevel(CampaignLevel.Lunar)).IsFalse();
            AssertThat(StoryManager.IsActIIILevel(CampaignLevel.LegacyNexus)).IsFalse();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AnActIIIResumeReturnsToTheLevelWhileActsOneAndTwoReturnToTheHub() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            var actIII = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.NeoEarth),
                LastCheckpointID = "level_14_neo_earth_checkpoint_1"
            };
            actIII.AttemptState = StoryAttemptState.CreateFresh("level_14_neo_earth", 2);
            saves.SaveSlots[scratchSlot] = actIII;
            story.ResumeCampaign(scratchSlot, actIII);
            AssertThat(story.LastRequestedScenePath)
                .OverrideFailureMessage("The hub is unreachable during the gauntlet.")
                .IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.NeoEarth));

            var actII = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Lunar),
                LastCheckpointID = "level_12_lunar_checkpoint_1"
            };
            actII.AttemptState = StoryAttemptState.CreateFresh("level_12_lunar", 0);
            saves.SaveSlots[scratchSlot] = actII;
            story.ResumeCampaign(scratchSlot, actII);
            AssertThat(story.LastRequestedScenePath)
                .OverrideFailureMessage("Acts I-II still resume through the Time-Ship.")
                .IsEqual(StoryManager.HubScenePath);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AllThreeActIIIAnchorsStayActiveOnHard() {
        // F12: Hard's middle fracture is inert in Acts I-II only. V7.6 suspends
        // that for the gauntlet — a run with one anchor charge and one usable
        // anchor is not the difficulty curve the design asked for.
        foreach (CampaignLevel level in new[] {
                     CampaignLevel.ChronalVoid, CampaignLevel.NeoEarth, CampaignLevel.Alexandria }) {
            AssertThat(StoryLevelControllerBase.IsMiddleAnchorInert(level, CheckpointRole.Middle, Difficulty.Hard))
                .OverrideFailureMessage($"{level}'s middle anchor must stay live on Hard.")
                .IsFalse();
        }
        AssertThat(StoryLevelControllerBase.IsMiddleAnchorInert(
                CampaignLevel.Lunar, CheckpointRole.Middle, Difficulty.Hard))
            .OverrideFailureMessage("Acts I-II keep the inert-middle rule.")
            .IsTrue();
        AssertThat(StoryLevelControllerBase.IsMiddleAnchorInert(
                CampaignLevel.Lunar, CheckpointRole.Middle, Difficulty.Normal))
            .OverrideFailureMessage("The rule is Hard-only.")
            .IsFalse();
        // Role, never the ID suffix: Level 4A's PreBoss anchor is spelled
        // `_checkpoint_1` and must never be disabled for it.
        AssertThat(StoryLevelControllerBase.IsMiddleAnchorInert(
                CampaignLevel.LegacyNexus, CheckpointRole.PreBoss, Difficulty.Hard))
            .OverrideFailureMessage("A PreBoss anchor is never inert, whatever its ID ends with.")
            .IsFalse();
        AssertThat(StoryLevelControllerBase.IsMiddleAnchorInert(
                CampaignLevel.Orleans, CheckpointRole.Entry, Difficulty.Hard))
            .IsFalse();
    }

    [TestCase]
    public void TheDrainStandInsCarryTheExtractorWeightWithTheirOwnEraIdentity() {
        // Each variant IS a Chronal Extractor mechanically: the whole point of
        // the Resonance Hold is that the gauge, the rules, the HUD and the Tremor
        // are unchanged and only the fiction moves.
        var conduit = new ResonanceHoldNode { Variant = ResonanceHoldVariant.SeveredConduit };
        var valve = new ResonanceHoldNode { Variant = ResonanceHoldVariant.CradleIntakeValve };
        var pylon = new ResonanceHoldNode { Variant = ResonanceHoldVariant.AnchorPylon };
        try {
            AssertThat(conduit is ChronalExtractor)
                .OverrideFailureMessage("Same drain weight means the same type, not a copy of it.")
                .IsTrue();
            AssertThat(conduit.DustReward)
                .OverrideFailureMessage("The F05 optional-dust share is resource-owned and unchanged.")
                .IsEqual(new ChronalExtractor().DustReward);

            // Three distinct era identities, three distinct localized names.
            AssertThat(conduit.DisplayNameKey).IsEqual("hold_node_severed_conduit_name");
            AssertThat(valve.DisplayNameKey).IsEqual("hold_node_cradle_valve_name");
            AssertThat(pylon.DisplayNameKey).IsEqual("hold_node_anchor_pylon_name");
            AssertThat(conduit.DrainSlowedNoticeKey).IsEqual("hold_drain_slowed_conduit");
            AssertThat(valve.DrainSlowedNoticeKey).IsEqual("hold_drain_slowed_valve");
            AssertThat(pylon.DrainSlowedNoticeKey).IsEqual("hold_drain_slowed_pylon");
            AssertThat(ResonanceHoldNode.CoreColorFor(ResonanceHoldVariant.SeveredConduit))
                .IsNotEqual(ResonanceHoldNode.CoreColorFor(ResonanceHoldVariant.AnchorPylon));

            // The candle rule: a stand-in touches no player stat or ability. It is
            // a drain source and a breakable object, nothing else.
            AssertThat(conduit.UltimateDrain).IsEqual(new ChronalExtractor().UltimateDrain);
            AssertThat(conduit.HazardDamage).IsEqual(new ChronalExtractor().HazardDamage);
        } finally {
            conduit.Free();
            valve.Free();
            pylon.Free();
        }
    }

}
