using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W10 / M15. The Act III Beacon Anchors element against
/// HUD_CONTRACT: a 24 × 24 Beacon icon immediately right of the rewind
/// hourglass, up to three 8 × 8 gold <b>diamond</b> pips, a crack-and-fade on an
/// Anchor Snap, a hollow last-stand icon at zero, and the localized accessible
/// label "Anchor charges: N".
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudBeaconAnchorTests {
    private const string TemporalRow = "SafeArea/TopLeft_Panel/Vitals/TemporalRow";

    [TestCase]
    public void TheBeaconSitsImmediatelyRightOfTheHourglassAheadOfTheFreezeIndicator() {
        Node host = CreateHost("BeaconPlacementHost");
        try {
            StoryHUD hud = AddHud(host);
            Node row = hud.GetNode(TemporalRow);
            Node rewind = hud.GetNode($"{TemporalRow}/RewindCounter");
            Node beacon = hud.GetNode($"{TemporalRow}/BeaconAnchors");
            Node freeze = hud.GetNode($"{TemporalRow}/TimeFreezeIndicator");
            AssertThat(beacon.GetIndex()).IsEqual(rewind.GetIndex() + 1);
            // The Time Freeze indicator stays its own sibling, never a child of the
            // rewind counter (F24), and follows the Beacon.
            AssertObject(freeze.GetParent()).IsSame(row);
            AssertThat(freeze.GetIndex()).IsGreater(beacon.GetIndex());

            var icon = hud.GetNode<Control>($"{TemporalRow}/BeaconAnchors/BeaconIcon");
            AssertObject(icon as BeaconAnchorIcon).IsNotNull();
            AssertThat(icon.CustomMinimumSize).IsEqual(new Vector2(24f, 24f));

            EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 3, Max = 3 });
            AssertThat(hud.AnchorPipCount).IsEqual(3);
            foreach (AnchorPip pip in hud.AnchorPips) {
                AssertThat(pip.CustomMinimumSize).IsEqual(new Vector2(AnchorPip.PipSize, AnchorPip.PipSize));
                AssertThat(AnchorPip.PipSize).IsEqual(8);
            }
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ASnapCracksAndFadesOnePipWhileABindOrLoadDrawsWithoutTheBeat() {
        Node host = CreateHost("BeaconSnapHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;

            // Bind / load at 1 of 2: the spent pip is drawn spent, not cracked.
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 1, Max = 2 });
            AssertThat(hud.LitAnchorPips).IsEqual(1);
            AssertThat(hud.AnchorPips[1].State).IsEqual(AnchorPip.PipState.Spent);

            // Full Restart Level refills without animation.
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 2 });
            AssertThat(hud.LitAnchorPips).IsEqual(2);

            // An Anchor Snap: the count falls at unchanged capacity.
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 1, Max = 2 });
            AssertThat(hud.LitAnchorPips).IsEqual(1);
            AnchorPip spent = hud.AnchorPips[1];
            AssertThat(spent.State).IsEqual(AnchorPip.PipState.Cracking);
            AssertThat(hud.AnchorPips[0].State).IsEqual(AnchorPip.PipState.Filled);

            spent.Advance(AnchorPip.CrackSeconds * 0.5f);
            AssertThat(spent.State).IsEqual(AnchorPip.PipState.Cracking);
            AssertFloat(spent.CrackProgress).IsEqualApprox(0.5f, 0.001f);
            spent.Advance(AnchorPip.CrackSeconds);
            AssertThat(spent.State).IsEqual(AnchorPip.PipState.Spent);
            AssertThat(hud.BeaconLastStand).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ZeroChargesShowsTheHollowLastStandAndTheLabelReadsTheCount() {
        Node host = CreateHost("BeaconLastStandHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            var anchors = hud.GetNode<Control>($"{TemporalRow}/BeaconAnchors");

            // Levels 0-12 publish Max 0: hidden.
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 0, Max = 0 });
            AssertThat(anchors.Visible).IsFalse();

            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 1, Max = 1 });
            AssertThat(anchors.Visible).IsTrue();
            AssertThat(hud.BeaconLastStand).IsFalse();
            string template = TranslationServer.Translate(StoryHUD.AnchorChargesAccessibleKey);
            AssertThat(hud.AnchorChargesAccessibleText).IsEqual(string.Format(template, 1));

            // Spending the last anchor: hollow icon, steady last-stand tint.
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 0, Max = 1 });
            AssertThat(hud.BeaconLastStand).IsTrue();
            var icon = hud.GetNode<BeaconAnchorIcon>($"{TemporalRow}/BeaconAnchors/BeaconIcon");
            AssertThat(icon.CurrentColor).IsEqual(BeaconAnchorIcon.LastStandTint);
            AssertThat(hud.AnchorChargesAccessibleText).IsEqual(string.Format(template, 0));
            AssertThat(hud.AnchorChargesAccessibleText.Contains('0')).IsTrue();
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
