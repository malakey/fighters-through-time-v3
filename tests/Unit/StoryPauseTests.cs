using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A1. Story pause discipline, mirroring
/// <c>LocalFighterPauseTests</c> for the surface that now shares its base class.
///
/// Both pause menus derive from <see cref="PauseMenuBase"/>, so the pause-handback
/// invariant is proven on both sides rather than assumed to have survived the
/// extraction. A pause leaked through a scene change freezes whatever loads next,
/// and under GdUnit4 it stops the runner's transport node and hangs the whole
/// session — which is why every test here restores
/// <see cref="SceneTree.Paused"/> in a finally block.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryPauseTests {

    [TestCase]
    public void PausingAndResumingHandsTheSceneTreePauseBack() {
        var scene = new Node { Name = "StoryPauseHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused).IsFalse();

            pause.TogglePause();
            AssertThat(pause.IsPaused).IsTrue();
            AssertThat(tree.Paused).IsTrue();

            pause.TogglePause();
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void LeavingTheTreeWhilePausedReleasesThePause() {
        var scene = new Node { Name = "StoryPauseTeardownHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            pause.TogglePause();
            AssertThat(tree.Paused).IsTrue();

            // Quitting to the menu from an open pause must not freeze the menu.
            scene.RemoveChild(pause);
            pause.Free();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void TheMenuIsHiddenUntilPausedAndHiddenAgainOnResume() {
        var scene = new Node { Name = "StoryPauseVisibilityHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            var root = pause.GetNodeOrNull<Control>("Root");
            AssertObject(root).IsNotNull();
            AssertThat(root.Visible).IsFalse();

            pause.SetPaused(true);
            AssertThat(root.Visible).IsTrue();

            pause.SetPaused(false);
            AssertThat(root.Visible).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void ResumingClosesAnOpenQuitConfirmation() {
        var scene = new Node { Name = "StoryPauseConfirmHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            pause.SetPaused(true);
            var modal = pause.FindChild("ConfirmModal", recursive: true, owned: false) as ConfirmModal;
            AssertObject(modal).IsNotNull();

            var quit = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/QuitButton");
            AssertObject(quit).IsNotNull();
            quit.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(modal.IsOpen).IsTrue();

            // Resuming must not leave a stale confirmation armed behind the HUD,
            // waiting to quit the campaign the next time the player pauses.
            pause.SetPaused(false);
            AssertThat(modal.IsOpen).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void TheAuthoredSceneSuppliesTheMenuAndItsLocalisedCopy() {
        AssertThat(ResourceLoader.Exists(PauseMenu.SceneResourcePath)).IsTrue();

        var scene = new Node { Name = "StoryPauseAuthoredHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            // CreateDefault must resolve the authored scene, not the code fallback,
            // and both paths must produce the same node name so campaign scene
            // trees are identical either way.
            AssertThat(pause.Name.ToString()).IsEqual("PauseMenu");

            var title = pause.GetNodeOrNull<Label>("Root/Center/Panel/Layout/Title");
            AssertObject(title).IsNotNull();
            AssertThat(title.Text).IsEqual("menu_paused");

            // The designed five options (audit M-2): Resume / Settings / Save /
            // Restart Level / Exit (quit to menu).
            AssertObject(pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/ResumeButton")).IsNotNull();
            AssertObject(pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/SettingsButton")).IsNotNull();
            var save = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/SaveButton");
            AssertObject(save).IsNotNull();
            AssertThat(save.Text).IsEqual("menu_save");
            var restart = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/RestartButton");
            AssertObject(restart).IsNotNull();
            AssertThat(restart.Text).IsEqual("menu_restart");
            AssertObject(pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/QuitButton")).IsNotNull();

            // The screen adopts the shared theme rather than per-node overrides.
            var root = pause.GetNodeOrNull<Control>("Root");
            AssertObject(root.Theme).IsNotNull();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void RestartOpensItsOwnConfirmationAndCancelReturnsToTheMenu() {
        var scene = new Node { Name = "StoryPauseRestartHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            pause.SetPaused(true);
            var modal = pause.FindChild("RestartConfirmModal", recursive: true, owned: false) as ConfirmModal;
            AssertObject(modal).IsNotNull();
            AssertThat(modal.IsOpen).IsFalse();

            var restart = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/RestartButton");
            AssertObject(restart).IsNotNull();
            restart.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(modal.IsOpen).IsTrue();

            // Cancel backs out without restarting anything and without
            // releasing the pause.
            modal.Cancel();
            AssertThat(modal.IsOpen).IsFalse();
            AssertThat(pause.IsPaused).IsTrue();

            // Resuming closes a stale restart confirmation, like the quit one.
            restart.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(modal.IsOpen).IsTrue();
            pause.SetPaused(false);
            AssertThat(modal.IsOpen).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void SaveFeedbackFlipsTheLabelAndResetsOnTheNextPause() {
        var scene = new Node { Name = "StoryPauseSaveHost" };
        AddToTree(scene);
        PauseMenu pause = AddPause(scene);
        SceneTree tree = scene.GetTree();

        // Point the session at no slot so the checkpoint write is a no-op:
        // this test pins the menu feedback contract, not the save envelope.
        var gameManager = FTT.Core.GameManager.Instance;
        int previousSlot = gameManager != null ? gameManager.CurrentSession.ActiveSaveSlot : -1;
        if (gameManager != null) {
            var session = gameManager.CurrentSession;
            session.ActiveSaveSlot = -1;
            gameManager.CurrentSession = session;
        }
        try {
            pause.SetPaused(true);
            var save = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/SaveButton");
            AssertObject(save).IsNotNull();
            AssertThat(save.Text).IsEqual("menu_save");

            save.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(save.Text).IsEqual("pause_save_done");

            // A fresh pause offers a fresh save.
            pause.SetPaused(false);
            pause.SetPaused(true);
            AssertThat(save.Text).IsEqual("menu_save");
        } finally {
            if (gameManager != null) {
                var session = gameManager.CurrentSession;
                session.ActiveSaveSlot = previousSlot;
                gameManager.CurrentSession = session;
            }
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void TheExitPenaltyForfeitsTwentyPercentOfUnbankedDust() {
        // V7 unified exit rule ("one rule, one number", enforced V7.2): any exit
        // from an incomplete level forfeits 20% of undeposited dust — the player
        // retains 80%, rounded down, matching StoryManager's collapse path.
        AssertThat(PauseMenu.CalculateExitRetainedDust(40)).IsEqual(32);
        AssertThat(PauseMenu.CalculateExitRetainedDust(41)).IsEqual(32);
        AssertThat(PauseMenu.CalculateExitRetainedDust(1)).IsEqual(0);
        AssertThat(PauseMenu.CalculateExitRetainedDust(0)).IsEqual(0);
        AssertThat(PauseMenu.CalculateExitRetainedDust(-5)).IsEqual(0);

        // Whole wallet earned this level: 40 unbanked -> 32 retained.
        AssertThat(PauseMenu.CalculateExitWalletAfterPenalty(40, 40)).IsEqual(32);
        // Only the level's share of the wallet is penalized.
        AssertThat(PauseMenu.CalculateExitWalletAfterPenalty(100, 40)).IsEqual(92);
        // A tally larger than the wallet clamps to the wallet.
        AssertThat(PauseMenu.CalculateExitWalletAfterPenalty(30, 40)).IsEqual(24);
        // Odd amounts round the retained share down (the forfeit rounds up).
        AssertThat(PauseMenu.CalculateExitWalletAfterPenalty(41, 41)).IsEqual(32);
        AssertThat(PauseMenu.CalculateExitWalletAfterPenalty(0, 0)).IsEqual(0);
    }

    private static PauseMenu AddPause(Node scene) {
        PauseMenu pause = PauseMenu.CreateDefault();
        scene.AddChild(pause);
        return pause;
    }

    private static void AddToTree(Node node) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
    }
}
