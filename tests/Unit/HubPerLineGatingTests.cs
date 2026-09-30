using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;
using HubPeriod = FTT.Environment.HubDialogueSelector.HubPeriod;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W3 (S23/S35): Sarah's Act I and Act II hub sets are one line per
/// visit, in order, with "only after Level N" gates; the Act I→II boundary scene
/// (Okafor's faint-shard report with Sarah present) opens Act II; Wren has one
/// tip per act. A visit is keyed by the next mission, stamped on the slot as
/// <see cref="StorySaveData.LastHubLineVisit"/>; the order itself comes from the
/// effect ledger. Pure C#: no engine call anywhere (failure signature 7).
/// </summary>
[TestSuite]
public class HubPerLineGatingTests {

    /// <summary>Plays the hub arrival the way the hub does: start, then record and stamp.</summary>
    private static string Arrive(StorySaveData save, CampaignLevel next) {
        string visit = VisitKey(next);
        string arrival = HubDialogueSelector.ArrivalSequenceFor(
            HubDialogueSelector.PeriodFor(next, save.IsCompleted),
            save.ViewedDialogueIDs.Contains,
            save.CompletedLevels.Contains,
            visit,
            save.LastHubLineVisit);
        if (string.IsNullOrEmpty(arrival)) return "";
        save.ViewedDialogueIDs.Add(arrival);
        if (HubDialogueSelector.IsPerVisitLine(arrival)) save.LastHubLineVisit = visit;
        return arrival;
    }

    /// <summary>Mirrors StoryManager's level-ID table for the route slots used here.</summary>
    private static string VisitKey(CampaignLevel next) => next switch {
        CampaignLevel.Orleans => "level_02_orleans",
        CampaignLevel.Chicago => "level_03_chicago",
        CampaignLevel.Paris => "level_04_paris",
        CampaignLevel.Titanic => "level_05_titanic",
        CampaignLevel.Pompeii => "level_06_pompeii",
        CampaignLevel.Nassau => "level_07_nassau",
        CampaignLevel.Egypt => "level_08_egypt",
        CampaignLevel.Berlin => "level_09_berlin",
        CampaignLevel.London => "level_10_globe",
        CampaignLevel.Gettysburg => "level_11_gettysburg",
        CampaignLevel.Lunar => "level_12_lunar",
        _ => next.ToString()
    };

    [TestCase]
    public void ActOnePlaysOneLinePerVisitInOrderAndLineFourWaitsForLevelFour() {
        var save = new StorySaveData();
        save.CompletedLevels.Add("level_01_florence");
        AssertString(Arrive(save, CampaignLevel.Orleans)).IsEqual("hub.sarah_act1.1");
        // A Collapse or Holodeck return in the same visit plays nothing new.
        AssertString(Arrive(save, CampaignLevel.Orleans)).IsEqual("");

        save.CompletedLevels.Add("level_02_orleans");
        AssertString(Arrive(save, CampaignLevel.Chicago)).IsEqual("hub.sarah_act1.2");
        save.CompletedLevels.Add("level_03_chicago");
        AssertString(Arrive(save, CampaignLevel.Paris)).IsEqual("hub.sarah_act1.3");

        // Line 4 is "only after Level 4": an extra visit before it plays nothing.
        save.LastHubLineVisit = "a_previous_visit";
        AssertString(Arrive(save, CampaignLevel.Paris)).IsEqual("");

        save.CompletedLevels.Add("level_04_paris");
        AssertString(Arrive(save, CampaignLevel.Titanic)).IsEqual("hub.sarah_act1.4");
        save.CompletedLevels.Add("level_05_titanic");
        save.LastHubLineVisit = "another_visit";
        AssertString(Arrive(save, CampaignLevel.Titanic))
            .OverrideFailureMessage("The Act I set is spent; nothing repeats.").IsEqual("");
    }

