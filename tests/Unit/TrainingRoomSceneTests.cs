using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-TRAIN (9c). The real training-room scene in physics frames: a sandbox with a dummy driven through the input-source
/// seam, settings that reach the dummy, and a teardown that clears the source and restores the session.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TrainingRoomSceneTests {

    private static bool HasInputSource(int playerIndex) {
        FieldInfo field = typeof(InputManager).GetField("_overrides", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(InputManager.Instance) is IDictionary map && map.Contains(playerIndex);
    }

    private static TrainingRoom Open(SceneTree tree) {
        var room = (TrainingRoom)ResourceLoader.Load<PackedScene>(TrainingRoom.ScenePath).Instantiate();
        tree.Root.AddChild(room);
        return room;
    }

    private static void Close(TrainingRoom room) {
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        room.GetParent()?.RemoveChild(room);
        room.Free();
    }

    [TestCase]
    public async Task TheRoomBuildsADummySandboxAndItsExitClearsTheInputSourceAndRestoresTheSession() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        bool wasPaused = tree.Paused;
        SessionData session = savedSession;
        session.FighterOpponentType = FighterOpponentType.Cpu;
        session.FighterMatchOrigin = FighterMatchOrigin.Training;
        gameManager.CurrentSession = session;
        TrainingRoom room = null;
        try {
            room = Open(tree);
            for (int frame = 0; frame < 5; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertThat(room.Driver != null && room.Driver.IsInitialized).IsTrue();
            AssertThat(room.Driver.TryGetFighter(1, out FighterStateComponent dummy)).IsTrue();
            AssertThat(dummy.Stocks).IsGreater(0);
            AssertThat(HasInputSource(1)).IsTrue();
            AssertThat(room.DummySource != null).IsTrue();
            // The CPU controller would otherwise take player one; the room runs two local fighters.
            AssertThat(gameManager.CurrentSession.FighterOpponentType).IsEqual(FighterOpponentType.LocalHuman);
            AssertThat(room.Panel.Visible).IsFalse();
            AssertThat(tree.Paused).IsFalse();

            // The exit resolves through FighterFlowRoutes: the hub, in front of the console, and the origin ends.
            AssertThat(TrainingRoom.ApplyExitToSession()).IsEqual(FighterFlowRoutes.HubScenePath);
            AssertThat(gameManager.CurrentSession.ArriveAtHolodeckConsole).IsTrue();
            AssertThat(gameManager.CurrentSession.FighterMatchOrigin).IsEqual(FighterMatchOrigin.LocalVersus);

            Close(room);
            room = null;
            AssertThat(HasInputSource(1)).IsFalse();
            AssertThat(gameManager.CurrentSession.FighterOpponentType).IsEqual(FighterOpponentType.Cpu);
        } finally {
            Close(room);
            InputManager.Instance?.ClearInputSource(1);
            tree.Paused = wasPaused;
            gameManager.CurrentSession = savedSession;
        }
    }

    [TestCase]
    public async Task ADummySetToJumpAlwaysLeavesTheGroundAndTheSettingsPanelPausesAndResumesTheRoom() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        TrainingRoom room = null;
        try {
            room = Open(tree);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            // Settings reach the dummy through the panel's event, and the panel owns the pause while it is up.
            room.Panel.Open();
            AssertThat(tree.Paused).IsTrue();
            room.Panel.JumpSelect.Select((int)TrainingJumpMode.Always);
            room.Panel.JumpSelect.EmitSignal(OptionButton.SignalName.ItemSelected, (int)TrainingJumpMode.Always);
            AssertThat(room.Settings.Jump).IsEqual(TrainingJumpMode.Always);
            room.Panel.Close();
            AssertThat(tree.Paused).IsFalse();

            bool leftTheGround = false;
            // The pre-match countdown is 180 frames; the dummy acts once the match is live.
            for (int frame = 0; frame < 420 && !leftTheGround; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                if (room.Driver.TryGetFighter(1, out FighterStateComponent dummy) && dummy.IsGrounded == 0) leftTheGround = true;
            }
            AssertThat(leftTheGround)
                .OverrideFailureMessage("The dummy never left the ground with Jump set to Always")
                .IsTrue();

            // Reset rebuilds the sandbox and the readouts start over.
            FighterSimulationDriver before = room.Driver;
            room.StartSandbox();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(room.Driver != null && room.Driver != before).IsTrue();
            AssertThat(room.Readouts.TotalDamage).IsEqual(0);
        } finally {
            Close(room);
            InputManager.Instance?.ClearInputSource(1);
            tree.Paused = wasPaused;
            gameManager.CurrentSession = savedSession;
        }
    }

    private static LocalFighterPause FindPause(Node node) {
        if (node is LocalFighterPause pause) return pause;
        foreach (Node child in node.GetChildren()) {
            LocalFighterPause found = FindPause(child);
            if (found != null) return found;
        }
        return null;
    }

    [TestCase]
    public async Task WhileTheSettingsPanelIsUpAPauseOrEscapePressAndAFocusLossOpenNoSecondMenu() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        TrainingRoom room = null;
        try {
            room = Open(tree);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            LocalFighterPause pause = FindPause(room);
            AssertObject(pause).OverrideFailureMessage("the driver's pause menu is part of the room").IsNotNull();

            // A real pause press opens the panel (not the driver's menu).
            PressPause();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(room.Panel.Visible).IsTrue();
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused).IsTrue();

            // A focus loss with the panel up does not open the driver's menu either.
            AssertThat(pause.HandleFocusLost()).OverrideFailureMessage("focus loss opened a second pause owner").IsFalse();
            AssertThat(pause.IsPaused).IsFalse();

            // A second press closes the panel and hands the pause back; no menu opens and nothing stays paused.
            PressPause();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(room.Panel.Visible).IsFalse();
            AssertThat(pause.IsPaused).OverrideFailureMessage("a second pause menu opened over the panel").IsFalse();
            AssertThat(tree.Paused).IsFalse();
            AssertThat(PauseMenuBase.ExternalPauseOwnerActive).IsFalse();

            // With the panel closed a focus loss opens the driver's own menu again.
            AssertThat(pause.HandleFocusLost()).IsTrue();
        } finally {
            Close(room);
            PauseMenuBase.ExternalPauseOwnerActive = false;
            tree.Paused = wasPaused;
            gameManager.CurrentSession = savedSession;
        }
    }

    [TestCase]
    public async Task AControllerDisconnectResolvedUnderTheSettingsPanelLeavesTheTreePausedAndTheFlagDoesNotOutliveTheRoom() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        TrainingRoom room = null;
        try {
            room = Open(tree);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            LocalFighterPause pause = FindPause(room);
            AssertObject(pause).IsNotNull();

            // Panel first, then the controller drops and comes back: the panel is still up, so the sim must stay paused.
            room.Panel.Open();
            AssertThat(tree.Paused).IsTrue();
            pause.ForceDisconnectPause(0);
            pause.ResolveDisconnect(0);
            AssertThat(room.Panel.Visible).IsTrue();
            AssertThat(tree.Paused).OverrideFailureMessage("the disconnect resolve unpaused the sim under the open settings panel").IsTrue();
            room.Panel.Close();
            AssertThat(tree.Paused).OverrideFailureMessage("the panel's close did not hand the pause back").IsFalse();

            // Disconnect modal first: the panel cannot open over it (the pause press is refused while a menu is open).
            pause.ForceDisconnectPause(0);
            PressPause();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(room.Panel.Visible).OverrideFailureMessage("the settings panel opened over the disconnect modal").IsFalse();
            pause.ResolveDisconnect(0);
            AssertThat(tree.Paused).IsFalse();

            // The flag is static: leave with the panel open and it must not stick into the next match.
            room.Panel.Open();
            AssertThat(PauseMenuBase.ExternalPauseOwnerActive).IsTrue();
            Close(room);
            room = null;
            AssertThat(PauseMenuBase.ExternalPauseOwnerActive).OverrideFailureMessage("the static owner flag outlived the room").IsFalse();
        } finally {
            Close(room);
            PauseMenuBase.ExternalPauseOwnerActive = false;
            tree.Paused = wasPaused;
            gameManager.CurrentSession = savedSession;
        }
    }

    [TestCase]
    public async Task ClosingThePanelUnderAnUnresolvedDisconnectKeepsTheTreePausedUntilItResolvesAndAPlainCloseStillResumes() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gameManager = GameManager.Instance;
        SessionData savedSession = gameManager.CurrentSession;
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        TrainingRoom room = null;
        try {
            room = Open(tree);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            LocalFighterPause pause = FindPause(room);
            AssertObject(pause).IsNotNull();

            room.Panel.Open();
            pause.ForceDisconnectPause(0);
            room.Panel.Close();
            AssertThat(tree.Paused).OverrideFailureMessage("closing the panel unpaused under the disconnect modal").IsTrue();
            pause.ResolveDisconnect(0);
            AssertThat(tree.Paused).OverrideFailureMessage("the pause stuck after the disconnect resolved").IsFalse();
            AssertThat(PauseMenuBase.ExternalPauseOwnerActive).IsFalse();

            room.Panel.Open();
            AssertThat(tree.Paused).IsTrue();
            room.Panel.Close();
            AssertThat(tree.Paused).OverrideFailureMessage("a plain panel open and close did not resume play").IsFalse();
        } finally {
            Close(room);
            PauseMenuBase.ExternalPauseOwnerActive = false;
            tree.Paused = wasPaused;
            gameManager.CurrentSession = savedSession;
        }
    }

    [TestCase]
    public async Task ForfeitUnderTheOpenPanelReleasesThePauseSoTheSceneLoadCanRun() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gameManager = GameManager.Instance;
        SessionData savedSession = gameManager.CurrentSession;
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        FieldInfo loading = typeof(GameManager).GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic);
        bool wasLoading = (bool)loading.GetValue(gameManager);
        TrainingRoom room = null;
        try {
            room = Open(tree);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            LocalFighterPause pause = FindPause(room);
            AssertObject(pause).IsNotNull();
            room.Panel.Open();
            pause.ForceDisconnectPause(0);
            // Keep LoadScene a no-op so the test never leaves the scene.
            loading.SetValue(gameManager, true);
            var forfeit = pause.FindChild("ForfeitButton", true, false) as Button;
            AssertObject(forfeit).IsNotNull();
            forfeit.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(tree.Paused).OverrideFailureMessage("Forfeit left the tree paused for the scene load").IsFalse();
            AssertThat(PauseMenuBase.ExternalPauseOwnerActive).IsFalse();
        } finally {
            loading.SetValue(gameManager, wasLoading);
            Close(room);
            PauseMenuBase.ExternalPauseOwnerActive = false;
            tree.Paused = wasPaused;
            gameManager.CurrentSession = savedSession;
        }
    }

    private static void PressPause() {
        // Viewport.PushInput walks the real dispatch chain: _Input, GUI, then _UnhandledInput.
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.PushInput(new InputEventAction { Action = InputManager.Actions.Pause, Pressed = true });
    }
}
