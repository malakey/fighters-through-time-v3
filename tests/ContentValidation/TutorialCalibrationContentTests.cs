using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Audit M-3 content: the tutorial dialogue set carries the block and ultimate
/// calibration sequences and every key the new steps show is authored in
/// localization/en.csv. The compiled-translation resolution of the dlg_* family
/// is swept by <c>CampaignLocalizationTests</c>; this suite pins the tutorial's
/// specific structure so a renamed sequence ID (which the controller starts by
/// literal) or a dropped line key fails by name.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TutorialCalibrationContentTests {

    private const string DialogueSetPath = "res://resources/Dialogue/level_00_dialogue.tres";

    [TestCase]
    public void TheTutorialDialogueSetCarriesTheCalibrationSequencesInOrder() {
        var set = AuthoredResources.Load<DialogueSetData>(DialogueSetPath);
        AssertObject(set).IsNotNull();
        AssertThat(set.DialogueSetID).IsEqual("dialogue_level_00");

        // Package 11 A6: the V7.5 First Strike opens at THIS character's historic
        // nexus, so level_00.intro carries one hero variant per roster member after
        // the five shared calibration beats. The base order is what the tutorial
        // flow steps through; the variants are selected by hero ID, never played in
        // sequence, so they are excluded from the ordering pin.
        DialogueSequenceData[] sequences = (set.Sequences ?? Array.Empty<DialogueSequenceData>())
            .Where(sequence => sequence != null)
            .ToArray();
        string[] baseIDs = sequences
            .Where(sequence => string.IsNullOrEmpty(sequence.HeroConditionCharacterID))
            .Select(sequence => sequence.DialogueID)
            .ToArray();
        AssertThat(string.Join(",", baseIDs)).IsEqual(
            "level_00.intro,level_00.block_intro,level_00.ultimate_intro," +
            "level_00.mobility_intro,level_00.complete");

        // Every hero variant is a full replacement for the shared opening, so it
        // must be reachable as "level_00.intro@<heroID>" and declare its hero.
        foreach (DialogueSequenceData variant in sequences
                     .Where(sequence => !string.IsNullOrEmpty(sequence.HeroConditionCharacterID))) {
            AssertThat(variant.DialogueID).IsEqual("level_00.intro@" + variant.HeroConditionCharacterID);
        }

        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertThat(sequence.SpeakerNameKeys.Length).OverrideFailureMessage(
                $"{sequence.DialogueID}: speaker/line key counts diverge.")
                .IsEqual(sequence.LineKeys.Length);
            AssertThat(sequence.EmotionKeys.Length).OverrideFailureMessage(
                $"{sequence.DialogueID}: emotion/line key counts diverge.")
                .IsEqual(sequence.LineKeys.Length);
        }
    }

    [TestCase]
    public void TheNewLessonSequencesCarryTheirAuthoredLines() {
        var set = AuthoredResources.Load<DialogueSetData>(DialogueSetPath);
        AssertObject(set).IsNotNull();

        DialogueSequenceData block = set.Find("level_00.block_intro");
        AssertObject(block).IsNotNull();
        AssertThat(string.Join(",", block.LineKeys)).IsEqual("dlg_l00_block_1,dlg_l00_block_2");
        AssertThat(block.PausesGameplay).IsTrue();

        DialogueSequenceData ultimate = set.Find("level_00.ultimate_intro");
        AssertObject(ultimate).IsNotNull();
        AssertThat(string.Join(",", ultimate.LineKeys)).IsEqual("dlg_l00_ultimate_1");

        // The mobility briefing gained the movement-ability gate instruction.
        DialogueSequenceData mobility = set.Find("level_00.mobility_intro");
        AssertObject(mobility).IsNotNull();
        AssertThat(string.Join(",", mobility.LineKeys)).IsEqual("dlg_l00_mobility_1,dlg_l00_mobility_2");
    }

    [TestCase]
    public void EveryKeyTheNewStepsShowIsAuthoredInTheCsv() {
        HashSet<string> csvKeys = EnglishCsvKeys();

        string[] required = {
            // Tutorial objective and gate copy (audit M-3).
            "tutorial_step_block",
            "tutorial_step_ultimate",
            "tutorial_step_movement",
            "tutorial_gate_movement",
            // Dialogue lines behind the new sequences.
            "dlg_l00_block_1",
            "dlg_l00_block_2",
            "dlg_l00_ultimate_1",
            "dlg_l00_mobility_2",
            // Story pause additions (audit M-2).
            "menu_save",
            "menu_restart",
            "pause_save_done",
            "pause_restart_confirm",
            "pause_quit_confirm",
            "pause_quit_to_menu"
        };

        string[] missing = required.Where(key => !csvKeys.Contains(key)).ToArray();
        AssertThat(missing.Length).OverrideFailureMessage(
            "Keys missing from localization/en.csv: " + string.Join(", ", missing))
            .IsEqual(0);
    }

    private static HashSet<string> EnglishCsvKeys() {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.StartsWith('#')) continue;
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        return keys;
    }
}
