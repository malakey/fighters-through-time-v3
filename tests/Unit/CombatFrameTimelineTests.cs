using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
public class CombatFrameTimelineTests {
    [TestCase]
    public void PhaseBoundariesAreExactAndNonOverlapping() {
        var timeline = new CombatFrameTimeline(6, 3, 4);

        AssertThat(timeline.GetPhase(0)).IsEqual(CombatFramePhase.Startup);
        AssertThat(timeline.GetPhase(5)).IsEqual(CombatFramePhase.Startup);
        AssertThat(timeline.GetPhase(6)).IsEqual(CombatFramePhase.Active);
        AssertThat(timeline.GetPhase(8)).IsEqual(CombatFramePhase.Active);
        AssertThat(timeline.GetPhase(9)).IsEqual(CombatFramePhase.Recovery);
        AssertThat(timeline.GetPhase(12)).IsEqual(CombatFramePhase.Recovery);
        AssertThat(timeline.GetPhase(13)).IsEqual(CombatFramePhase.Complete);
    }

    [TestCase]
    public void InvalidInputsAreClampedToAUsableWindow() {
        var timeline = new CombatFrameTimeline(-5, 0, -2);

        AssertThat(timeline.StartupFrames).IsEqual(0);
        AssertThat(timeline.ActiveFrames).IsEqual(1);
        AssertThat(timeline.RecoveryFrames).IsEqual(0);
        AssertThat(timeline.TotalFrames).IsEqual(1);
        AssertThat(timeline.IsActive(0)).IsTrue();
    }
}
