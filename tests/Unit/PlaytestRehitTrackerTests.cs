using FTT.Diagnostics;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-BOT (decision 7.2, 2026-10-16; plan dossier 2.7.2): the Godot-free re-hit
/// counter that measures whether Story needs post-hit mercy invulnerability.
/// Pure C#: <see cref="RehitTracker"/> reads nothing but its arguments, so this
/// suite carries no <c>[RequireGodotRuntime]</c> and touches no engine API
/// (CLAUDE.md failure signature 7). The engine half (the hero's
/// <c>DamageTaken</c> observation and the boss stamp) is in
/// <c>PlaytestCombatStatsTests</c>.
/// </summary>
[TestSuite]
public class PlaytestRehitTrackerTests {
    [TestCase]
    public void TwoSourcesTenFramesApartAreOneCrossSourceReHit() {
        var tracker = new RehitTracker();

        RehitTracker.Entry first = tracker.Record(100, "actor:1", RehitTracker.KindStandard, 10, "chrono_slasher");
        RehitTracker.Entry second = tracker.Record(110, "actor:2", RehitTracker.KindElite, 14, "chrono_warden");

        AssertThat(first.Cross30).IsFalse();
        AssertThat(second.Cross30)
            .OverrideFailureMessage("A different source's hit 10 frames earlier makes a cross-source re-hit.")
            .IsTrue();
        AssertThat(second.Cross20).IsTrue();
        AssertThat(second.Same30).IsFalse();
        AssertThat(second.CrossGap).IsEqual(10);
        AssertThat(second.CrossKind).IsEqual(RehitTracker.KindStandard);
        AssertThat(tracker.Hits).IsEqual(2);
        AssertThat(tracker.HitDamage).IsEqual(24);
        AssertThat(tracker.CrossSource30).IsEqual(1);
        AssertThat(tracker.CrossSource20).IsEqual(1);
        AssertThat(tracker.SameSource30).IsEqual(0);
        AssertThat(tracker.CrossSourceDamage30)
            .OverrideFailureMessage("The re-hit's own damage is what a mercy window could have refused.")
            .IsEqual(14);
        AssertThat(tracker.CrossGapHistogram.Count).IsEqual(1);
        AssertThat(tracker.CrossGapHistogram[10]).IsEqual(1);
        AssertThat(tracker.ByKind[RehitTracker.KindElite].Cross30)
            .OverrideFailureMessage("A kind's figures count its entries as the second hit.")
            .IsEqual(1);
        AssertThat(tracker.ByKind[RehitTracker.KindStandard].Cross30).IsEqual(0);
        AssertThat(tracker.ByKind[RehitTracker.KindStandard].Hits).IsEqual(1);
    }

    [TestCase]
    public void TheWindowIsHalfOpenAndTheTwentyFrameFigureIsNarrower() {
        var tracker = new RehitTracker();

        tracker.Record(100, "a", RehitTracker.KindStandard, 5);
        // 25 frames on: inside 30, outside 20.
        RehitTracker.Entry b = tracker.Record(125, "b", RehitTracker.KindStandard, 5);
        AssertThat(b.Cross30).IsTrue();
        AssertThat(b.Cross20).IsFalse();
        // Exactly 30 frames after b: F - 30 is inside [F - 30, F).
        RehitTracker.Entry c = tracker.Record(155, "c", RehitTracker.KindStandard, 5);
        AssertThat(c.Cross30)
            .OverrideFailureMessage("A hit exactly 30 frames back is inside the window.")
            .IsTrue();
        AssertThat(c.CrossGap).IsEqual(30);
        // 31 frames after c, and nothing nearer: outside.
        RehitTracker.Entry d = tracker.Record(186, "d", RehitTracker.KindStandard, 5);
        AssertThat(d.Cross30)
            .OverrideFailureMessage("A hit 31 frames back is outside the window.")
            .IsFalse();
        AssertThat(d.CrossGap).IsEqual(-1);
        AssertThat(d.CrossKind).IsEqual("");

        // Two earlier sources inside the window: the gap and kind are the latest one's.
        tracker.Record(300, "e", RehitTracker.KindHazard, 5);
        tracker.Record(310, "f", RehitTracker.KindBoss, 5);
        RehitTracker.Entry g = tracker.Record(318, "g", RehitTracker.KindStandard, 5);
        AssertThat(g.CrossGap).IsEqual(8);
        AssertThat(g.CrossKind).IsEqual(RehitTracker.KindBoss);

        AssertThat(tracker.CrossSource30).IsEqual(4);
        AssertThat(tracker.CrossSource20).IsEqual(2);
        AssertThat(tracker.CrossGapHistogram[25]).IsEqual(1);
        AssertThat(tracker.CrossGapHistogram[30]).IsEqual(1);
        AssertThat(tracker.CrossGapHistogram[10]).IsEqual(1);
        AssertThat(tracker.CrossGapHistogram[8]).IsEqual(1);
    }

