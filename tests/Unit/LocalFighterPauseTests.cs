using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 6 A2. Local Fighter Mode pause discipline, the forced
/// controller-disconnect pause, and the device-assignment fix that stops a
/// reconnect from silently reshuffling both fighters mid-match.
///
/// Every test that pauses restores <see cref="SceneTree.Paused"/> in a finally
/// block: a leaked pause stops GdUnit4's transport node and hangs the session.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LocalFighterPauseTests {

    [TestCase]
    public void PausingAndResumingHandsTheSceneTreePauseBack() {
        var scene = new Node { Name = "PauseHost" };
        AddToTree(scene);
        var pause = new LocalFighterPause { Name = "LocalFighterPause" };
        scene.AddChild(pause);
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
        var scene = new Node { Name = "PauseTeardownHost" };
        AddToTree(scene);
        var pause = new LocalFighterPause { Name = "LocalFighterPause" };
        scene.AddChild(pause);
        SceneTree tree = scene.GetTree();
        try {
            pause.TogglePause();
            AssertThat(tree.Paused).IsTrue();

            // A scene change while paused must not freeze whatever loads next.
            scene.RemoveChild(pause);
            pause.Free();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void ADisconnectedControllerForcesThePauseUntilTheSlotIsBoundAgain() {
        var scene = new Node { Name = "DisconnectHost" };
        AddToTree(scene);
        var pause = new LocalFighterPause { Name = "LocalFighterPause" };
        scene.AddChild(pause);
        SceneTree tree = scene.GetTree();
        try {
            pause.ForceDisconnectPause(1);
            AssertThat(pause.IsPaused).IsTrue();
            AssertThat(pause.DisconnectedPlayer).IsEqual(1);
            AssertThat(tree.Paused).IsTrue();

            // The wrong slot resolving must not clear the modal.
            pause.ResolveDisconnect(0);
            AssertThat(pause.DisconnectedPlayer).IsEqual(1);
            AssertThat(tree.Paused).IsTrue();

            pause.ResolveDisconnect(1);
            AssertThat(pause.DisconnectedPlayer).IsEqual(-1);
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    [TestCase]
    public void ReconnectingAJoypadKeepsExistingAssignmentsInsteadOfReshufflingThem() {
        var manager = AutoFree(new InputManager { MaxPlayers = 2 });

        // Deliberately non-default: pad 1 on player one, pad 0 on player two.
        manager.SetConnectedJoypadsForTesting(0, 1);
        manager.AssignJoypadToPlayer(1, 0);
        manager.AssignJoypadToPlayer(0, 1);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(1);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(0);

        // A third pad appears. The old code re-ran a full auto-assign here and
        // silently swapped both fighters' controllers mid-match.
        manager.SetConnectedJoypadsForTesting(0, 1, 2);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(1);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(0);
    }

    [TestCase]
    public void ADisconnectOnlyClearsTheAffectedSlot() {
        var manager = AutoFree(new InputManager { MaxPlayers = 2 });
        manager.SetConnectedJoypadsForTesting(0, 1);
        manager.AssignJoypadToPlayer(0, 0);
        manager.AssignJoypadToPlayer(1, 1);

        // Player two's pad drops out; player one keeps its device untouched.
        manager.SetConnectedJoypadsForTesting(0);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(0);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);

        // Reconnect: the free pad binds back to the empty slot only.
        manager.SetConnectedJoypadsForTesting(0, 1);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(0);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(1);
    }

    [TestCase]
    public void RebindingTakesTheFirstUnclaimedConnectedDevice() {
        var manager = AutoFree(new InputManager { MaxPlayers = 2 });
        manager.SetConnectedJoypadsForTesting(3);
        manager.AssignJoypadToPlayer(3, 0);

        // Nothing free: the affected slot stays unbound rather than stealing one.
        AssertThat(manager.TryAssignFirstFreeDevice(1)).IsFalse();
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(3);

        manager.SetConnectedJoypadsForTesting(3, 5);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(5);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(3);
    }

    /// <summary>
    /// V7.3 UI-scale pass: the pause title and the disconnect modal's message
    /// ride theme Label variations (TitleLabel / EmphasisLabel) instead of
    /// font-size overrides, so they follow the accessibility UI scale.
    /// </summary>
    [TestCase]
    public void TitleAndDisconnectLabelUseThemeVariationsWithNoFontSizeOverride() {
        var scene = new Node { Name = "PauseScalePinHost" };
        AddToTree(scene);
        var pause = new LocalFighterPause { Name = "LocalFighterPause" };
        scene.AddChild(pause);
        SceneTree tree = scene.GetTree();
        try {
            var panel = pause.FindChild("PausePanel", recursive: true, owned: false) as Control;
            AssertObject(panel).IsNotNull();
            Node layout = panel.GetChild(0);
            Label title = null;
            for (int index = 0; index < layout.GetChildCount() && title == null; index++) {
                title = layout.GetChild(index) as Label;
            }
            AssertObject(title).IsNotNull();
            AssertThat(title.ThemeTypeVariation.ToString()).IsEqual(UIPalette.TitleLabelVariation);
            AssertThat(title.HasThemeFontSizeOverride("font_size")).IsFalse();

            var disconnect = pause.FindChild("DisconnectLabel", recursive: true, owned: false) as Label;
            AssertObject(disconnect).IsNotNull();
            AssertThat(disconnect.ThemeTypeVariation.ToString())
                .IsEqual(UIPalette.EmphasisLabelVariation);
            AssertThat(disconnect.HasThemeFontSizeOverride("font_size")).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    private static void AddToTree(Node node) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
    }
}
