using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit H-9: the mixer machinery passed its unit tests while three of its four
/// designed triggers (Pause, LowHealth, UltimateCinematic) were never applied by
/// anything. This suite pins the trigger layer on <see cref="AudioManager"/>:
/// pause-state edges, Story HP events against the 20% threshold, the timed
/// ultimate window, the scene-transition clear, and layering with Rewind.
///
/// <para>The pause trigger is exercised through
/// <see cref="AudioManager.ObservePauseState"/> rather than by pausing the real
/// tree: a leaked <c>SceneTree.Paused</c> hangs the whole GdUnit session
/// (CLAUDE.md failure signature 4), and the method is exactly what
/// <c>_Process</c> feeds the live flag into.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioSnapshotTriggerTests {

    [TestCase]
    public void APauseEdgeAppliesTheSnapshotAndTheReleaseEdgeRemovesIt() {
        RunWithCleanSnapshots(audio => {
            audio.ObservePauseState(true);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Pause)).IsTrue();

            // Level-triggered: repeating the same state must not stack.
            audio.ObservePauseState(true);
            AssertThat(audio.Snapshots.ActiveSnapshotCount).IsEqual(1);

            audio.ObservePauseState(false);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Pause)).IsFalse();
        });
    }

    [TestCase]
    public void TheTwentyPercentThresholdMatchesTheDesign() {
        AssertThat(AudioManager.IsLowHealthState(19f, 100f)).IsTrue();
        AssertThat(AudioManager.IsLowHealthState(0f, 100f)).IsTrue();
        AssertThat(AudioManager.IsLowHealthState(20f, 100f)).IsFalse();
        AssertThat(AudioManager.IsLowHealthState(100f, 100f)).IsFalse();
        AssertThat(AudioManager.IsLowHealthState(5f, 0f)).IsFalse();
    }

    [TestCase]
    public void AStoryHpEventBelowThresholdEngagesLowHealthAndAHealReleasesIt() {
        RunWithCleanSnapshots(audio => {
            EventBus.Instance.RaisePlayerHPChanged(new PlayerHPPayload {
                PlayerIndex = 0, CurrentHP = 15f, MaxHP = 100f, DamageAmount = 10f
            });
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.LowHealth)).IsTrue();

            // A heal back above the threshold — the same event stream a rewind
            // restore or respawn produces — releases it.
            EventBus.Instance.RaisePlayerHPChanged(new PlayerHPPayload {
                PlayerIndex = 0, CurrentHP = 55f, MaxHP = 100f, DamageAmount = -40f
            });
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.LowHealth)).IsFalse();
        });
    }

    /// <summary>
    /// The Mirror Paradox clone reports HP as player 1 and must never muffle the
    /// mix; in Fighter Mode no controller HP events fire at all and the driver
    /// feeds <see cref="AudioManager.SetLowHealth"/> from simulation state.
    /// </summary>
    [TestCase]
    public void ANonHeroHpEventNeverDrivesTheLowHealthSnapshot() {
        RunWithCleanSnapshots(audio => {
            EventBus.Instance.RaisePlayerHPChanged(new PlayerHPPayload {
                PlayerIndex = 1, CurrentHP = 5f, MaxHP = 100f, DamageAmount = 10f
            });
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.LowHealth)).IsFalse();
        });
    }

    [TestCase]
    public void TheUltimateWindowOpensOnActivationAndExpiresThroughTheFadePump() {
        RunWithCleanSnapshots(audio => {
            audio.BeginUltimateWindow(0.5f);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsTrue();

            audio.AdvanceFades(0.25);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsTrue();

            audio.AdvanceFades(0.5);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsFalse();
        });
    }

    [TestCase]
    public void AnUltimateActivationEventFromTheBusOpensTheWindow() {
        RunWithCleanSnapshots(audio => {
            EventBus.Instance.RaiseUltimateActivation(new UltimateActivationPayload {
                PlayerIndex = 0, AbilityID = "einstein_ultimate"
            });
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsTrue();

            // Re-activation extends rather than stacks.
            audio.BeginUltimateWindow(0.5f);
            AssertThat(audio.Snapshots.ActiveSnapshotCount).IsEqual(1);
        });
    }

    [TestCase]
    public void ASceneTransitionClearsTheTransientGameplaySnapshots() {
        RunWithCleanSnapshots(audio => {
            audio.SetLowHealth(true);
            audio.BeginUltimateWindow();
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.LowHealth)).IsTrue();
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsTrue();

            audio.OnSceneTransitionStarted();
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.LowHealth)).IsFalse();
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsFalse();
            // The window really is disarmed, not merely released: pumping time
            // afterwards must not re-release or throw.
            audio.AdvanceFades(5.0);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.Ultimate)).IsFalse();
        });
    }

    /// <summary>
    /// The new triggers must layer with the pre-existing Rewind duck exactly as
    /// the mixer's stacking contract promises: offsets sum, and releasing the
    /// rewind falls back to the low-health duck rather than to transparent.
    /// </summary>
    [TestCase]
    public void LowHealthLayersUnderAndSurvivesTheRewindDuck() {
        RunWithCleanSnapshots(audio => {
            audio.SetLowHealth(true);
            audio.ApplySnapshot(AudioSnapshot.Rewind, -12f);
            AssertThat(audio.Snapshots.ActiveSnapshotCount).IsEqual(2);
            // LowHealth music offset is -2; stacked with the -12 rewind duck.
            AssertThat(audio.Snapshots.GetTargetOffsetDb(AudioBuses.Music))
                .IsEqualApprox(-14f, 0.001f);

            audio.ReleaseSnapshot(AudioSnapshot.Rewind);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.LowHealth)).IsTrue();
            AssertThat(audio.Snapshots.GetTargetOffsetDb(AudioBuses.Music))
                .IsEqualApprox(-2f, 0.001f);
        });
    }

    /// <summary>
    /// Runs the body against the live autoload and hands every snapshot back in a
    /// finally block; a leaked duck would quietly follow the rest of the suite.
    /// </summary>
    private static void RunWithCleanSnapshots(System.Action<AudioManager> body) {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio).IsNotNull();
        try {
            body(audio);
        } finally {
            audio.ObservePauseState(false);
            audio.OnSceneTransitionStarted();
            audio.ReleaseSnapshot(AudioSnapshot.Rewind);
            audio.Snapshots.SettleImmediately();
            audio.ApplySavedVolumes();
        }
    }
}
