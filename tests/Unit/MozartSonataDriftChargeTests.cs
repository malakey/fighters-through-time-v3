using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — Mozart's Extra Note (design §5, Low-item decision
/// 2026-09-26): "Sonata Drift gains a second charge: two uses before recharge…
/// Charges recharge one at a time on the 5 s cooldown; landing on his own
/// platform refunds half of the recharging charge's remaining time." Story only.
/// The owner's <c>MovementAbilityCooldownTimer</c> stays the one recharge clock;
/// the ability banks the restored charge that is not yet spent.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MozartSonataDriftChargeTests {
    private const double Tick = 1.0 / 60.0;

    [TestCase]
    public void ExtraNoteGivesTwoChargesThatRechargeOneAtATime() {
        PlayerController player = CharacterFactory.CreateCharacter("mozart");
        Node host = Attach(player);
        try {
            var drift = player.GetNode<MozartSonataDrift>("MovementAbility");
            player.StoryAbilityPerks.Add(MozartSonataDrift.ExtraNotePerkKey);
            drift._PhysicsProcess(Tick);
            AssertThat(drift.MaxCharges).IsEqual(2);
            AssertThat(drift.AvailableCharges).IsEqual(2);

            // First cast from a full stock arms the recharge through the
            // ordinary path.
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.MovementAbility);
            AssertThat(drift.IsExecuting).IsTrue();
            float armed = player.MovementAbilityCooldownTimer;
            AssertFloat(armed).IsGreater(4.9f);
            AssertThat(drift.AvailableCharges).IsEqual(1);
            FinishCast(player, drift);

            // Second cast spends the banked charge and does NOT restart the
            // recharge that is already running.
            player.TransitionTo(CharacterState.Airborne);
            float running = player.MovementAbilityCooldownTimer;
            Press(player, GameplayButtons.MovementAbility);
            AssertThat(drift.IsExecuting).IsTrue();
            AssertFloat(player.MovementAbilityCooldownTimer).IsLess(running + 0.001f);
            AssertThat(drift.AvailableCharges).IsEqual(0);
            FinishCast(player, drift);

            // A third press with both charges spent is refused.
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.MovementAbility);
            AssertThat(drift.IsExecuting).IsFalse();

            // The first recharge completes: one charge back, and the second
            // charge starts recharging at once on the full cooldown.
            player.MovementAbilityCooldownTimer = 0f;
            drift._PhysicsProcess(Tick);
            AssertThat(drift.AvailableCharges).IsEqual(1);
            AssertFloat(player.MovementAbilityCooldownTimer).IsGreater(4.9f);

            // The second completes: full stock, and the clock stops.
            player.MovementAbilityCooldownTimer = 0f;
            drift._PhysicsProcess(Tick);
            AssertThat(drift.AvailableCharges).IsEqual(2);
            AssertFloat(player.MovementAbilityCooldownTimer).IsEqual(0f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void WithoutExtraNoteThereIsOneChargeAndNoBypass() {
        PlayerController player = CharacterFactory.CreateCharacter("mozart");
        Node host = Attach(player);
        try {
            var drift = player.GetNode<MozartSonataDrift>("MovementAbility");
            drift._PhysicsProcess(Tick);
            AssertThat(drift.MaxCharges).IsEqual(1);
            player.MovementAbilityCooldownTimer = 3f;
            AssertThat(drift.TryConsumeCooldownBypass()).IsFalse();
        } finally {
            Teardown(host, player);
        }
    }

    /// <summary>
    /// Package 13 W7b (M04), rewritten in place from the retired "once per
    /// platform" pin: the staff refund is at most once per AIRTIME — a second
    /// staff in the same airtime refunds nothing (so he gets at most two drifts
    /// before touching down), and real ground or a ledge grab re-arms it. Extra
    /// Note obeys the same limit.
    /// </summary>
    [TestCase]
    public void AStaffLandingRefundsHalfTheCooldownOncePerAirtime() {
        AssertThat(MozartSonataDrift.StaffLandingRefundShare).IsEqual(0.5f);
        PlayerController player = CharacterFactory.CreateCharacter("mozart");
        Node host = Attach(player);
        try {
            var drift = player.GetNode<MozartSonataDrift>("MovementAbility");
            player.StoryAbilityPerks.Add(MozartSonataDrift.ExtraNotePerkKey);
            player.MovementAbilityCooldownTimer = 4f;
            AssertThat(drift.ResolveStaffRefund(landedOnOwnStaff: true, onRealGroundOrLedge: false)).IsTrue();
            AssertFloat(player.MovementAbilityCooldownTimer).IsEqualApprox(2f, 0.0001f);

            // A second staff landing in the same airtime (Extra Note's second
            // platform included) refunds nothing.
            AssertThat(drift.ResolveStaffRefund(landedOnOwnStaff: true, onRealGroundOrLedge: false))
                .OverrideFailureMessage("A second staff in one airtime must not keep halving the recharge.")
                .IsFalse();
            AssertFloat(player.MovementAbilityCooldownTimer).IsEqualApprox(2f, 0.0001f);

            // Real ground (or a ledge) re-arms it.
            drift.ResolveStaffRefund(landedOnOwnStaff: false, onRealGroundOrLedge: true);
            AssertThat(drift.RefundUsedThisAirtime).IsFalse();
            AssertThat(drift.ResolveStaffRefund(landedOnOwnStaff: true, onRealGroundOrLedge: false)).IsTrue();
            AssertFloat(player.MovementAbilityCooldownTimer).IsEqualApprox(1f, 0.0001f);
        } finally {
            Teardown(host, player);
        }
    }

    private static void FinishCast(PlayerController player, MozartSonataDrift drift) {
        int frames = drift.Data.StartupFrames + drift.Data.ActiveFrames + drift.Data.RecoveryFrames + 2;
        for (int frame = 0; frame < frames && drift.IsExecuting; frame++) drift._PhysicsProcess(Tick);
        AssertThat(drift.IsExecuting).IsFalse();
    }

    private static void Press(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Tick);
    }

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "MozartChargeHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        return host;
    }

    private static void Teardown(Node host, PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        // Hand any deployed staff back to its pool rather than freeing a pooled
        // node with the host (the pool would keep a dead reference).
        foreach (Node2D node in new System.Collections.Generic.List<Node2D>(player.ActivePersistentObjects)) {
            if (node is SonataPlatformNode platform && GodotObject.IsInstanceValid(platform)) platform.ReturnToPool();
        }
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
