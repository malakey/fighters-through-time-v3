using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;
using HubPeriod = FTT.Environment.HubDialogueSelector.HubPeriod;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W7 (GAP-15): the hub's act-boundary dialogue selector. Sarah carries
/// one fresh set per hub visit period — the Level 0 prologue, Act I, Act II, the
/// final departure before the Act III hub lockout, and a post-campaign epilogue on
/// the Bridge — and the period's set plays once, on the first arrival of the
/// period, recorded in the slot's dialogue effect ledger.
///
/// <para>The four GAP-15 cases are here: a first return, a repeat visit, a load
/// (the ledger read back from a normalized save), and a completed save. Pure C#:
/// the selector is engine-free and <see cref="StorySaveData"/> is plain data. One
/// <c>[TestSuite]</c> per file.</para>
/// </summary>
[TestSuite]
public class HubDialogueSelectorTests {

    [TestCase]
    public void EveryRouteSlotMapsToItsHubVisitPeriod() {
        var expected = new Dictionary<CampaignLevel, HubPeriod> {
            [CampaignLevel.Tutorial] = HubPeriod.Prologue,
            [CampaignLevel.Florence] = HubPeriod.Prologue,
            [CampaignLevel.Orleans] = HubPeriod.ActI,
            [CampaignLevel.Chicago] = HubPeriod.ActI,
            [CampaignLevel.Paris] = HubPeriod.ActI,
            [CampaignLevel.Titanic] = HubPeriod.ActI,
            [CampaignLevel.Pompeii] = HubPeriod.ActII,
            [CampaignLevel.Nassau] = HubPeriod.ActII,
            [CampaignLevel.Egypt] = HubPeriod.ActII,
            [CampaignLevel.Berlin] = HubPeriod.ActII,
            [CampaignLevel.London] = HubPeriod.ActII,
            [CampaignLevel.Gettysburg] = HubPeriod.ActII,
            [CampaignLevel.Lunar] = HubPeriod.ActII,
            [CampaignLevel.ChronalVoid] = HubPeriod.ActIIIDeparture,
            [CampaignLevel.NeoEarth] = HubPeriod.ActIIIDeparture,
            [CampaignLevel.Alexandria] = HubPeriod.ActIIIDeparture,
        };
        foreach (CampaignLevel level in Enum.GetValues<CampaignLevel>()) {
            // Package 13 W2 (S27): the retired Level 4A ordinal (16) is never a
            // next mission — the route skips it and the v8 derivation re-parks
            // any save that still names it — so it has no hub period to pin.
            if ((int)level == 16) continue;
            AssertThat(expected.ContainsKey(level))
                .OverrideFailureMessage($"{level} has no hub period pinned here.").IsTrue();
            AssertThat(HubDialogueSelector.PeriodFor(level, campaignCompleted: false))
                .OverrideFailureMessage($"{level} maps to the wrong hub period.")
                .IsEqual(expected[level]);
        }
    }

