using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B1. The conditional overhead-bar state machine.
///
/// The reset contract is the part that matters most: the bars live on pooled
/// enemy bodies, so a state that survives a release/spawn cycle shows the next
/// occupant of that body a bar it never earned — and pool bugs of this shape are
/// invisible until a level happens to recycle a body the player already fought.
/// </summary>
[TestSuite]
public class OverheadBarVisibilityTests {

    [TestCase]
    public void AnUndamagedEnemyShowsNothingNoMatterHowLongItLives() {
        var visibility = new OverheadBarVisibility();

        AssertThat(visibility.HasBeenDamaged).IsFalse();
        AssertThat(visibility.IsVisible).IsFalse();
        AssertFloat(visibility.Alpha).IsEqual(0f);

        for (int frame = 0; frame < 600; frame++) visibility.Tick(1f / 60f);

        AssertThat(visibility.IsVisible).IsFalse();
        AssertFloat(visibility.Alpha).IsEqual(0f);
    }

    [TestCase]
    public void TheFirstHitRevealsTheBarAtFullOpacity() {
        var visibility = new OverheadBarVisibility();

        visibility.NotifyDamaged();

        AssertThat(visibility.HasBeenDamaged).IsTrue();
        AssertThat(visibility.IsVisible).IsTrue();
        AssertFloat(visibility.Alpha).IsEqual(1f);
    }

    [TestCase]
    public void TheBarHoldsThroughTheQuietWindowThenFadesToNothing() {
        var visibility = new OverheadBarVisibility { QuietSeconds = 2f, FadeSeconds = 1f };
        visibility.NotifyDamaged();

        // Full opacity for the whole quiet window: a live exchange must not dim.
        visibility.Tick(1.9f);
        AssertFloat(visibility.Alpha).IsEqual(1f);

        // Then a linear fade over the authored fade window.
        visibility.Tick(0.6f);
        AssertFloat(visibility.Alpha).IsEqualApprox(0.5f, 0.0001f);

        visibility.Tick(0.5f);
        AssertFloat(visibility.Alpha).IsEqual(0f);
        AssertThat(visibility.IsVisible).IsFalse();
        // Still "damaged": the enemy was engaged, it is simply quiet again.
        AssertThat(visibility.HasBeenDamaged).IsTrue();
    }

    [TestCase]
    public void EveryFurtherHitReArmsTheWindowSoASustainedFightNeverFlickers() {
        var visibility = new OverheadBarVisibility { QuietSeconds = 2f, FadeSeconds = 1f };
        visibility.NotifyDamaged();

        for (int exchange = 0; exchange < 10; exchange++) {
            visibility.Tick(2.5f);   // into the fade
            AssertThat(visibility.Alpha).IsLess(1f);
            visibility.NotifyDamaged();
            AssertFloat(visibility.Alpha).IsEqual(1f);
        }
    }

    [TestCase]
    public void AResetReturnsAPooledBodyToNeverEngaged() {
        var visibility = new OverheadBarVisibility();
        visibility.NotifyDamaged();
        visibility.Tick(0.2f);
        AssertThat(visibility.IsVisible).IsTrue();

        // OnDespawn and OnSpawn both call this; either one alone must suffice.
        visibility.Reset();

        AssertThat(visibility.HasBeenDamaged).IsFalse();
        AssertThat(visibility.IsVisible).IsFalse();
        AssertFloat(visibility.Alpha).IsEqual(0f);
        AssertFloat(visibility.RemainingSeconds).IsEqual(0f);

        // And the reset state must not "remember" its way back on the next tick.
        visibility.Tick(1f / 60f);
        AssertFloat(visibility.Alpha).IsEqual(0f);
    }

    [TestCase]
    public void ADegenerateConfigurationStillShowsSomethingRatherThanDividingByZero() {
        var visibility = new OverheadBarVisibility { QuietSeconds = 0f, FadeSeconds = 0f };

        visibility.NotifyDamaged();
        AssertFloat(visibility.Alpha).IsEqual(1f);

        visibility.Tick(1f / 60f);
        AssertFloat(visibility.Alpha).IsEqual(0f);
    }

    [TestCase]
    public void ANonPositiveDeltaDoesNotAdvanceOrRewindTheFade() {
        var visibility = new OverheadBarVisibility { QuietSeconds = 1f, FadeSeconds = 1f };
        visibility.NotifyDamaged();
        visibility.Tick(1.5f);
        float alpha = visibility.Alpha;

        visibility.Tick(0f);
        visibility.Tick(-5f);

        AssertFloat(visibility.Alpha).IsEqualApprox(alpha, 0.0001f);
    }
}
