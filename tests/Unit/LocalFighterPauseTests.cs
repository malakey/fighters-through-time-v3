using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
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
        // 9.1 (2026-10-16): two pads in a local match means a local join.
        var manager = AutoFree(new InputManager { MaxPlayers = 2 });
        manager.SetConnectedJoypadsForTesting(0, 1);
        manager.BeginLocalJoin();
        manager.AssignJoypadToPlayer(0, 0);
        manager.AssignJoypadToPlayer(1, 1);

        // Player two's pad drops out; player one keeps its device untouched.
        manager.SetConnectedJoypadsForTesting(0);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(0);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);

        // Reconnect: the free pad binds back to the slot the disconnect emptied only.
        manager.SetConnectedJoypadsForTesting(0, 1);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(0);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(1);

        // Outside a join, slot 1 is never filled just because a pad is connected:
        // a lone pad follows onto slot 0 instead of becoming player two.
        manager.EndLocalJoin();
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);
        manager.SetConnectedJoypadsForTesting(0);
        manager.SetConnectedJoypadsForTesting(0, 1);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);
    }

    [TestCase]
    public void RebindingTakesTheFirstUnclaimedConnectedDevice() {
        var manager = AutoFree(new InputManager { MaxPlayers = 2 });
        manager.SetConnectedJoypadsForTesting(3);
        manager.AssignJoypadToPlayer(3, 0);

        // Nothing free: the affected slot stays unbound rather than stealing one.
        AssertThat(manager.TryAssignFirstFreeDevice(1)).IsFalse();
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(3);

        // 9.1: a newly connected pad is not handed a slot by itself; the
        // disconnect modal's Rebind (this call) takes the first unclaimed one.
        manager.SetConnectedJoypadsForTesting(3, 5);
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);
        AssertThat(manager.TryAssignFirstFreeDevice(1)).IsTrue();
        AssertThat(manager.GetDeviceForPlayer(1)).IsEqual(5);
        AssertThat(manager.GetDeviceForPlayer(0)).IsEqual(3);
    }

    /// <summary>
    /// V7.3 UI-scale pass: the pause title and the disconnect modal's message
    /// ride theme Label variations instead of font-size overrides, so they
    /// follow the accessibility UI scale. TG-PAUSE: the disconnect message is
    /// the confirm modal's body copy (UI_SPEC_GLASS 9.2, BodyLabel).
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
            Node layout = panel.GetNode("Layout");
            Label title = null;
            for (int index = 0; index < layout.GetChildCount() && title == null; index++) {
                title = layout.GetChild(index) as Label;
            }
            AssertObject(title).IsNotNull();
            AssertThat(title.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.PauseTitle);
            AssertThat(title.HasThemeFontSizeOverride("font_size")).IsFalse();

            var disconnect = pause.FindChild("DisconnectLabel", recursive: true, owned: false) as Label;
            AssertObject(disconnect).IsNotNull();
            AssertThat(disconnect.ThemeTypeVariation.ToString())
                .IsEqual(UIPalette.LabelRoles.BodyLabel);
            AssertThat(disconnect.HasThemeFontSizeOverride("font_size")).IsFalse();
        } finally {
            tree.Paused = false;
            scene.QueueFree();
        }
    }

    /// <summary>
    /// TG-CLOSE polish: the Fighter pause's two Move List rows name each fighter by the short name the select tiles use
    /// (<c>character_&lt;id&gt;_card_name</c>, EINSTEIN ... TUBMAN), the same way in both rows, and every hero's row reads
    /// whole at its role size (no shrink, no ellipsis) at UI scale 1.0 and 1.4. With the full name, "MOVE LIST — WOLFGANG
    /// AMADEUS …" was shrunk and cut. Every hero is tested in both rows, a different one in each.
    /// </summary>
    [TestCase]
    public async Task EveryHerosMoveListRowsNameItByItsShortNameAndReadWholeAtBothUiScales() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager manager = GameManager.Instance;
        AssertObject(manager).IsNotNull();
        SessionData saved = manager.CurrentSession;
        float scale = UIPalette.AppliedUiScale;
        string[] roster = CharacterRoster.ToArray();
        string format = TranslationServer.Translate("movelist_title_for").ToString();
        var problems = new List<string>();
        try {
            foreach (float ui in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(ui);
                for (int i = 0; i < roster.Length; i++) {
                    string p1 = roster[i], p2 = roster[roster.Length - 1 - i];
                    SessionData session = saved;
                    session.SelectedCharacterID = p1;
                    session.OpponentCharacterID = p2;
                    manager.CurrentSession = session;
                    var host = new Node { Name = "MoveListRowHost" };
                    tree.Root.AddChild(host);
                    try {
                        var pause = new LocalFighterPause { Name = "LocalFighterPause" };
                        host.AddChild(pause);
                        pause.SetPaused(true);
                        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                        foreach ((string name, string id) in new[] { ("MoveListP1Button", p1), ("MoveListP2Button", p2) }) {
                            var row = pause.FindChild(name, true, false) as Button;
                            var decor = row?.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor");
                            if (row == null || decor == null) {
                                problems.Add($"{ui}: {name} or its decor is missing");
                                continue;
                            }
                            string expected = string.Format(format, TranslationServer.Translate($"character_{id}_card_name").ToString());
                            if (row.Text != expected) problems.Add($"{ui}: {name} for {id} reads '{row.Text}', not '{expected}'");
                            int role = row.GetThemeFontSize("font_size");
                            if (decor.DrawnFontSize != role || decor.LabelIsTrimmed) {
                                problems.Add($"{ui}: {name} for {id} draws '{decor.DrawnText}' at {decor.DrawnFontSize} px of {role}" +
                                    (decor.LabelIsTrimmed ? ", cut" : "") + $" (row {row.Size.X:0} wide)");
                            }
                        }
                        pause.SetPaused(false);
                    } finally {
                        tree.Paused = false;
                        host.GetParent()?.RemoveChild(host);
                        host.Free();
                    }
                }
            }
        } finally {
            tree.Paused = false;
            manager.CurrentSession = saved;
            UIPalette.ApplyUiScale(scale);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    private static void AddToTree(Node node) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
    }
}
