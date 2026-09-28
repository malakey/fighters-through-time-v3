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
    // V7.6 (Package 11 A1): within a slot the rule is STRONGER-WINS, not
    // newest-wins. A different type always replaces; the same type replaces only
    // when newIntensity x newDuration >= currentIntensity x currentRemaining,
    // bringing its own duration; a weaker same-type application does nothing at
    // all; and the other slot is never touched.
    [TestCase]
    public void StrongerWinsGovernsReplacementWithinASlotAndNeverTouchesTheOther() {
        (PlayerController player, StatusController status) = CreateSubject();
        try {
            // A different type in the same slot always replaces, weaker or not.
            status.ApplyStatus(StatusType.TimeDilation, 2f);
            AssertThat(player.StatusMovementMultiplier).IsEqual(0.5f);
            status.ApplyStatus(StatusType.Root, 1f);
            AssertThat(status.ControlStatusType)
                .OverrideFailureMessage("A different type in the slot always replaces.")
                .IsEqual(StatusType.Root);
            AssertThat(player.StatusMovementMultiplier).IsEqual(1f);
            AssertThat(player.IsMovementRooted).IsTrue();

            // Occupy the damage slot so the control-slot traffic below can be
            // proved not to touch it.
            status.ApplyStatus(StatusType.Venom, 4f, 1f);
            AssertThat(status.DamageStatusType).IsEqual(StatusType.Venom);

            // A WEAKER same-type application is a complete no-op: no replace, no
            // refresh, and the occupant keeps its own remaining duration.
            status.ApplyStatus(StatusType.Root, 0.25f);
            status._PhysicsProcess(0.5);
            AssertThat(player.IsMovementRooted)
                .OverrideFailureMessage("A weaker same-type application must not shorten the occupant.")
                .IsTrue();

            // EQUAL strength replaces (the rule is >=): the same type at the same
            // intensity and the same remaining duration brings its own duration.
            AssertThat(StatusRouting.ShouldReplace(StatusType.Root, 1f, 0.5f, StatusType.Root, 1f, 0.5f))
                .OverrideFailureMessage("Equal strength must replace.")
                .IsTrue();
            AssertThat(StatusRouting.ShouldReplace(StatusType.Root, 1f, 2f, StatusType.Root, 1f, 1f))
                .OverrideFailureMessage("A weaker same-type application must not replace.")
                .IsFalse();
            // Intensity participates: half the duration at triple the potency wins.
            AssertThat(StatusRouting.ShouldReplace(StatusType.Venom, 1f, 2f, StatusType.Venom, 3f, 1f))
                .OverrideFailureMessage("Intensity x duration is the strength product.")
                .IsTrue();

            // A stronger same-type application replaces and brings its duration.
            status.ApplyStatus(StatusType.Root, 5f, 1f);
            status._PhysicsProcess(1.0);
            AssertThat(player.IsMovementRooted).IsTrue();

            // Through all of that the damage slot was never touched.
            AssertThat(status.DamageStatusType)
                .OverrideFailureMessage("Control-slot traffic must never touch the damage slot.")
                .IsEqual(StatusType.Venom);
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void StatusRoutingIsTheOneTableAndCoversEverySlotAssignment() {
        AssertThat(StatusRouting.SlotOf(StatusType.Venom)).IsEqual(StatusSlot.Damage);
        AssertThat(StatusRouting.SlotOf(StatusType.RadiantBurn)).IsEqual(StatusSlot.Damage);
        AssertThat(StatusRouting.SlotOf(StatusType.TimeDilation)).IsEqual(StatusSlot.Control);
        AssertThat(StatusRouting.SlotOf(StatusType.StaticCharge)).IsEqual(StatusSlot.Control);
        AssertThat(StatusRouting.SlotOf(StatusType.Root)).IsEqual(StatusSlot.Control);
        AssertThat(StatusRouting.SlotOf(StatusType.Suppression)).IsEqual(StatusSlot.Control);
        // The legacy forwarder must agree with the table at every value, because
        // the simulation hot path and the HUD both still call it.
        foreach (StatusType type in System.Enum.GetValues<StatusType>()) {
            AssertThat(StatusController.IsDamageStatus(type))
                .OverrideFailureMessage($"IsDamageStatus must forward to the table for {type}.")
                .IsEqual(StatusRouting.SlotOf(type) == StatusSlot.Damage);
        }
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

    /// <summary>
    /// Package 12 W10: a slot clear publishes exactly one slot-scoped Cleared
    /// event naming the slot and the type that left, and never re-announces the
    /// surviving slot through a second Applied event (the retired hack).
    /// </summary>
    [TestCase]
    public void AClearPublishesOnePerSlotEventAndNeverReannouncesTheSurvivor() {
        (PlayerController player, StatusController status) = CreateSubject();
        EventBus bus = EventBus.Instance;
        AssertObject(bus).IsNotNull();
        var applied = new System.Collections.Generic.List<StatusEffectPayload>();
        var cleared = new System.Collections.Generic.List<StatusEffectPayload>();
        void OnApplied(StatusEffectPayload payload) { if (payload.TargetIndex == 1) applied.Add(payload); }
        void OnCleared(StatusEffectPayload payload) { if (payload.TargetIndex == 1) cleared.Add(payload); }
        bus.OnStatusEffectApplied += OnApplied;
        bus.OnStatusEffectCleared += OnCleared;
        try {
            player.PlayerIndex = 1;
            status.ApplyStatus(StatusType.Venom, 5f);
            status.ApplyStatus(StatusType.Root, 1f);
            AssertThat(applied.Count).IsEqual(2);
            AssertThat(applied[0].SlotScoped).IsTrue();
            AssertThat(applied[0].Slot).IsEqual(StatusSlot.Damage);
            AssertThat(applied[1].Slot).IsEqual(StatusSlot.Control);

            // Root expires; Venom survives.
            status._PhysicsProcess(1.5);
            AssertThat(status.DamageStatusType).IsEqual(StatusType.Venom);
            AssertThat(applied.Count)
                .OverrideFailureMessage("The survivor must not be re-announced through Applied.")
                .IsEqual(2);
            AssertThat(cleared.Count).IsEqual(1);
            AssertThat(cleared[0].SlotScoped).IsTrue();
            AssertThat(cleared[0].Slot).IsEqual(StatusSlot.Control);
            AssertThat(cleared[0].Type).IsEqual(StatusType.Root);
            AssertThat(cleared[0].ClearsAllSlots).IsFalse();

            // The last slot clears the same way: its own slot, its own type.
            status.ClearStatus();
            AssertThat(cleared.Count).IsEqual(2);
            AssertThat(cleared[1].Slot).IsEqual(StatusSlot.Damage);
            AssertThat(cleared[1].Type).IsEqual(StatusType.Venom);

            // A legacy un-scoped None clear still means "every slot".
            AssertThat(new StatusEffectPayload { Type = StatusType.None }.ClearsAllSlots).IsTrue();
        } finally {
            bus.OnStatusEffectApplied -= OnApplied;
            bus.OnStatusEffectCleared -= OnCleared;
            player.Free();
        }
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
