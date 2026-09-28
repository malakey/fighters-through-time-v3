using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6 (G11, D7(a)). Window focus loss — and, through the no-op
/// platform seam, a future Steam overlay — opens the ordinary pause menu in every
/// pausable mode, and regaining focus never resumes. Controller loss pauses Story
/// and the hub, and a face button on a connected pad resumes.
///
/// <para>Pause discipline (failure signature 4): every case restores
/// <see cref="SceneTree.Paused"/> in a finally block.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PauseFocusLossTests {

    [TestCase]
    public void LosingWindowFocusOpensTheStoryPauseAndRegainingItDoesNotResume() {
        var scene = Host("FocusLossStoryHost");
        PauseMenu pause = AddStoryPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            pause.Notification((int)Node.NotificationApplicationFocusOut);
            AssertThat(pause.IsPaused).IsTrue();
            AssertThat(tree.Paused).IsTrue();

            pause.Notification((int)Node.NotificationApplicationFocusIn);
            AssertThat(pause.IsPaused)
                .OverrideFailureMessage("Nothing resumes until the player chooses Resume.").IsTrue();

            // A second focus loss while paused is a no-op, not a toggle.
            AssertThat(pause.HandleFocusLost()).IsFalse();
            AssertThat(pause.IsPaused).IsTrue();
        } finally {
            tree.Paused = false;
            Free(scene);
        }
    }

    [TestCase]
    public void TheLocalFighterPauseAlsoPausesOnFocusLoss() {
        var scene = Host("FocusLossFighterHost");
        var pause = new LocalFighterPause { Name = "LocalFighterPause" };
        scene.AddChild(pause);
        SceneTree tree = scene.GetTree();
        try {
            AssertThat(pause.HandleFocusLost()).IsTrue();
            AssertThat(tree.Paused).IsTrue();
        } finally {
            tree.Paused = false;
            Free(scene);
        }
    }

    [TestCase]
    public void ThePlatformOverlaySeamIsANoOpThatStillReachesThePause() {
        AssertThat(PlatformOverlay.Current.IsAvailable).IsFalse();
        AssertThat(PlatformOverlay.Current.Name).IsEqual("none");
        var scene = Host("OverlayHost");
        PauseMenu pause = AddStoryPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            PlatformOverlay.ReportOverlayActive(true);
            AssertThat(pause.IsPaused).IsTrue();
        } finally {
            tree.Paused = false;
            Free(scene);
        }
        // Freed menus unsubscribed: a later report reaches nothing and throws nothing.
        PlatformOverlay.ReportOverlayActive(true);
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void ControllerLossPausesStoryAndAFaceButtonOnAConnectedPadResumes() {
        var scene = Host("ControllerLossHost");
        PauseMenu pause = AddStoryPause(scene);
        SceneTree tree = scene.GetTree();
        InputManager input = InputManager.Instance;
        try {
            pause.ForceControllerLostPause();
            AssertThat(pause.IsPaused).IsTrue();
            AssertThat(pause.IsAwaitingController).IsTrue();
            var notice = pause.GetNode<Label>("Root/Center/Panel/Layout/ControllerNotice");
            AssertThat(notice.Visible).IsTrue();

            input?.SetConnectedJoypadsForTesting(7);
            AssertThat(pause.ResolveControllerLost(7)).IsTrue();
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused).IsFalse();
            AssertThat(notice.Visible).IsFalse();
            if (input != null) AssertThat(input.GetDeviceForPlayer(0)).IsEqual(7);
        } finally {
            tree.Paused = false;
            input?.SetConnectedJoypadsForTesting();
            input?.AutoAssignDevices();
            Free(scene);
        }
    }

    [TestCase]
    public void ResumingByAnyRouteClearsTheControllerNotice() {
        var scene = Host("ControllerNoticeHost");
        PauseMenu pause = AddStoryPause(scene);
        SceneTree tree = scene.GetTree();
        try {
            pause.ForceControllerLostPause();
            pause.SetPaused(false);
            AssertThat(pause.IsAwaitingController).IsFalse();
            AssertThat(pause.GetNode<Label>("Root/Center/Panel/Layout/ControllerNotice").Visible).IsFalse();
        } finally {
            tree.Paused = false;
            Free(scene);
        }
    }

    private static Node Host(string name) {
        var scene = new Node { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(scene);
        return scene;
    }

    private static PauseMenu AddStoryPause(Node scene) {
        PauseMenu pause = PauseMenu.CreateDefault();
        scene.AddChild(pause);
        return pause;
    }

    private static void Free(Node scene) {
        if (!GodotObject.IsInstanceValid(scene)) return;
        scene.GetParent()?.RemoveChild(scene);
        scene.Free();
    }
}
