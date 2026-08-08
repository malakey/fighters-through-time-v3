using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B3. The nine authored dialogue chirp pitches.
///
/// <para>design-godot.md's Dialogue Presentation section asks for
/// "character-specific pitch-modulated text chirps ... lower, weightier
/// square-wave sounds for Lincoln; fast, whimsical triangle-wave tones for
/// Mozart". One placeholder sample is pitch-shifted per character rather than
/// nine samples being authored, so the tuning lives in the character resources
/// and production audio can replace the sample without retuning the roster.</para>
///
/// <para>The failure this guards is the quiet one: a roster resource that never
/// gets a value simply chirps at 1.0 like everyone else, and the feature looks
/// implemented while conveying nothing. Distinctness is therefore the
/// contract, not just presence.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DialogueChirpPitchTests {

    private static readonly string[] RosterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    /// <summary>Below this the chirp sounds broken rather than low; above it, shrill.</summary>
    private const float MinPitch = 0.5f;
    private const float MaxPitch = 2.0f;

    /// <summary>Closer than this and two characters are indistinguishable in play.</summary>
    private const float MinSeparation = 0.04f;

    private static CharacterData Load(string characterID) {
        string path = $"res://resources/Characters/{characterID}_data.tres";
        AssertThat(ResourceLoader.Exists(path))
            .OverrideFailureMessage($"Roster resource missing at {path}").IsTrue();
        var data = AuthoredResources.Load<CharacterData>(path);
        AssertObject(data).IsNotNull();
        return data;
    }

    [TestCase]
    public void EveryRosterCharacterAuthorsAChirpPitchInTheAudibleBand() {
        var issues = new List<string>();
        foreach (string id in RosterIDs) {
            float pitch = Load(id).DialogueChirpPitch;
            if (pitch < MinPitch || pitch > MaxPitch) {
                issues.Add($"{id} chirp pitch {pitch} is outside [{MinPitch}, {MaxPitch}]");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void NoTwoCharactersChirpAtTheSamePitch() {
        var issues = new List<string>();
        for (int left = 0; left < RosterIDs.Length; left++) {
            float leftPitch = Load(RosterIDs[left]).DialogueChirpPitch;
            for (int right = left + 1; right < RosterIDs.Length; right++) {
                float rightPitch = Load(RosterIDs[right]).DialogueChirpPitch;
                if (Mathf.Abs(leftPitch - rightPitch) < MinSeparation) {
                    issues.Add(
                        $"{RosterIDs[left]} ({leftPitch}) and {RosterIDs[right]} ({rightPitch}) " +
                        "are too close to tell apart");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void LincolnSitsAtTheBottomOfTheRangeAndMozartAtTheTop() {
        // The two the design names by example; the other seven are spread between.
        float lincoln = Load("lincoln").DialogueChirpPitch;
        float mozart = Load("mozart").DialogueChirpPitch;

        foreach (string id in RosterIDs) {
            float pitch = Load(id).DialogueChirpPitch;
            AssertThat(pitch >= lincoln)
                .OverrideFailureMessage($"{id} ({pitch}) chirps lower than Lincoln ({lincoln}).")
                .IsTrue();
            AssertThat(pitch <= mozart)
                .OverrideFailureMessage($"{id} ({pitch}) chirps higher than Mozart ({mozart}).")
                .IsTrue();
        }
        AssertThat(lincoln < 1f)
            .OverrideFailureMessage("Lincoln should read as weightier than the neutral chirp.").IsTrue();
        AssertThat(mozart > 1f)
            .OverrideFailureMessage("Mozart should read as brighter than the neutral chirp.").IsTrue();
    }

    [TestCase]
    public void TheFieldDefaultsToTheNeutralPitchSoAnUnauthoredResourceIsNotDetuned() {
        var fresh = new CharacterData();
        AssertThat(fresh.DialogueChirpPitch).IsEqual(1.0f);
        AssertThat(DialogueManager.NeutralChirpPitch)
            .OverrideFailureMessage(
                "Narration and NPC speakers must chirp at the same neutral pitch the field defaults to.")
            .IsEqual(1.0f);
    }
}
