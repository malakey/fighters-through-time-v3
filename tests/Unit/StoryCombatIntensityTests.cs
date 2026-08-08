using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B5. The Story vertical-layer policy: when a campaign scene moves
/// between the ambient bed, the combat layer, and the boss climax.
///
/// <para>Driven directly against <see cref="StoryCombatIntensityModel"/> rather than
/// through a live level, because the interesting cases are timing edges — a wave
/// that staggers, a boss that dies while adds are still up — and reproducing those
/// in a real scene would take seconds of engine time per assertion for a decision
/// that is arithmetic.</para>
/// </summary>
[TestSuite]
public class StoryCombatIntensityTests {

    [TestCase]
    public void AFreshSceneSitsOnTheAmbientBed() {
        var model = new StoryCombatIntensityModel();
        AssertThat(model.Current).IsEqual(StemIntensity.Ambient);
        AssertThat(model.Advance(0, 1f / 60f)).IsEqual(StemIntensity.Ambient);
        AssertThat(model.BossEngaged).IsFalse();
    }

    [TestCase]
    public void TheFirstEngagedEnemyRaisesCombatOnTheSameStep() {
        var model = new StoryCombatIntensityModel();
        AssertThat(model.Advance(1, 1f / 60f)).IsEqual(StemIntensity.Combat);
    }

    /// <summary>
    /// The hold is the reason a staggered wave does not make the music flap: the
    /// gap between the last enemy of one group dying and the next trigger firing is
    /// routinely under a second.
    /// </summary>
    [TestCase]
    public void CombatIsHeldForTheFullHoldAfterTheLastEnemyAndThenFallsBack() {
        var model = new StoryCombatIntensityModel { CombatHoldSeconds = 4f };
        model.Advance(2, 0.25f);

        AssertThat(model.Advance(0, 1f)).IsEqual(StemIntensity.Combat);
        AssertThat(model.Advance(0, 1f)).IsEqual(StemIntensity.Combat);
        AssertThat(model.Advance(0, 1f)).IsEqual(StemIntensity.Combat);
        AssertThat(model.HoldRemaining > 0f).IsTrue();
        AssertThat(model.Advance(0, 1f)).IsEqual(StemIntensity.Ambient);
        AssertThat(model.HoldRemaining).IsEqual(0f);
    }

    [TestCase]
    public void AnEnemyArrivingDuringTheHoldRefillsItRatherThanExtendingASlide() {
        var model = new StoryCombatIntensityModel { CombatHoldSeconds = 4f };
        model.Advance(1, 0.25f);
        model.Advance(0, 3.5f);
        AssertThat(model.HoldRemaining < 1f).IsTrue();

        AssertThat(model.Advance(1, 0.25f)).IsEqual(StemIntensity.Combat);
        AssertThat(model.HoldRemaining).IsEqual(4f);
    }

    [TestCase]
    public void ABossEngagementOutranksBothLowerLayersInEitherDirection() {
        var model = new StoryCombatIntensityModel();
        model.SetBossEngaged(true);

        // No adds at all: still climax.
        AssertThat(model.Advance(0, 0.25f)).IsEqual(StemIntensity.Climax);
        // Adds present: still climax, not merely combat.
        AssertThat(model.Advance(4, 0.25f)).IsEqual(StemIntensity.Climax);
    }

    /// <summary>
    /// A defeated boss must not slam straight to ambient: the post-boss dialogue beat
    /// plays over an arena that is usually still populated, and an abrupt silence
    /// there reads as a bug.
    /// </summary>
    [TestCase]
    public void LeavingABossFightLandsOnCombatForTheHoldRatherThanSilence() {
        var model = new StoryCombatIntensityModel { CombatHoldSeconds = 4f };
        model.SetBossEngaged(true);
        model.Advance(0, 0.25f);

        model.SetBossEngaged(false);
        AssertThat(model.Advance(0, 0.25f)).IsEqual(StemIntensity.Combat);
        AssertThat(model.Advance(0, 5f)).IsEqual(StemIntensity.Ambient);
    }

    [TestCase]
    public void ResetReturnsTheModelToAFreshScene() {
        var model = new StoryCombatIntensityModel();
        model.SetBossEngaged(true);
        model.Advance(3, 0.25f);

        model.Reset();
        AssertThat(model.Current).IsEqual(StemIntensity.Ambient);
        AssertThat(model.BossEngaged).IsFalse();
        AssertThat(model.HoldRemaining).IsEqual(0f);
        AssertThat(model.Advance(0, 0.25f)).IsEqual(StemIntensity.Ambient);
    }
}
