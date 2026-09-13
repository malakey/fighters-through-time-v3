using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A2. The named snapshot/duck layer from <c>design-godot.md</c>'s
/// "Audio Snapshots" section.
///
/// <para>The behaviour under test that actually cost a bug: snapshots are
/// <em>offsets</em> over the settings-owned base volume. The rewind duck this
/// replaces captured the Music bus's absolute dB on entry and restored it on exit,
/// so a volume change made during a rewind was thrown away when the rewind ended.
/// <see cref="ASettingsChangeDuringAnActiveSnapshotSurvivesItsRelease"/> is the
/// regression guard.</para>
///
/// <para>Each test builds its own mixer. They do write to the real AudioServer
/// buses, so every one restores the bus volumes it touched in a finally block —
/// a leaked -12 dB duck would quietly follow the rest of the suite around.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioSnapshotMixerTests {

    [TestCase]
    public void ANewMixerIsTransparent() {
        RunWithRestoredBuses(mixer => {
            AssertThat(mixer.ActiveSnapshotCount).IsEqual(0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqual(0f);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqual(AudioSnapshotMixer.TransparentCutoffHz);
            AssertThat(mixer.IsSnapshotActive(AudioSnapshot.Pause)).IsFalse();
        });
    }

    [TestCase]
    public void ApplyingASnapshotTweensTheOffsetInRatherThanSnapping() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.Ultimate);

            AssertThat(mixer.IsSnapshotActive(AudioSnapshot.Ultimate)).IsTrue();
            AssertThat(mixer.GetTargetOffsetDb(AudioBuses.Music)).IsEqual(-12f);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqual(0f);
            AssertThat(mixer.IsFading).IsTrue();

            mixer.AdvanceFades(1.0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-12f, 0.001f);
            AssertThat(mixer.GetOffsetDb(AudioBuses.SFX)).IsEqualApprox(-12f, 0.001f);
            AssertThat(mixer.IsFading).IsFalse();
        });
    }

    /// <summary>
    /// Package 11 A8 / C01b: profiles do not stack. Only the highest-priority
    /// active request is applied, and releasing it re-selects from what is left
    /// rather than jumping back to Normal.
    ///
    /// <para>The old behaviour summed the offsets, so Pause over Ultimate ducked
    /// music by 20 dB — exactly the "a prior 12 dB rewind duck cannot make it
    /// 24 dB" case the contract names.</para>
    /// </summary>
    [TestCase]
    public void OnlyTheHighestPriorityProfileIsAppliedAndReleaseReselects() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.Ultimate);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-12f, 0.001f);
            AssertThat(mixer.SelectedSnapshot).IsEqual(AudioSnapshot.Ultimate);

            // Pause outranks Ultimate; the result is Pause ALONE, not the sum.
            mixer.ApplySnapshot(AudioSnapshot.Pause);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.ActiveSnapshotCount).IsEqual(2);
            AssertThat(mixer.SelectedSnapshot).IsEqual(AudioSnapshot.Pause);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-8f, 0.001f);

            // Releasing the winner falls back to the next real request.
            mixer.ReleaseSnapshot(AudioSnapshot.Pause);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.SelectedSnapshot).IsEqual(AudioSnapshot.Ultimate);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-12f, 0.001f);

            mixer.ReleaseSnapshot(AudioSnapshot.Ultimate);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.HasSelection).IsFalse();
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(0f, 0.001f);
        });
    }

    /// <summary>
    /// C01b forbids an automatic UI boost: "UI/dialogue stays clear at the user's
    /// configured level, without automatic boost." The Pause profile used to lift
    /// UI by +2 dB, and UI is no longer a mixed bus at all.
    /// </summary>
    [TestCase]
    public void NoProfileTouchesTheUiBus() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.Pause);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.GetTargetOffsetDb(AudioBuses.UI)).IsEqual(0f);
            AssertThat(mixer.GetOffsetDb(AudioBuses.UI)).IsEqual(0f);
            AssertThat(System.Array.IndexOf(AudioSnapshotMixer.MixedBuses, AudioBuses.UI)).IsEqual(-1);
        });
    }

    /// <summary>
    /// Cutoffs are selected, not combined. The winning profile's cutoff is the
    /// one that applies; releasing it restores the next winner's, not the
    /// most-aggressive-ever.
    /// </summary>
    [TestCase]
    public void FilterCutoffFollowsTheSelectedProfileOnly() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.LowHealth);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqualApprox(3200f, 0.1f);

            mixer.ApplySnapshot(AudioSnapshot.Pause);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqualApprox(900f, 0.1f);

            mixer.ReleaseSnapshot(AudioSnapshot.Pause);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqualApprox(3200f, 0.1f);

            mixer.ReleaseSnapshot(AudioSnapshot.LowHealth);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqualApprox(AudioSnapshotMixer.TransparentCutoffHz, 0.1f);
        });
    }

    /// <summary>
    /// A C01b priority row with no authored treatment "adds none" — but it still
    /// WINS, which is how Time Freeze suppresses a low-health heartbeat rather
    /// than playing under it.
    /// </summary>
    [TestCase]
    public void AHigherRowWithNoAuthoredTreatmentStillSuppressesALowerOne() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.LowHealth);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-2f, 0.001f);

            mixer.ApplySnapshot(AudioSnapshot.TimeFreeze);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.SelectedSnapshot).IsEqual(AudioSnapshot.TimeFreeze);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(0f, 0.001f);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqualApprox(AudioSnapshotMixer.TransparentCutoffHz, 0.1f);

            mixer.ReleaseSnapshot(AudioSnapshot.TimeFreeze);
            mixer.AdvanceFades(2.0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-2f, 0.001f);
        });
    }

    /// <summary>
    /// Repeated transitions must return to the selected target rather than
    /// accumulating attenuation — the measurable half of C01b's "rapid
    /// transitions must not accumulate attenuation, filters or queued
    /// transitions".
    /// </summary>
    [TestCase]
    public void RepeatedTransitionsDoNotAccumulateAttenuation() {
        RunWithRestoredBuses(mixer => {
            for (int pass = 0; pass < 6; pass++) {
                mixer.ApplySnapshot(AudioSnapshot.Ultimate);
                mixer.AdvanceFades(1.0);
                mixer.ReleaseSnapshot(AudioSnapshot.Ultimate);
                mixer.AdvanceFades(1.0);
            }
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(0f, 0.001f);

            mixer.ApplySnapshot(AudioSnapshot.Ultimate);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-12f, 0.001f);
        });
    }

    /// <summary>
    /// The rewind path passes the authored <c>MusicDuckDecibels</c> through, so the
    /// resource keeps owning the number rather than the snapshot table.
    /// </summary>
    [TestCase]
    public void AnAppliedSnapshotAcceptsACallerSuppliedMusicOffset() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.Rewind, -6f);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-6f, 0.001f);

            // Re-applying an active snapshot updates it in place instead of stacking.
            mixer.ApplySnapshot(AudioSnapshot.Rewind, -18f);
            mixer.AdvanceFades(1.0);
            AssertThat(mixer.ActiveSnapshotCount).IsEqual(1);
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(-18f, 0.001f);
        });
    }

    /// <summary>
    /// The regression guard for the duck this workstream replaced: a settings change
    /// while a snapshot is active must survive the snapshot's release.
    /// </summary>
    [TestCase]
    public void ASettingsChangeDuringAnActiveSnapshotSurvivesItsRelease() {
        RunWithRestoredBuses(mixer => {
            int musicBus = AudioServer.GetBusIndex(AudioBuses.Music);
            mixer.SetBaseVolume(AudioBuses.Music, -4f);
            mixer.ApplySnapshot(AudioSnapshot.Rewind, -12f);
            mixer.AdvanceFades(1.0);
            AssertThat(AudioServer.GetBusVolumeDb(musicBus)).IsEqualApprox(-16f, 0.01f);

            // Player drags the music slider mid-rewind.
            mixer.SetBaseVolume(AudioBuses.Music, -10f);
            AssertThat(AudioServer.GetBusVolumeDb(musicBus)).IsEqualApprox(-22f, 0.01f);

            mixer.ReleaseSnapshot(AudioSnapshot.Rewind);
            mixer.AdvanceFades(1.0);
            // The new base survives; the old absolute-dB approach would have
            // restored -4 here.
            AssertThat(mixer.GetBaseDb(AudioBuses.Music)).IsEqualApprox(-10f, 0.001f);
            AssertThat(AudioServer.GetBusVolumeDb(musicBus)).IsEqualApprox(-10f, 0.01f);
        });
    }

    [TestCase]
    public void ReleaseAllClearsEverySnapshotAndSettleFinishesTheBlendImmediately() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.Pause);
            mixer.ApplySnapshot(AudioSnapshot.LowHealth);
            AssertThat(mixer.ActiveSnapshotCount).IsEqual(2);

            mixer.ReleaseAllSnapshots();
            AssertThat(mixer.ActiveSnapshotCount).IsEqual(0);
            AssertThat(mixer.HasSelection).IsFalse();
            AssertThat(mixer.IsFading).IsTrue();

            mixer.SettleImmediately();
            AssertThat(mixer.IsFading).IsFalse();
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(0f, 0.001f);
            AssertThat(mixer.CurrentMusicCutoffHz).IsEqualApprox(AudioSnapshotMixer.TransparentCutoffHz, 0.1f);
        });
    }

    [TestCase]
    public void ReleasingASnapshotThatWasNeverAppliedChangesNothing() {
        RunWithRestoredBuses(mixer => {
            mixer.ApplySnapshot(AudioSnapshot.Pause);
            mixer.AdvanceFades(1.0);
            float before = mixer.GetOffsetDb(AudioBuses.Music);

            mixer.ReleaseSnapshot(AudioSnapshot.Ultimate);
            AssertThat(mixer.IsFading).IsFalse();
            AssertThat(mixer.GetOffsetDb(AudioBuses.Music)).IsEqualApprox(before, 0.001f);
        });
    }

    /// <summary>
    /// Runs the body against a fresh mixer and puts every bus volume and filter
    /// cutoff back afterwards; the AudioServer is process-global state shared with
    /// the rest of the suite and the running autoload.
    /// </summary>
    private static void RunWithRestoredBuses(System.Action<AudioSnapshotMixer> body) {
        var restore = new System.Collections.Generic.Dictionary<string, float>();
        foreach (string bus in AudioSnapshotMixer.BasedBuses) {
            int index = AudioServer.GetBusIndex(bus);
            if (index >= 0) restore[bus] = AudioServer.GetBusVolumeDb(index);
        }
        try {
            body(new AudioSnapshotMixer());
        } finally {
            // Clear any filter cutoff this test drove, then hand the bus volumes
            // back: through the live mixer when there is one (it knows its own
            // offsets), otherwise from the captured values.
            new AudioSnapshotMixer().SettleImmediately();
            if (AudioManager.Instance != null) {
                AudioManager.Instance.ApplySavedVolumes();
            } else {
                foreach (System.Collections.Generic.KeyValuePair<string, float> entry in restore) {
                    int index = AudioServer.GetBusIndex(entry.Key);
                    if (index >= 0) AudioServer.SetBusVolumeDb(index, entry.Value);
                }
            }
        }
    }
}