    [TestCase]
    public void SameSourceReHitsAreTheContrastAndAHitCanBeBoth() {
        var tracker = new RehitTracker();

        tracker.Record(100, "a", RehitTracker.KindStandard, 6);
        RehitTracker.Entry again = tracker.Record(110, "a", RehitTracker.KindStandard, 6);
        AssertThat(again.Same30).IsTrue();
        AssertThat(again.Same20).IsTrue();
        AssertThat(again.Cross30)
            .OverrideFailureMessage("The same source again is never a cross-source re-hit.")
            .IsFalse();

        RehitTracker.Entry other = tracker.Record(115, "b", RehitTracker.KindProjectile, 4);
        AssertThat(other.Cross30).IsTrue();
        AssertThat(other.Same30).IsFalse();

        // a again: a at 110 (same, 10 back) and b at 115 (cross, 5 back).
        RehitTracker.Entry both = tracker.Record(120, "a", RehitTracker.KindStandard, 6);
        AssertThat(both.Same30).IsTrue();
        AssertThat(both.Cross30).IsTrue();
        AssertThat(both.CrossGap).IsEqual(5);
        AssertThat(both.CrossKind).IsEqual(RehitTracker.KindProjectile);

        // 25 frames after the last a: same within 30 but not within 20.
        RehitTracker.Entry late = tracker.Record(145, "a", RehitTracker.KindStandard, 6);
        AssertThat(late.Same30).IsTrue();
        AssertThat(late.Same20).IsFalse();

        AssertThat(tracker.SameSource30).IsEqual(3);
        AssertThat(tracker.SameSource20).IsEqual(2);
        AssertThat(tracker.CrossSource30).IsEqual(3);
        AssertThat(tracker.ByKind[RehitTracker.KindStandard].Same30).IsEqual(3);
        AssertThat(tracker.ByKind[RehitTracker.KindProjectile].Hits).IsEqual(1);
    }

    /// <summary>
    /// CM-FIXA-PRES (2026-10-16) rewrote this case in place: status ticks now
    /// live in the tick block (<see cref="RehitTracker.TicksByKind"/>), not in a
    /// <see cref="RehitTracker.ByKind"/> row, which holds hits only.
    /// </summary>
    [TestCase]
    public void StatusTicksAreCountedApartAsTicksAndKeptOutOfTheHeadline() {
        var tracker = new RehitTracker();

        RehitTracker.Entry first = tracker.Record(100, "status", RehitTracker.KindStatus, 2);
        AssertThat(first.Tick)
            .OverrideFailureMessage("A status entry is always a periodic tick.")
            .IsTrue();
        RehitTracker.Entry hit = tracker.Record(105, "a", RehitTracker.KindStandard, 8);
        AssertThat(hit.Cross30)
            .OverrideFailureMessage("A status tick is never the predecessor of a re-hit.")
            .IsFalse();
        RehitTracker.Entry tick = tracker.Record(108, "status", RehitTracker.KindStatus, 2);
        AssertThat(tick.Cross30)
            .OverrideFailureMessage("A status tick is never a re-hit itself.")
            .IsFalse();
        AssertObject(tracker.Record(109, "b", RehitTracker.KindStandard, 0))
            .OverrideFailureMessage("A zero-damage entry books nothing.")
            .IsNull();

        AssertThat(tracker.StatusTicks).IsEqual(2);
        AssertThat(tracker.Ticks).IsEqual(2);
        AssertThat(tracker.TickDamage).IsEqual(4);
        AssertThat(tracker.Hits).IsEqual(1);
        AssertThat(tracker.HitDamage).IsEqual(8);
        AssertThat(tracker.CrossSource30).IsEqual(0);
        AssertThat(tracker.TicksByKind[RehitTracker.KindStatus].Ticks).IsEqual(2);
        AssertThat(tracker.TicksByKind[RehitTracker.KindStatus].Damage).IsEqual(4);
        AssertThat(tracker.ByKind.ContainsKey(RehitTracker.KindStatus))
            .OverrideFailureMessage("by_kind holds hits only; status ticks live in the tick block.")
            .IsFalse();
        AssertThat(tracker.Log.Count)
            .OverrideFailureMessage("The log keeps hits only.")
            .IsEqual(1);
        AssertThat(tracker.Log[0].Kind).IsEqual(RehitTracker.KindStandard);
    }

