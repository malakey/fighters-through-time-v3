using System;
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
        public const string SarahActI = "hub.sarah_act1";
        public const string SarahActII = "hub.sarah_act2";
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
                    or CampaignLevel.LegacyNexus or CampaignLevel.Titanic => HubPeriod.ActI,
                CampaignLevel.Pompeii or CampaignLevel.Nassau or CampaignLevel.Egypt
                    or CampaignLevel.Berlin or CampaignLevel.London or CampaignLevel.Gettysburg
                    or CampaignLevel.Lunar => HubPeriod.ActII,
                _ => HubPeriod.ActIIIDeparture
            };
        }

        /// <summary>Sarah's set for a period. Never null: she always has something to say.</summary>
        public static string SarahSequenceFor(HubPeriod period) => period switch {
            HubPeriod.Prologue => SarahPrologue,
            HubPeriod.ActI => SarahActI,
            HubPeriod.ActII => SarahActII,
            HubPeriod.ActIIIDeparture => SarahActIIIDeparture,
            _ => SarahEpilogue
        };

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

        /// <summary>
        /// The set that should auto-play on arriving in the hub, or <c>""</c>
        /// when the period's Sarah set has already been seen on this slot.
        /// <paramref name="hasSeen"/> is the per-slot effect ledger lookup.
        /// </summary>
        public static string ArrivalSequenceFor(HubPeriod period, Func<string, bool> hasSeen) {
            string sarah = SarahSequenceFor(period);
            return hasSeen != null && hasSeen(sarah) ? "" : sarah;
        }
    }
}
