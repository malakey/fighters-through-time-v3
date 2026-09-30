using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W1 — M17. Act III has no extraction: the Wardens cannot reach
/// past the Void and "no rift comes", so the Act III collapse beat carries no
/// "pulling you out" transmission. Outside Act III a death collapse plays the
/// ordinary line and a timer collapse the era-lost variant, whose English value
/// is synced to the design master.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CollapseTransmissionTests {

    [TestCase]
    public void ActThreeCarriesNoExtractionLineAndTheTimerCarriesTheEraLostVariant() {
        AssertThat(ChronalRewindManager.CollapseTransmissionKeyFor(actIII: true, TimelineCollapseCause.Death))
            .IsEqual("");
        AssertThat(ChronalRewindManager.CollapseTransmissionKeyFor(actIII: true, TimelineCollapseCause.Timer))
            .IsEqual("");
        AssertThat(ChronalRewindManager.CollapseTransmissionKeyFor(actIII: false, TimelineCollapseCause.Death))
            .IsEqual(ChronalRewindManager.CollapseTransmissionLineKey);
        AssertThat(ChronalRewindManager.CollapseTransmissionKeyFor(actIII: false, TimelineCollapseCause.Timer))
            .IsEqual(ChronalRewindManager.EraLostCollapseLineKey);
        // Every Act III level resolves through the one Act III rule.
        AssertThat(StoryManager.IsActIIILevel(CampaignLevel.ChronalVoid)).IsTrue();
        AssertThat(StoryManager.IsActIIILevel(CampaignLevel.NeoEarth)).IsTrue();
        AssertThat(StoryManager.IsActIIILevel(CampaignLevel.Alexandria)).IsTrue();
        AssertThat(StoryManager.IsActIIILevel((CampaignLevel)16)).IsFalse();
    }

    [TestCase]
    public void TheOverlayHidesTheTransmissionOnTheActThreeBeat() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new RewindPresentationOverlay { Name = "M17Overlay" };
        tree.Root.AddChild(overlay);
        try {
            EventBus.Instance.RaiseRewindPresentation(ChronalRewindManager.CreateCollapsePayload(
                Vector2.Zero, skippable: false,
                ChronalRewindManager.CollapseTransmissionKeyFor(actIII: true, TimelineCollapseCause.Death)));
            AssertThat(overlay.CollapseTreatment.Visible).IsTrue();
            AssertThat(overlay.TransmissionLine.Visible)
                .OverrideFailureMessage("M17: the Act III collapse beat showed an extraction line.")
                .IsFalse();

            EventBus.Instance.RaiseRewindPresentation(ChronalRewindManager.CreateCollapsePayload(
                Vector2.Zero, skippable: false,
                ChronalRewindManager.CollapseTransmissionKeyFor(actIII: false, TimelineCollapseCause.Timer)));
            AssertThat(overlay.TransmissionLine.Visible).IsTrue();
            AssertThat(overlay.TransmissionLine.Text)
                .IsEqual(TranslationServer.Translate(ChronalRewindManager.EraLostCollapseLineKey).ToString());
        } finally {
            overlay.Free();
        }
    }

    [TestCase]
    public void TheEraLostValueMatchesTheDesignMaster() {
        string csv = FileAccess.GetFileAsString("res://localization/en.csv");
        AssertThat(csv.Contains(
                "dialogue_collapse_sarah_era_lost,\"Sarah: The era's slipping away — pulling you out!\""))
            .OverrideFailureMessage("M17: the era-lost value must be the master's wording.")
            .IsTrue();
        AssertThat(csv.Contains(
                "collapse_transmission_line,\"Sarah: We've lost the thread — pulling you out!\""))
            .IsTrue();
    }
}
