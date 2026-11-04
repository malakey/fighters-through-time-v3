using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-TRAIN (9c). The training room's readouts, fed hand-built samples. Pure C#: no engine API is touched.
/// </summary>
[TestSuite]
public class TrainingReadoutTrackerTests {

    private static TrainingReadoutSample Tick(int hp = 100, int hitstun = 0, int shield = 0, int phase = 0, bool frozen = false, bool live = true) =>
        new() { Live = live, DummyHP = hp, DummyHitstunFrames = hitstun, DummyShieldStunFrames = shield, StudentPhase = phase, StudentFrozen = frozen };

    private static void Feed(TrainingReadoutTracker tracker, params TrainingReadoutSample[] samples) {
        foreach (TrainingReadoutSample sample in samples) tracker.Observe(in sample);
    }

    [TestCase]
    public void ADamagingHitRecordsItsDamageTheTotalAndTheStunItApplied() {
        var tracker = new TrainingReadoutTracker();
        Feed(tracker, Tick(), Tick(), Tick(hp: 92, hitstun: 30), Tick(hp: 92, hitstun: 29), Tick(hp: 80, hitstun: 30));
        AssertThat(tracker.LastHitDamage).IsEqual(12);
        AssertThat(tracker.TotalDamage).IsEqual(20);
        AssertThat(tracker.StunApplied).IsEqual(30);
        AssertThat(tracker.FramesSinceHit).IsEqual(0);

        // A blocked hit applies shield stun and no damage.
        Feed(tracker, Tick(hp: 80, shield: 9));
        AssertThat(tracker.LastHitDamage).IsEqual(0);
        AssertThat(tracker.TotalDamage).IsEqual(20);
        AssertThat(tracker.StunApplied).IsEqual(9);

        // Nothing counts outside a live match, and Reset clears it all.
        Feed(tracker, Tick(hp: 10, live: false));
        AssertThat(tracker.TotalDamage).IsEqual(20);
        tracker.Reset();
        AssertThat(tracker.TotalDamage).IsEqual(0);
        AssertThat(tracker.FramesSinceHit).IsEqual(TrainingReadoutTracker.NoHit);
        AssertThat(tracker.Advantage.HasValue).IsFalse();
    }

    [TestCase]
    public void PhaseFramesCountPerMoveSkipHitstopAndAChainedHitStartsAgain() {
        var tracker = new TrainingReadoutTracker();
        // 4 startup, 2 active (one frozen), 3 recovery.
        Feed(tracker, Tick(phase: 1), Tick(phase: 1), Tick(phase: 1), Tick(phase: 1),
            Tick(phase: 2), Tick(phase: 2, frozen: true), Tick(phase: 3), Tick(phase: 3), Tick(phase: 3), Tick());
        AssertThat(tracker.StartupFrames).IsEqual(4);
        AssertThat(tracker.ActiveFrames).IsEqual(1);
        AssertThat(tracker.RecoveryFrames).IsEqual(3);

        // The next hit of a string starts a fresh record from its first startup tick.
        Feed(tracker, Tick(phase: 1), Tick(phase: 1), Tick(phase: 2));
        AssertThat(tracker.StartupFrames).IsEqual(2);
        AssertThat(tracker.ActiveFrames).IsEqual(1);
        AssertThat(tracker.RecoveryFrames).IsEqual(0);
    }

    [TestCase]
    public void AdvantageIsMeasuredWhenTheStudentIsFreeAgainPositiveOrNegative() {
        var tracker = new TrainingReadoutTracker();
        // The hit lands in the student's active frames; the dummy has 12 frames of stun left when the student is free.
        Feed(tracker, Tick(phase: 2), Tick(phase: 2, hp: 90, hitstun: 20), Tick(phase: 3, hp: 90, hitstun: 19));
        AssertThat(tracker.AdvantagePending).IsTrue();
        AssertThat(tracker.Advantage.HasValue).IsFalse();
        Feed(tracker, Tick(phase: 0, hp: 90, hitstun: 12));
        AssertThat(tracker.AdvantagePending).IsFalse();
        AssertThat(tracker.Advantage).IsEqual(12);

        // A block recovers faster than the attacker: the dummy is free for 3 ticks before the student is.
        Feed(tracker, Tick(phase: 2, hp: 90, shield: 8), Tick(phase: 3, hp: 90, shield: 1), Tick(phase: 3, hp: 90),
            Tick(phase: 3, hp: 90), Tick(phase: 3, hp: 90), Tick(phase: 0, hp: 90));
        AssertThat(tracker.Advantage).IsEqual(-3);
    }
}