    /// <summary>
    /// CM-FIXA-PRES (2026-10-16): a periodic tick of any kind — a drown tick
    /// (hazard), a Mirror zone pulse (mirror) — is booked with <c>tick: true</c>:
    /// it is not a hit, never a predecessor and never a re-hit, and it is counted
    /// apart per kind. The same kinds as discrete contacts stay hits.
    /// </summary>
    [TestCase]
    public void PeriodicZoneAndHazardTicksAreNeitherHitsNorPredecessors() {
        var tracker = new RehitTracker();

        // Level 5's drown tick (hazard:env, 4 HP) 20 frames before the boss's swing.
        RehitTracker.Entry drown = tracker.Record(600, "hazard:env", RehitTracker.KindHazard, 4, "env", tick: true);
        AssertThat(drown.Tick).IsTrue();
        RehitTracker.Entry boss = tracker.Record(620, "actor:7", RehitTracker.KindBoss, 18, "tidal_overseer");
        AssertThat(boss.Cross30)
            .OverrideFailureMessage("A periodic hazard tick is never the predecessor of a boss re-hit.")
            .IsFalse();
        AssertThat(boss.CrossGap).IsEqual(-1);

        // The Mirror clone's zone pulse 5 frames after the boss: not a re-hit, not a hit.
        RehitTracker.Entry pulse = tracker.Record(625, "actor:9", RehitTracker.KindMirror, 3, "mirror", tick: true);
        AssertThat(pulse.Cross30)
            .OverrideFailureMessage("A zone pulse is never a re-hit itself.")
            .IsFalse();
        // And a mob 10 frames on: the boss swing is its only predecessor, the pulse is none.
        RehitTracker.Entry mob = tracker.Record(635, "actor:11", RehitTracker.KindStandard, 6, "chrono_slasher");
        AssertThat(mob.Cross30).IsTrue();
        AssertThat(mob.CrossGap)
            .OverrideFailureMessage("The latest predecessor is the boss swing at gap 15, not the pulse at 10.")
            .IsEqual(15);
        AssertThat(mob.CrossKind).IsEqual(RehitTracker.KindBoss);

        // A discrete hazard contact (a discharge) is still a hit and a predecessor.
        tracker.Record(900, "hazard:extractor", RehitTracker.KindHazard, 10, "extractor");
        RehitTracker.Entry afterContact = tracker.Record(910, "actor:11", RehitTracker.KindStandard, 6);
        AssertThat(afterContact.Cross30)
            .OverrideFailureMessage("A discrete hazard contact stays in the headline (CM-BOT Decision 6).")
            .IsTrue();

        AssertThat(tracker.Hits).IsEqual(4);
        AssertThat(tracker.HitDamage).IsEqual(18 + 6 + 10 + 6);
        AssertThat(tracker.Ticks).IsEqual(2);
        AssertThat(tracker.TickDamage).IsEqual(7);
        AssertThat(tracker.StatusTicks)
            .OverrideFailureMessage("Only a status kind counts as a status tick.")
            .IsEqual(0);
        AssertThat(tracker.TicksByKind[RehitTracker.KindHazard].Ticks).IsEqual(1);
        AssertThat(tracker.TicksByKind[RehitTracker.KindMirror].Ticks).IsEqual(1);
        AssertThat(tracker.TicksByKind[RehitTracker.KindMirror].Damage).IsEqual(3);
        AssertThat(tracker.ByKind.ContainsKey(RehitTracker.KindMirror))
            .OverrideFailureMessage("A kind with ticks only has no hit row.")
            .IsFalse();
        AssertThat(tracker.ByKind[RehitTracker.KindHazard].Hits)
            .OverrideFailureMessage("The hazard hit row counts the discrete contact only.")
            .IsEqual(1);
        AssertThat(tracker.CrossSource30).IsEqual(2);
        AssertThat(tracker.Log.Count)
            .OverrideFailureMessage("Ticks are not logged.")
            .IsEqual(4);
    }

