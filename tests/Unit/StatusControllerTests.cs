using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class StatusControllerTests {
    [TestCase]
    public void NewStatusCompletelyReplacesPreviousStatus() {
        (PlayerController player, StatusController status) = CreateSubject();
        status.ApplyStatus(StatusType.TimeDilation, 2f);
        AssertThat(player.StatusMovementMultiplier).IsEqual(0.5f);

        status.ApplyStatus(StatusType.Root, 1f);

        AssertThat(status.ActiveType).IsEqual(StatusType.Root);
        AssertThat(player.StatusMovementMultiplier).IsEqual(1f);
        AssertThat(player.IsMovementRooted).IsTrue();
        player.Free();
    }

    [TestCase]
    public void StatusExpiresAndRestoresModifiers() {
        (PlayerController player, StatusController status) = CreateSubject();
        status.ApplyStatus(StatusType.Root, 0.1f);

        status._PhysicsProcess(0.11);

        AssertThat(status.ActiveType).IsEqual(StatusType.None);
        AssertThat(player.IsMovementRooted).IsFalse();
        player.Free();
    }

    [TestCase]
    public void VenomTicksAndSnapshotRestoresItsAccumulator() {
        (PlayerController player, StatusController status) = CreateSubject();
        status.ApplyStatus(StatusType.Venom, 3f);
        status._PhysicsProcess(0.4);
        StatusSnapshot snapshot = status.CaptureState();

        status._PhysicsProcess(0.7);
        AssertThat(player.CurrentHP).IsEqual(98);

        player.CurrentHP = 100;
        status.RestoreState(snapshot);
        status._PhysicsProcess(0.59);
        AssertThat(player.CurrentHP).IsEqual(100);
        status._PhysicsProcess(0.02);
        AssertThat(player.CurrentHP).IsEqual(98);
        player.Free();
    }

    private static (PlayerController player, StatusController status) CreateSubject() {
        var player = new PlayerController { CurrentHP = 100, PlayerIndex = 0 };
        var status = new StatusController { Name = "StatusController" };
        player.AddChild(status);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(player);
        return (player, status);
    }
}
