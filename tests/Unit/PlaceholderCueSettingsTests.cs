using FTT.Core;
using GdUnit4;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-AUDIO (gameplay review ruling 13, 2026-11-03). The "Mute placeholder cues" preference: an additive,
/// engine-free global field with no schema bump, and the cached read point that boot and the Settings
/// toggle push into.
///
/// <para><b>Pure C#, no <c>[RequireGodotRuntime]</c>:</b> the save payload and the settings class must
/// stay usable with no Godot runtime (CLAUDE.md failure signature 7).</para>
/// </summary>
[TestSuite]
public class PlaceholderCueSettingsTests {

    [TestCase]
    public void TheMuteDefaultsOffForALegacyPayloadAndRoundTrips() {
        // A payload that predates the field keeps the cues on, and normalizing it does not invent a mute.
        GlobalSaveData legacy = JsonConvert.DeserializeObject<GlobalSaveData>("{\"MasterVolume\":0.5}");
        legacy.Normalize();
        AssertThat(legacy.PlaceholderCuesMuted).IsFalse();
        AssertThat(new GlobalSaveData().PlaceholderCuesMuted).IsFalse();

        legacy.PlaceholderCuesMuted = true;
        GlobalSaveData back = JsonConvert.DeserializeObject<GlobalSaveData>(JsonConvert.SerializeObject(legacy));
        back.Normalize();
        AssertThat(back.PlaceholderCuesMuted).IsTrue();
        // It is independent of the SFX category mute, which it must not imply.
        AssertThat(back.SFXMuted).IsFalse();
    }

    [TestCase]
    public void ApplyingThePayloadSetsTheCachedValueAndOnlyAChangeRaisesTheEvent() {
        PlaceholderCueSettings.ResetForTesting();
        try {
            int raised = 0;
            bool last = false;
            PlaceholderCueSettings.Changed += value => { raised++; last = value; };
            AssertThat(PlaceholderCueSettings.Muted).IsFalse();

            var data = new GlobalSaveData { PlaceholderCuesMuted = true };
            data.ApplyPlaceholderCueSettings();
            AssertThat(PlaceholderCueSettings.Muted).IsTrue();
            AssertThat(raised).IsEqual(1);
            AssertThat(last).IsTrue();

            data.ApplyPlaceholderCueSettings(); // the same value again raises nothing
            AssertThat(raised).IsEqual(1);

            data.PlaceholderCuesMuted = false;
            data.ApplyPlaceholderCueSettings();
            AssertThat(PlaceholderCueSettings.Muted).IsFalse();
            AssertThat(raised).IsEqual(2);
        } finally {
            PlaceholderCueSettings.ResetForTesting();
        }
    }
}
