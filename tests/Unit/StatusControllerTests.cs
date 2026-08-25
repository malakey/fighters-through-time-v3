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

    [TestCase]
    public void PlayerDamageAndControlStatusesCoexistInSeparateSlots() {
        (PlayerController player, StatusController status) = CreateSubject();

        // V7 two-slot rule: a DoT and a slow occupy different slots, so a
        // trapper's Venom survives landing a follow-up control status.
        status.ApplyStatus(StatusType.Venom, 3f);
        status.ApplyStatus(StatusType.TimeDilation, 2f);

        AssertThat(status.HasStatus(StatusType.Venom)).IsTrue();
        AssertThat(status.HasStatus(StatusType.TimeDilation)).IsTrue();
        AssertThat(player.StatusMovementMultiplier).IsEqual(0.5f);
        status._PhysicsProcess(1.05);
        AssertThat(player.CurrentHP).IsEqual(98);

        // The control slot expiring restores movement without touching the DoT.
        status._PhysicsProcess(1.0);
        AssertThat(player.StatusMovementMultiplier).IsEqual(1f);
        AssertThat(status.HasStatus(StatusType.Venom)).IsTrue();
        player.Free();
    }

    [TestCase]
    public void EnemyMinimalStatusFollowsTwoSlotSemantics() {
        var enemy = new FTT.Enemies.EnemyController { CurrentHP = 100 };

        enemy.ApplyStatusEffect(StatusType.TimeDilation, 3f, 1f);
        AssertThat(enemy.ActiveStatusType).IsEqual(StatusType.TimeDilation);
        AssertThat(enemy.StatusMoveMultiplier).IsEqual(0.5f);

        // V7 two-slot rule: the burn occupies the damage slot while the slow
        // keeps its control slot — both apply at once.
        enemy.ApplyStatusEffect(StatusType.RadiantBurn, 3f, 1f);
        AssertThat(enemy.HasStatusEffect(StatusType.TimeDilation)).IsTrue();
        AssertThat(enemy.HasStatusEffect(StatusType.RadiantBurn)).IsTrue();
        AssertThat(enemy.StatusMoveMultiplier).IsEqual(0.5f);
        AssertThat(enemy.StatusDamageTakenMultiplier).IsEqual(1.25f);

        // RadiantBurn amplifies incoming damage (Einstein's Critical Mass burst).
        enemy.TakeDamage(20);
        AssertThat(enemy.CurrentHP).IsEqual(75);
        enemy.Free();
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
