using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class PlaceholderAudioKitTests {
    private static readonly string[] StemPaths = {
        "res://resources/Audio/placeholder_stem_ambient.tres",
        "res://resources/Audio/placeholder_stem_combat.tres",
        "res://resources/Audio/placeholder_stem_boss.tres"
    };

    private static readonly string[] SfxPaths = {
        "res://resources/Audio/placeholder_sfx_hit.tres",
        "res://resources/Audio/placeholder_sfx_footstep.tres",
        "res://resources/Audio/placeholder_sfx_ui.tres",
        "res://resources/Audio/placeholder_sfx_pickup.tres",
        "res://resources/Audio/placeholder_sfx_hazard.tres",
        "res://resources/Audio/placeholder_sfx_dialogue_chirp.tres"
    };

    [TestCase]
    public void MusicStemsAreLoopableStreamsWithSharedLoopLength() {
        foreach (string path in StemPaths) {
            AudioStreamWav stream = ResourceLoader.Load<AudioStreamWav>(path);
            AssertObject(stream).IsNotNull();
            AssertThat(stream.LoopMode).IsEqual(AudioStreamWav.LoopModeEnum.Forward);
            AssertThat(stream.Data.Length > 0).IsTrue();
            // Stems must share one loop length so layered playback stays synchronized.
            AssertThat(stream.LoopEnd).IsEqual((int)stream.MixRate);
        }
    }

    [TestCase]
    public void SfxOneShotsDoNotLoopAndCarryAudioData() {
        foreach (string path in SfxPaths) {
            AudioStreamWav stream = ResourceLoader.Load<AudioStreamWav>(path);
            AssertObject(stream).IsNotNull();
            AssertThat(stream.LoopMode).IsEqual(AudioStreamWav.LoopModeEnum.Disabled);
            AssertThat(stream.Data.Length > 0).IsTrue();
        }
    }
}