    /// <summary>
    /// CM-FIXA-PRES (2026-10-16): the combat-only figures leave hazard contacts
    /// out on both sides, so a mob hit whose only cross-source predecessor is a
    /// hazard contact is in the headline but not in <c>xsrc_30_combat</c>, and a
    /// hazard contact after a mob is never a combat re-hit. <c>by_kind</c> books
    /// by the second hit's kind, which is why it cannot do this.
    /// </summary>
    [TestCase]
    public void TheCombatFigureLeavesHazardContactsOutOnBothSides() {
        var tracker = new RehitTracker();

        tracker.Record(100, "hazard:debris", RehitTracker.KindHazard, 5, "env");
        RehitTracker.Entry mob = tracker.Record(110, "actor:1", RehitTracker.KindStandard, 8, "chrono_slasher");
        AssertThat(mob.Cross30).IsTrue();
        AssertThat(mob.Cross30Combat)
            .OverrideFailureMessage("A mob hit whose only predecessor is a hazard contact is not a combat re-hit.")
            .IsFalse();
        AssertThat(tracker.ByKind[RehitTracker.KindStandard].Cross30)
            .OverrideFailureMessage("by_kind books the hazard-preceded re-hit under the mob.")
            .IsEqual(1);

        // A hazard contact 5 frames after the mob: a headline re-hit, never a combat one.
        RehitTracker.Entry hazard = tracker.Record(115, "hazard:debris", RehitTracker.KindHazard, 5, "env");
        AssertThat(hazard.Cross30).IsTrue();
        AssertThat(hazard.Cross30Combat).IsFalse();

        // A boss 10 frames after the mob (with the hazard between): a combat re-hit at both windows.
        RehitTracker.Entry boss = tracker.Record(120, "actor:2", RehitTracker.KindBoss, 20, "duke");
        AssertThat(boss.Cross30Combat).IsTrue();
        AssertThat(boss.Cross20Combat).IsTrue();
        // Another mob 25 frames after the boss: combat within 30, not within 20.
        RehitTracker.Entry late = tracker.Record(145, "actor:3", RehitTracker.KindStandard, 8);
        AssertThat(late.Cross30Combat).IsTrue();
        AssertThat(late.Cross20Combat).IsFalse();

        AssertThat(tracker.Hits).IsEqual(5);
        AssertThat(tracker.CombatHits)
            .OverrideFailureMessage("The combat denominator leaves the hazard contacts out.")
            .IsEqual(3);
        AssertThat(tracker.CrossSource30).IsEqual(4);
        AssertThat(tracker.CrossSource30Combat).IsEqual(2);
        AssertThat(tracker.CrossSource20Combat).IsEqual(1);
        AssertThat(tracker.CrossSourceDamage30Combat).IsEqual(28);
        AssertThat(tracker.ByKind[RehitTracker.KindBoss].Cross30Combat).IsEqual(1);
        AssertThat(tracker.ByKind[RehitTracker.KindStandard].Cross30Combat).IsEqual(1);
        AssertThat(tracker.ByKind[RehitTracker.KindHazard].Cross30Combat).IsEqual(0);
    }

    [TestCase]
    public void SameFrameDoublesAreCountedApartAndTheLogIsCapped() {
        var tracker = new RehitTracker();

        tracker.Record(100, "a", RehitTracker.KindStandard, 5);
        RehitTracker.Entry together = tracker.Record(100, "b", RehitTracker.KindStandard, 5);
        AssertThat(together.CrossSameFrame)
            .OverrideFailureMessage("Two sources connecting on one frame are a same-frame double.")
            .IsTrue();
        AssertThat(together.Cross30)
            .OverrideFailureMessage("The half-open window [F - 30, F) leaves the same frame out.")
            .IsFalse();
        RehitTracker.Entry sameSourceTwice = tracker.Record(100, "b", RehitTracker.KindStandard, 5);
        AssertThat(sameSourceTwice.Same30)
            .OverrideFailureMessage("A same-frame second contact from one source is neither figure.")
            .IsFalse();
        AssertThat(sameSourceTwice.CrossSameFrame).IsTrue();
        AssertThat(tracker.CrossSourceSameFrame).IsEqual(2);
        AssertThat(tracker.CrossSource30).IsEqual(0);

        for (int i = 0; i < RehitTracker.LogCap + 100; i++) {
            tracker.Record(1000 + i * 40, i % 2 == 0 ? "c" : "d", RehitTracker.KindHazard, 1);
        }
        AssertThat(tracker.Log.Count).IsEqual(RehitTracker.LogCap);
        AssertThat(tracker.Hits)
            .OverrideFailureMessage("The cap trims the log, never the counts.")
            .IsEqual(3 + RehitTracker.LogCap + 100);
        AssertThat(tracker.ByKind[RehitTracker.KindHazard].Hits).IsEqual(RehitTracker.LogCap + 100);
        AssertThat(tracker.CrossSource30)
            .OverrideFailureMessage("Entries 40 frames apart never re-hit.")
            .IsEqual(0);
    }
}
