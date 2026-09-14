using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A1b — V7.6 F13, the Defy seal read-model and F10's per-attempt
/// persistence.
///
/// <para>The seal is DERIVED from authoritative meter / Defy-used / life state
/// at the end of the gameplay update and published through
/// <c>EventBus.OnDefySealChanged</c>. It is never persisted as its own cosmetic
/// flag, and the HUD never replays a proc because it reloaded. A5 declared the
/// payload in Wave 1 and publishes Level 0's forced states; A1b is the general
/// publisher.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefySealStateTests {

    [TestCase]
    public void TheSealReadsBuildingReadyAndSpentFromAuthoritativeState() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            AssertThat(player.DefySeal)
                .OverrideFailureMessage("An empty meter reads as the dim intact seal.")
                .IsEqual(DefySealState.Building);

            player.CurrentUltimateMeter = UltimateMeter.MaxValue;
            AssertThat(player.DefySeal)
                .OverrideFailureMessage("A full unused meter lights the seal.")
                .IsEqual(DefySealState.Ready);

            player.SetStoryDefyHistoryUsed(true);
            AssertThat(player.DefySeal)
                .OverrideFailureMessage("Spent takes precedence over a full meter.")
                .IsEqual(DefySealState.Spent);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("Refilling the meter never repairs the seal.")
                .IsEqual(UltimateMeter.MaxValue);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void DeathBarsTheSealWithoutClearingTheSpentFlag() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.SetStoryDefyHistoryUsed(true);
            player.TransitionTo(CharacterState.Dead);

            AssertThat(player.DefySeal)
                .OverrideFailureMessage("Barred takes precedence while dead.")
                .IsEqual(DefySealState.Barred);
            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("The underlying spent flag is NOT cleared by the bar.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void TheSealIsPublishedOnlyWhenItActuallyChanges() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        int raises = 0;
        DefySealState last = DefySealState.Barred;
        void OnSeal(DefySealPayload payload) { raises++; last = payload.State; }
        EventBus.Instance.OnDefySealChanged += OnSeal;
        try {
            HoldInput(player, GameplayButtons.None, 10);
            AssertThat(raises)
                .OverrideFailureMessage("Ten steady frames must publish exactly once.")
                .IsEqual(1);
            AssertThat(last).IsEqual(DefySealState.Building);

            player.CurrentUltimateMeter = UltimateMeter.MaxValue;
            HoldInput(player, GameplayButtons.None, 10);
            AssertThat(raises).IsEqual(2);
            AssertThat(last).IsEqual(DefySealState.Ready);
        } finally {
            EventBus.Instance.OnDefySealChanged -= OnSeal;
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AStoryDefySurvivesAMidLevelQuitAndResumeAsSpent() {
        // V7.6 F10: "once per level" means once per ATTEMPT. Before this pass
        // the flag was a private PlayerController field with no persistence, so
        // a mid-level quit and resume - or a death rewind that rebuilt the
        // player - handed the run a second saved life.
        StoryManager story = StoryManager.Instance;
        AssertThat(story)
            .OverrideFailureMessage("The StoryManager autoload must be live.")
            .IsNotNull();

        bool previous = story.StoryDefyHistoryUsed;
        try {
            story.StoryDefyHistoryUsed = true;
            var save = new StorySaveData();
            story.WriteAttemptStateToSave(save);
            AssertThat(save.StoryDefyHistoryUsed)
                .OverrideFailureMessage("The checkpoint save commits the spent use.")
                .IsTrue();

            story.StoryDefyHistoryUsed = false;
            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.StoryDefyHistoryUsed)
                .OverrideFailureMessage("A mid-level resume brings the attempt back SPENT.")
                .IsTrue();

            // Only a fresh entry or a full Restart Level hands it back.
            story.ClearLevelAttemptState();
            AssertThat(story.StoryDefyHistoryUsed)
                .OverrideFailureMessage("A fresh attempt restores the one saved life.")
                .IsFalse();
        } finally {
            story.StoryDefyHistoryUsed = previous;
        }
    }

    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }
}
