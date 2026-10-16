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

    [TestCase]
    public void StatusTicksAreCountedUnderTheirOwnKindAndKeptOutOfTheHeadline() {
        var tracker = new RehitTracker();

        tracker.Record(100, "status", RehitTracker.KindStatus, 2);
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
        AssertThat(tracker.Hits).IsEqual(1);
        AssertThat(tracker.HitDamage).IsEqual(8);
        AssertThat(tracker.CrossSource30).IsEqual(0);
        AssertThat(tracker.ByKind[RehitTracker.KindStatus].Hits).IsEqual(2);
        AssertThat(tracker.ByKind[RehitTracker.KindStatus].Damage).IsEqual(4);
        AssertThat(tracker.ByKind[RehitTracker.KindStatus].Cross30).IsEqual(0);
        AssertThat(tracker.Log.Count)
            .OverrideFailureMessage("The log keeps hits only; status ticks live in their kind row.")
            .IsEqual(1);
        AssertThat(tracker.Log[0].Kind).IsEqual(RehitTracker.KindStandard);
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
