using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W3 (S11/S15): the hub's wordless triage readout. The next era
/// flashes red at the top (always the portal's destination), unsealed eras burn
/// cold in triage order, the era sealed by the level just completed flares gold
/// and fades, older sealed eras are gone, and from Act III on the readout is dark.
///
/// <para>Requires the runtime only because <see cref="HubTriageReadout"/> is a
/// <c>Node2D</c> and reads <c>StoryManager</c>'s static route; the rules
/// themselves are static and deterministic. The built node is freed in
/// <c>finally</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HubTriageReadoutTests {

    [TestCase]
    public void TheNextEraFlashesRedAtTheTopAndTheJustSealedEraFlaresOut() {
        // After Level 3 (next: Paris): Florence and Orléans sealed earlier, Chicago
        // sealed just now.
        var sealedEras = new HashSet<CampaignLevel> {
            CampaignLevel.Florence, CampaignLevel.Orleans, CampaignLevel.Chicago
        };
        var ranked = HubTriageReadout.Rank(CampaignLevel.Paris, false, sealedEras.Contains, CampaignLevel.Chicago);

        AssertThat(ranked[0].Era).IsEqual(CampaignLevel.Paris);
        AssertThat(ranked[0].State).IsEqual(TriageShardState.NextFlashing);
        AssertThat(ranked[^1].Era).IsEqual(CampaignLevel.Chicago);
        AssertThat(ranked[^1].State).IsEqual(TriageShardState.SealedFlare);
        foreach ((CampaignLevel era, TriageShardState state) in ranked) {
            AssertThat(era == CampaignLevel.Florence || era == CampaignLevel.Orleans)
                .OverrideFailureMessage($"{era} was sealed on an earlier visit and must be gone.").IsFalse();
            if (era != CampaignLevel.Paris && era != CampaignLevel.Chicago) {
                AssertThat(state).IsEqual(TriageShardState.Cold);
            }
        }
        // Everything struck and unsealed is present: 12 eras - 3 sealed + the flare.
        AssertThat(ranked.Count).IsEqual(HubTriageReadout.StruckEras.Count - 2);
        // Cold shards keep triage (route) order below the red one.
        AssertThat(ranked[1].Era).IsEqual(CampaignLevel.Titanic);

        AssertThat(HubTriageReadout.StateFor(CampaignLevel.Florence, CampaignLevel.Paris, sealedEras.Contains, null))
            .IsEqual(TriageShardState.Hidden);
    }

    [TestCase]
    public void TheReadoutGoesDarkFromActThreeAndAfterTheCampaign() {
        AssertThat(HubTriageReadout.IsDark(CampaignLevel.Florence, false)).IsFalse();
        AssertThat(HubTriageReadout.IsDark(CampaignLevel.Lunar, false)).IsFalse();
        foreach (CampaignLevel next in new[] { CampaignLevel.ChronalVoid, CampaignLevel.NeoEarth, CampaignLevel.Alexandria }) {
            AssertThat(HubTriageReadout.IsDark(next, false)).IsTrue();
            AssertThat(HubTriageReadout.Rank(next, false, _ => false, null).Count).IsEqual(0);
        }
        AssertThat(HubTriageReadout.IsDark(CampaignLevel.Pompeii, campaignCompleted: true)).IsTrue();
        // The route predecessor is what "just sealed" means on arrival.
        AssertThat(HubTriageReadout.RoutePredecessor(CampaignLevel.Orleans)).IsEqual(CampaignLevel.Florence);
        AssertThat(HubTriageReadout.RoutePredecessor(CampaignLevel.Tutorial).HasValue).IsFalse();
    }

    [TestCase]
    public void TheBuiltReadoutIsWordlessAndNonInteractive() {
        var readout = new HubTriageReadout();
        try {
            readout.Build(CampaignLevel.Florence, false, _ => false);
            AssertThat(readout.Dark).IsFalse();
            AssertThat(readout.Shards[0].Era).IsEqual(CampaignLevel.Florence);
            AssertThat(readout.Shards[0].State).IsEqual(TriageShardState.NextFlashing);
            AssertObject(readout.GetNodeOrNull<ColorRect>("Shard_Florence")).IsNotNull();
            foreach (Node child in readout.GetChildren()) {
                AssertThat(child is Label || child is CollisionObject2D)
                    .OverrideFailureMessage("The readout is a wordless display, never a map or level select.")
                    .IsFalse();
            }

            readout.Build(CampaignLevel.ChronalVoid, false, _ => true);
            AssertThat(readout.Dark).IsTrue();
            AssertThat(readout.Shards.Count).IsEqual(0);
        } finally {
            readout.Free();
        }
    }
}
