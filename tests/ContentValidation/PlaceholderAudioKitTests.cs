using System.Collections.Generic;
using FTT.Core;
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

    /// <summary>
    /// GR-AUDIO (2026-11-03): the generator's manifest states, for every <c>.ogg</c> it writes, whether
    /// the asset is audible, its peak and its length. Audible assets stay under the 0.3 ceiling and are
    /// not silence; silent ones have a peak of zero; each is a short non-looping stream whose length
    /// matches the manifest; and the audible set is exactly the catalog's audible rows' assets.
    /// </summary>
    [TestCase]
    public void TheGeneratorManifestDescribesEveryOggAndTheAudibleOnesStayUnderThePeakCeiling() {
        Dictionary<string, PlaceholderCueManifest.Asset> manifest = PlaceholderCueManifest.Load();
        AssertThat(manifest.Count > 0).OverrideFailureMessage("the manifest is missing or empty").IsTrue();
        var issues = new List<string>();
        var audibleAssets = new HashSet<string>();
        foreach (KeyValuePair<string, PlaceholderCueManifest.Asset> pair in manifest) {
            AudioStream stream = ResourceLoader.Load<AudioStream>("res://" + pair.Key);
            if (stream == null) { issues.Add($"{pair.Key} does not load"); continue; }
            if (stream is AudioStreamOggVorbis ogg && ogg.Loop) issues.Add($"{pair.Key} loops");
            if (System.Math.Abs(stream.GetLength() - pair.Value.Seconds) > 0.02) {
                issues.Add($"{pair.Key} is {stream.GetLength():0.000}s, the manifest says {pair.Value.Seconds}");
            }
            if (pair.Value.Audible) {
                audibleAssets.Add(pair.Key);
                if (pair.Value.Peak > 0.3 + 1e-6) issues.Add($"{pair.Key} peaks at {pair.Value.Peak}");
                if (pair.Value.Peak < 0.05) issues.Add($"{pair.Key} is flagged audible but is silent");
                if (pair.Value.Seconds > 0.5) issues.Add($"{pair.Key} is {pair.Value.Seconds}s: placeholder cues are short");
            } else if (pair.Value.Peak != 0.0) {
                issues.Add($"{pair.Key} is flagged silent but peaks at {pair.Value.Peak}");
            }
        }
        // The audible assets are exactly what the audible catalog rows play.
        var rowAssets = new HashSet<string>();
        CoreMechanicCueCatalog catalog = CoreMechanicCueCatalog.LoadDefault();
        foreach (string id in CoreMechanicCueIDs.AudiblePlaceholders) {
            string key = PlaceholderCueManifest.KeyFor(catalog.Find(id)?.Stream);
            if (key == null) issues.Add($"{id} does not play a generator asset"); else rowAssets.Add(key);
        }
        if (!audibleAssets.SetEquals(rowAssets)) {
            issues.Add("audible assets " + string.Join(",", audibleAssets) + " differ from the rows' " + string.Join(",", rowAssets));
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
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
