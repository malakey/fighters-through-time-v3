using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 A2. The three placeholder one-shot cue assets under <c>audio/sfx/</c>.
///
/// <para>These are pinned by path, not just by existence. <c>FighterSimulationDriver</c>
/// holds the KO stinger and victory fanfare paths as string constants and calls them
/// through <c>ResourceLoader.Exists</c> — so a missing or moved file does not error,
/// it silently plays nothing. That is exactly the failure this suite exists to catch:
/// the cue call sites were already written and had been no-oping since Package 6.</para>
///
/// <para>The complementary <c>PlaceholderAudioKitTests</c> covers the older
/// <c>.tres</c> stem/SFX kit; this suite covers only the assets A2 authored.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlaceholderCueAssetTests {
    private static readonly string[] CuePaths = {
        AudioManager.KnockoutStingerPath,
        AudioManager.VictoryFanfarePath,
        AudioManager.CountdownBlipPath
    };

    [TestCase]
    public void EveryCueAssetExistsAtThePathItsCallSiteReferences() {
        AssertThat(AudioManager.KnockoutStingerPath).IsEqual("res://audio/sfx/combat/ko_stinger.ogg");
        AssertThat(AudioManager.VictoryFanfarePath).IsEqual("res://audio/sfx/ui/victory_fanfare.ogg");
        AssertThat(AudioManager.CountdownBlipPath).IsEqual("res://audio/sfx/ui/countdown_blip.ogg");

        foreach (string path in CuePaths) {
            AssertThat(ResourceLoader.Exists(path))
                .OverrideFailureMessage($"cue asset missing: {path}")
                .IsTrue();
        }
    }

    [TestCase]
    public void EveryCueAssetLoadsAsAPlayableStream() {
        foreach (string path in CuePaths) {
            AudioStream stream = ResourceLoader.Load<AudioStream>(path);
            AssertObject(stream)
                .OverrideFailureMessage($"cue asset did not load: {path}")
                .IsNotNull();
            AssertThat(stream.GetLength() > 0.0)
                .OverrideFailureMessage($"cue asset has no duration: {path}")
                .IsTrue();
        }
    }

    /// <summary>
    /// One-shots must not loop and must be short. A looping KO stinger would never
    /// release its pooled voice, and the pool's steal-oldest policy would then chew
    /// through the whole pool over a match.
    /// </summary>
    [TestCase]
    public void CueAssetsAreShortNonLoopingOneShots() {
        foreach (string path in CuePaths) {
            AudioStream stream = ResourceLoader.Load<AudioStream>(path);
            if (stream is AudioStreamOggVorbis ogg) {
                AssertThat(ogg.Loop)
                    .OverrideFailureMessage($"cue asset loops: {path}")
                    .IsFalse();
            }
            AssertThat(stream.GetLength() < 3.0)
                .OverrideFailureMessage($"cue asset is longer than a one-shot should be: {path}")
                .IsTrue();
        }
    }
}
