using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / HUD_CONTRACT "Chronal Dust". The persistent top-right counter
/// and its separately fading <c>+N</c> pickup float.
///
/// <para>Two rules and one trap. The rules: the numeric balance is persistent —
/// visible at zero, visible through the boss fight after the Integrity clock hides
/// — and only the pooled <c>+N</c> notification fades, after one second. The trap:
/// pickup notifications are cosmetic and <b>cannot drive the balance</b>. The
/// balance is read from the committed wallet, so a mirrored event or a rollback
/// re-raising a collection cannot add dust twice.</para>
///
/// <para>The counter was previously in the left vitals column using the hub's
/// <c>hub_carried_dust</c> key, which is the wallet's <em>deposited</em> family;
/// the level HUD shows undeposited dust, and the contract forbids ever presenting
/// a combined balance that implies the carried amount is spendable.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DustCounterPersistenceTests {

    [TestCase]
    public void TheBalanceIsVisibleAtZeroAndNeverFades() {
        Node host = CreateHost("DustCounterZeroHost");
        try {
            StoryHUD hud = AddHud(host);
            var container = hud.GetNode<Control>("SafeArea/TopRight_Panel/CurrencyContainer");
            var text = hud.GetNode<Label>("SafeArea/TopRight_Panel/CurrencyContainer/ChronalDustText");
            var icon = hud.GetNode<Label>("SafeArea/TopRight_Panel/CurrencyContainer/ChronalDustIcon");

            AssertThat(container.Visible).IsTrue();
            AssertThat(icon.Visible).IsTrue();

            // Ten seconds of inactivity — far past the float's one-second life.
            for (int frame = 0; frame < 600; frame++) hud._Process(1.0 / 60.0);
            AssertThat(text.Visible)
                .OverrideFailureMessage("The numeric balance must never fade after inactivity.")
                .IsTrue();
            AssertFloat(text.Modulate.A).IsEqual(1f);
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// HUD_CONTRACT: the counter "remains visible… during the boss fight after the
    /// Integrity clock hides". The two widgets are siblings in the same panel with
    /// independent visibility, so one leaving cannot take the other with it.
    /// </summary>
    [TestCase]
    public void TheCounterOutlivesTheIntegrityClock() {
        Node host = CreateHost("DustCounterBossHost");
        try {
            StoryHUD hud = AddHud(host);
            var container = hud.GetNode<Control>("SafeArea/TopRight_Panel/CurrencyContainer");
            var clock = hud.GetNode<Control>("SafeArea/TopRight_Panel/IntegrityClock");

            clock.Visible = false;
            hud._Process(1.0 / 60.0);
            AssertThat(container.Visible)
                .OverrideFailureMessage("Hiding the Integrity clock must not hide the dust counter.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// Only the <c>+N</c> float fades — and it is cosmetic. Raising the same
    /// collection twice (a mirrored event, a rollback re-raise) shows the float
    /// again and changes the balance by nothing, because the balance is read from
    /// the committed wallet rather than accumulated from the notifications.
    /// </summary>
    [TestCase]
    public void OnlyThePickupFloatFadesAndItCannotDriveTheBalance() {
        Node host = CreateHost("DustCounterFloatHost");
        try {
            StoryHUD hud = AddHud(host);
            var text = hud.GetNode<Label>("SafeArea/TopRight_Panel/CurrencyContainer/ChronalDustText");
            var pickupFloat = hud.GetNode<Label>("SafeArea/TopRight_Panel/CurrencyContainer/DustPickup_Float");
            AssertThat(pickupFloat.Visible).IsFalse();

            string balanceBefore = text.Text;
            EventBus.Instance.RaiseDustAwardCollected(new DustAwardCollectedPayload {
                Amount = 25, Source = DustAwardSource.Boss
            });
            AssertThat(pickupFloat.Visible).IsTrue();
            AssertThat(pickupFloat.Text.Contains("25")).IsTrue();

            // The same award mirrored a second time: the float replays (it is
            // presentation), the balance does not move.
            EventBus.Instance.RaiseDustAwardCollected(new DustAwardCollectedPayload {
                Amount = 25, Source = DustAwardSource.Boss
            });
            hud._Process(1.0 / 60.0);
            AssertThat(text.Text)
                .OverrideFailureMessage("A mirrored pickup event must not add dust to the readout.")
                .IsEqual(balanceBefore);

            // It rises and fades after one second, and then it is gone.
            for (int frame = 0; frame < 70; frame++) hud._Process(1.0 / 60.0);
            AssertThat(pickupFloat.Visible).IsFalse();
            AssertThat(text.Visible).IsTrue();
        } finally {
            host.Free();
        }
    }

    private static StoryHUD AddHud(Node host) {
        StoryHUD hud = StoryHUD.CreateDefault();
        host.AddChild(hud);
        return hud;
    }

    private static Node CreateHost(string name) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = name };
        tree.Root.AddChild(host);
        return host;
    }
}
