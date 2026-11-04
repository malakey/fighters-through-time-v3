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
}