    [TestCase]
    public void ActTwoOpensOnTheBoundarySceneAndHonoursItsLevelGates() {
        var save = new StorySaveData();
        foreach (string id in new[] { "level_01_florence", "level_02_orleans", "level_03_chicago",
                                      "level_04_paris", "level_05_titanic" }) {
            save.CompletedLevels.Add(id);
        }
        // After Level 5: the faint-shard report with Sarah present, not a line.
        AssertString(Arrive(save, CampaignLevel.Pompeii)).IsEqual(HubDialogueSelector.ActIBoundary);
        // Died in Pompeii and came home: same visit, nothing new.
        AssertString(Arrive(save, CampaignLevel.Pompeii)).IsEqual("");

        save.CompletedLevels.Add("level_06_pompeii");
        AssertString(Arrive(save, CampaignLevel.Nassau)).IsEqual("hub.sarah_act2.1");
        save.CompletedLevels.Add("level_07_nassau");
        AssertString(Arrive(save, CampaignLevel.Egypt)).IsEqual("hub.sarah_act2.2");

        // Line 3 is only after Level 9 (Hakata): after Level 8 it waits, and line 4
        // never jumps the queue.
        save.CompletedLevels.Add("level_08_egypt");
        AssertString(Arrive(save, CampaignLevel.Berlin)).IsEqual("");
        save.CompletedLevels.Add("level_09_berlin");
        AssertString(Arrive(save, CampaignLevel.London)).IsEqual("hub.sarah_act2.3");
        save.CompletedLevels.Add("level_10_globe");
        AssertString(Arrive(save, CampaignLevel.Gettysburg)).IsEqual("");
        save.CompletedLevels.Add("level_11_gettysburg");
        AssertString(Arrive(save, CampaignLevel.Lunar)).IsEqual("hub.sarah_act2.4");
    }

    [TestCase]
    public void TheWholeScenePeriodsStillPlayOnceAndIgnoreTheVisitStamp() {
        var save = new StorySaveData { LastHubLineVisit = "level_13_chronal_void" };
        HubPeriod departure = HubDialogueSelector.PeriodFor(CampaignLevel.ChronalVoid, false);
        string first = HubDialogueSelector.ArrivalSequenceFor(
            departure, save.ViewedDialogueIDs.Contains, _ => true, "level_13_chronal_void", save.LastHubLineVisit);
        AssertString(first).IsEqual("hub.sarah_act3");
        AssertThat(HubDialogueSelector.IsPerVisitLine(first)).IsFalse();
        save.ViewedDialogueIDs.Add(first);
        AssertString(HubDialogueSelector.ArrivalSequenceFor(
                departure, save.ViewedDialogueIDs.Contains, _ => true, "level_13_chronal_void", save.LastHubLineVisit))
            .IsEqual("");
        AssertString(HubDialogueSelector.ArrivalSequenceFor(HubPeriod.Prologue, _ => false))
            .IsEqual("hub.sarah_briefing");
    }

    [TestCase]
    public void TalkingToSarahReplaysHerMostRecentLineAndWrenHasOneTipPerAct() {
        var heard = new HashSet<string>(StringComparer.Ordinal);
        AssertString(HubDialogueSelector.SarahTalkSequenceFor(HubPeriod.ActI, heard.Contains))
            .IsEqual("hub.sarah_act1.1");
        heard.Add("hub.sarah_act1.1");
        heard.Add("hub.sarah_act1.2");
        AssertString(HubDialogueSelector.SarahTalkSequenceFor(HubPeriod.ActI, heard.Contains))
            .IsEqual("hub.sarah_act1.2");
        // Act II before any line: the boundary scene is the conversation.
        AssertString(HubDialogueSelector.SarahTalkSequenceFor(HubPeriod.ActII, heard.Contains))
            .IsEqual(HubDialogueSelector.ActIBoundary);
        AssertString(HubDialogueSelector.SarahTalkSequenceFor(HubPeriod.Epilogue, heard.Contains))
            .IsEqual("hub.sarah_epilogue");

        var wren = new HashSet<string>(StringComparer.Ordinal);
        foreach (HubPeriod period in Enum.GetValues<HubPeriod>()) wren.Add(HubDialogueSelector.WrenSequenceFor(period));
        // Prologue shares Act I's tip; the other four periods each have their own.
        AssertThat(wren.Count).IsEqual(4);
        AssertString(HubDialogueSelector.WrenSequenceFor(HubPeriod.Prologue))
            .IsEqual(HubDialogueSelector.WrenSequenceFor(HubPeriod.ActI));
    }

    [TestCase]
    public void TheVisitStampIsAnEngineFreeAdditiveFieldThatNormalizes() {
        var fresh = new StorySaveData();
        AssertString(fresh.LastHubLineVisit).IsEqual("");
        var loaded = new StorySaveData { LastHubLineVisit = null };
        loaded.Normalize();
        AssertString(loaded.LastHubLineVisit).IsEqual("");

        var roundTrip = Newtonsoft.Json.JsonConvert.DeserializeObject<StorySaveData>(
            Newtonsoft.Json.JsonConvert.SerializeObject(new StorySaveData { LastHubLineVisit = "level_07_nassau" }));
        AssertString(roundTrip.LastHubLineVisit).IsEqual("level_07_nassau");
    }
}