    [TestCase]
    public void EachPeriodHasItsOwnSarahSetAndNeverAPriorActs() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (HubPeriod period in Enum.GetValues<HubPeriod>()) {
            string sarah = HubDialogueSelector.SarahSequenceFor(period);
            AssertThat(string.IsNullOrEmpty(sarah)).IsFalse();
            AssertThat(seen.Add(sarah))
                .OverrideFailureMessage($"{period} reuses another period's Sarah set '{sarah}'.").IsTrue();
        }
        // The retargeted Act III set is the final departure, not an in-hub Act III visit.
        AssertString(HubDialogueSelector.SarahSequenceFor(HubPeriod.ActIIIDeparture)).IsEqual("hub.sarah_act3");
        AssertString(HubDialogueSelector.SarahSequenceFor(HubPeriod.Epilogue)).IsEqual("hub.sarah_epilogue");
        // Okafor's first reading is taken after Florence.
        AssertString(HubDialogueSelector.OkaforSequenceFor(HubPeriod.Prologue)).IsEqual("");
        AssertString(HubDialogueSelector.OkaforSequenceFor(HubPeriod.ActI)).IsEqual("hub.okafor_act1");
    }

    [TestCase]
    public void TheFirstReturnOfAPeriodPlaysItsSetAndARepeatVisitDoesNot() {
        // Package 13 W3 (S23/S35): Acts I and II are one line per visit, so the
        // "period" check is now about visits. The per-line rules have their own
        // suite (HubPerLineGatingTests); this pins the visit boundary.
        var save = new StorySaveData();
        HubPeriod actI = HubDialogueSelector.PeriodFor(CampaignLevel.Orleans, false);
        Func<string, bool> allCompleted = _ => true;

        // First return after Florence: the Act I set's first line auto-plays.
        string arrival = HubDialogueSelector.ArrivalSequenceFor(
            actI, save.ViewedDialogueIDs.Contains, allCompleted, "level_02_orleans", save.LastHubLineVisit);
        AssertString(arrival).IsEqual("hub.sarah_act1.1");

        // DialogueManager's effect ledger records it; the hub stamps the visit.
        save.ViewedDialogueIDs.Add(arrival);
        save.LastHubLineVisit = "level_02_orleans";

        // A repeat arrival in the SAME visit (a Collapse or Holodeck return): nothing.
        AssertString(HubDialogueSelector.ArrivalSequenceFor(
                actI, save.ViewedDialogueIDs.Contains, allCompleted, "level_02_orleans", save.LastHubLineVisit))
            .IsEqual("");

        // Crossing the Act I boundary is a new period: the boundary scene comes first.
        HubPeriod actII = HubDialogueSelector.PeriodFor(CampaignLevel.Pompeii, false);
        AssertString(HubDialogueSelector.ArrivalSequenceFor(
                actII, save.ViewedDialogueIDs.Contains, allCompleted, "level_06_pompeii", save.LastHubLineVisit))
            .IsEqual("hub.act1_boundary");
    }

    [TestCase]
    public void ALoadedSaveRemembersWhatItHasAlreadyHeard() {
        var original = new StorySaveData();
        original.ViewedDialogueIDs.Add("hub.sarah_briefing");
        original.ViewedDialogueIDs.Add("hub.sarah_act3");

        // A load hands back a freshly constructed, normalized payload carrying the list.
        var loaded = new StorySaveData { ViewedDialogueIDs = new List<string>(original.ViewedDialogueIDs) };
        loaded.Normalize();

        AssertString(HubDialogueSelector.ArrivalSequenceFor(HubPeriod.Prologue, loaded.ViewedDialogueIDs.Contains))
            .IsEqual("");
        // The final departure before the Act III lockout plays once, not on every reload.
        AssertString(HubDialogueSelector.ArrivalSequenceFor(
                HubDialogueSelector.PeriodFor(CampaignLevel.ChronalVoid, false), loaded.ViewedDialogueIDs.Contains))
            .IsEqual("");
    }

    [TestCase]
    public void ACompletedSaveIsAlwaysTheEpilogueWhateverItsNextMission() {
        foreach (CampaignLevel level in Enum.GetValues<CampaignLevel>()) {
            AssertThat(HubDialogueSelector.PeriodFor(level, campaignCompleted: true)).IsEqual(HubPeriod.Epilogue);
        }
        var completed = new StorySaveData { IsCompleted = true };
        completed.ViewedDialogueIDs.Add("hub.sarah_act3");
        AssertString(HubDialogueSelector.ArrivalSequenceFor(HubPeriod.Epilogue, completed.ViewedDialogueIDs.Contains))
            .IsEqual("hub.sarah_epilogue");
        completed.ViewedDialogueIDs.Add("hub.sarah_epilogue");
        AssertString(HubDialogueSelector.ArrivalSequenceFor(HubPeriod.Epilogue, completed.ViewedDialogueIDs.Contains))
            .IsEqual("");
        AssertString(HubDialogueSelector.OkaforSequenceFor(HubPeriod.Epilogue)).IsEqual("hub.okafor_epilogue");
    }
}
