using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Package 12 W7 (GAP-15): which dialogue set the hub's crew speaks in the
    /// current hub visit period, and whether it should play on arrival.
    ///
    /// <para><b>Periods.</b> Commander Sarah carries one fresh set per hub
    /// visit period (design §3, hub NPC table): the Level 0 prologue, Act I,
    /// Act II, the final departure before the Act III hub lockout, and a
    /// post-campaign epilogue on the Bridge. Act III itself never returns to
    /// the ship — its lines are in-level comms. Medic Okafor follows the same
    /// periods from Act I onward. The period is derived from the campaign's
    /// <b>next</b> mission (<see cref="StoryManager.CurrentLevel"/>) and the
    /// completion flag, so it needs no new save field.</para>
    ///
    /// <para><b>Once-only.</b> The period's set auto-plays on the first hub
    /// arrival of that period and never again: the per-slot dialogue effect
    /// ledger (<c>StorySaveData.ViewedDialogueIDs</c>, which both the completion
    /// and the confirmed-skip path write) is the seen record, so a repeat visit,
    /// a reload and a quit-and-resume all see it as played. Talking to the NPC
    /// replays the current set on demand; it never plays a prior period's.</para>
    ///
    /// <para>Pure C#: no engine calls, so the selection is provable in the
    /// plain test host.</para>
    /// </summary>
    public static class HubDialogueSelector {

        public enum HubPeriod { Prologue, ActI, ActII, ActIIIDeparture, Epilogue }

        public const string SarahPrologue = "hub.sarah_briefing";
        /// <summary>Retargeted (Package 12 W7): the final departure after Level 12, not an in-hub Act III set.</summary>
        public const string SarahActIIIDeparture = "hub.sarah_act3";
        public const string SarahEpilogue = "hub.sarah_epilogue";

        public const string OkaforActI = "hub.okafor_act1";
        public const string OkaforActII = "hub.okafor_act2";
        public const string OkaforActIIIDeparture = "hub.okafor_act3";
        public const string OkaforEpilogue = "hub.okafor_epilogue";

        /// <summary>
        /// The hub visit period for a campaign whose next mission is
        /// <paramref name="nextLevel"/>. A completed campaign is always the
        /// epilogue.
        /// </summary>
        public static HubPeriod PeriodFor(CampaignLevel nextLevel, bool campaignCompleted) {
            if (campaignCompleted) return HubPeriod.Epilogue;
            return nextLevel switch {
                CampaignLevel.Tutorial => HubPeriod.Prologue,
                CampaignLevel.Florence => HubPeriod.Prologue,
                CampaignLevel.Orleans or CampaignLevel.Chicago or CampaignLevel.Paris
                    or CampaignLevel.Titanic => HubPeriod.ActI,
                CampaignLevel.Pompeii or CampaignLevel.Nassau or CampaignLevel.Egypt
                    or CampaignLevel.Berlin or CampaignLevel.London or CampaignLevel.Gettysburg
                    or CampaignLevel.Lunar => HubPeriod.ActII,
                _ => HubPeriod.ActIIIDeparture
            };
        }


        // === Package 13 W3 (S23/S35): per-line hub gating =====================
        //
        // Sarah's Act I and Act II sets are no longer whole scenes replayed once
        // per period. Each is four single lines, played ONE PER HUB VISIT, IN
        // ORDER, some gated "only after Level N". A visit is keyed by the
        // campaign's next mission: a Collapse or Holodeck return lands on the
        // Bridge with the same next mission, so it is the same visit and plays
        // nothing new (StorySaveData.LastHubLineVisit). The order is derived from
        // the slot's effect ledger (ViewedDialogueIDs), so no index is stored.
        //
        // The Act I→II boundary (after Level 5) is its own scene — Okafor's
        // faint-shard report with Sarah present (S16/S35) — and it is the first
        // Act II visit's line. The Act II→III departure (after Level 12) plays
        // Okafor's per-hero line and Sarah's Beacon handover (S42/S47).

        /// <summary>One single-line hub beat and the level that must be completed first ("" = none).</summary>
        public readonly record struct HubLine(string SequenceID, string RequiresCompletedLevelID);

        public const string SarahActILine1 = "hub.sarah_act1.1";
        public const string ActIBoundary = "hub.act1_boundary";
        public const string SarahActIILine1 = "hub.sarah_act2.1";

        /// <summary>Sarah, Act I (after Levels 1–4): line 4 only after Level 4.</summary>
        public static readonly IReadOnlyList<HubLine> SarahActILines = new[] {
            new HubLine(SarahActILine1, ""),
            new HubLine("hub.sarah_act1.2", ""),
            new HubLine("hub.sarah_act1.3", ""),
            new HubLine("hub.sarah_act1.4", "level_04_paris"),
        };

        /// <summary>
        /// Sarah, Act II (after Levels 6–11): line 1 after Level 6 (the first Act
        /// II visit belongs to the boundary scene), line 3 only after Level 9,
        /// line 4 only after Level 11.
        /// </summary>
        public static readonly IReadOnlyList<HubLine> SarahActIILines = new[] {
            new HubLine(SarahActIILine1, "level_06_pompeii"),
            new HubLine("hub.sarah_act2.2", ""),
            new HubLine("hub.sarah_act2.3", "level_09_berlin"),
            new HubLine("hub.sarah_act2.4", "level_11_gettysburg"),
        };

        /// <summary>The per-line set for a period; empty for the whole-scene periods.</summary>
        public static IReadOnlyList<HubLine> SarahLinesFor(HubPeriod period) => period switch {
            HubPeriod.ActI => SarahActILines,
            HubPeriod.ActII => SarahActIILines,
            _ => Array.Empty<HubLine>()
        };

        /// <summary>
        /// Sarah's representative set for a period — the first line of a per-line
        /// period, the whole scene otherwise. Never null, and never a prior act's.
        /// </summary>
        public static string SarahSequenceFor(HubPeriod period) => period switch {
            HubPeriod.Prologue => SarahPrologue,
            HubPeriod.ActI => SarahActILine1,
            HubPeriod.ActII => SarahActIILine1,
            HubPeriod.ActIIIDeparture => SarahActIIIDeparture,
            _ => SarahEpilogue
        };

        /// <summary>
        /// What talking to Sarah replays on demand: in a per-line period the most
        /// recent line she has spoken (the boundary scene before any Act II line),
        /// else the period's first beat; otherwise the period's whole scene.
        /// </summary>
        public static string SarahTalkSequenceFor(HubPeriod period, Func<string, bool> hasSeen) {
            IReadOnlyList<HubLine> lines = SarahLinesFor(period);
            if (lines.Count == 0) return SarahSequenceFor(period);
            string latest = "";
            foreach (HubLine line in lines) {
                if (hasSeen != null && hasSeen(line.SequenceID)) latest = line.SequenceID;
            }
            if (!string.IsNullOrEmpty(latest)) return latest;
            if (period == HubPeriod.ActII) return ActIBoundary;
            return lines[0].SequenceID;
        }

        /// <summary>
        /// Okafor's set for a period, or <c>""</c> in the prologue — his
        /// first reading is taken after Florence, so he has nothing to report
        /// before the first mission.
        /// </summary>
        public static string OkaforSequenceFor(HubPeriod period) => period switch {
            HubPeriod.Prologue => "",
            HubPeriod.ActI => OkaforActI,
            HubPeriod.ActII => OkaforActII,
            HubPeriod.ActIIIDeparture => OkaforActIIIDeparture,
            _ => OkaforEpilogue
        };

        public const string WrenActI = "hub.wren_act1";
        public const string WrenActII = "hub.wren_act2";
        public const string WrenActIIIDeparture = "hub.wren_act3";
        public const string WrenEpilogue = "hub.wren_epilogue";

        /// <summary>
        /// Chief Engineer Wren's tip for a period (S09: one hint per act, keyed to
        /// what the next stretch introduces). The Level 0 prologue shares Act I's.
        /// </summary>
        public static string WrenSequenceFor(HubPeriod period) => period switch {
            HubPeriod.Prologue or HubPeriod.ActI => WrenActI,
            HubPeriod.ActII => WrenActII,
            HubPeriod.ActIIIDeparture => WrenActIIIDeparture,
            _ => WrenEpilogue
        };

        /// <summary>
        /// The whole-scene arrival for a non-per-line period, or <c>""</c> when
        /// the period's Sarah set has already been seen on this slot.
        /// <paramref name="hasSeen"/> is the per-slot effect ledger lookup. A
        /// per-line period always answers through
        /// <see cref="ArrivalSequenceFor(HubPeriod, Func{string,bool}, Func{string,bool}, string, string)"/>.
        /// </summary>
        public static string ArrivalSequenceFor(HubPeriod period, Func<string, bool> hasSeen) =>
            ArrivalSequenceFor(period, hasSeen, _ => true, "", "");

        /// <summary>
        /// The one sequence to auto-play on this hub arrival, or <c>""</c>.
        ///
        /// <para>Whole-scene periods (prologue, the Act III departure, the
        /// epilogue) play their scene once. Per-line periods play at most one line
        /// per visit: nothing when <paramref name="visitKey"/> equals
        /// <paramref name="lastLineVisit"/> (the same visit), else the first
        /// unheard line in order — the Act I→II boundary scene first, in Act II —
        /// provided its "after Level N" gate is met. An unmet gate waits; later
        /// lines never jump the queue.</para>
        /// </summary>
        public static string ArrivalSequenceFor(
            HubPeriod period,
            Func<string, bool> hasSeen,
            Func<string, bool> isLevelCompleted,
            string visitKey,
            string lastLineVisit) {
            hasSeen ??= _ => false;
            isLevelCompleted ??= _ => false;
            IReadOnlyList<HubLine> lines = SarahLinesFor(period);
            if (lines.Count == 0) {
                string scene = SarahSequenceFor(period);
                return hasSeen(scene) ? "" : scene;
            }
            if (!string.IsNullOrEmpty(visitKey)
                && string.Equals(visitKey, lastLineVisit ?? "", StringComparison.Ordinal)) return "";
            if (period == HubPeriod.ActII && !hasSeen(ActIBoundary)) return ActIBoundary;
            foreach (HubLine line in lines) {
                if (hasSeen(line.SequenceID)) continue;
                if (!string.IsNullOrEmpty(line.RequiresCompletedLevelID)
                    && !isLevelCompleted(line.RequiresCompletedLevelID)) return "";
                return line.SequenceID;
            }
            return "";
        }

        /// <summary>True when <paramref name="sequenceID"/> is a one-line-per-visit beat (it stamps the visit).</summary>
        public static bool IsPerVisitLine(string sequenceID) {
            if (string.IsNullOrEmpty(sequenceID)) return false;
            if (sequenceID == ActIBoundary) return true;
            foreach (HubLine line in SarahActILines) if (line.SequenceID == sequenceID) return true;
            foreach (HubLine line in SarahActIILines) if (line.SequenceID == sequenceID) return true;
            return false;
        }
    }
}
