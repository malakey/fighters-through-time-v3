using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A2. Saved volumes are applied at boot.
///
/// <para>Before this package the four saved volumes were only ever pushed onto the
/// buses by <c>SettingsMenu</c>, and only once it had been opened. A player who
/// turned the music down and relaunched got full-volume music until they went
/// looking for the slider again — the setting persisted correctly and simply did
/// nothing. The autoload order (SaveManager before AudioManager) already made the
/// boot read legal.</para>
///
/// <para>Volumes go in as <em>base</em> values under the snapshot layer, which is
/// what lets a settings change survive an active duck. These tests assert both the
/// stored base and the dB actually written to the bus.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioBootVolumeTests {

    [TestCase]
    public void ApplyingSavedVolumesPushesEveryGlobalSliderOntoItsBus() {
        AudioManager audio = Audio();
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        AssertObject(data).IsNotNull();

        float master = data.MasterVolume;
        float music = data.MusicVolume;
        float sfx = data.SFXVolume;
        float ui = data.UIVolume;
        try {
            data.MasterVolume = 0.5f;
            data.MusicVolume = 0.25f;
            data.SFXVolume = 0.75f;
            data.UIVolume = 1.0f;

            audio.ApplySavedVolumes();

            AssertThat(audio.GetBusBaseVolumeDb(AudioBuses.Master)).IsEqualApprox(Mathf.LinearToDb(0.5f), 0.01f);
            AssertThat(audio.GetBusBaseVolumeDb(AudioBuses.Music)).IsEqualApprox(Mathf.LinearToDb(0.25f), 0.01f);
            AssertThat(audio.GetBusBaseVolumeDb(AudioBuses.SFX)).IsEqualApprox(Mathf.LinearToDb(0.75f), 0.01f);
            AssertThat(audio.GetBusBaseVolumeDb(AudioBuses.UI)).IsEqualApprox(0f, 0.01f);

            AssertThat(BusDb(AudioBuses.Music)).IsEqualApprox(Mathf.LinearToDb(0.25f), 0.01f);
            AssertThat(BusDb(AudioBuses.Master)).IsEqualApprox(Mathf.LinearToDb(0.5f), 0.01f);
        } finally {
            data.MasterVolume = master;
            data.MusicVolume = music;
            data.SFXVolume = sfx;
            data.UIVolume = ui;
            audio.ApplySavedVolumes();
        }
    }

    /// <summary>
    /// A zero slider must be inaudible without producing the negative infinity
    /// <c>Mathf.LinearToDb(0)</c> returns, which Godot will not accept as a bus volume.
    /// </summary>
    [TestCase]
    public void AZeroedSliderResolvesToAFiniteSilentDecibelValue() {
        AudioManager audio = Audio();
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        float music = data.MusicVolume;
        try {
            audio.SetMusicVolume(0f);
            float db = audio.GetBusBaseVolumeDb(AudioBuses.Music);
            AssertThat(float.IsNegativeInfinity(db)).IsFalse();
            AssertThat(db <= StemDirector.SilentDb).IsTrue();
            AssertThat(float.IsNegativeInfinity(BusDb(AudioBuses.Music))).IsFalse();
        } finally {
            data.MusicVolume = music;
            audio.ApplySavedVolumes();
        }
    }

    [TestCase]
    public void SliderValuesAreClampedToTheAudibleRange() {
        AudioManager audio = Audio();
        try {
            audio.SetSFXVolume(4f);
            AssertThat(audio.GetBusBaseVolumeDb(AudioBuses.SFX)).IsEqualApprox(0f, 0.01f);

            audio.SetSFXVolume(-1f);
            AssertThat(audio.GetBusBaseVolumeDb(AudioBuses.SFX) <= StemDirector.SilentDb).IsTrue();
        } finally {
            audio.ApplySavedVolumes();
        }
    }

    /// <summary>
    /// The boot read is null-safe: the test host and any tooling that runs without a
    /// populated save must get the documented defaults rather than throwing.
    /// </summary>
    [TestCase]
    public void TheDefaultsMatchTheGlobalSaveDefaults() {
        var fresh = new GlobalSaveData();
        AssertThat(fresh.MasterVolume).IsEqual(1.0f);
        AssertThat(fresh.MusicVolume).IsEqual(0.8f);
        AssertThat(fresh.SFXVolume).IsEqual(1.0f);
        AssertThat(fresh.UIVolume).IsEqual(1.0f);
    }

    private static AudioManager Audio() {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio)
            .OverrideFailureMessage("the AudioManager autoload is not running")
            .IsNotNull();
        return audio;
    }

    private static float BusDb(string busName) => AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(busName));
}
