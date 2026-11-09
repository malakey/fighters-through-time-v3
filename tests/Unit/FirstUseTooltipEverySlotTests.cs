using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-LV1 (orchestrator's decision): no calibration teaches Specials, so the
/// <c>special_momentum</c> tooltip shows on every slot, in real frames through the presenter.
/// The pure rule is pinned in <c>FirstUseTooltipsTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FirstUseTooltipEverySlotTests {
    private const int ScratchSlot = 2;

    /// <summary>A full-calibration slot on Level 1 sees it once, across two presenters, and is offered nothing else.</summary>
    [TestCase(Timeout = 30000)]
    public async Task AFullCalibrationSlotOnLevelOneSeesTheSpecialTooltipOnceAndNoOther() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        StoryManager story = StoryManager.Instance;
        SessionData originalSession = GameManager.Instance.CurrentSession;
        CampaignLevel originalLevel = story.CurrentLevel;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
        string originalSaveDirectory = SaveManager.SaveDirectoryOverrideForTesting;
        SaveManager.SaveDirectoryOverrideForTesting = $"user://test-saves/{System.Guid.NewGuid():N}/";

        var host = new Node { Name = "TooltipEverySlotHost" };
        tree.Root.AddChild(host);
        PlayerController hero = null;
        try {
            var save = new StorySaveData {
                SelectedCharacterID = "einstein",
                Difficulty = Difficulty.Normal,
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Florence),
                CurrentHP = 100,
                CompletedLevels = new List<string> { "level_00_tutorial" }
            };
            AssertThat(save.SkippedCalibration).IsFalse();
            story.PrepareDirectLevel(CampaignLevel.Florence, "einstein", Difficulty.Normal);
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession = session;

            hero = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(hero);
            AssertThat(hero.IsAbilityUnlocked(AbilitySlot.Special1)).IsTrue();

            FirstUseTooltipPresenter first = FirstUseTooltipPresenter.Attach(host, sceneHasTimeFreeze: false);
            for (int frame = 0; frame < 12; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(first.ShowingTooltipID).IsEqual(FirstUseTooltips.SpecialMomentum);
            AssertThat(save.ShownFirstUseTooltips.Count).IsEqual(1);

            // The rest of the rule stands: the skipped lessons stay silent here.
            AssertThat(first.Offer(FirstUseTooltips.Grab)).IsFalse();
            AssertThat(first.PendingCount).IsEqual(0);
            first.GetParent().RemoveChild(first);
            first.Free();

            // A second load of the level: it does not show again.
            FirstUseTooltipPresenter second = FirstUseTooltipPresenter.Attach(host, sceneHasTimeFreeze: false);
            for (int frame = 0; frame < 12; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(second.ShowingTooltipID).IsEqual("");
            AssertThat(save.ShownFirstUseTooltips.Count).IsEqual(1);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
            story.PrepareDirectLevel(originalLevel,
                string.IsNullOrEmpty(originalSession.SelectedCharacterID) ? "einstein" : originalSession.SelectedCharacterID,
                originalSession.Difficulty);
            SaveManager.Instance.SaveSlots[ScratchSlot] = originalSave;
            GameManager.Instance.CurrentSession = originalSession;
            SaveManager.SaveDirectoryOverrideForTesting = originalSaveDirectory;
        }
    }
}
