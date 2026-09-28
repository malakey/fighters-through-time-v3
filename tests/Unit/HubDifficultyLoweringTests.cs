using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6 (G14). Lowering the campaign difficulty from the hub's pause
/// menu: offered only at the hub between levels, one step down behind a
/// confirmation, applied to the session every fresh level entry reads (rewind
/// pool, Act III anchors, enemy/drop scaling) — never mid-level, never raised.
///
/// <para>The one case that commits a lowering writes the slot to disk, so it
/// picks a slot the environment has proven empty and deletes it afterwards (the
/// MainMenuSceneTests idiom). Pause discipline: finally restores the tree.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HubDifficultyLoweringTests {

    [TestCase]
    public void TheHubPauseOffersLoweringAndConfirmingItStepsTheSessionAndSaveDown() {
        SaveManager saves = SaveManager.Instance;
        int slot = FirstEmptySlot(saves);
        if (slot < 0) return;
        SessionData session = GameManager.Instance.CurrentSession;
        var scene = Host("LowerDifficultyHost");
        PauseMenu pause = PauseMenu.CreateDefault();
        pause.AtHubOverrideForTesting = true;
        scene.AddChild(pause);
        SceneTree tree = scene.GetTree();
        try {
            var save = new StorySaveData {
                SelectedCharacterID = "lincoln", Difficulty = Difficulty.Hard, LowestDifficultyUsed = Difficulty.Hard
            };
            saves.SaveSlots[slot] = save;
            SessionData active = session;
            active.ActiveSaveSlot = slot;
            active.Difficulty = Difficulty.Hard;
            GameManager.Instance.CurrentSession = active;

            pause.SetPaused(true);
            var button = pause.GetNode<Button>("Root/Center/Panel/Layout/LowerDifficultyButton");
            AssertThat(button.Visible).IsTrue();
            button.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(pause.LowerDifficultyConfirmation.IsOpen).IsTrue();
            pause.LowerDifficultyConfirmation.Confirm();

            AssertThat(save.Difficulty).IsEqual(Difficulty.Normal);
            AssertThat(save.LowestDifficultyUsed).IsEqual(Difficulty.Normal);
            AssertThat(GameManager.Instance.CurrentSession.Difficulty).IsEqual(Difficulty.Normal);
            // The next fresh entry reads the new tier: Normal's pool and anchors.
            AssertThat(FTT.Environment.ChronalRewindManager.GetMaximumRewinds(
                GameManager.Instance.CurrentSession.Difficulty)).IsEqual(3);
            AssertThat(StoryManager.AnchorChargesFor(GameManager.Instance.CurrentSession.Difficulty)).IsEqual(2);
        } finally {
            tree.Paused = false;
            GameManager.Instance.CurrentSession = session;
            saves.DeleteStorySlot(slot);
            FreeHost(scene);
        }
    }

    [TestCase]
    public void LoweringIsNotOfferedAwayFromTheHubMidLevelOrOnEasy() {
        SaveManager saves = SaveManager.Instance;
        const int scratch = 2;
        StorySaveData original = saves.SaveSlots[scratch];
        SessionData session = GameManager.Instance.CurrentSession;
        var scene = Host("LowerDifficultyRefusalHost");
        PauseMenu pause = PauseMenu.CreateDefault();
        scene.AddChild(pause);
        SceneTree tree = scene.GetTree();
        try {
            var save = new StorySaveData { SelectedCharacterID = "tesla", Difficulty = Difficulty.Normal };
            saves.SaveSlots[scratch] = save;
            SessionData active = session;
            active.ActiveSaveSlot = scratch;
            GameManager.Instance.CurrentSession = active;

            pause.AtHubOverrideForTesting = false;
            AssertThat(pause.CanOfferLowerDifficulty()).IsFalse();

            pause.AtHubOverrideForTesting = true;
            AssertThat(pause.CanOfferLowerDifficulty()).IsTrue();

            // A parked mid-level attempt: resuming it would apply the tier mid-level.
            save.LastCheckpointID = "level_03_checkpoint_1";
            AssertThat(CampaignDifficultyService.EvaluateActive(atHub: true))
                .IsEqual(CampaignDifficultyRules.Verdict.MidLevelAttempt);
            AssertThat(pause.CanOfferLowerDifficulty()).IsFalse();

            save.LastCheckpointID = "";
            save.Difficulty = Difficulty.Easy;
            AssertThat(CampaignDifficultyService.EvaluateActive(atHub: true))
                .IsEqual(CampaignDifficultyRules.Verdict.AlreadyEasiest);

            pause.SetPaused(true);
            AssertThat(pause.GetNode<Button>("Root/Center/Panel/Layout/LowerDifficultyButton").Visible).IsFalse();
        } finally {
            tree.Paused = false;
            saves.SaveSlots[scratch] = original;
            GameManager.Instance.CurrentSession = session;
            FreeHost(scene);
        }
    }

    private static int FirstEmptySlot(SaveManager saves) {
        for (int slot = 0; slot < saves.SaveSlots.Length; slot++) {
            if (saves.SaveSlots[slot] == null) return slot;
        }
        return -1;
    }

    private static Node Host(string name) {
        var scene = new Node { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(scene);
        return scene;
    }

    private static void FreeHost(Node scene) {
        if (!GodotObject.IsInstanceValid(scene)) return;
        scene.GetParent()?.RemoveChild(scene);
        scene.Free();
    }
}
