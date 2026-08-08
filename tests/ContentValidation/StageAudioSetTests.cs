using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Contract for the per-stage Fighter music sets, in the
/// <see cref="PlaceholderAudioKitTests"/> style. The ten authored
/// <c>resources/Audio/stage_*_audio.tres</c> sets land with their stages in
/// Package 6 Phase B; this suite pins the schema and the stem rules those sets
/// must satisfy, and exposes <see cref="Validate"/> so each per-stage test can
/// check its own set with one call.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StageAudioSetTests {
    private const string AmbientStemPath = "res://resources/Audio/placeholder_stem_ambient.tres";
    private const string CombatStemPath = "res://resources/Audio/placeholder_stem_combat.tres";
    private const string ClimaxStemPath = "res://resources/Audio/placeholder_stem_boss.tres";

    /// <summary>
    /// Returns one message per contract violation; empty means the set is valid.
    /// Phase B per-stage suites call this against their authored <c>.tres</c>.
    /// </summary>
    public static List<string> Validate(StageAudioSet set, string expectedSetID, string expectedStageID) {
        var issues = new List<string>();
        if (set == null) { issues.Add("audio set is null"); return issues; }

        if (set.SchemaVersion != 1) issues.Add($"schema version {set.SchemaVersion}, expected 1");
        if (set.SetID != expectedSetID) issues.Add($"SetID '{set.SetID}', expected '{expectedSetID}'");
        if (set.StageID != expectedStageID) issues.Add($"StageID '{set.StageID}', expected '{expectedStageID}'");
        if (set.CrossfadeSeconds <= 0f) issues.Add("CrossfadeSeconds must be greater than zero");
        if (set.LoopSeconds <= 0f) issues.Add("LoopSeconds must be greater than zero");

        string[] names = { "AmbientStem", "CombatStem", "ClimaxStem" };
        AudioStream[] stems = set.Stems();
        for (int index = 0; index < stems.Length; index++) {
            if (stems[index] is not AudioStreamWav wav) {
                issues.Add($"{names[index]} is not an AudioStreamWav");
                continue;
            }
            if (wav.LoopMode != AudioStreamWav.LoopModeEnum.Forward) {
                issues.Add($"{names[index]} does not loop forward");
            }
            if (wav.Data.Length == 0) issues.Add($"{names[index]} carries no audio data");
            // Layered stems must share one loop length or they drift apart as the
            // mix changes mid-match.
            if (wav.LoopEnd != (int)wav.MixRate) {
                issues.Add($"{names[index]} loop length {wav.LoopEnd} does not match its mix rate {wav.MixRate}");
            }
        }
        return issues;
    }

    [TestCase]
    public void ANewStageAudioSetDefaultsToASynchronizedCrossfadingSchema() {
        var set = new StageAudioSet();
        AssertThat(set.SchemaVersion).IsEqual(1);
        AssertThat(set.SetID).IsEqual("");
        AssertThat(set.StageID).IsEqual("");
        AssertThat(set.StemsAreSynchronized).IsTrue();
        AssertThat(set.CrossfadeSeconds > 0f).IsTrue();
        AssertThat(set.LoopSeconds > 0f).IsTrue();
        AssertThat(set.Stems().Length).IsEqual(3);
    }

    [TestCase]
    public void AStageSetBuiltFromThePlaceholderKitSatisfiesTheStemContract() {
        var set = BuildPlaceholderSet("audio_stage_florence", "florence_workshop");
        List<string> issues = Validate(set, "audio_stage_florence", "florence_workshop");
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The validator must actually reject a broken set — nine Phase B stages will
    /// lean on it, so an always-passing check would be worse than none.
    /// </summary>
    [TestCase]
    public void ValidatorRejectsAMissingStemAndAMismatchedIdentity() {
        var set = BuildPlaceholderSet("audio_stage_florence", "florence_workshop");
        set.ClimaxStem = null;
        set.CrossfadeSeconds = 0f;

        List<string> issues = Validate(set, "audio_stage_orleans", "orleans_vanguard");
        AssertThat(issues.Count >= 4).IsTrue();
    }

    private static StageAudioSet BuildPlaceholderSet(string setID, string stageID) => new() {
        SetID = setID,
        StageID = stageID,
        AmbientStem = ResourceLoader.Load<AudioStreamWav>(AmbientStemPath),
        CombatStem = ResourceLoader.Load<AudioStreamWav>(CombatStemPath),
        ClimaxStem = ResourceLoader.Load<AudioStreamWav>(ClimaxStemPath)
    };
}
